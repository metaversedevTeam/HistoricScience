using System.Collections.Generic;
using UnityEngine;

// IGatherable을 구현하여 채집 쿨타임을 가지며, 여러 후보 아이템 중 하나를 가중치 비율로 뽑아 주는 채집 가능한 오브젝트
public class GatherableObject : MonoBehaviour, IGatherable
{
    // 채집할 때마다 이 목록에서 가중치 비율에 따라 하나가 뽑힌다.
    [SerializeField] private List<GatherDrop> _gatherDrops = new List<GatherDrop>();
    [SerializeField] private float _gatherCooldown = 3f;
    // 채집 쿨타임을 줄여 주는 연구 보너스. 비워 두면 인스펙터에 설정한 쿨타임을 그대로 쓴다.
    [SerializeField] private ResearchBonusData _gatherSpeedBonus;

    private float _lastGatherTime = float.NegativeInfinity;

    // 연구 보너스를 반영한 실제 채집 쿨타임(초). 채집 속도 배율이 높을수록 쿨타임이 짧아진다.
    private float EffectiveCooldown
    {
        get
        {
            if (_gatherSpeedBonus == null) return _gatherCooldown;

            // 보너스가 음수로 쌓여 배율이 0 이하가 되면 나눗셈 결과가 깨지므로 하한을 둔다.
            float multiplier = Mathf.Max(0.01f, ResearchManager.Instance.GetMultiplier(_gatherSpeedBonus));
            return _gatherCooldown / multiplier;
        }
    }

    // 채집이 시작될 때 호출되어 쿨타임을 지금부터 다시 세게 한다. 이 때문에 첫 자원도 채집 시간을 기다린 뒤에 나온다.
    public void OnGatherBegin()
    {
        _lastGatherTime = Time.time;
    }

    // 쿨타임이 지나 채집 가능한 상태인지 확인한다. 쿨타임에는 연구 보너스가 반영된다.
    public bool CanGather()
    {
        return Time.time - _lastGatherTime >= EffectiveCooldown;
    }

    // 채집을 수행하여 무작위로 뽑힌 결과 아이템을 반환하고 쿨타임을 시작한다.
    public (bool isSuccess, ItemData itemType, int count) OnGather()
    {
        if (!CanGather()) return (false, null, 0);

        GatherDrop drop = PickRandomDrop();
        if (drop == null) return (false, null, 0);

        _lastGatherTime = Time.time;
        return (true, drop.ItemData, drop.Amount);
    }

    // 유효한 후보들의 가중치 합을 구한 뒤 그 비율대로 아이템 하나를 뽑는다. 유효한 후보가 없으면 null을 반환한다.
    private GatherDrop PickRandomDrop()
    {
        float totalWeight = GetTotalWeight();
        if (totalWeight <= 0f) return null;

        float pick = Random.value * totalWeight;
        GatherDrop lastValid = null;

        foreach (GatherDrop drop in _gatherDrops)
        {
            if (drop == null || !drop.IsValid()) continue;

            lastValid = drop;
            pick -= drop.Weight;
            if (pick <= 0f) return drop;
        }

        // 부동소수점 오차로 끝까지 뽑히지 않은 경우 마지막 유효 후보를 돌려준다.
        return lastValid;
    }

    // 유효한 후보들의 가중치 총합을 계산한다.
    private float GetTotalWeight()
    {
        float totalWeight = 0f;

        foreach (GatherDrop drop in _gatherDrops)
        {
            if (drop == null || !drop.IsValid()) continue;
            totalWeight += drop.Weight;
        }

        return totalWeight;
    }
}
