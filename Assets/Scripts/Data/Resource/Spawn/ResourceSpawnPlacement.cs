using UnityEngine;

// 자원 소스 하나가 놓일 자리(청크 루트 기준 로컬 위치와 Y축 회전)를 담는 읽기 전용 묶음
public readonly struct ResourceSpawnPlacement
{
    // 청크 루트 기준 로컬 위치
    public readonly Vector3 LocalPosition;
    // Y축 회전 각도(도 단위)
    public readonly float RotationY;

    public ResourceSpawnPlacement(Vector3 localPosition, float rotationY)
    {
        LocalPosition = localPosition;
        RotationY = rotationY;
    }
}
