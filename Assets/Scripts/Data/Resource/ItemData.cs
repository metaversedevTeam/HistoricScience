using UnityEngine;

// 아이템 한 종류의 기본 정보와, 맵 청크에 자원 소스로 소환될 때의 규칙(프리팹, 소환 방식)을 담는 스크립터블 오브젝트
[CreateAssetMenu(fileName = "아이템", menuName = "스크립터블 오브젝트/자원/아이템", order = int.MinValue)]
public class ItemData : ResourceData
{
    // 이 아이템을 채집할 수 있는 자원 소스 프리팹 (예: Stone Source). 비어 있으면 맵에 소환되지 않는다.
    [SerializeField] private GameObject _sourcePrefab;
    // 자원 소스를 청크 어디에 몇 개 놓을지 결정하는 소환 방식. 비어 있으면 맵에 소환되지 않는다.
    [SerializeField] private ResourceSpawnRule _spawnRule;
    [SerializeField, Min(0)] private float _craftingTime;
    [SerializeField] private Age _itemAge;
    // 이 아이템을 도감에 표시할지 여부. 꺼두면 도감 목록에서 제외된다.
    [SerializeField] private bool _showInCodex = true;

    public GameObject SourcePrefab => _sourcePrefab;
    public ResourceSpawnRule SpawnRule => _spawnRule;
    public float CraftingTime => _craftingTime;
    public Age Age => _itemAge;
    public bool ShowInCodex => _showInCodex;
}
