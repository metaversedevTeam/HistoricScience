using System;
using System.Collections.Generic;
using UnityEngine;

// 플레이어가 한 번이라도 획득한 아이템을 기록하는 도감. ResourceInventory의 획득 이벤트를 구독해 채워지고, 맵 저장 데이터에 함께 기록된다.
public class ItemCodex : MonoBehaviour, ISavable
{
    // 아이템이 새로 도감에 등록될 때 발화한다. (도감 UI 갱신용)
    public event Action<ItemData> OnDiscover;

    // 획득 이벤트를 구독하고, 도감 대상 아이템 목록을 참조할 인벤토리
    [SerializeField] private ResourceInventory _resourceInventory;

    // 지금까지 한 번이라도 획득한 아이템 ID 집합
    private HashSet<int> _discovered = new();

    private void OnEnable()
    {
        if (_resourceInventory != null)
            _resourceInventory.OnAddItem += HandleItemAdded;
    }

    private void OnDisable()
    {
        if (_resourceInventory != null)
            _resourceInventory.OnAddItem -= HandleItemAdded;
    }

    // 주어진 아이템을 한 번이라도 획득했는지 반환한다.
    public bool IsDiscovered(ItemData item) => item != null && _discovered.Contains(item.Id);

    // 씬에 상주하는 객체라 프리팹 소환에 쓰이지 않는 고정 식별자
    public string PrefabId => "ItemCodex";

    // 현재 도감 상태를 JSON 문자열로 캡처한다.
    public string CaptureJson()
    {
        SaveState state = new SaveState();
        state.Ids.AddRange(_discovered);
        return JsonUtility.ToJson(state);
    }

    // JSON 문자열에서 도감 상태를 복원한다. 기존 기록에 병합하며, 새로 추가된 아이템마다 OnDiscover를 발화한다.
    public void ApplyJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        SaveState state = JsonUtility.FromJson<SaveState>(json);
        if (state == null) return;

        foreach (int id in state.Ids)
            HandleDiscover(id);
    }

    // 아이템이 획득되면(수량 > 0) 도감에 발견된 것으로 등록한다.
    private void HandleItemAdded(ItemData item, int count)
    {
        if (count <= 0) return;
        HandleDiscover(item.Id);
    }

    // 아이템 ID를 도감에 등록하고, 새로 추가된 경우에만 OnDiscover를 발화한다.
    private void HandleDiscover(int id)
    {
        if (!_discovered.Add(id)) return;

        ItemData item = HandleFindItem(id);
        if (item != null) OnDiscover?.Invoke(item);
    }

    // 인벤토리의 아이템 목록에서 ID로 아이템 데이터를 찾는다. 없으면 null을 반환한다.
    private ItemData HandleFindItem(int id)
    {
        if (_resourceInventory == null) return null;

        foreach (ItemData item in _resourceInventory.ItemDataList.Items)
        {
            if (item.Id == id) return item;
        }

        return null;
    }

    // 도감 상태의 직렬화 래퍼. JsonUtility가 HashSet을 지원하지 않아 리스트로 변환한다.
    [Serializable]
    private class SaveState
    {
        public List<int> Ids = new();
    }
}
