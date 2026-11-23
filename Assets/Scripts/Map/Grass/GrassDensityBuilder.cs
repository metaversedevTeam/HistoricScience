using UnityEngine;

// 청크 하나의 알파맵·높이맵과 풀 레이아웃으로 풀 종류별 디테일 레이어(디테일 셀마다 풀 개수)를 계산하는 클래스.
// 셀의 기대 개수 = Σ(바이옴 알파맵 가중치 × 그 바이옴의 풀 밀도) × 군락 마스크 × 셀 면적이며, 높이 조건을 벗어난 셀은 0이다.
// 블러된 알파맵 가중치를 쓰므로 바이옴 경계에서 밀도가 자연스럽게 섞이고, 군락 노이즈와 반올림 난수가 모두 월드 좌표 기준이라 청크 경계에 이음매가 생기지 않는다.
// Unity 씬 API를 쓰지 않는 순수 계산이라 백그라운드 스레드에서 호출해도 안전하다.
public static class GrassDensityBuilder
{
    // 디테일 셀 하나에 놓을 수 있는 최대 풀 개수
    private const int k_MaxInstancesPerCell = 255;
    // 보로노이 특징점 위치와 셀 채움 여부를 서로 다른 난수로 뽑기 위해 풀 종류 구분 번호에 섞는 값
    private const int k_VoronoiJitterXSalt = 0x1F3D5B79;
    private const int k_VoronoiJitterZSalt = 0x5A17C3E1;
    private const int k_VoronoiActiveSalt = 0x3C6EF372;

    // 풀 종류별 디테일 레이어를 계산해 레이아웃의 풀 종류 순서대로 반환한다. 각 레이어는 [z, x] 순서의 detailResolution × detailResolution 배열이다.
    public static int[][,] Build(ChunkGrassLayout layout, float[,,] alphamap, float[,] heightmap, int detailResolution, Vector2 mapViewOrigin, float mapViewSize, MapTerrainConfig config, int seed)
    {
        if (layout == null || layout.GrassTypeCount == 0 || detailResolution <= 0 || mapViewSize <= 0f)
            return new int[0][,];

        int grassCount = layout.GrassTypeCount;
        int[][,] layers = new int[grassCount][,];
        GrassType[] grassTypes = new GrassType[grassCount];
        Vector2[] noiseOffsets = new Vector2[grassCount];

        for (int g = 0; g < grassCount; g++)
        {
            layers[g] = new int[detailResolution, detailResolution];
            grassTypes[g] = layout.GetGrassType(g);
            noiseOffsets[g] = HandleGetNoiseOffset(seed, grassTypes[g].NoiseSalt);
        }

        HandleFillLayers(layout, alphamap, heightmap, detailResolution, mapViewOrigin, mapViewSize, config, seed, grassTypes, noiseOffsets, layers);
        return layers;
    }

    // 디테일 셀마다 바이옴 가중치와 높이를 한 번만 샘플링하고, 그 값으로 모든 풀 종류의 개수를 채운다.
    private static void HandleFillLayers(ChunkGrassLayout layout, float[,,] alphamap, float[,] heightmap, int detailResolution, Vector2 mapViewOrigin, float mapViewSize, MapTerrainConfig config, int seed, GrassType[] grassTypes, Vector2[] noiseOffsets, int[][,] layers)
    {
        int biomeCount = Mathf.Min(layout.BiomeCount, alphamap.GetLength(2));
        float[] biomeWeights = new float[biomeCount];

        float metersPerMapUnit = config.ChunkWorldSize / mapViewSize;
        float cellSize = config.ChunkWorldSize / detailResolution;
        float cellArea = cellSize * cellSize;

        // 청크 좌표 × 해상도로 전역 셀 좌표를 만들어, 반올림 난수가 청크마다 같은 패턴으로 반복되지 않게 한다.
        int globalCellOriginX = Mathf.RoundToInt(mapViewOrigin.x / mapViewSize) * detailResolution;
        int globalCellOriginZ = Mathf.RoundToInt(mapViewOrigin.y / mapViewSize) * detailResolution;

        for (int z = 0; z < detailResolution; z++)
        {
            for (int x = 0; x < detailResolution; x++)
            {
                // 디테일 셀은 터레인을 균등하게 나눈 칸이므로 셀 중심의 정규화 좌표에서 샘플링한다.
                float u = (x + 0.5f) / detailResolution;
                float v = (z + 0.5f) / detailResolution;

                HandleSampleAlphamap(alphamap, u, v, biomeWeights);
                float height = HandleSampleHeightmap(heightmap, u, v) * config.MaxTerrainHeight;
                Vector2 worldPosition = (mapViewOrigin + new Vector2(u, v) * mapViewSize) * metersPerMapUnit;

                for (int g = 0; g < grassTypes.Length; g++)
                {
                    float density = HandleGetBlendedDensity(layout, biomeWeights, g);
                    if (density <= 0f)
                        continue;

                    GrassType grassType = grassTypes[g];
                    if (height < grassType.MinSpawnHeight || height > grassType.MaxSpawnHeight)
                        continue;

                    float clumpMask = HandleSampleClumpMask(grassType, worldPosition, noiseOffsets[g], seed);
                    if (clumpMask <= 0f)
                        continue;

                    float expectedCount = density * clumpMask * cellArea;
                    layers[g][z, x] = HandleDitherCount(expectedCount, globalCellOriginX + x, globalCellOriginZ + z, seed, grassType.NoiseSalt);
                }
            }
        }
    }

