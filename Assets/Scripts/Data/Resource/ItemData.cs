using UnityEngine;

// 아이템 한 종류의 기본 정보와, 맵 청크에 자원 소스로 소환될 때의 규칙(프리팹, 허용 바이옴, 개수)을 담는 스크립터블 오브젝트
[CreateAssetMenu(fileName = "아이템", menuName = "스크립터블 오브젝트/자원/아이템", order = int.MinValue)]
public class ItemData : ResourceData
{
    // 이 아이템을 채집할 수 있는 자원 소스 프리팹 (예: Stone Source). 비어 있으면 맵에 소환되지 않는다.
    [SerializeField] private GameObject _sourcePrefab;
    // 자원 소스가 소환될 수 있는 바이옴 목록. 비어 있으면 모든 바이옴에 소환된다.
    [SerializeField] private MapBiome[] _spawnBiomes;
    // 청크당 자원 소스 목표 소환 개수. 0이면 소환되지 않는다.
    [SerializeField, Min(0)] private int _spawnCountPerChunk;

    public GameObject SourcePrefab => _sourcePrefab;
    public int SpawnCountPerChunk => _spawnCountPerChunk;

    // 주어진 바이옴에 이 아이템의 자원 소스가 소환될 수 있는지 검사한다. 바이옴 목록이 비어 있으면 항상 허용된다.
    public bool CanSpawnIn(MapBiome biome)
    {
        if (_spawnBiomes == null || _spawnBiomes.Length == 0)
            return true;

        for (int i = 0; i < _spawnBiomes.Length; i++)
        {
            if (_spawnBiomes[i] == biome)
                return true;
        }

        return false;
    }
}
