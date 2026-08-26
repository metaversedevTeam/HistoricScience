using UnityEngine;

public interface IGatherable
{
    bool CanGather();
    // 채집이 시작되는 시점을 알려, 첫 자원도 채집 시간을 기다린 뒤에 나오게 한다.
    void OnGatherBegin();
    (bool isSuccess, ItemData itemType, int count) OnGather();
}
