using System.Collections.Generic;
using UnityEngine;

// 청크 터레인이 구워진 뒤, 자원 소스 목록의 각 항목에 연결된 소환 방식이 계산한 자리에 자원 소스 프리팹을 소환하는 컴포넌트
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

    // 청크 정보를 묶어 각 자원 소스의 소환 방식에 넘기고, 돌려받은 자리마다 자원 소스 프리팹을 소환한다.
    // 터레인 크기는 여기(메인 스레드)에서 값으로 읽어 컨텍스트에 넣으므로, 소환 방식의 위치 계산은 터레인을 건드리지 않는다.
    public void SpawnResources(Vector2 mapViewOrigin, float mapViewSize, MapData mapData)
    {
        if (m_Terrain == null || m_Terrain.terrainData == null || m_ResourceSourceList == null || mapData == null)
            return;

        HandleClearSpawned();
        Transform container = HandleCreateContainer();

        ResourceSpawnContext context = new ResourceSpawnContext(mapViewOrigin, mapViewSize, mapData, m_Terrain.terrainData.size);

        foreach (ResourceSpawnRule source in m_ResourceSourceList.Sources)
        {
            if (source == null || !source.IsValid)
                continue;

            HandleSpawnSource(source, context, container);
        }
    }

    // 자원 소스 하나의 소환 방식에게 소환할 자리를 물어보고, 받은 자리마다 자원 소스를 소환한다.
    private void HandleSpawnSource(ResourceSpawnRule source, in ResourceSpawnContext context, Transform container)
    {
        // 목록을 호출마다 새로 만들어 소스 사이에 상태를 공유하지 않는다. 나중에 소스별 계산을 병렬로 돌려도 서로 간섭하지 않는다.
        List<ResourceSpawnPlacement> placements = new List<ResourceSpawnPlacement>();
        source.GetPlacements(context, placements);

        for (int i = 0; i < placements.Count; i++)
            HandleSpawnResource(source.SourcePrefab, container, placements[i]);
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
