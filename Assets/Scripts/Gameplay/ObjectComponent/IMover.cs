using System;
using UnityEngine;

// 위치 또는 대상을 향해 이동하는 기능을 정의하는 인터페이스. 목적지에 완전히 도달할 수 없으면 갈 수 있는 데까지 이동을 시도한다.
public interface IMover
{
    // 요청한 목적지에 실제로 도달했을 때 발생. 경로가 막혀 갈 수 있는 데까지만 가고 멈춘 경우에는 발생하지 않는다.
    // 도착도 멈춤의 한 경우이므로, 이 이벤트가 발생한 직후 OnMoveEnd도 이어서 발생한다.
    event Action OnArrived;

    // 이동이 어떤 방식으로든 끝나 멈췄을 때 발생. 목적지 도착, 더 갈 수 없어 멈춤, Stop() 호출을 모두 포함한다.
    // 새 Move() 명령으로 목적지만 바뀐 경우는 그대로 이동이 이어지므로 발생하지 않는다.
    event Action OnMoveEnd;

    // 지정 위치로 이동; 이동 자체가 불가능하면 false 반환
    bool Move(Vector2 targetPos);
    // 대상 Transform을 추적; 순환 체인이거나 이동 자체가 불가능하면 false 반환
    bool Move(Transform targetTransform);
    // 현재 이동을 즉시 중지
    void Stop();
}
