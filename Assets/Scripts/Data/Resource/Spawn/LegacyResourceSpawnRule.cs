using System.Collections.Generic;
using UnityEngine;

// 기존 ChunkResourceSpawner가 쓰던 소환 공식을 그대로 옮긴 레거시 소환 방식.
// 시드 기반 결정론적 랜덤으로 청크 안의 좌표를 추첨해, 공용 소환 조건을 만족하는 곳에 목표 개수만큼 자리를 잡는다.
[CreateAssetMenu(fileName = "레거시 소환 방식", menuName = "스크립터블 오브젝트/자원/소환 방식/레거시", order = int.MinValue + 2)]
public class LegacyResourceSpawnRule : ResourceSpawnRule
{
    [Header("레거시 소환 설정")]
    // 청크당 자원 소스 목표 소환 개수. 0이면 소환되지 않는다.
    [SerializeField, Min(0)] private int m_SpawnCountPerChunk;
    // 소환 조건에 맞는 위치를 찾기 위해 개수당 재추첨할 최대 시도 횟수 (무한 루프 방지)
    [SerializeField, Min(1)] private int m_MaxAttemptsPerSpawn = 8;

    // 자신의 시드 번호를 시드에 섞어 소환 방식마다 독립적인 랜덤 스트림을 쓰므로, 목록 순서가 바뀌어도 배치가 유지된다.
    public override void GetPlacements(in ResourceSpawnContext context, List<ResourceSpawnPlacement> results)
    {
        if (results == null || m_SpawnCountPerChunk <= 0)
            return;

        System.Random random = new System.Random(context.CreateSeed(SeedId));
        int maxAttempts = m_SpawnCountPerChunk * m_MaxAttemptsPerSpawn;
        int spawned = 0;

        for (int attempt = 0; attempt < maxAttempts && spawned < m_SpawnCountPerChunk; attempt++)
        {
            // 추첨을 걸러내기 전에 끝내 두어야, 자리가 통과하든 버려지든 난수 소비량이 같아 배치가 결정론적으로 유지된다.
            float normalizedX = (float)random.NextDouble();
            float normalizedZ = (float)random.NextDouble();
            float rotationY = (float)random.NextDouble() * 360f;

            if (!CanSpawnAt(context, normalizedX, normalizedZ, out float height))
                continue;

            results.Add(new ResourceSpawnPlacement(context.GetLocalPosition(normalizedX, normalizedZ, height), rotationY));
            spawned++;
        }
    }
}
