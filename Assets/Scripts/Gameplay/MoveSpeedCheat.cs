using System;
using UnityEngine;

// 치트 관리 화면에서 조절하는 이동속도 배율. 모든 GroundMover가 연구 보너스에 더해 이 배율을 곱해 움직인다.
// 씬 오브젝트가 아니라 정적 상태로 두어, 시민이 새로 소환되거나 씬이 바뀌어도 게임을 끌 때까지 같은 배율이 유지된다.
public static class MoveSpeedCheat
{
    // 슬라이더로 고를 수 있는 최소 배율 (치트 꺼짐과 같다)
    public const float MinMultiplier = 1f;

    // 슬라이더로 고를 수 있는 최대 배율
    public const float MaxMultiplier = 10f;

    // 배율이 바뀌었을 때 발생한다. GroundMover가 구독해 에이전트 속도를 다시 계산한다.
    public static event Action OnMultiplierChanged;

    // 지금 적용 중인 이동속도 배율 (1~10)
    public static float Multiplier
    {
        get => s_multiplier;
        set
        {
            float clamped = Mathf.Clamp(value, MinMultiplier, MaxMultiplier);
            if (Mathf.Approximately(s_multiplier, clamped)) return;

            s_multiplier = clamped;
            OnMultiplierChanged?.Invoke();
        }
    }

    private static float s_multiplier = MinMultiplier;

    // 도메인 리로드를 끈 에디터 플레이에서도 이전 플레이의 배율과 구독자가 남지 않도록 초기화한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        s_multiplier = MinMultiplier;
        OnMultiplierChanged = null;
    }
}
