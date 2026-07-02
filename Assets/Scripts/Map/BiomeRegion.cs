using UnityEngine;

// 보로노이 다이어그램의 정점 하나를 나타내는 구조체
public struct BiomeRegion
{
    // 전체 정점 목록에서의 고유 인덱스
    public int Index;
    // 정점의 위치 (0~1로 정규화된 좌표)
    public Vector2 Position;
    // 정점이 차지하는 영역의 가중치
    public float Weight;
    // 이 정점에 배정된 바이옴
    public MapBiome Biome;
}
