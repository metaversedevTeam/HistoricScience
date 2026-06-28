using UnityEngine;

// 위치 또는 대상을 향해 이동하는 기능을 정의하는 인터페이스
public interface IMover
{
    // 지정 위치로 이동; 도달 불가능하면 false 반환
    bool Move(Vector2 targetPos);
    // 대상 Transform을 추적; 순환 체인이나 도달 불가면 false 반환
    bool Move(Transform targetTransform);
    // 현재 이동을 즉시 중지
    void Stop();
}
