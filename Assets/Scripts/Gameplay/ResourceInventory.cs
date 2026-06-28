using System.Collections.Generic;
using UnityEngine;

public class ResourceInventory : MonoBehaviour
{
    public ItemDataList ItemDataList => _itemDataList;

    [SerializeField] private ItemDataList _itemDataList;

    private Dictionary<int, int> _counts = new();

    private void Awake()
    {
        foreach (var item in _itemDataList.Items)
            _counts[item.Id] = 0;
    }

    // 자원의 현재 보유 개수를 반환한다.
    public int Get(ResourceData data) =>
        _counts.TryGetValue(data.Id, out var count) ? count : 0;

    // 자원을 지정한 수량만큼 추가한다.
    public void Add(ResourceData data, int amount)
    {
        if (!_counts.ContainsKey(data.Id)) return;
        _counts[data.Id] += amount;
    }

    // 자원을 지정한 수량만큼 차감한다. 수량이 부족하면 false를 반환하고 변경하지 않는다.
    public bool Remove(ResourceData data, int amount)
    {
        if (!_counts.ContainsKey(data.Id)) return false;
        if (_counts[data.Id] < amount) return false;

        _counts[data.Id] -= amount;
        return true;
    }

    // 자원이 지정한 수량 이상 있는지 확인한다.
    public bool Has(ResourceData data, int amount = 1) =>
        Get(data) >= amount;
}
