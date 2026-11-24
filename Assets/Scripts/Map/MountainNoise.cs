using UnityEngine;

// 산 바이옴의 능선·봉우리 모양을 정하는 능선 노이즈 설정을 묶은 불변 값 묶음. MapTerrainSettings가 만들고 MapInlandConfig에 담겨 맵 데이터로 전달된다.
public readonly struct MountainShapeConfig
{
    // 합산할 옥타브 수. 많을수록 큰 능선 위에 작은 바위 능선까지 겹쳐 거칠어진다.
    public readonly int Octaves;
    // 첫 옥타브의 주파수 (정규화 맵 좌표 기준). 클수록 능선 사이 간격이 좁아져 봉우리가 촘촘해진다.
    public readonly float BaseFrequency;
    // 옥타브마다 주파수에 곱해지는 값
    public readonly float Lacunarity;
    // 옥타브마다 세기에 곱해지는 값 (0~1)
    public readonly float Persistence;
    // 산 높이 중 능선 노이즈가 깎아 내는 비율 (0~1). 0이면 기존처럼 매끈한 언덕, 1이면 계곡이 산 바닥까지 깊게 파인다.
    public readonly float RidgeStrength;
    // 능선 노이즈에 거는 지수. 1보다 크면 봉우리가 뾰족해지고 계곡이 넓어진다.
    public readonly float RidgeSharpness;
    // 샘플 좌표를 비트는 세기 (정규화 맵 좌표). 클수록 능선이 곧게 반복되지 않고 휘어지며 갈라진다.
    public readonly float WarpStrength;

    public MountainShapeConfig(int octaves, float baseFrequency, float lacunarity, float persistence, float ridgeStrength, float ridgeSharpness, float warpStrength)
    {
        Octaves = Mathf.Max(octaves, 1);
        BaseFrequency = Mathf.Max(baseFrequency, 0.0001f);
        Lacunarity = Mathf.Max(lacunarity, 1f);
        Persistence = Mathf.Clamp01(persistence);
        RidgeStrength = Mathf.Clamp01(ridgeStrength);
        RidgeSharpness = Mathf.Max(ridgeSharpness, 0.01f);
        WarpStrength = Mathf.Max(warpStrength, 0f);
    }
}

// 능선 다중 프랙탈(ridged multifractal) 노이즈로 산 높이에 곱할 형태 배율을 계산하는 불변 클래스 (생성 후 상태가 변하지 않아 여러 스레드에서 동시에 읽어도 안전하다)
public sealed class MountainNoise
{
    // 시드 오프셋 범위. Mathf.PerlinNoise는 float 좌표라, 좌표가 너무 크면 정밀도가 떨어져 고주파 옥타브에 계단 무늬가 생긴다.
    private const float k_OffsetRange = 1000f;
    // 좌표 왜곡용 노이즈의 주파수를 첫 옥타브 주파수의 몇 배로 할지. 능선보다 완만하게 비틀어야 능선이 끊기지 않고 휘어진다.
    private const float k_WarpFrequencyRatio = 0.5f;
    // 이전 옥타브 값이 다음 옥타브 세기에 미치는 배율. 능선 근처(값이 큰 곳)에만 잔 능선이 붙고 계곡은 매끈하게 남게 한다.
    private const float k_OctaveWeightGain = 2f;
    // 시드에 섞어 다른 노이즈(높이 굴곡 등)와 오프셋이 겹치지 않게 하는 값
    private const int k_SeedSalt = 0x4D4F554E;

    private readonly MountainShapeConfig m_Config;
    // 옥타브마다 서로 다른 패턴이 나오도록 시드로 미리 계산해 둔 샘플 오프셋
    private readonly Vector2[] m_OctaveOffsets;
    // 좌표 왜곡의 x/y 성분을 서로 다른 패턴으로 만들기 위한 샘플 오프셋
    private readonly Vector2 m_WarpOffsetX;
    private readonly Vector2 m_WarpOffsetY;
    // 옥타브 세기 합. 결과를 0~1로 정규화할 때 쓴다.
    private readonly float m_AmplitudeSum;

