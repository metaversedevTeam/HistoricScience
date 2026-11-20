// 풀 종류가 바이옴 안에서 군락을 이루는 방식
public enum GrassDistributionMode
{
    // 펄린 노이즈가 임계값을 넘는 곳에 군락이 생긴다. 경계가 구불구불한 덩어리 모양이 된다.
    PerlinClump,
    // 지면을 보로노이 셀로 나누고 일부 셀만 채운다. 다각형 모양의 군락이 흩어져 생긴다.
    Voronoi,
}
