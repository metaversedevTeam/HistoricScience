using UnityEngine;

// 주변의 IGatherable 대상을 채집하여 ResourceInventory에 결과를 추가하는 컴포넌트
public class Gatherer : MonoBehaviour
{
    [SerializeField] private ResourceInventory _inventory;
    [SerializeField] private float _gatherRange = 3f;

    // 지정한 IGatherable 대상이 채집 범위 안에 있는지 확인한다.
    private bool IsInRange(IGatherable target)
    {
        if (target is not Component component) return false;
        return IsInRange(component.transform.position);
    }

    private bool IsInRange(Vector3 pos)
    {
        Vector2 myPos = new Vector2(transform.position.x, transform.position.z);
        Vector2 targetPos = new Vector2(pos.x, pos.z);
        return Vector2.Distance(myPos, targetPos) <= _gatherRange;
    }

    // 대상을 채집하여 결과 아이템을 인벤토리에 추가한다. 범위 밖이거나 채집 불가 상태면 false를 반환한다.
    public bool TryGather(IGatherable target)
    {
        if (!IsInRange(target)) return false;
        if (!target.CanGather()) return false;

        var (isSuccess, itemType, count) = target.OnGather();
        if (isSuccess && itemType != null && count > 0)
            _inventory.Add(itemType, count);

        return true;
    }
}
