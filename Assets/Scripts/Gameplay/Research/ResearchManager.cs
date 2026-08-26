using System;
using System.Collections.Generic;
using UnityEngine;

// 완료한 연구를 들고 있으면서, 연구가 끝날 때마다 완료된 연구들을 순회해 같은 종류의 연구 보너스를 합산하고
// 그 결과를 BonusTotals로 제공하는 관리자.
// 시대 제한의 기준이 되는 현재 시대는 저장하지 않고, 도감에서 앞 시대를 모두 모았는지로 그때그때 계산한다.
// 씬에 미리 놓아 두면 그 인스턴스를 쓰고, 없으면 처음 접근할 때 기본값으로 하나 만들어 쓴다. (UIManager와 같은 지연 생성 방식)
// 완료한 연구 목록은 맵 저장 파일에 함께 기록되며, 복원할 때 연구 목록에서 식별자로 연구를 되찾는다.
public class ResearchManager : MonoBehaviour, ISavable
{
    // 인스펙터에 연구 목록이 지정되지 않았을 때 대신 읽어 올 Resources 폴더 하위 경로 (확장자 제외)
    private const string ResearchListResourcePath = "연구 목록";

    public static ResearchManager Instance
    {
        get
        {
            if (_instance != null) return _instance;

            _instance = FindFirstObjectByType<ResearchManager>();
            if (_instance == null)
                _instance = new GameObject(nameof(ResearchManager)).AddComponent<ResearchManager>();

            return _instance;
        }
    }

    // 연구가 완료될 때 발화한다. (목록·상세 UI 갱신용)
    public event Action<ResearchData> OnCompleted;

    // 시대 제한의 기준이 되는 현재 시대가 바뀔 때 발화한다.
    public event Action<Age> OnAgeChanged;

    // 합산된 연구 보너스가 다시 계산됐을 때 발화한다. 보너스 값을 캐싱해 쓰는 쪽이 갱신 시점을 알기 위한 이벤트다.
    public event Action OnBonusesChanged;

    // 저장된 식별자로 연구를 되찾을 때 쓰는 전체 연구 목록. 비워 두면 Resources에서 읽어 온다.
    [SerializeField] private ResearchDataList _researchDataList;

    // 시대 구분의 첫 시대. 도감을 아직 다 모으지 못했을 때의 현재 시대다.
    private const Age k_FirstAge = Age.Paleolithic;

    private static ResearchManager _instance;

    // 시대 계산의 기준이 되는 도감. 씬에서 찾아 획득 이벤트를 구독한다.
    private ItemCodex _codex;

    // 도감 진행으로 계산해 둔 현재 시대. 저장하지 않고 도감이 바뀔 때마다 다시 구한다.
    private Age _currentAge = k_FirstAge;

    // 현재 시대를 한 번이라도 계산했는지 여부
    private bool _ageEvaluated;

    // 지금까지 완료한 연구 집합
    private readonly HashSet<ResearchData> _completed = new();

    // 완료한 연구들의 보너스를 종류별로 합산한 결과. 바깥에는 읽기 전용으로 노출한다.
    private readonly List<ResearchBonusTotal> _bonusTotals = new();

    // 합산 도중 같은 종류를 빠르게 찾기 위한 보너스 종류 -> _bonusTotals 인덱스 매핑
    private readonly Dictionary<ResearchBonusData, int> _bonusIndices = new();

    // 완료한 연구들에서 종류별로 합산된 연구 보너스 목록. 값을 쓰는 쪽은 여기서 가져다 쓴다.
    public IReadOnlyList<ResearchBonusTotal> BonusTotals => _bonusTotals;

