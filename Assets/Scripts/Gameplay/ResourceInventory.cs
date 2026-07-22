using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceInventory : MonoBehaviour, ISavable
{
    public event Action<ItemData,int> OnAddItem;
    public event Action<ItemData,int> OnRemoveItem;

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
        if (data is ItemData item) OnAddItem?.Invoke(item, _counts[data.Id]);
    }

    // 자원을 지정한 수량만큼 차감한다. 수량이 부족하면 false를 반환하고 변경하지 않는다.
    public bool Remove(ResourceData data, int amount)
    {
        if (!_counts.ContainsKey(data.Id)) return false;
        if (_counts[data.Id] < amount) return false;

        _counts[data.Id] -= amount;
        if (data is ItemData item) OnRemoveItem?.Invoke(item, _counts[data.Id]);
        return true;
    }

    // 자원이 지정한 수량 이상 있는지 확인한다.
    public bool Has(ResourceData data, int amount = 1) =>
        Get(data) >= amount;

    // 씬에 상주하는 객체라 프리팹 소환에 쓰이지 않는 고정 식별자
    public string PrefabId => "ResourceInventory";

    // 현재 자원 보유량을 JSON 문자열로 캡처한다.
    public string CaptureJson()
    {
        var state = new SaveState();
        foreach (var pair in _counts)
        {
            state.Ids.Add(pair.Key);
            state.Counts.Add(pair.Value);
        }
        return JsonUtility.ToJson(state);
    }

    // JSON 문자열에서 자원 보유량을 복원하고, UI 갱신을 위해 아이템마다 OnAddItem을 발화한다.
    public void ApplyJson(string json)
    {
        var state = JsonUtility.FromJson<SaveState>(json);
        if (state == null) return;

        for (int i = 0; i < state.Ids.Count && i < state.Counts.Count; i++)
            _counts[state.Ids[i]] = state.Counts[i];

        foreach (var item in _itemDataList.Items)
            OnAddItem?.Invoke(item, _counts.TryGetValue(item.Id, out var count) ? count : 0);
    }

    // 인벤토리 상태의 직렬화 래퍼. JsonUtility가 Dictionary를 지원하지 않아 병렬 리스트로 변환한다.
    [Serializable]
    private class SaveState
    {
        public List<int> Ids = new();
        public List<int> Counts = new();
    }
}
