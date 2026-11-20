using UnityEngine;

// 바다 평면 오브젝트의 높이(월드 Y)를 지형 설정의 해수면 높이에 맞추고, 필요하면 X/Z를 카메라에 맞춰 따라가게 하는 컴포넌트. 걷기 판정에 쓰는 해수면과 화면에 보이는 수면이 어긋나지 않게 한다.
public class SeaLevelPlane : MonoBehaviour
{
    // 해수면 높이를 읽어 올 지형 설정
    [SerializeField] private MapTerrainSettings m_TerrainSettings;

    // 무한 청크 맵에서 수면이 끊기지 않도록 평면의 X/Z를 따라갈 대상으로 옮길지 여부
    [SerializeField] private bool m_FollowTarget = true;

    // 수면이 따라갈 대상. 비워 두면 메인 카메라를 따라간다.
    [SerializeField] private Transform m_Target;

    // 런타임 시작 시 수면 높이를 설정값에 맞춘다.
    private void Awake()
    {
        HandleApplySeaLevel();
    }

    private void LateUpdate()
    {
        HandleFollowTarget();
    }

    // 수면 평면의 X/Z를 대상 위치로 옮긴다. 셰이더가 월드 좌표로 무늬를 계산하므로 평면이 움직여도 물결이 함께 미끄러지지 않는다.
    private void HandleFollowTarget()
    {
        if (!m_FollowTarget)
            return;

        Transform target = m_Target;
        if (target == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            target = mainCamera.transform;
        }

        Vector3 position = transform.position;
        Vector3 targetPosition = target.position;
        position.x = targetPosition.x;
        position.z = targetPosition.z;
        transform.position = position;
    }

#if UNITY_EDITOR
    // 에디터에서 설정을 연결하거나 바꾸면 다음 에디터 갱신 때 수면 높이를 맞춘다. OnValidate 안에서 바로 트랜스폼을 바꾸면 경고가 나므로 한 프레임 미룬다.
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += HandleApplySeaLevelDelayed;
    }

    // 미뤄 둔 수면 높이 적용을 실행한다. 그사이 오브젝트가 파괴되었으면 건너뛴다.
    private void HandleApplySeaLevelDelayed()
    {
        if (this == null)
            return;

        HandleApplySeaLevel();
    }
#endif

    // 오브젝트의 X/Z는 그대로 두고 월드 Y만 해수면 높이로 옮긴다.
    private void HandleApplySeaLevel()
    {
        if (m_TerrainSettings == null)
        {
            Debug.LogError("SeaLevelPlane: TerrainSettings가 지정되지 않아 수면 높이를 맞출 수 없습니다.", this);
            return;
        }

        Vector3 position = transform.position;
        if (Mathf.Approximately(position.y, m_TerrainSettings.SeaLevel))
            return;

        position.y = m_TerrainSettings.SeaLevel;
        transform.position = position;
    }
}
