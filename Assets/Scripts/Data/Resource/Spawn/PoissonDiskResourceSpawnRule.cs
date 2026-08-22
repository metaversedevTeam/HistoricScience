using System.Collections.Generic;
using UnityEngine;

// 샘플 소거(Yuksel 2015) 방식으로 자원 소스를 서로 고르게 떨어뜨려 흩뿌리는 소환 방식.
// 청크 전체에 목표 밀도만큼의 고른 점 집합을 먼저 만들고, 그 점들을 소환 조건으로 걸러 남은 자리에만 소환한다.
// 조건 검사는 이미 완성된 점 집합에서 일부를 덜어 낼 뿐이라, 바다나 절벽이 넓은 청크는 개수만 줄고 자원 사이 간격은 다른 청크와 같게 유지된다.
[CreateAssetMenu(fileName = "푸아송 디스크 소환 방식", menuName = "스크립터블 오브젝트/자원/소환 방식/푸아송 디스크", order = int.MinValue + 3)]
public class PoissonDiskResourceSpawnRule : ResourceSpawnRule
{
    // 육각 최밀 충전에서 점 하나가 차지하는 면적 계수(2√3). 점당 면적을 이 값으로 나눈 제곱근이 이론상 최대 푸아송 반지름이다.
    private const float k_HexagonalPackingFactor = 3.4641016f;

    [Header("푸아송 디스크 소환 설정")]
    // 소환 밀도 기준값. 청크 전체가 소환 가능한 지형일 때의 개수이며, 실제 소환 개수는 소환 가능한 면적에 비례해 이보다 줄어든다. 0이면 소환되지 않는다.
    [SerializeField, Min(0)] private int m_SpawnCountPerChunk = 16;
    // 밀도 기준값의 몇 배만큼 후보를 뽑아 소거를 시작할지. 클수록 배치가 고와지지만 계산이 늘어난다(논문 권장값 5배).
    [SerializeField, Min(1)] private int m_InputSampleMultiplier = 5;

    // 자신의 시드 번호를 시드에 섞어 소환 방식마다 독립적인 랜덤 스트림을 쓰므로, 목록 순서가 바뀌어도 배치가 유지된다.
    public override void GetPlacements(in ResourceSpawnContext context, List<ResourceSpawnPlacement> results)
    {
        if (results == null || m_SpawnCountPerChunk <= 0)
            return;

        if (context.ChunkSize.x <= 0f || context.ChunkSize.z <= 0f)
            return;

        System.Random random = new System.Random(context.CreateSeed(SeedId));
        List<Vector2> samples = HandleCreateSamples(context, random);

        // 청크 평면을 상하좌우로 이어진 것으로 보고 소거해야, 자원이 청크 가장자리에 몰리는 편향이 생기지 않는다.
        Vector2 domainSize = new Vector2(context.ChunkSize.x, context.ChunkSize.z);
        PoissonSampleEliminator eliminator = new PoissonSampleEliminator(samples, HandleGetMaxDistance(context), domainSize);
        bool[] survived = eliminator.Eliminate(m_SpawnCountPerChunk);

        HandleFillPlacements(context, random, samples, survived, results);
    }

    // 청크 평면(월드 XZ)에 소거의 출발점이 될 후보 점들을 균등 난수로 뽑는다. 지형은 아직 보지 않으므로 청크 전체에 고르게 퍼진다.
    private List<Vector2> HandleCreateSamples(in ResourceSpawnContext context, System.Random random)
    {
        int sampleCount = m_SpawnCountPerChunk * m_InputSampleMultiplier;
        List<Vector2> samples = new List<Vector2>(sampleCount);

        for (int i = 0; i < sampleCount; i++)
        {
            float x = (float)random.NextDouble() * context.ChunkSize.x;
            float z = (float)random.NextDouble() * context.ChunkSize.z;

            samples.Add(new Vector2(x, z));
        }

        return samples;
    }

    // 소거에서 살아남은 점들을 소환 조건으로 걸러, 통과한 자리를 배치 결과로 채운다.
    private void HandleFillPlacements(in ResourceSpawnContext context, System.Random random, List<Vector2> samples, bool[] survived, List<ResourceSpawnPlacement> results)
    {
        for (int i = 0; i < samples.Count; i++)
        {
            if (!survived[i])
                continue;

            // 추첨을 걸러내기 전에 끝내 두어야, 자리가 통과하든 버려지든 난수 소비량이 같아 배치가 결정론적으로 유지된다.
            float rotationY = (float)random.NextDouble() * 360f;
            float normalizedX = samples[i].x / context.ChunkSize.x;
            float normalizedZ = samples[i].y / context.ChunkSize.z;

            if (!CanSpawnAt(context, normalizedX, normalizedZ, out float height))
                continue;

            results.Add(new ResourceSpawnPlacement(context.GetLocalPosition(normalizedX, normalizedZ, height), rotationY));
        }
    }

    // 소거에서 이웃으로 볼 최대 거리를 구한다. 청크 면적을 밀도 기준 개수로 나눈 점당 면적에서 이론상 최대 푸아송 반지름을 얻어 그 지름을 쓴다.
    // 간격을 사람이 정하지 않고 밀도에서 유도하므로, 청크 크기가 바뀌어도 체감 밀도가 유지된다.
    private float HandleGetMaxDistance(in ResourceSpawnContext context)
    {
        float areaPerSample = context.ChunkSize.x * context.ChunkSize.z / m_SpawnCountPerChunk;

        return 2f * Mathf.Sqrt(areaPerSample / k_HexagonalPackingFactor);
    }
}
