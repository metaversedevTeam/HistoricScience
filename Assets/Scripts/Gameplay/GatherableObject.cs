using UnityEngine;

// IGatherable을 구현하여 채집 쿨타임을 가진 채집 가능한 오브젝트
public class GatherableObject : MonoBehaviour, IGatherable
{
    [SerializeField] private ItemData _itemData;
    [SerializeField] private int _gatherAmount = 1;
    [SerializeField] private float _gatherCooldown = 3f;

    private float _lastGatherTime = float.NegativeInfinity;

    // 쿨타임이 지나 채집 가능한 상태인지 확인한다.
    public bool CanGather()
    {
        return Time.time - _lastGatherTime >= _gatherCooldown;
    }

    // 채집을 수행하여 결과 아이템을 반환하고 쿨타임을 시작한다.
    public (bool isSuccess, ItemData itemType, int count) OnGather()
    {
        if (!CanGather()) return (false, null, 0);

        _lastGatherTime = Time.time;
        return (true, _itemData, _gatherAmount);
    }
}
