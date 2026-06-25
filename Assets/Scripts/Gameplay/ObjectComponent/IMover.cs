using UnityEngine;

public interface IMover
{
    // 지정 위치로 이동; 도달 불가능하면 false 반환
    bool Move(Vector2 targetPos);
    // 대상 Transform을 추적; 순환 체인이나 도달 불가면 false 반환
    bool Move(Transform targetTransform);
}