    // 시대 제한의 기준이 되는 현재 시대. 따로 저장하지 않고, 도감에서 앞 시대를 모두 모았는지로 계산한다.
    public Age CurrentAge
    {
        get
        {
            if (!_ageEvaluated) HandleEvaluateAge();
            return _currentAge;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    private void OnEnable()
    {
        HandleSubscribeCodex();
    }

    private void OnDisable()
    {
        HandleUnsubscribeCodex();
    }

    // 시대 계산의 기준이 되는 도감을 씬에서 찾아 획득 이벤트를 구독한다. 도감이 없으면 항상 첫 시대로 본다.
    private void HandleSubscribeCodex()
    {
        _codex = FindFirstObjectByType<ItemCodex>();
        if (_codex == null) return;

        _codex.OnDiscover += HandleCodexDiscovered;
    }

    // 구독 중인 도감 획득 이벤트를 해제하고 참조를 비운다.
    private void HandleUnsubscribeCodex()
    {
        if (_codex == null) return;

        _codex.OnDiscover -= HandleCodexDiscovered;
        _codex = null;
    }

    // 도감에 아이템이 새로 등록되면 시대가 넘어갔는지 다시 따진다.
    private void HandleCodexDiscovered(ItemData _) => HandleEvaluateAge();

    // 현재 시대를 다시 계산하고, 값이 바뀌었으면 OnAgeChanged를 발화한다.
    private void HandleEvaluateAge()
    {
        _ageEvaluated = true;

        Age evaluated = EvaluateAge();
        if (_currentAge == evaluated) return;

        _currentAge = evaluated;
        OnAgeChanged?.Invoke(_currentAge);
    }

    // 앞 시대의 도감을 모두 모았으면 다음 시대로 넘어가는 방식으로 현재 시대를 구한다.
    private Age EvaluateAge()
    {
        if (_codex == null) return k_FirstAge;

        Age current = k_FirstAge;

        foreach (Age candidate in Enum.GetValues(typeof(Age)))
        {
            // 자연 자원은 시대 구분 대상이 아니고, 이미 지난 시대는 다시 따질 필요가 없다.
            if (candidate == Age.nature || candidate <= current) continue;
            if (!candidate.TryGetPreviousAge(out Age previous)) continue;

            // 앞 시대를 다 모으지 못했으면 거기서 멈춘다.
            if (!_codex.IsAgeCompleted(previous)) break;

            current = candidate;
        }

        return current;
    }

    // 해당 연구를 이미 끝냈는지 반환한다.
    public bool IsCompleted(ResearchData research) => research != null && _completed.Contains(research);

    // 해당 연구의 시대 제한이 풀렸는지 반환한다. 현재 시대가 요구 시대 이상이면 풀린 것으로 본다.
    public bool IsAgeUnlocked(ResearchData research) => research != null && research.RequiredAge <= CurrentAge;

    // 해당 연구의 선행 연구를 모두 끝냈는지 반환한다. 선행 연구가 없으면 항상 true다.
    public bool ArePrerequisitesMet(ResearchData research)
    {
        if (research == null) return false;

        foreach (ResearchData prerequisite in research.Prerequisites)
        {
            if (prerequisite != null && !IsCompleted(prerequisite))
                return false;
        }

        return true;
    }

    // 해당 연구가 지금 어떤 상태인지 판정한다. 자원 부족은 상태에 반영하지 않고 연구 시점에만 본다.
    public ResearchState GetState(ResearchData research)
    {
        if (research == null) return ResearchState.AgeLocked;
        if (IsCompleted(research)) return ResearchState.Completed;
        if (!IsAgeUnlocked(research)) return ResearchState.AgeLocked;
        if (!ArePrerequisitesMet(research)) return ResearchState.PrerequisiteLocked;

        return ResearchState.Available;
    }

    // 인벤토리에 해당 연구의 비용을 모두 치를 만큼 자원이 있는지 반환한다.
    public bool CanAfford(ResearchData research, ResourceInventory inventory)
    {
        if (research == null) return false;
        if (inventory == null) return false;

        foreach (ResearchCostEntry cost in research.Costs)
        {
            if (cost.Resource != null && !inventory.Has(cost.Resource, cost.Count))
                return false;
        }

        return true;
    }

    // 비용을 치르고 연구를 완료한다. 상태나 자원이 조건에 맞지 않으면 아무것도 하지 않고 false를 반환한다.
    public bool TryResearch(ResearchData research, ResourceInventory inventory)
    {
        if (GetState(research) != ResearchState.Available) return false;
        if (!CanAfford(research, inventory)) return false;

        foreach (ResearchCostEntry cost in research.Costs)
        {
            if (cost.Resource != null)
                inventory.Remove(cost.Resource, cost.Count);
        }

        _completed.Add(research);
        RebuildBonusTotals();
        OnCompleted?.Invoke(research);
        return true;
    }

    // 해당 종류의 합산된 보너스 값을 반환한다. 아직 그 종류의 보너스를 하나도 얻지 못했으면 0이다.
    public float GetBonus(ResearchBonusData bonus)
    {
        if (bonus == null) return 0f;
        return _bonusIndices.TryGetValue(bonus, out int index) ? _bonusTotals[index].Value : 0f;
    }

    // 비율 보너스를 곱셈 배율로 바꿔 반환한다. (합계 0.15 -> 1.15)
    public float GetMultiplier(ResearchBonusData bonus) => 1f + GetBonus(bonus);

    // 완료한 연구들을 순회하며 각 연구의 보너스를 종류별로 합산해 목록을 다시 만든다.
    private void RebuildBonusTotals()
    {
        _bonusTotals.Clear();
        _bonusIndices.Clear();

        foreach (ResearchData research in _completed)
        {
            if (research == null) continue;

            foreach (ResearchBonusEntry entry in research.Bonuses)
                AccumulateBonus(entry);
        }

        OnBonusesChanged?.Invoke();
    }

    // 보너스 한 줄을 같은 종류의 합계에 더한다. 처음 보는 종류면 목록에 새로 넣는다.
    private void AccumulateBonus(ResearchBonusEntry entry)
    {
        if (entry.Bonus == null) return;

        if (_bonusIndices.TryGetValue(entry.Bonus, out int index))
        {
            _bonusTotals[index] = new ResearchBonusTotal(entry.Bonus, _bonusTotals[index].Value + entry.Value);
            return;
        }

        _bonusIndices.Add(entry.Bonus, _bonusTotals.Count);
        _bonusTotals.Add(new ResearchBonusTotal(entry.Bonus, entry.Value));
    }

    // 씬에 상주하는 객체라 프리팹 소환에 쓰이지 않는 고정 식별자
    public string PrefabId => "ResearchManager";

    // 완료한 연구 식별자를 JSON 문자열로 캡처한다. 현재 시대는 도감으로 계산되므로 저장하지 않는다.
    public string CaptureJson()
    {
        SaveState state = new SaveState();

        foreach (ResearchData research in _completed)
        {
            if (research != null)
                state.CompletedIds.Add(research.ResearchId);
        }

        return JsonUtility.ToJson(state);
    }

    // JSON 문자열에서 완료한 연구를 복원한다. 기존 기록에 병합하며, 새로 완료된 연구마다 OnCompleted를 발화한다.
    public void ApplyJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        SaveState state = JsonUtility.FromJson<SaveState>(json);
        if (state == null) return;

        HandleRestoreCompleted(state);
    }

    // 저장된 식별자로 연구를 찾아 완료 목록에 넣는다. 하나라도 새로 추가되면 보너스를 다시 합산한다.
    private void HandleRestoreCompleted(SaveState state)
    {
        List<ResearchData> restored = new();

        foreach (string id in state.CompletedIds)
        {
            ResearchData research = ResearchList != null ? ResearchList.Find(id) : null;
            if (research == null)
            {
                Debug.LogWarning($"ResearchManager: 저장된 연구 '{id}'를 연구 목록에서 찾지 못해 건너뜁니다.", this);
                continue;
            }

            if (_completed.Add(research))
                restored.Add(research);
        }

        if (restored.Count == 0) return;

        RebuildBonusTotals();

        foreach (ResearchData research in restored)
            OnCompleted?.Invoke(research);
    }

    // 식별자로 연구를 되찾을 때 쓸 연구 목록. 인스펙터에 없으면 Resources에서 한 번 읽어 캐싱한다.
    private ResearchDataList ResearchList =>
        _researchDataList != null ? _researchDataList : _researchDataList = Resources.Load<ResearchDataList>(ResearchListResourcePath);

    // 연구 현황 저장 상태의 직렬화 래퍼. JsonUtility가 HashSet을 그대로 다루지 못해 목록으로 바꿔 담는다.
    [Serializable]
    private class SaveState
    {
        public List<string> CompletedIds = new();
    }
}
