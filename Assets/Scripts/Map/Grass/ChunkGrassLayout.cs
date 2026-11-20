using System.Collections.Generic;
using UnityEngine;

// 바이옴 목록에 등록된 풀 종류들을 터레인 디테일 레이어 순서로 정리하고, 바이옴×풀 종류별 밀도 표를 담아 두는 불변 클래스.
// 메인 스레드에서 바이옴 에셋을 읽어 한 번 만들고, 백그라운드의 밀도 계산은 이 객체만 읽는다.
public sealed class ChunkGrassLayout
{
    // 디테일 레이어 순서의 풀 종류 목록. 터레인의 detailPrototypes 순서와 같다.
    private readonly GrassType[] m_GrassTypes;
    // 디테일 레이어 순서의 터레인 디테일 프로토타입
    private readonly DetailPrototype[] m_Prototypes;
    // [바이옴 인덱스, 풀 종류 인덱스]의 출력 밀도(㎡당 개수). 바이옴 인덱스는 알파맵 레이어 순서와 같다.
    private readonly float[,] m_Densities;

    // 풀 종류 개수 (= 디테일 레이어 개수)
    public int GrassTypeCount => m_GrassTypes.Length;

    // 바이옴 개수 (= 알파맵 레이어 개수)
    public int BiomeCount => m_Densities.GetLength(0);

    private ChunkGrassLayout(GrassType[] grassTypes, DetailPrototype[] prototypes, float[,] densities)
    {
        m_GrassTypes = grassTypes;
        m_Prototypes = prototypes;
        m_Densities = densities;
    }

    // 알파맵 레이어 순서의 바이옴 목록에서 등록된 풀 종류를 모아 레이아웃을 만든다. 프로토타입을 만들고 검사하므로 메인 스레드에서 호출해야 한다.
    // 프리팹이 없거나 터레인 디테일로 쓸 수 없는 풀 종류는 경고를 남기고 제외한다.
    public static ChunkGrassLayout Create(MapBiome[] biomes)
    {
        List<GrassType> grassTypes = new List<GrassType>();
        List<DetailPrototype> prototypes = new List<DetailPrototype>();

        foreach (MapBiome biome in biomes)
            HandleCollectGrassTypes(biome, grassTypes, prototypes);

        float[,] densities = new float[biomes.Length, grassTypes.Count];
        for (int biomeIndex = 0; biomeIndex < biomes.Length; biomeIndex++)
            HandleFillBiomeDensities(biomes[biomeIndex], biomeIndex, grassTypes, densities);

        return new ChunkGrassLayout(grassTypes.ToArray(), prototypes.ToArray(), densities);
    }

    // 주어진 레이어의 풀 종류를 반환한다.
    public GrassType GetGrassType(int grassIndex)
    {
        return m_GrassTypes[grassIndex];
    }

    // 주어진 바이옴에서 주어진 풀 종류의 출력 밀도(㎡당 개수)를 반환한다. 등록되지 않았으면 0이다.
    public float GetDensity(int biomeIndex, int grassIndex)
    {
        return m_Densities[biomeIndex, grassIndex];
    }

    // 터레인에 할당할 디테일 프로토타입 배열을 복사해 반환한다.
    public DetailPrototype[] CopyPrototypes()
    {
        return (DetailPrototype[])m_Prototypes.Clone();
    }

    // 바이옴 하나의 풀 목록에서 아직 모으지 않은 유효한 풀 종류를 목록 끝에 추가한다.
    private static void HandleCollectGrassTypes(MapBiome biome, List<GrassType> grassTypes, List<DetailPrototype> prototypes)
    {
        if (biome == null)
            return;

        foreach (BiomeGrassEntry entry in biome.GrassEntries)
        {
            GrassType grassType = entry?.GrassType;
            if (grassType == null || grassTypes.Contains(grassType))
                continue;

            if (!HandleTryCreatePrototype(grassType, out DetailPrototype prototype))
                continue;

            grassTypes.Add(grassType);
            prototypes.Add(prototype);
        }
    }

    // 풀 종류의 디테일 프로토타입을 만들고 터레인 디테일로 쓸 수 있는지 검사한다. 쓸 수 없으면 이유를 경고로 남긴다.
    private static bool HandleTryCreatePrototype(GrassType grassType, out DetailPrototype prototype)
    {
        prototype = null;

        if (grassType.Prefab == null)
        {
            Debug.LogWarning($"ChunkGrassLayout: 풀 종류 '{grassType.name}'에 프리팹이 없어 출력하지 않습니다.", grassType);
            return false;
        }

        DetailPrototype candidate = grassType.CreateDetailPrototype();
        if (!candidate.Validate(out string errorMessage))
        {
            Debug.LogWarning($"ChunkGrassLayout: 풀 종류 '{grassType.name}'을(를) 터레인 디테일로 쓸 수 없어 출력하지 않습니다. {errorMessage}", grassType);
            return false;
        }

        prototype = candidate;
        return true;
    }

    // 바이옴 하나의 풀 목록을 밀도 표의 해당 행에 채운다. 같은 풀 종류가 여러 번 등록되어 있으면 밀도를 더한다.
    private static void HandleFillBiomeDensities(MapBiome biome, int biomeIndex, List<GrassType> grassTypes, float[,] densities)
    {
        if (biome == null)
            return;

        foreach (BiomeGrassEntry entry in biome.GrassEntries)
        {
            if (entry == null)
                continue;

            int grassIndex = grassTypes.IndexOf(entry.GrassType);
            if (grassIndex < 0)
                continue;

            densities[biomeIndex, grassIndex] += entry.Density;
        }
    }
}
