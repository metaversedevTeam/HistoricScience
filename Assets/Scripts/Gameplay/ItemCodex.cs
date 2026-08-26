using System;
using System.Collections.Generic;
using UnityEngine;

// 플레이어가 한 번이라도 획득한 아이템과, 비용을 치르고 공개한 조합법 힌트 횟수를 기록하는 도감.
// ResourceInventory의 획득 이벤트를 구독해 채워지고, 맵 저장 데이터에 함께 기록된다.
public class ItemCodex : MonoBehaviour, ISavable
{
    // 아이템이 새로 도감에 등록될 때 발화한다. (도감 UI 갱신용)
    public event Action<ItemData> OnDiscover;

    // 어떤 아이템의 조합법 힌트가 하나 더 공개될 때 발화한다. (도감·힌트 UI 갱신용)
    public event Action<ItemData> OnHintRevealed;

    // 획득 이벤트를 구독하고, 도감 대상 아이템 목록을 참조할 인벤토리
    [SerializeField] private ResourceInventory _resourceInventory;

    // 지금까지 한 번이라도 획득한 아이템 ID 집합
    private HashSet<int> _discovered = new();

    // 아이템 ID -> 지금까지 공개한 조합법 힌트 횟수. 공개된 칸이 어디인지는 CraftingHintOrder가 ID로 다시 계산한다.
    private Dictionary<int, int> _hintCounts = new();

    // 도감 집계·조회 대상이 되는 아이템 목록. 인벤토리가 연결되지 않았으면 빈 목록으로 본다.
    private IReadOnlyList<ItemData> Items =>
        _resourceInventory != null && _resourceInventory.ItemDataList != null
            ? _resourceInventory.ItemDataList.Items
            : Array.Empty<ItemData>();

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

    // 도감에 표시되는 아이템 기준으로 수집 현황을 센다. age가 null이면 전체, 값이 있으면 그 시대만 집계한다.
    public CodexProgress GetProgress(Age? age = null)
    {
        int total = 0;
        int discovered = 0;

        foreach (ItemData item in Items)
        {
            if (!item.ShowInCodex) continue;
            if (age.HasValue && item.Age != age.Value) continue;

            total++;
            if (IsDiscovered(item)) discovered++;
        }

        return new CodexProgress(discovered, total);
    }

    // 해당 시대의 도감 아이템을 모두 획득했는지 반환한다. 그 시대에 셀 아이템이 없으면 false다.
    public bool IsAgeCompleted(Age age) => GetProgress(age).IsCompleted;

    // 주어진 아이템의 조합법 힌트를 지금까지 몇 번 받았는지 반환한다.
    public int GetHintCount(ItemData item)
    {
        if (item == null) return 0;
        return _hintCounts.TryGetValue(item.Id, out int count) ? count : 0;
    }

    // 주어진 아이템의 조합법에서 힌트로 공개할 수 있는 칸의 총 개수를 반환한다.
    public int GetHintTotal(ItemData item) => CraftingHintOrder.GetRevealOrder(item).Count;

    // 아직 공개할 칸이 남아 있는지 반환한다. 조합법이 없는 아이템은 항상 false다.
    public bool CanRevealHint(ItemData item) => GetHintCount(item) < GetHintTotal(item);

    // 조합법 힌트를 한 칸 더 공개한다. 더 공개할 칸이 없으면 아무것도 하지 않고 false를 반환한다. (비용 차감은 호출하는 쪽의 책임)
    public bool TryRevealHint(ItemData item)
    {
        if (!CanRevealHint(item)) return false;

        _hintCounts[item.Id] = GetHintCount(item) + 1;
        OnHintRevealed?.Invoke(item);
        return true;
    }

    // 힌트로 이미 공개된 조합법 칸들의 좌표 집합을 반환한다. 좌표는 좌상단 기준으로 정규화된 값이다.
    public HashSet<Vector2Int> GetRevealedCoords(ItemData item)
    {
        var revealed = new HashSet<Vector2Int>();
        IReadOnlyList<Vector2Int> order = CraftingHintOrder.GetRevealOrder(item);
        int count = Mathf.Min(GetHintCount(item), order.Count);

        for (int i = 0; i < count; i++)
            revealed.Add(order[i]);

        return revealed;
    }

    // 도감 대상 아이템을 모두 발견 상태로 등록한다. (에디터 테스트용)
    [ContextMenu("Unlock All Items")]
    private void UnlockAllItems()
    {
        int unlocked = 0;

        foreach (ItemData item in Items)
        {
            if (_discovered.Contains(item.Id)) continue;

            HandleDiscover(item.Id);
            unlocked++;
        }

        Debug.Log($"ItemCodex: 아이템 {unlocked}개를 새로 해금했습니다. (전체 {Items.Count}개)");
    }

    // 씬에 상주하는 객체라 프리팹 소환에 쓰이지 않는 고정 식별자
    public string PrefabId => "ItemCodex";

    // 현재 도감 상태를 JSON 문자열로 캡처한다.
    public string CaptureJson()
    {
        SaveState state = new SaveState();
        state.Ids.AddRange(_discovered);

        foreach (var pair in _hintCounts)
        {
            // 한 번도 받지 않은 것과 같은 상태이므로 파일에 남기지 않는다.
            if (pair.Value <= 0) continue;

            state.HintIds.Add(pair.Key);
            state.HintCounts.Add(pair.Value);
        }

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

        HandleRestoreHints(state);
    }

    // 저장된 힌트 횟수를 병합한다. 이미 더 많이 공개해 둔 아이템은 그대로 두고, 늘어난 아이템만 알린다.
    private void HandleRestoreHints(SaveState state)
    {
        for (int i = 0; i < state.HintIds.Count && i < state.HintCounts.Count; i++)
        {
            int id = state.HintIds[i];
            int saved = state.HintCounts[i];
            _hintCounts.TryGetValue(id, out int current);
            if (saved <= current) continue;

            _hintCounts[id] = saved;

            ItemData item = HandleFindItem(id);
            if (item != null) OnHintRevealed?.Invoke(item);
        }
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
        foreach (ItemData item in Items)
        {
            if (item.Id == id) return item;
        }

        return null;
    }

    // 도감 상태의 직렬화 래퍼. JsonUtility가 HashSet·Dictionary를 지원하지 않아 리스트로 변환한다.
    [Serializable]
    private class SaveState
    {
        public List<int> Ids = new();
        // HintIds[i]번 아이템의 힌트를 HintCounts[i]번 받았다는 뜻의 병렬 리스트
        public List<int> HintIds = new();
        public List<int> HintCounts = new();
    }
}

// 도감 수집 현황 한 건 — 집계 대상 아이템 수와 그중 획득한 수를 담는다.
public readonly struct CodexProgress
{
    public readonly int Discovered;
    public readonly int Total;

    // 집계 결과로 수집 현황을 구성한다.
    public CodexProgress(int discovered, int total)
    {
        Discovered = discovered;
        Total = total;
    }

    // 0~1 사이의 수집 비율. 집계 대상이 없으면 0이다.
    public float Ratio => Total > 0 ? (float)Discovered / Total : 0f;

    // 집계 대상을 모두 획득했는지 여부. 셀 대상이 하나도 없으면 false다.
    public bool IsCompleted => Total > 0 && Discovered >= Total;
}
