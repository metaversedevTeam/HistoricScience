using System.Collections.Generic;
using UnityEngine;

// 인스펙터에서 자원 소스에 연결해 사용하는 자원 소환 방식 스크립터블 오브젝트의 공통 기반.
// 소환할 자원 소스 프리팹과 공통 소환 조건(허용 바이옴, 소환 높이 범위)을 여기서 제공하고, 자리를 고르는 공식만 파생 클래스가 구현한다.
public abstract class ResourceSpawnRule : ScriptableObject, IResourceSpawnRule
{
    [Header("소환 대상")]
    // 맵에 소환할 자원 소스 프리팹 (예: Stone Source)
    [SerializeField] private GameObject m_SourcePrefab;
    // 이 소환 방식의 소환 자리를 고정하는 고유 번호. 자원 소스 목록에 등록될 때 자동으로 부여되며, 이후 바뀌지 않아 맵 배치가 유지된다.
    [SerializeField, HideInInspector] private int m_SeedId;

    public GameObject SourcePrefab => m_SourcePrefab;
    public int SeedId => m_SeedId;

    // 소환할 프리팹과 시드 번호가 모두 갖춰져 실제로 소환할 수 있는 상태인지 검사한다.
    public bool IsValid => m_SeedId > 0 && m_SourcePrefab != null;

    [Header("공용 소환 조건")]
    // 자원 소스가 소환될 수 있는 바이옴 목록. 비어 있으면 모든 바이옴에 소환된다.
    [SerializeField] private MapBiome[] m_SpawnBiomes;
    // 이 높이(월드 Y) 미만의 위치에는 소환하지 않는다. 기본값은 해수면 높이.
    [SerializeField] private float m_MinSpawnHeight = 12f;
    // 이 높이(월드 Y)를 초과하는 위치에는 소환하지 않는다. 기본값은 터레인 최대 높이라 사실상 상한이 없다.
    [SerializeField] private float m_MaxSpawnHeight = 100f;

    // 주어진 청크 정보로 자원 소스가 놓일 자리들을 계산해 결과 목록에 채운다.
    public abstract void GetPlacements(in ResourceSpawnContext context, List<ResourceSpawnPlacement> results);

#if UNITY_EDITOR
    // 자원 소스 목록이 아직 번호가 없는 소환 방식에 시드 번호를 부여할 때만 쓰는 에디터 전용 설정자.
    public void EditorAssignSeedId(int seedId)
    {
        m_SeedId = seedId;
    }
#endif

    // 정규화 청크 좌표(0~1)가 높이 조건과 바이옴 조건을 모두 만족하는지 검사하고, 만족하면 그 지점의 표면 높이를 함께 돌려준다.
    // 파생 클래스는 이 검사를 통과한 좌표에만 자리를 만들면 되고, 돌려받은 높이를 그대로 배치 Y로 쓸 수 있다.
    protected bool CanSpawnAt(in ResourceSpawnContext context, float normalizedX, float normalizedZ, out float height)
    {
        height = context.GetHeight(normalizedX, normalizedZ);
        // 바이옴 판정이 높이 계산보다 비싸므로, 높이에서 먼저 걸러 불필요한 조회를 건너뛴다.
        if (!IsHeightAllowed(height))
            return false;

        return IsBiomeAllowed(context.GetBiome(normalizedX, normalizedZ));
    }

    // 주어진 표면 높이(월드 Y)가 소환 높이 범위 안에 있는지 검사한다. 최소/최대 모두 경계값을 포함한다.
    protected bool IsHeightAllowed(float height)
    {
        return height >= m_MinSpawnHeight && height <= m_MaxSpawnHeight;
    }

    // 주어진 바이옴에 이 소환 방식이 자원 소스를 놓을 수 있는지 검사한다. 바이옴 목록이 비어 있으면 항상 허용된다.
    protected bool IsBiomeAllowed(MapBiome biome)
    {
        if (m_SpawnBiomes == null || m_SpawnBiomes.Length == 0)
            return true;

        for (int i = 0; i < m_SpawnBiomes.Length; i++)
        {
            if (m_SpawnBiomes[i] == biome)
                return true;
        }

        return false;
    }
}
