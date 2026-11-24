using UnityEngine;

// 플레이어가 설정 화면에서 바꾸는 값(카메라 수평 감도, 청크 로딩·유지 범위)을 PlayerPrefs에 보관하고 읽어 주는 정적 저장소.
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

    // 청크 로딩 범위(청크 단위)의 최솟값
    public const int MinChunkLoadRadius = 1;

    // 청크 로딩 범위(청크 단위)의 최댓값
    public const int MaxChunkLoadRadius = 10;

    // 청크 로딩 범위(청크 단위)의 기본값
    public const int DefaultChunkLoadRadius = 2;

    // 청크 유지 범위(청크 단위)의 최솟값
    public const int MinChunkKeepRadius = 2;

    // 청크 유지 범위(청크 단위)의 최댓값
    public const int MaxChunkKeepRadius = 14;

    // 청크 유지 범위(청크 단위)의 기본값
    public const int DefaultChunkKeepRadius = 4;

    // 유지 범위가 로딩 범위보다 최소한 이만큼 커야 경계에서 로딩과 해제가 반복되지 않는다.
    public const int MinChunkKeepMargin = 1;

    // 청크 로딩 범위를 저장하는 PlayerPrefs 키
    private const string k_ChunkLoadRadiusKey = "Settings.ChunkLoadRadius";

    // 청크 유지 범위를 저장하는 PlayerPrefs 키
    private const string k_ChunkKeepRadiusKey = "Settings.ChunkKeepRadius";

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

    // 추적 대상 주변 이 반경(청크 단위) 안의 청크를 로딩한다(1~10).
    // 값을 바꿀 때 유지 범위가 로딩 범위 + 1보다 작아지면 유지 범위를 함께 늘린다.
    public static int ChunkLoadRadius
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(k_ChunkLoadRadiusKey, DefaultChunkLoadRadius), MinChunkLoadRadius, MaxChunkLoadRadius);
        set
        {
            int load = Mathf.Clamp(value, MinChunkLoadRadius, MaxChunkLoadRadius);
            PlayerPrefs.SetInt(k_ChunkLoadRadiusKey, load);

            if (ReadStoredKeepRadius() < load + MinChunkKeepMargin)
                PlayerPrefs.SetInt(k_ChunkKeepRadiusKey, load + MinChunkKeepMargin);
        }
    }

    // 모든 추적 대상에게서 이 반경(청크 단위) 밖으로 벗어난 청크만 해제한다(2~14). 항상 로딩 범위 + 1 이상이다.
    // 값을 바꿀 때 로딩 범위가 유지 범위 - 1보다 커지면 로딩 범위를 함께 줄인다.
    public static int ChunkKeepRadius
    {
        get => Mathf.Max(ReadStoredKeepRadius(), ChunkLoadRadius + MinChunkKeepMargin);
        set
        {
            int keep = Mathf.Clamp(value, MinChunkKeepRadius, MaxChunkKeepRadius);
            PlayerPrefs.SetInt(k_ChunkKeepRadiusKey, keep);

            if (ChunkLoadRadius > keep - MinChunkKeepMargin)
                PlayerPrefs.SetInt(k_ChunkLoadRadiusKey, keep - MinChunkKeepMargin);
        }
    }

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

    // 저장된 유지 범위를 최솟값~최댓값 사이로 잘라 읽는다. 로딩 범위와의 차이는 따지지 않는다.
    private static int ReadStoredKeepRadius()
    {
        return Mathf.Clamp(PlayerPrefs.GetInt(k_ChunkKeepRadiusKey, DefaultChunkKeepRadius), MinChunkKeepRadius, MaxChunkKeepRadius);
    }
}
