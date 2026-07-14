using UnityEngine;

// 청크 터레인이 구워진 뒤 시드 기반 결정론적 랜덤 위치에, 아이템 데이터에 정의된 자원 소스 프리팹을 바이옴별로 터레인 표면 높이에 맞춰 소환하는 컴포넌트
public class ChunkResourceSpawner : MonoBehaviour
{
    // 소환된 자원들을 모아 둘 컨테이너 오브젝트 이름. 다시 굽기 전 이 컨테이너를 통째로 파괴해 중복을 막는다.
    private const string k_ContainerName = "SpawnedResources";

    // 높이를 샘플링할 대상 터레인
    [SerializeField] private Terrain m_Terrain;
    // 소환 규칙을 읽어 올 아이템 목록. 소스 프리팹과 소환 개수가 설정된 아이템만 소환에 참여한다.
    [SerializeField] private ItemDataList m_ItemDataList;
    // 이 높이(월드 Y) 미만의 위치에는 소환하지 않는다. 기본값은 해수면 높이.
    [SerializeField] private float m_MinSpawnHeight = 12f;
    // 높이/바이옴 조건에 맞는 위치를 찾기 위해 개수당 재추첨할 최대 시도 횟수 (무한 루프 방지)
    [SerializeField, Min(1)] private int m_MaxAttemptsPerSpawn = 8;

    // 마지막으로 만든 컨테이너. 같은 프레임에 파괴가 지연되어도 이전 컨테이너를 다시 찾지 않도록 직접 들고 있는다.
    private Transform m_Container;

    // 전역 시드와 청크 원점으로 결정론적 랜덤 위치를 만들어, 아이템 목록의 각 아이템에 정의된 자원 소스 프리팹을 허용된 바이옴의 터레인 표면 위에 소환한다.
    public void SpawnResources(int seed, Vector2 mapViewOrigin, float mapViewSize, MapData mapData)
    {
        if (m_Terrain == null || m_Terrain.terrainData == null || m_ItemDataList == null || mapData == null)
            return;

        HandleClearSpawned();
        Transform container = HandleCreateContainer();

        foreach (ItemData item in m_ItemDataList.Items)
        {
            if (item == null || item.SourcePrefab == null || item.SpawnCountPerChunk <= 0)
                continue;

            // 아이템 ID를 시드에 섞어 아이템마다 독립적인 랜덤 스트림을 쓰므로, 목록 순서가 바뀌어도 배치가 유지된다.
            int itemSeed = HandleCombineSeed(seed, mapViewOrigin, item.Id);
            HandleSpawnItem(item, itemSeed, container, mapViewOrigin, mapViewSize, mapData);
        }
    }

    // 아이템 하나에 대해 결정론적 랜덤 위치를 추첨하며 높이/바이옴 조건에 맞는 곳에 목표 개수만큼 자원 소스를 소환한다.
    private void HandleSpawnItem(ItemData item, int itemSeed, Transform container, Vector2 mapViewOrigin, float mapViewSize, MapData mapData)
    {
        System.Random random = new System.Random(itemSeed);
        TerrainData terrainData = m_Terrain.terrainData;
        int maxAttempts = item.SpawnCountPerChunk * m_MaxAttemptsPerSpawn;
        int spawned = 0;

        for (int attempt = 0; attempt < maxAttempts && spawned < item.SpawnCountPerChunk; attempt++)
        {
            float normalizedX = (float)random.NextDouble();
            float normalizedZ = (float)random.NextDouble();
            float rotationY = (float)random.NextDouble() * 360f;

            // 청크 루트가 Y=0에 놓이므로 터레인 기준 높이가 곧 월드 높이다.
            float height = terrainData.GetInterpolatedHeight(normalizedX, normalizedZ);
            if (height < m_MinSpawnHeight)
                continue;

            // 터레인을 칠할 때와 같은 변환(원점 + 정규화 좌표 × 출력 크기)으로 맵 좌표를 구해 이 위치의 바이옴을 판정한다.
            Vector2 mapPosition = mapViewOrigin + new Vector2(normalizedX, normalizedZ) * mapViewSize;
            if (!item.CanSpawnIn(mapData.GetBiome(mapPosition)))
                continue;

            Vector3 localPosition = new Vector3(normalizedX * terrainData.size.x, height, normalizedZ * terrainData.size.z);
            HandleSpawnResource(item.SourcePrefab, container, localPosition, rotationY);
            spawned++;
        }
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

    // 자원 소스 프리팹 하나를 컨테이너 자식으로 소환해 로컬 위치와 Y축 회전을 설정한다.
    private void HandleSpawnResource(GameObject resourcePrefab, Transform container, Vector3 localPosition, float rotationY)
    {
        GameObject resource = Instantiate(resourcePrefab, container);
        resource.transform.localPosition = localPosition;
        resource.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);
    }

    // FNV-1a 해시로 전역 시드, 청크 원점, 아이템 ID를 섞어 아이템마다 고유하면서 세션이 바뀌어도 유지되는 시드를 만든다.
    private int HandleCombineSeed(int seed, Vector2 mapViewOrigin, int itemId)
    {
        uint hash = 2166136261u;
        hash = (hash ^ (uint)seed) * 16777619u;
        // 원점은 청크 좌표 × 출력 크기로 만들어진 유한한 float이므로, 정수화해 해시하면 부동소수 오차 없이 결정론이 유지된다.
        hash = (hash ^ (uint)Mathf.RoundToInt(mapViewOrigin.x * 1000f)) * 16777619u;
        hash = (hash ^ (uint)Mathf.RoundToInt(mapViewOrigin.y * 1000f)) * 16777619u;
        hash = (hash ^ (uint)itemId) * 16777619u;

        return (int)hash;
    }
}
