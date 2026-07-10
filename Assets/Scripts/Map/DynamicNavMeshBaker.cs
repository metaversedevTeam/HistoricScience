using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 여러 대상 각각의 주변 좁은 영역만 내브메시로 구워, 대상이 이동하면 그 영역을 따라 다시 굽는 컴포넌트.
// 대상마다 독립된 NavMeshData를 만들어 등록하고 따로 갱신하므로, 대상끼리 멀리 떨어져 있어도 그 사이의
// 넓은 영역까지 통째로 구울 필요가 없다. NavMeshSurface를 쓰지 않고 저수준 NavMeshBuilder API로 직접
// NavMeshData를 만들어 등록한 뒤 그 자리에서 계속 갱신한다. NavMeshSurface.BuildNavMesh()는 컴포넌트/
// 오브젝트가 비활성이면 결과를 등록하지 않는데, 이 방식은 등록을 우리가 직접 한 번만 하므로 그 문제가 없다.
// Unity 공식 NavMeshComponents 샘플의 LocalNavMeshBuilder를 참고했다.
// https://github.com/Unity-Technologies/NavMeshComponents/blob/master/Assets/Examples/Scripts/LocalNavMeshBuilder.cs
public class DynamicNavMeshBaker : MonoBehaviour
{
    // 이 대상들 각각의 주변을 계속 내브메시로 구운다 (예: 메인 카메라, 조작 중인 유닛들)
    [SerializeField] private List<Transform> m_FollowTargets = new List<Transform>();
    // 구울 영역의 한 변 절반 길이(정사각형 반경)
    [SerializeField, Min(1f)] private float m_BakeRadius = 750f;
    // 굽는 영역의 y축 범위. 터레인 최대 높이를 덮을 만큼 충분히 커야 한다.
    [SerializeField, Min(1f)] private float m_BakeHeight = 100f;

    // 대상 하나를 따라다니며 굽는 데 필요한 상태를 한데 묶은 것
    private class TargetBaker
    {
        public Transform Target;
        public NavMeshData NavMeshData;
        public NavMeshDataInstance Instance;
        public AsyncOperation Operation;
        public Coroutine TrackingCoroutine;
        public readonly List<NavMeshBuildSource> Sources = new List<NavMeshBuildSource>();
    }

    // 현재 추적 중인 대상마다 하나씩 만들어 둔 굽기 상태
    private readonly List<TargetBaker> m_Bakers = new List<TargetBaker>();

    // 설정된 대상마다 내브메시 데이터를 만들어 등록하고 추적을 시작한다
    private void OnEnable()
    {
        foreach (Transform target in m_FollowTargets)
            AddTarget(target);
    }

    // 추적 코루틴을 모두 멈추고, 추적 중이던 모든 내브메시 데이터를 내비게이션 월드에서 제거한다
    private void OnDisable()
    {
        StopAllCoroutines();

        foreach (TargetBaker baker in m_Bakers)
            baker.Instance.Remove();

        m_Bakers.Clear();
    }

    // 이미 추적 중이 아니면 새 대상을 추가해 그 주변을 굽기 시작한다
    public void AddTarget(Transform target)
    {
        if (target == null) return;
        if (m_Bakers.Exists(baker => baker.Target == target)) return;

        HandleStartTracking(target);
    }

    // 대상 추적을 멈추고 그동안 구워 둔 내브메시를 제거한다
    public void RemoveTarget(Transform target)
    {
        int index = m_Bakers.FindIndex(baker => baker.Target == target);
        if (index < 0) return;

        TargetBaker baker = m_Bakers[index];
        if (baker.TrackingCoroutine != null)
            StopCoroutine(baker.TrackingCoroutine);

        baker.Instance.Remove();
        m_Bakers.RemoveAt(index);
    }

    // 대상용 내브메시 데이터를 만들어 즉시 등록하고, 한 번 동기로 구운 뒤 계속 따라다니며 갱신하는 코루틴을 시작한다
    private void HandleStartTracking(Transform target)
    {
        TargetBaker baker = new TargetBaker { Target = target, NavMeshData = new NavMeshData() };
        baker.Instance = NavMesh.AddNavMeshData(baker.NavMeshData);
        m_Bakers.Add(baker);

        HandleUpdateNavMesh(baker, false);
        baker.TrackingCoroutine = StartCoroutine(HandleTrackTarget(baker));
    }

    // 대상이 존재하는 동안 그 주변 내브메시를 계속 비동기로 다시 굽는다. 대상이 파괴되면 데이터를 정리하고 멈춘다.
    private IEnumerator HandleTrackTarget(TargetBaker baker)
    {
        while (baker.Target != null)
        {
            HandleUpdateNavMesh(baker, true);
            yield return baker.Operation;
        }

        baker.Instance.Remove();
        m_Bakers.Remove(baker);
    }

    // 대상 주변 볼륨의 지형 소스를 모아 내브메시 데이터를 갱신한다. 동기/비동기 여부를 선택할 수 있다.
    private void HandleUpdateNavMesh(TargetBaker baker, bool async)
    {
        Bounds bounds = HandleGetBounds(baker.Target);

        baker.Sources.Clear();
        List<NavMeshBuildMarkup> markups = new List<NavMeshBuildMarkup>();
        NavMeshBuilder.CollectSources(bounds, LayerMask.GetMask("Ground"), NavMeshCollectGeometry.RenderMeshes, 0, false, markups, false, baker.Sources);

        NavMeshBuildSettings buildSettings = NavMesh.GetSettingsByID(0);

        if (async)
            baker.Operation = NavMeshBuilder.UpdateNavMeshDataAsync(baker.NavMeshData, buildSettings, baker.Sources, bounds);
        else
            NavMeshBuilder.UpdateNavMeshData(baker.NavMeshData, buildSettings, baker.Sources, bounds);
    }

    // 대상 위치를 볼륨 크기의 10% 단위로 스냅한 경계를 반환한다. 대상이 그 안에서 조금만 움직이면 경계가
    // 그대로라 다음 갱신이 사실상 재사용되고, 스냅 칸을 넘어야만 실제로 다른 영역이 구워진다.
    private Bounds HandleGetBounds(Transform target)
    {
        Vector3 targetPosition = target != null ? target.position : transform.position;
        Vector3 size = new Vector3(m_BakeRadius * 2f, m_BakeHeight, m_BakeRadius * 2f);
        Vector3 center = new Vector3(targetPosition.x, m_BakeHeight * 0.5f, targetPosition.z);

        return new Bounds(HandleQuantize(center, size * 0.1f), size);
    }

    // 각 축을 quant 간격으로 내림해 스냅한다
    private static Vector3 HandleQuantize(Vector3 value, Vector3 quant)
    {
        float x = quant.x * Mathf.Floor(value.x / quant.x);
        float y = quant.y * Mathf.Floor(value.y / quant.y);
        float z = quant.z * Mathf.Floor(value.z / quant.z);
        return new Vector3(x, y, z);
    }

    // 선택 시 대상마다 현재 구운 소스 경계와 다음에 구울 볼륨 경계를 씬 뷰에 표시한다
    private void OnDrawGizmosSelected()
    {
        foreach (TargetBaker baker in m_Bakers)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(baker.NavMeshData.sourceBounds.center, baker.NavMeshData.sourceBounds.size);

            Gizmos.color = Color.yellow;
            Bounds bounds = HandleGetBounds(baker.Target);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
