using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceInventory : MonoBehaviour, ISavable
{
    public event Action<ItemData,int> OnAddItem;
    public event Action<ItemData,int> OnRemoveItem;

    // 월드 좌표와 함께 아이템이 추가되었을 때 발생한다. (아이템, 이번에 추가된 수량, 추가된 월드 좌표)
    // 획득 팝업처럼 "어디서 얻었는지"가 필요한 표현이 구독한다.
    public event Action<ItemData,int,Vector3> OnAddItemAt;

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

    // 자원을 지정한 수량만큼 추가한다. 획득 위치를 알 수 없는 경로(UI 조합 등)에서 쓴다.
    public void Add(ResourceData data, int amount)
    {
        AddInternal(data, amount);
    }

    // 자원을 지정한 수량만큼 추가하고, 획득한 월드 좌표를 함께 알린다.
    public void Add(ResourceData data, int amount, Vector3 worldPosition)
    {
        if (!AddInternal(data, amount)) return;
        if (data is ItemData item) OnAddItemAt?.Invoke(item, amount, worldPosition);
    }

    // 실제 보유량을 늘리고 획득 이벤트를 발화한다. 목록에 없는 자원이면 아무것도 하지 않고 false를 반환한다.
    private bool AddInternal(ResourceData data, int amount)
    {
        if (!_counts.ContainsKey(data.Id)) return false;

        _counts[data.Id] += amount;
        if (data is ItemData item) OnAddItem?.Invoke(item, _counts[data.Id]);
        return true;
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
