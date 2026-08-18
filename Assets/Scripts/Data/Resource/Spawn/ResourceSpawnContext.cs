using UnityEngine;

// 자원 소환 방식이 소환 위치를 계산할 때 필요한 청크 한 개분의 정보(맵 출력 영역, 맵 데이터, 청크 크기)를 담는 읽기 전용 묶음.
// 터레인 크기는 메인 스레드에서 미리 받아 값으로 들고 있고 높이는 MapData에서 계산하므로, 이 구조체를 쓰는 계산은 Unity 씬 API에 의존하지 않는다.
public readonly struct ResourceSpawnContext
{
    // 이 청크가 출력하는 맵 영역의 좌하단 원점 (정규화 맵 좌표)
    public readonly Vector2 MapViewOrigin;
    // 이 청크가 출력하는 맵 영역의 한 변 길이 (정규화 맵 좌표)
    public readonly float MapViewSize;
    // 바이옴 판정, 표면 높이 계산, 전역 시드(MapData.Seed)를 얻어 올 맵 데이터
    public readonly MapData MapData;
    // 이 청크의 월드 크기. y는 터레인 최대 높이로, 정규화 높이(0~1)를 월드 높이로 바꾸는 데 쓴다.
    public readonly Vector3 ChunkSize;

    public ResourceSpawnContext(Vector2 mapViewOrigin, float mapViewSize, MapData mapData, Vector3 chunkSize)
    {
        MapViewOrigin = mapViewOrigin;
        MapViewSize = mapViewSize;
        MapData = mapData;
        ChunkSize = chunkSize;
    }

    // 터레인을 칠할 때와 같은 변환(원점 + 정규화 좌표 × 출력 크기)으로 정규화 청크 좌표(0~1)를 맵 좌표로 바꾼다.
    public Vector2 ToMapPosition(float normalizedX, float normalizedZ)
    {
        return MapViewOrigin + new Vector2(normalizedX, normalizedZ) * MapViewSize;
    }

    // 정규화 청크 좌표(0~1) 위치의 지형 표면 높이를 월드 높이로 반환한다. 청크 루트가 Y=0에 놓이므로 이 값이 곧 월드 Y다.
    // 터레인을 굽는 높이맵과 같은 계산(MapData.GetSurfaceHeight)을 쓰므로 터레인을 샘플링하지 않고도 같은 표면 높이를 얻는다.
    public float GetHeight(float normalizedX, float normalizedZ)
    {
        return MapData.GetSurfaceHeight(ToMapPosition(normalizedX, normalizedZ)) * ChunkSize.y;
    }

    // 정규화 청크 좌표(0~1) 위치의 바이옴을 반환한다.
    public MapBiome GetBiome(float normalizedX, float normalizedZ)
    {
        return MapData.GetBiome(ToMapPosition(normalizedX, normalizedZ));
    }

    // 정규화 청크 좌표와 표면 높이를 청크 루트 기준 로컬 위치로 변환한다.
    public Vector3 GetLocalPosition(float normalizedX, float normalizedZ, float height)
    {
        return new Vector3(normalizedX * ChunkSize.x, height, normalizedZ * ChunkSize.z);
    }

    // FNV-1a 해시로 맵 데이터의 전역 시드, 청크 원점, 소금값(주로 아이템 ID)을 섞어 아이템마다 고유하면서 세션이 바뀌어도 유지되는 시드를 만든다.
    public int CreateSeed(int salt)
    {
        uint hash = 2166136261u;
        hash = (hash ^ (uint)MapData.Seed) * 16777619u;
        // 원점은 청크 좌표 × 출력 크기로 만들어진 유한한 float이므로, 정수화해 해시하면 부동소수 오차 없이 결정론이 유지된다.
        hash = (hash ^ (uint)Mathf.RoundToInt(MapViewOrigin.x * 1000f)) * 16777619u;
        hash = (hash ^ (uint)Mathf.RoundToInt(MapViewOrigin.y * 1000f)) * 16777619u;
        hash = (hash ^ (uint)salt) * 16777619u;

        return (int)hash;
    }
}
