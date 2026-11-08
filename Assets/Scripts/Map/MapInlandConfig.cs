using UnityEngine;

// 내륙 노이즈(inlandNoise)로 산/바다 배치와 산 높이를 조절하는 규칙 값을 묶어 MapData와 BiomeRegionMap에 넘기는 불변 구조체
public readonly struct MapInlandConfig
{
    // 중간 구간(바다 기준 초과 ~ 산 기준 미만)에서 판정용 고도에 더할 보정의 세기. 고도에 (내륙 노이즈 - 0.5) × 이 값이 더해진다.
    public readonly float ElevationBias;
    // 내륙 노이즈가 이 값 이상이면 무조건 산 바이옴이 배치된다.
    public readonly float MountainThreshold;
    // 내륙 노이즈가 이 값 이하이면 무조건 바다 바이옴이 배치된다.
    public readonly float SeaThreshold;
    // 산 바이옴의 높이에 더할 값의 배율(m). 산 바이옴의 높이에 높이 커브(내륙 노이즈) × 이 값이 더해진다.
    public readonly float MountainHeightScale;
    // 높이 커브를 0~1 구간에서 균등 간격으로 미리 샘플링한 값. AnimationCurve는 백그라운드 스레드에서 안전하게 읽을 수 없어 배열로 들고 다닌다.
    // 이 구조체 밖으로 내보내지 않고 읽기만 하므로 불변으로 취급한다. null이면 커브 없이 내륙 노이즈를 그대로 쓴다.
    private readonly float[] m_HeightCurveSamples;

    // 내륙 노이즈 규칙 값과 미리 샘플링한 높이 커브로 설정을 만든다.
    public MapInlandConfig(float elevationBias, float mountainThreshold, float seaThreshold, float mountainHeightScale, float[] heightCurveSamples)
    {
        ElevationBias = elevationBias;
        MountainThreshold = mountainThreshold;
        SeaThreshold = seaThreshold;
        MountainHeightScale = mountainHeightScale;
        m_HeightCurveSamples = heightCurveSamples;
    }

    // 높이 커브를 sampleCount개 지점에서 샘플링해 MapInlandConfig에 넘길 배열을 만든다. 메인 스레드에서 호출해야 한다.
    public static float[] BakeHeightCurve(AnimationCurve curve, int sampleCount)
    {
        if (curve == null || sampleCount < 2)
            return null;

        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
            samples[i] = curve.Evaluate((float)i / (sampleCount - 1));

        return samples;
    }

    // 내륙 노이즈 값에 높이 커브를 적용한 값을 반환한다. 샘플 사이는 선형 보간하고, 커브가 없으면 입력값을 그대로 반환한다.
    public float EvaluateHeightCurve(float inland)
    {
        if (m_HeightCurveSamples == null || m_HeightCurveSamples.Length < 2)
            return inland;

        float position = Mathf.Clamp01(inland) * (m_HeightCurveSamples.Length - 1);
        int index = Mathf.Min((int)position, m_HeightCurveSamples.Length - 2);
        return Mathf.Lerp(m_HeightCurveSamples[index], m_HeightCurveSamples[index + 1], position - index);
    }
}
