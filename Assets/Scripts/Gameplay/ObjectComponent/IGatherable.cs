using UnityEngine;

public interface IGatherable
{
    bool CanGather();
    (bool isSuccess, ItemData itemType, int count) OnGather();
}
