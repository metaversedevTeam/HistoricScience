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
    // onArrived/onMoveEnd는 이 호출로 시작된 이동에 대해서만 최대 한 번 호출되는 콜백으로, 발생 조건은 같은 이름의
    // 이벤트와 같고 이벤트 구독자가 끼어들어 취소하지 못하도록 이벤트보다 먼저 호출된다.
    // 콜백이 호출되기 전에 Move()가 다시 호출되면 이 이동은 취소된 것이므로, 그 호출의 성공 여부와 관계없이 콜백도 폐기된다.
    // stoppingDistance는 목적지에서 이 거리 이내에 들어오면 도착으로 인정한다(기본 0이면 목적지 지점에 정확히 도달해야 한다).
    // 목적지에 부피가 있는 오브젝트(건물 등)가 있어 그 중심까지 겹쳐 들어갈 수 없을 때 사용한다.
    bool Move(Vector2 targetPos, Action onArrived = null, Action onMoveEnd = null, float stoppingDistance = 0f);
    // 대상 Transform을 추적; 순환 체인이거나 이동 자체가 불가능하면 false 반환
    // 추적은 대상이 다시 멀어지면 이동이 이어져 종료 시점이 하나로 정해지지 않으므로, 대상에 처음 도달했을 때 한 번 호출되는
    // onArrived만 받는다. 중간에 막혀 멈춘 것은 도달이 아니므로 그때는 호출되지 않고 추적과 함께 유지된다.
    bool Move(Transform targetTransform, Action onArrived = null);
    // 현재 이동을 즉시 중지; 이동 중이었다면 대기 중인 이동 종료 콜백도 호출한다(도착 콜백은 폐기)
    void Stop();
}
