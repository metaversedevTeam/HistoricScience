using System.Collections.Generic;
using UnityEngine;

// 건설 비용 UI — 선택한 건물의 필요 자원별 수량과 보유 인벤토리를 페이로드로 받아 여는 관리형 UI
public class BuildCostUI : OpenableUIBase<BuildCostData>
{
    [SerializeField] private BuildCostRowUI _rowPrefab;
    [SerializeField] private RectTransform _rowContainer;

    // 배치를 확정·취소할 때마다 따라 붙었다 사라지는 HUD 패널이라, 창 여닫는 효과음을 내지 않는다.
    protected override bool UsesWindowSfx => false;

    // 전달받은 비용·인벤토리로 자원별 행을 채운다.
    protected override void ApplyData(BuildCostData data)
    {
        ClearRows();
        foreach (var (resource, needed) in data.Cost)
        {
            var row = Instantiate(_rowPrefab, _rowContainer);
            row.Setup(resource, needed, data.Inventory.Get(resource));
        }
    }

    // 풀 반납 전 생성된 행들을 정리한다.
    protected override void OnReturnToPool()
    {
        ClearRows();
    }

    // 행 컨테이너 아래의 모든 행을 제거한다.
    private void ClearRows()
    {
        for (int i = _rowContainer.childCount - 1; i >= 0; i--)
            Destroy(_rowContainer.GetChild(i).gameObject);
    }
}

// 건설 비용 UI에 전달되는 페이로드 — 필요한 자원별 수량과 비교할 인벤토리
public readonly struct BuildCostData
{
    public readonly IReadOnlyDictionary<ResourceData, int> Cost;
    public readonly ResourceInventory Inventory;

    // 비용 목록과 비교할 인벤토리로 페이로드를 구성한다.
    public BuildCostData(IReadOnlyDictionary<ResourceData, int> cost, ResourceInventory inventory)
    {
        Cost = cost;
        Inventory = inventory;
    }
}
