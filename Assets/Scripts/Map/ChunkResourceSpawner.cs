using System.Collections.Generic;
using UnityEngine;

// 청크 터레인이 구워진 뒤, 자원 소스 목록의 각 항목에 연결된 소환 방식이 계산한 자리에 자원 소스 프리팹을 소환하는 컴포넌트.
// 자리 계산은 무거워서 CreatePlan으로 만든 계획을 백그라운드에서 계산하게 하고, 이 컴포넌트는 메인 스레드에서 소환만 맡는다.
public class ChunkResourceSpawner : MonoBehaviour
{
    // 소환된 자원들을 모아 둘 컨테이너 오브젝트 이름. 다시 굽기 전 이 컨테이너를 통째로 파괴해 중복을 막는다.
    private const string k_ContainerName = "SpawnedResources";

    // 소환된 자원을 올려 둘 대상 터레인. 청크 크기를 읽는 데만 사용하고, 표면 높이는 MapData에서 계산한다.
    [SerializeField] private Terrain m_Terrain;
    // 소환할 자원 소스 목록. 아이템 전체를 순회하지 않고 이 목록에 등록된 소스만 소환에 참여한다.
    [SerializeField] private ResourceSourceList m_ResourceSourceList;

    // 마지막으로 만든 컨테이너. 같은 프레임에 파괴가 지연되어도 이전 컨테이너를 다시 찾지 않도록 직접 들고 있는다.
    private Transform m_Container;

    // 유효한 소환 방식과 터레인 크기를 담은 소환 계획을 만든다. 자리 계산은 계획의 ComputePlacements가 백그라운드에서 따로 맡는다.
    // 소환 방식 유효성 검사와 터레인 크기 읽기는 Unity API라 반드시 메인 스레드에서 호출해야 한다. 소환할 수 없는 상태면 null을 반환한다.
    public ChunkResourcePlan CreatePlan()
    {
        if (m_Terrain == null || m_Terrain.terrainData == null || m_ResourceSourceList == null)
            return null;

        List<ResourceSpawnRule> rules = new List<ResourceSpawnRule>(m_ResourceSourceList.Sources.Count);
        foreach (ResourceSpawnRule source in m_ResourceSourceList.Sources)
        {
            if (source == null || !source.IsValid)
                continue;

            rules.Add(source);
        }

        return new ChunkResourcePlan(rules.ToArray(), m_Terrain.terrainData.size);
    }

    // 자리 계산이 끝난 소환 계획대로 이전 자원을 지우고 새 자원 소스 프리팹을 소환한다. Instantiate를 쓰므로 메인 스레드에서 호출해야 한다.
    public void SpawnPlan(ChunkResourcePlan plan)
    {
        if (plan == null || !plan.IsComputed)
        {
            Debug.LogError("ChunkResourceSpawner: 자리 계산이 끝나지 않은 소환 계획이라 자원을 소환하지 않습니다.", this);
            return;
        }

        HandleClearSpawned();
        Transform container = HandleCreateContainer();

        for (int i = 0; i < plan.RuleCount; i++)
            HandleSpawnSource(plan.GetPrefab(i), plan.GetPlacements(i), container);
    }

    // 소환해 둔 자원을 모두 지운다. 청크를 LOD1 이하로 낮춰 자원을 두지 않을 때 쓴다.
    public void ClearResources()
    {
        HandleClearSpawned();
    }

    // 자원 소스 하나에 대해 미리 계산된 자리마다 자원 소스 프리팹을 소환한다.
    private void HandleSpawnSource(GameObject sourcePrefab, IReadOnlyList<ResourceSpawnPlacement> placements, Transform container)
    {
        for (int i = 0; i < placements.Count; i++)
            HandleSpawnResource(sourcePrefab, container, placements[i]);
    }

    // 이전에 소환해 둔 자원 컨테이너가 있으면 통째로 파괴한다.
    private void HandleClearSpawned()
    {
        Transform container = m_Container != null ? m_Container : transform.Find(k_ContainerName);
        if (container == null)
            return;

        if (Application.isPlaying)
            Destroy(container.gameObject);
        else
            DestroyImmediate(container.gameObject);

        m_Container = null;
    }

    // 소환된 자원들을 담을 컨테이너를 이 청크의 자식으로 새로 만든다.
    private Transform HandleCreateContainer()
    {
        GameObject containerObject = new GameObject(k_ContainerName);
        containerObject.transform.SetParent(transform, false);

        m_Container = containerObject.transform;
        return m_Container;
    }

    // 자원 소스 프리팹 하나를 컨테이너 자식으로 소환해 계산된 로컬 위치와 Y축 회전을 설정한다.
    private void HandleSpawnResource(GameObject resourcePrefab, Transform container, ResourceSpawnPlacement placement)
    {
        GameObject resource = Instantiate(resourcePrefab, container);
        resource.transform.localPosition = placement.LocalPosition;
        resource.transform.localRotation = Quaternion.Euler(0f, placement.RotationY, 0f);
    }
}
