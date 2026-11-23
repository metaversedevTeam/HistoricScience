using UnityEngine;

// 플레이어가 설정 화면에서 바꾸는 값(현재는 카메라 수평 감도)을 PlayerPrefs에 보관하고 읽어 주는 정적 저장소.
// 메인 메뉴와 인게임이 같은 값을 공유하며, 게임을 다시 켜도 유지된다.
public static class GameSettings
{
    // 설정 화면에 보이는 수평 감도의 최솟값
    public const float MinHorizontalLookSensitivity = 0.2f;

    // 설정 화면에 보이는 수평 감도의 최댓값
    public const float MaxHorizontalLookSensitivity = 2f;

    // 설정 화면에 보이는 수평 감도의 기본값
    public const float DefaultHorizontalLookSensitivity = 1f;

    // 설정 화면의 수평 감도를 카메라에 실제로 적용할 때 곱하는 배율
    private const float k_HorizontalApplyScale = 0.25f;

    // 수직 감도를 수평 감도에 대해 맞추는 비율. 수직은 항상 수평의 절반이다.
    private const float k_VerticalToHorizontalRatio = 0.5f;

    // 수평 감도를 저장하는 PlayerPrefs 키
    private const string k_HorizontalLookSensitivityKey = "Settings.HorizontalLookSensitivity";

    // 설정 화면에 보이는 수평 감도(0.2~2.0)
    public static float HorizontalLookSensitivity
    {
        get => ClampHorizontal(PlayerPrefs.GetFloat(k_HorizontalLookSensitivityKey, DefaultHorizontalLookSensitivity));
        set => PlayerPrefs.SetFloat(k_HorizontalLookSensitivityKey, ClampHorizontal(value));
    }

    // 카메라 좌우 회전에 실제로 곱하는 배율. 설정 화면의 수평 감도에 0.25를 곱한 값이다.
    public static float AppliedHorizontalLookSensitivity => HorizontalLookSensitivity * k_HorizontalApplyScale;

    // 카메라 위아래 회전에 실제로 곱하는 배율. 실제 수평 배율의 절반이다.
    public static float AppliedVerticalLookSensitivity => AppliedHorizontalLookSensitivity * k_VerticalToHorizontalRatio;

    // 바뀐 설정을 디스크에 기록한다. 설정 화면을 닫을 때 부른다.
    public static void Save()
    {
        PlayerPrefs.Save();
    }

    // 수평 감도를 최솟값~최댓값 사이로 자른다.
    private static float ClampHorizontal(float value)
    {
        return Mathf.Clamp(value, MinHorizontalLookSensitivity, MaxHorizontalLookSensitivity);
    }
}