    // 셀의 바이옴 가중치로 각 바이옴에 등록된 풀 밀도를 섞어, 그 셀에서의 풀 종류 하나의 밀도(㎡당 개수)를 구한다.
    private static float HandleGetBlendedDensity(ChunkGrassLayout layout, float[] biomeWeights, int grassIndex)
    {
        float density = 0f;
        for (int b = 0; b < biomeWeights.Length; b++)
        {
            if (biomeWeights[b] > 0f)
                density += biomeWeights[b] * layout.GetDensity(b, grassIndex);
        }

        return density;
    }

    // 알파맵을 정규화 터레인 좌표에서 이중선형 보간해 바이옴(레이어)별 가중치를 채운다. 알파맵 셀은 양 끝이 터레인 가장자리에 걸치므로 해상도-1로 나눈다.
    private static void HandleSampleAlphamap(float[,,] alphamap, float u, float v, float[] weights)
    {
        HandleGetBilinearCell(u, alphamap.GetLength(1), out int x0, out int x1, out float tx);
        HandleGetBilinearCell(v, alphamap.GetLength(0), out int z0, out int z1, out float tz);

        for (int l = 0; l < weights.Length; l++)
        {
            float bottom = Mathf.Lerp(alphamap[z0, x0, l], alphamap[z0, x1, l], tx);
            float top = Mathf.Lerp(alphamap[z1, x0, l], alphamap[z1, x1, l], tx);
            weights[l] = Mathf.Lerp(bottom, top, tz);
        }
    }

    // 높이맵을 정규화 터레인 좌표에서 이중선형 보간해 높이 비율(0~1)을 구한다. 터레인 표면과 같은 보간 방식이다.
    private static float HandleSampleHeightmap(float[,] heightmap, float u, float v)
    {
        HandleGetBilinearCell(u, heightmap.GetLength(1), out int x0, out int x1, out float tx);
        HandleGetBilinearCell(v, heightmap.GetLength(0), out int z0, out int z1, out float tz);

        float bottom = Mathf.Lerp(heightmap[z0, x0], heightmap[z0, x1], tx);
        float top = Mathf.Lerp(heightmap[z1, x0], heightmap[z1, x1], tx);
        return Mathf.Lerp(bottom, top, tz);
    }

    // 정규화 좌표(0~1)를 양 끝이 가장자리에 걸치는 격자의 보간 셀 인덱스 두 개와 보간 비율로 바꾼다.
    private static void HandleGetBilinearCell(float normalized, int resolution, out int index0, out int index1, out float t)
    {
        float position = Mathf.Clamp01(normalized) * Mathf.Max(resolution - 1, 0);
        index0 = Mathf.Min(Mathf.FloorToInt(position), resolution - 1);
        index1 = Mathf.Min(index0 + 1, resolution - 1);
        t = position - index0;
    }

    // 풀 종류의 군락 방식으로 주어진 월드 위치의 밀도 배율(0~1)을 구한다. 덮는 비율이 1이면 방식과 관계없이 항상 1이다.
    private static float HandleSampleClumpMask(GrassType grassType, Vector2 worldPosition, Vector2 noiseOffset, int seed)
    {
        float coverage = grassType.ClumpCoverage;
        if (coverage >= 1f)
            return 1f;
        if (coverage <= 0f)
            return 0f;

        if (grassType.DistributionMode == GrassDistributionMode.Voronoi)
            return HandleSampleVoronoiMask(grassType, worldPosition, seed);

        return HandleSamplePerlinMask(grassType, worldPosition, noiseOffset);
    }

