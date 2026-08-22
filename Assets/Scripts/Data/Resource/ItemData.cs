using UnityEngine;

// 아이템 한 종류의 기본 정보(조합 시간, 시대, 도감 표시 여부)를 담는 스크립터블 오브젝트. 맵에 소환될 자원 소스는 ResourceSourceList가 따로 관리한다.
[CreateAssetMenu(fileName = "아이템", menuName = "스크립터블 오브젝트/자원/아이템", order = int.MinValue)]
public class ItemData : ResourceData
{
    [SerializeField, Min(0)] private float _craftingTime;
    [SerializeField] private Age _itemAge;
    // 이 아이템을 도감에 표시할지 여부. 꺼두면 도감 목록에서 제외된다.
    [SerializeField] private bool _showInCodex = true;
    // 도감에서 이 아이템의 조합법 힌트를 한 번 받을 때 소모할 자원. 비워 두면 힌트를 공짜로 받는다.
    [SerializeField] private ResourceData _hintCostResource;
    // 힌트를 한 번 받는 데 드는 위 자원의 개수. 몇 번째 힌트든 매번 같은 값이 든다.
    [SerializeField, Min(0)] private int _hintCost = 50;

    public float CraftingTime => _craftingTime;
    public Age Age => _itemAge;
    public bool ShowInCodex => _showInCodex;
    public ResourceData HintCostResource => _hintCostResource;
    public int HintCost => _hintCost;
}
