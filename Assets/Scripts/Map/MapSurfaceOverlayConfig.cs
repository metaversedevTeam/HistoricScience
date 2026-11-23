using UnityEngine;

// 바이옴 페인팅 위에 지형의 경사와 고도에 따라 절벽(바위)·눈 레이어를 덮는 규칙을 담은 불변 값 묶음. MapTerrainSettings가 만들고 MapTerrainConfig에 담겨 전달된다.
// 터레인 레이어 참조는 메인 스레드에서 터레인에 레이어를 등록할 때만 읽고, 백그라운드 계산은 숫자 규칙(Evaluate)만 쓴다.
public readonly struct MapSurfaceOverlayConfig
{
    // 눈 경계선을 흔드는 노이즈의 스케일 (정규화 맵 좌표 기준). 경계가 등고선처럼 곧게 그어지지 않게 한다.
    private const float k_SnowLineNoiseScale = 2.5f;

    // 가파른 경사에 덮을 절벽 레이어. 비어 있으면 절벽 덮기를 하지 않는다.
    public readonly TerrainLayer CliffLayer;
    // 절벽 레이어가 나타나기 시작하는 경사(도)와 완전히 덮는 경사(도)
    public readonly Vector2 CliffSlopeRange;
    // 높은 곳에 덮을 눈 레이어. 비어 있으면 눈 덮기를 하지 않는다.
    public readonly TerrainLayer SnowLayer;
    // 눈이 나타나기 시작하는 높이(m)와 완전히 덮는 높이(m)
    public readonly Vector2 SnowHeightRange;
    // 눈이 옅어지기 시작하는 경사(도)와 완전히 사라지는 경사(도). 너무 가파른 곳에는 눈이 쌓이지 않는다.
    public readonly Vector2 SnowSlopeRange;
    // 눈 경계 높이를 위치마다 흔드는 폭(m)
    public readonly float SnowLineJitter;

    public MapSurfaceOverlayConfig(TerrainLayer cliffLayer, Vector2 cliffSlopeRange, TerrainLayer snowLayer, Vector2 snowHeightRange, Vector2 snowSlopeRange, float snowLineJitter)
    {
        CliffLayer = cliffLayer;
        CliffSlopeRange = cliffSlopeRange;
        SnowLayer = snowLayer;
        SnowHeightRange = snowHeightRange;
        SnowSlopeRange = snowSlopeRange;
        SnowLineJitter = Mathf.Max(snowLineJitter, 0f);
    }

    // 맵 좌표의 경사(도)와 높이(m)로 절벽·눈 레이어의 덮는 비율(0~1)을 계산한다. 절벽이 눈보다 우선하며, 두 비율의 합은 1을 넘지 않는다.
    // 절벽은 잔 요철까지 드러나도록 좁은 범위의 경사(cliffSlope)를, 눈은 얼룩지지 않도록 넓은 범위의 평균 경사(snowSlope)를 받는다.
    // 레이어가 없는 쪽은 0을 반환한다. 맵 좌표만 쓰는 순수 계산이라 이웃 청크 경계에서도 같은 값이 나온다.
    public void Evaluate(Vector2 mapPosition, float cliffSlope, float snowSlope, float height, bool hasCliffLayer, bool hasSnowLayer, out float cliff, out float snow)
    {
        cliff = hasCliffLayer ? HandleSmoothStep(CliffSlopeRange.x, CliffSlopeRange.y, cliffSlope) : 0f;

        snow = 0f;
        if (hasSnowLayer)
        {
            float jitter = (Mathf.PerlinNoise(mapPosition.x * k_SnowLineNoiseScale, mapPosition.y * k_SnowLineNoiseScale) - 0.5f) * 2f * SnowLineJitter;
            float byHeight = HandleSmoothStep(SnowHeightRange.x, SnowHeightRange.y, height + jitter);
            float bySlope = 1f - HandleSmoothStep(SnowSlopeRange.x, SnowSlopeRange.y, snowSlope);
            snow = byHeight * bySlope * (1f - cliff);
        }
    }

    // 두 경계 사이에서 0~1로 부드럽게 올라가는 값을 반환한다. 경계가 같거나 뒤집혀 있으면 계단 함수처럼 동작한다.
    private static float HandleSmoothStep(float from, float to, float value)
    {
        if (to <= from)
            return value >= from ? 1f : 0f;

        float t = Mathf.Clamp01((value - from) / (to - from));
        return t * t * (3f - 2f * t);
    }
}