    // 시드와 산 형태 설정으로 옥타브·왜곡 오프셋을 미리 계산한다.
    public MountainNoise(int seed, MountainShapeConfig config)
    {
        m_Config = config;

        System.Random random = new System.Random(seed ^ k_SeedSalt);
        m_OctaveOffsets = new Vector2[config.Octaves];
        for (int i = 0; i < m_OctaveOffsets.Length; i++)
            m_OctaveOffsets[i] = HandleNextOffset(random);

        m_WarpOffsetX = HandleNextOffset(random);
        m_WarpOffsetY = HandleNextOffset(random);
        m_AmplitudeSum = HandleGetAmplitudeSum(config);
    }

    // 주어진 맵 좌표에서 산 높이에 곱할 형태 배율(0~1)을 반환한다. 능선 위는 1에 가깝고, 계곡은 (1 - 능선 세기)까지 내려간다.
    public float SampleShape(Vector2 position)
    {
        float ridge = HandleSampleRidged(HandleWarp(position));
        float sharpened = Mathf.Pow(ridge, m_Config.RidgeSharpness);
        return Mathf.Lerp(1f, sharpened, m_Config.RidgeStrength);
    }

    // 저주파 노이즈로 샘플 좌표를 비틀어, 능선이 격자처럼 곧게 반복되지 않게 한다.
    private Vector2 HandleWarp(Vector2 position)
    {
        if (m_Config.WarpStrength <= 0f)
            return position;

        float frequency = m_Config.BaseFrequency * k_WarpFrequencyRatio;
        float warpX = Mathf.PerlinNoise(position.x * frequency + m_WarpOffsetX.x, position.y * frequency + m_WarpOffsetX.y) - 0.5f;
        float warpY = Mathf.PerlinNoise(position.x * frequency + m_WarpOffsetY.x, position.y * frequency + m_WarpOffsetY.y) - 0.5f;

        return position + new Vector2(warpX, warpY) * (m_Config.WarpStrength * 2f);
    }

    // 옥타브마다 펄린 노이즈의 중간값(0.5)을 능선으로 접어 합산한다. 이전 옥타브가 높은 곳일수록 다음 옥타브가 강하게 붙는다. (0~1)
    private float HandleSampleRidged(Vector2 position)
    {
        float frequency = m_Config.BaseFrequency;
        float amplitude = 1f;
        float weight = 1f;
        float sum = 0f;

        for (int i = 0; i < m_OctaveOffsets.Length; i++)
        {
            float noise = Mathf.PerlinNoise(position.x * frequency + m_OctaveOffsets[i].x, position.y * frequency + m_OctaveOffsets[i].y);
            float ridge = 1f - Mathf.Abs(noise * 2f - 1f);
            ridge *= ridge;
            ridge *= weight;

            weight = Mathf.Clamp01(ridge * k_OctaveWeightGain);
            sum += ridge * amplitude;

            amplitude *= m_Config.Persistence;
            frequency *= m_Config.Lacunarity;
        }

        return Mathf.Clamp01(sum / m_AmplitudeSum);
    }

    // 모든 옥타브 세기의 합을 구한다.
    private static float HandleGetAmplitudeSum(MountainShapeConfig config)
    {
        float amplitude = 1f;
        float sum = 0f;
        for (int i = 0; i < config.Octaves; i++)
        {
            sum += amplitude;
            amplitude *= config.Persistence;
        }

        return Mathf.Max(sum, 0.0001f);
    }

    // 시드 난수로 펄린 샘플 오프셋 하나를 만든다.
    private static Vector2 HandleNextOffset(System.Random random)
    {
        return new Vector2((float)(random.NextDouble() * k_OffsetRange), (float)(random.NextDouble() * k_OffsetRange));
    }
}
