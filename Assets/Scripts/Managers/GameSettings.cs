using System;
using UnityEngine;

// 플레이어가 설정 화면에서 바꾸는 값(카메라 수평 감도, 청크 LOD0·LOD1·LOD2 로딩 범위, 안개 시작 범위, 흑백 강조 사용 여부)을 PlayerPrefs에 보관하고 읽어 주는 정적 저장소.
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

    // LOD0·LOD1 로딩 범위(청크 단위)의 최솟값
    public const int MinChunkLoadRadius = 1;

    // LOD0 로딩 범위(청크 단위)의 최댓값
    public const int MaxChunkLod0LoadRadius = 10;

    // LOD0 로딩 범위(청크 단위)의 기본값
    public const int DefaultChunkLod0LoadRadius = 2;

    // LOD1 로딩 범위(청크 단위)의 최댓값
    public const int MaxChunkLod1LoadRadius = 16;

    // LOD1 로딩 범위(청크 단위)의 기본값
    public const int DefaultChunkLod1LoadRadius = 5;

    // LOD2 로딩 범위는 항상 LOD1 로딩 범위보다 최소 이만큼 크다.
    public const int MinChunkLod2Margin = 1;

    // LOD2 로딩 범위(청크 단위)의 최솟값. LOD1 로딩 범위의 최솟값 + 최소 차이다.
    public const int MinChunkLod2LoadRadius = MinChunkLoadRadius + MinChunkLod2Margin;

    // LOD2 로딩 범위(청크 단위)의 최댓값
    public const int MaxChunkLod2LoadRadius = 24;

    // LOD2 로딩 범위(청크 단위)의 기본값
    public const int DefaultChunkLod2LoadRadius = 8;

    // 안개 시작 범위는 항상 LOD1 로딩 범위보다 최소 이만큼 작다.
    public const int MinFogStartLod1Margin = 1;

    // 안개 시작 범위(청크 단위)의 최솟값. LOD1 로딩 범위의 최솟값 - 최소 차이다.
    public const int MinFogStartChunkRadius = MinChunkLoadRadius - MinFogStartLod1Margin;

    // 안개 시작 범위(청크 단위)의 최댓값. LOD1 로딩 범위의 최댓값 - 최소 차이다.
    public const int MaxFogStartChunkRadius = MaxChunkLod1LoadRadius - MinFogStartLod1Margin;

    // 안개 시작 범위(청크 단위)의 기본값. 예전처럼 LOD0 로딩 범위 기본값과 같은 거리에서 시작한다.
    public const int DefaultFogStartChunkRadius = 2;

    // 유지 범위는 로딩 범위보다 이만큼 크게 잡아, 경계에서 로딩과 해제(또는 LOD 전환)가 반복되지 않게 한다.
    private const int k_ChunkKeepMargin = 1;

    // LOD0 로딩 범위를 저장하는 PlayerPrefs 키. 예전 단일 로딩 범위 키를 그대로 써서 기존 설정값을 이어받는다.
    private const string k_ChunkLod0LoadRadiusKey = "Settings.ChunkLoadRadius";

    // LOD1 로딩 범위를 저장하는 PlayerPrefs 키
    private const string k_ChunkLod1LoadRadiusKey = "Settings.ChunkLod1LoadRadius";

    // LOD2 로딩 범위를 저장하는 PlayerPrefs 키
    private const string k_ChunkLod2LoadRadiusKey = "Settings.ChunkLod2LoadRadius";

    // 안개 시작 범위를 저장하는 PlayerPrefs 키
    private const string k_FogStartChunkRadiusKey = "Settings.FogStartChunkRadius";

    // 흑백 강조의 기본 사용 여부
    public const bool DefaultMonochromeFocusEnabled = true;

    // 흑백 강조 사용 여부를 저장하는 PlayerPrefs 키
    private const string k_MonochromeFocusEnabledKey = "Settings.MonochromeFocusEnabled";

    // 흑백 강조 사용 여부가 바뀌면 새 값과 함께 알린다. 흑백 강조 중인 화면을 바로 컬러로 되돌릴 때 쓴다.
    public static event Action<bool> MonochromeFocusEnabledChanged;

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

    // 추적 대상 주변 이 반경(청크 단위) 안의 청크를 풀·자원·알파맵 블러까지 갖춘 LOD0로 로딩한다(1~10).
    // 값을 바꿀 때 LOD1 로딩 범위가 이보다 작아지면 LOD1 로딩 범위를 함께 늘린다.
    public static int ChunkLod0LoadRadius
    {
        get => ReadStoredLod0LoadRadius();
        set
        {
            int lod0 = Mathf.Clamp(value, MinChunkLoadRadius, MaxChunkLod0LoadRadius);
            PlayerPrefs.SetInt(k_ChunkLod0LoadRadiusKey, lod0);

            if (ReadStoredLod1LoadRadius() < lod0)
                PlayerPrefs.SetInt(k_ChunkLod1LoadRadiusKey, lod0);

            HandlePushLod2AboveLod1();
        }
    }

    // 추적 대상 주변 이 반경(청크 단위) 안에서 LOD0 범위 밖의 청크를 풀·자원·알파맵 블러 없는 LOD1로 로딩한다(1~16). 항상 LOD0 로딩 범위 이상이다.
    // 값을 바꿀 때 LOD0 로딩 범위가 이보다 커지면 LOD0 로딩 범위를 함께 줄이고, LOD2 로딩 범위가 이보다 1 이상 크지 않으면 LOD2 로딩 범위를 함께 늘린다.
    public static int ChunkLod1LoadRadius
    {
        get => Mathf.Max(ReadStoredLod1LoadRadius(), ReadStoredLod0LoadRadius());
        set
        {
            int lod1 = Mathf.Clamp(value, MinChunkLoadRadius, MaxChunkLod1LoadRadius);
            PlayerPrefs.SetInt(k_ChunkLod1LoadRadiusKey, lod1);

            if (ReadStoredLod0LoadRadius() > lod1)
                PlayerPrefs.SetInt(k_ChunkLod0LoadRadiusKey, lod1);

            HandlePushLod2AboveLod1();
            HandlePullFogStartBelowLod1();
        }
    }

    // 추적 대상 주변 이 반경(청크 단위) 안에서 LOD1 범위 밖의 청크를 알파맵까지 칠하지 않는 LOD2로 로딩한다(2~24). 항상 LOD1 로딩 범위보다 1 이상 크다.
    // 값을 바꿀 때 LOD1 로딩 범위가 이보다 1 이상 작지 않으면 LOD1 로딩 범위(와 필요하면 LOD0 로딩 범위)를 함께 줄인다.
    public static int ChunkLod2LoadRadius
    {
        get => Mathf.Max(ReadStoredLod2LoadRadius(), ChunkLod1LoadRadius + MinChunkLod2Margin);
        set
        {
            int lod2 = Mathf.Clamp(value, MinChunkLod2LoadRadius, MaxChunkLod2LoadRadius);
            PlayerPrefs.SetInt(k_ChunkLod2LoadRadiusKey, lod2);

            int maxLod1 = lod2 - MinChunkLod2Margin;
            if (ReadStoredLod1LoadRadius() > maxLod1)
                PlayerPrefs.SetInt(k_ChunkLod1LoadRadiusKey, maxLod1);

            if (ReadStoredLod0LoadRadius() > maxLod1)
                PlayerPrefs.SetInt(k_ChunkLod0LoadRadiusKey, maxLod1);

            HandlePullFogStartBelowLod1();
        }
    }

    // 추적 대상 주변 이 반경(청크 단위)의 최소 거리부터 거리 안개가 시작된다(0~15). 항상 LOD1 로딩 범위보다 1 이상 작다.
    // 값을 바꿀 때 LOD1 로딩 범위가 이보다 1 이상 크지 않으면 LOD1 로딩 범위(와 필요하면 LOD2 로딩 범위)를 함께 늘린다.
    public static int FogStartChunkRadius
    {
        get => Mathf.Min(ReadStoredFogStartChunkRadius(), ChunkLod1LoadRadius - MinFogStartLod1Margin);
        set
        {
            int fogStart = Mathf.Clamp(value, MinFogStartChunkRadius, MaxFogStartChunkRadius);
            PlayerPrefs.SetInt(k_FogStartChunkRadiusKey, fogStart);

            int minLod1 = fogStart + MinFogStartLod1Margin;
            if (ChunkLod1LoadRadius < minLod1)
                ChunkLod1LoadRadius = minLod1;
        }
    }

    // 모든 추적 대상에게서 이 반경(청크 단위) 밖으로 벗어난 LOD0 청크를 LOD1로 낮춘다. 항상 LOD0 로딩 범위 + 1이다.
    public static int ChunkLod0KeepRadius => ChunkLod0LoadRadius + k_ChunkKeepMargin;

    // 모든 추적 대상에게서 이 반경(청크 단위) 밖으로 벗어난 LOD0·LOD1 청크를 LOD2로 낮춘다. 항상 LOD1 로딩 범위 + 1이다.
    public static int ChunkLod1KeepRadius => ChunkLod1LoadRadius + k_ChunkKeepMargin;

    // 모든 추적 대상에게서 이 반경(청크 단위) 밖으로 벗어난 청크를 해제한다. 항상 LOD2 로딩 범위 + 1이다.
    public static int ChunkLod2KeepRadius => ChunkLod2LoadRadius + k_ChunkKeepMargin;

    // 명령 목록이 뜰 때 대상과 시민만 남기고 화면을 흑백으로 바꾸는 흑백 강조를 쓸지 여부
    public static bool MonochromeFocusEnabled
    {
        get => PlayerPrefs.GetInt(k_MonochromeFocusEnabledKey, DefaultMonochromeFocusEnabled ? 1 : 0) != 0;
        set
        {
            if (MonochromeFocusEnabled == value)
                return;

            PlayerPrefs.SetInt(k_MonochromeFocusEnabledKey, value ? 1 : 0);
            MonochromeFocusEnabledChanged?.Invoke(value);
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

    // 저장된 LOD0 로딩 범위를 최솟값~최댓값 사이로 잘라 읽는다.
    private static int ReadStoredLod0LoadRadius()
    {
        return Mathf.Clamp(PlayerPrefs.GetInt(k_ChunkLod0LoadRadiusKey, DefaultChunkLod0LoadRadius), MinChunkLoadRadius, MaxChunkLod0LoadRadius);
    }

    // 저장된 LOD1 로딩 범위를 최솟값~최댓값 사이로 잘라 읽는다. LOD0 로딩 범위와의 대소는 따지지 않는다.
    private static int ReadStoredLod1LoadRadius()
    {
        return Mathf.Clamp(PlayerPrefs.GetInt(k_ChunkLod1LoadRadiusKey, DefaultChunkLod1LoadRadius), MinChunkLoadRadius, MaxChunkLod1LoadRadius);
    }

    // 저장된 LOD2 로딩 범위를 최솟값~최댓값 사이로 잘라 읽는다. LOD1 로딩 범위와의 대소는 따지지 않는다.
    private static int ReadStoredLod2LoadRadius()
    {
        return Mathf.Clamp(PlayerPrefs.GetInt(k_ChunkLod2LoadRadiusKey, DefaultChunkLod2LoadRadius), MinChunkLod2LoadRadius, MaxChunkLod2LoadRadius);
    }

    // 저장된 LOD2 로딩 범위가 LOD1 로딩 범위보다 1 이상 크지 않으면 LOD1 로딩 범위 + 1로 늘린다. LOD0·LOD1 로딩 범위를 바꾼 뒤 부른다.
    private static void HandlePushLod2AboveLod1()
    {
        int minLod2 = ChunkLod1LoadRadius + MinChunkLod2Margin;
        if (ReadStoredLod2LoadRadius() < minLod2)
            PlayerPrefs.SetInt(k_ChunkLod2LoadRadiusKey, minLod2);
    }

    // 저장된 안개 시작 범위를 최솟값~최댓값 사이로 잘라 읽는다. LOD1 로딩 범위와의 대소는 따지지 않는다.
    private static int ReadStoredFogStartChunkRadius()
    {
        return Mathf.Clamp(PlayerPrefs.GetInt(k_FogStartChunkRadiusKey, DefaultFogStartChunkRadius), MinFogStartChunkRadius, MaxFogStartChunkRadius);
    }

    // 저장된 안개 시작 범위가 LOD1 로딩 범위보다 1 이상 작지 않으면 LOD1 로딩 범위 - 1로 줄인다. LOD1·LOD2 로딩 범위를 바꾼 뒤 부른다.
    private static void HandlePullFogStartBelowLod1()
    {
        int maxFogStart = ChunkLod1LoadRadius - MinFogStartLod1Margin;
        if (ReadStoredFogStartChunkRadius() > maxFogStart)
            PlayerPrefs.SetInt(k_FogStartChunkRadiusKey, maxFogStart);
    }
}
