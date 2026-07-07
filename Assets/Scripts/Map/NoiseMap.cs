using UnityEngine;

// 옥타브를 합산한 펄린 노이즈로 높이 값을 계산하는 불변 클래스 (생성 후 상태가 변하지 않아 여러 스레드에서 동시에 읽어도 안전하다)
public sealed class NoiseMap
{
    // 노이즈 생성에 사용할 시드 (옥타브별 샘플링 오프셋을 결정한다)
    private readonly int m_Seed;
    // 노이즈 좌표를 나눌 스케일 값. 클수록 더 완만하고 넓은 지형이 만들어진다.
    private readonly float m_Scale;
    // 합산할 옥타브(레이어) 개수. 많을수록 디테일이 추가된다.
    private readonly int m_Octaves;
    // 옥타브가 거듭될 때마다 진폭에 곱해지는 값 (0~1, 작을수록 상위 옥타브의 영향이 줄어든다)
    private readonly float m_Persistence;
    // 옥타브가 거듭될 때마다 주파수에 곱해지는 값 (1보다 크면 옥타브마다 더 촘촘한 디테일이 추가된다)
    private readonly float m_Lacunarity;
    // 옥타브마다 서로 다른 패턴이 나오도록 시드 기반으로 미리 계산해 둔 샘플링 오프셋 목록
    private readonly Vector2[] m_OctaveOffsets;

    // 시드와 노이즈 파라미터를 받아 옥타브별 오프셋을 미리 계산한다.
    public NoiseMap(int seed, float scale, int octaves, float persistence, float lacunarity)
    {
        m_Seed = seed;
        m_Scale = Mathf.Max(scale, 0.0001f);
        m_Octaves = Mathf.Max(octaves, 1);
        m_Persistence = persistence;
        m_Lacunarity = lacunarity;
        m_OctaveOffsets = HandleGenerateOctaveOffsets();
    }

    // 주어진 좌표의 옥타브 합산 펄린 노이즈 값을 0~1 범위로 정규화해 반환한다.
    public float GetHeight(Vector2 pos)
    {
        float amplitude = 1f;
        float frequency = 1f;
        float noiseHeight = 0f;
        float maxAmplitude = 0f;

        for (int i = 0; i < m_Octaves; i++)
        {
            float sampleX = (pos.x + m_OctaveOffsets[i].x) / m_Scale * frequency;
            float sampleY = (pos.y + m_OctaveOffsets[i].y) / m_Scale * frequency;

            float perlinValue = (Mathf.PerlinNoise(sampleX, sampleY) * 2f) - 1f;
            noiseHeight += perlinValue * amplitude;

            maxAmplitude += amplitude;
            amplitude *= m_Persistence;
            frequency *= m_Lacunarity;
        }

        return Mathf.Clamp01(((noiseHeight / maxAmplitude) + 1f) * 0.5f);
    }

    // 시드를 기반으로 각 옥타브마다 사용할 랜덤 샘플링 오프셋을 생성한다.
    private Vector2[] HandleGenerateOctaveOffsets()
    {
        System.Random random = new System.Random(m_Seed);
        Vector2[] offsets = new Vector2[m_Octaves];

        for (int i = 0; i < m_Octaves; i++)
        {
            float offsetX = random.Next(-100000, 100000);
            float offsetY = random.Next(-100000, 100000);
            offsets[i] = new Vector2(offsetX, offsetY);
        }

        return offsets;
    }
}