    // 펄린 노이즈가 임계값을 넘는 곳에서만 풀이 나도록 밀도 배율(0~1)을 구한다.
    private static float HandleSamplePerlinMask(GrassType grassType, Vector2 worldPosition, Vector2 noiseOffset)
    {
        float coverage = grassType.ClumpCoverage;
        float noise = Mathf.PerlinNoise(
            worldPosition.x / grassType.ClumpSize + noiseOffset.x,
            worldPosition.y / grassType.ClumpSize + noiseOffset.y);

        // 펄린 노이즈 값은 0.5 근처에 몰려 있어 덮는 비율은 대략적인 값이다.
        float threshold = 1f - coverage;
        float softness = grassType.ClumpEdgeSoftness;
        if (softness <= 0f)
            return noise >= threshold ? 1f : 0f;

        float t = Mathf.Clamp01((noise - (threshold - softness)) / (softness * 2f));
        return t * t * (3f - 2f * t);
    }

    // 지면을 보로노이 셀로 나눠, 위치가 속한 셀이 채워지는 셀이면 경계까지의 거리에 따라 밀도 배율(0~1)을 구한다.
    // 셀마다 특징점을 격자 칸 안에 하나씩 흩뿌리고, 가장 가까운 특징점의 셀에 속한 것으로 본다. 두 번째로 가까운 특징점과의 거리 차로 경계까지의 거리를 어림한다.
    private static float HandleSampleVoronoiMask(GrassType grassType, Vector2 worldPosition, int seed)
    {
        Vector2 position = worldPosition / grassType.ClumpSize;
        int baseCellX = Mathf.FloorToInt(position.x);
        int baseCellZ = Mathf.FloorToInt(position.y);
        int salt = grassType.NoiseSalt;

        float nearestDistance = float.MaxValue;
        float secondDistance = float.MaxValue;
        int nearestCellX = baseCellX;
        int nearestCellZ = baseCellZ;

        for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
        {
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                int cellX = baseCellX + offsetX;
                int cellZ = baseCellZ + offsetZ;
                Vector2 featurePoint = new Vector2(
                    cellX + HandleHash01(cellX, cellZ, seed, salt ^ k_VoronoiJitterXSalt),
                    cellZ + HandleHash01(cellX, cellZ, seed, salt ^ k_VoronoiJitterZSalt));

                float distance = Vector2.Distance(featurePoint, position);
                if (distance < nearestDistance)
                {
                    secondDistance = nearestDistance;
                    nearestDistance = distance;
                    nearestCellX = cellX;
                    nearestCellZ = cellZ;
                }
                else if (distance < secondDistance)
                {
                    secondDistance = distance;
                }
            }
        }

        if (HandleHash01(nearestCellX, nearestCellZ, seed, salt ^ k_VoronoiActiveSalt) >= grassType.ClumpCoverage)
            return 0f;

        float softness = grassType.ClumpEdgeSoftness;
        if (softness <= 0f)
            return 1f;

        // 두 특징점까지의 거리 차의 절반은 셀 경계까지의 거리와 비슷하다(셀 크기 단위).
        float edgeDistance = (secondDistance - nearestDistance) * 0.5f;
        float t = Mathf.Clamp01(edgeDistance / softness);
        return t * t * (3f - 2f * t);
    }

    // 기대 개수(소수)를 셀 좌표 해시 난수로 확률 반올림해 정수 개수로 바꾼다. 같은 셀은 항상 같은 결과가 나온다.
    private static int HandleDitherCount(float expectedCount, int globalCellX, int globalCellZ, int seed, int salt)
    {
        int whole = Mathf.FloorToInt(expectedCount);
        float fraction = expectedCount - whole;
        int count = HandleHash01(globalCellX, globalCellZ, seed, salt) < fraction ? whole + 1 : whole;

        return Mathf.Min(count, k_MaxInstancesPerCell);
    }

    // 시드와 풀 종류 구분 번호로 군락 노이즈의 펄린 샘플 오프셋을 계산한다.
    private static Vector2 HandleGetNoiseOffset(int seed, int salt)
    {
        return new Vector2(HandleHash01(0, 0, seed, salt) * 997f, HandleHash01(1, 0, seed, salt) * 997f);
    }

    // 셀 좌표, 시드, 구분 번호를 섞어 0(포함)~1(미만) 범위의 결정론적 난수를 만든다.
    private static float HandleHash01(int cellX, int cellZ, int seed, int salt)
    {
        uint hash = (uint)seed;
        hash ^= (uint)cellX * 0x8DA6B343u;
        hash ^= (uint)cellZ * 0xD8163841u;
        hash ^= (uint)salt * 0xCB1AB31Fu;

        hash ^= hash >> 16;
        hash *= 0x7FEB352Du;
        hash ^= hash >> 15;
        hash *= 0x846CA68Bu;
        hash ^= hash >> 16;

        return (hash & 0xFFFFFF) / 16777216f;
    }
}
