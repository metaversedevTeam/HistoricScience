using UnityEngine;

// 보로노이 다이어그램의 정점 하나를 나타내는 불변 구조체
public readonly struct BiomeRegion
{
    // 생성 규칙이 배정하는 정점 인덱스 (경계 노이즈 오프셋 계산에 사용. 해시 기반이라 멀리 떨어진 정점끼리는 드물게 같은 값을 가질 수 있다)
    public readonly int Index;
    // 정점의 위치 (터레인 한 변을 1로 하는 정규화 좌표, 무한 평면이므로 0~1 범위 밖일 수 있음)
    public readonly Vector2 Position;
    // 정점이 차지하는 영역의 가중치
    public readonly float Weight;
    // 이 정점에 배정된 바이옴
    public readonly MapBiome Biome;

    // 정점의 모든 값을 받아 초기화한다.
    public BiomeRegion(int index, Vector2 position, float weight, MapBiome biome)
    {
        Index = index;
        Position = position;
        Weight = weight;
        Biome = biome;
    }
}
