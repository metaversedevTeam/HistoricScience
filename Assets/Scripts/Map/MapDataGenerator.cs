using UnityEngine;

namespace HistoricScience.Test
{
    // 보로노이 다이어그램 기반의 MapData를 생성하고 보관하는 클래스. 시드는 스스로 만들지 않고 항상 외부에서 주입받는다.
    public class MapDataGenerator : MonoBehaviour
    {
        // 각 정점에 랜덤으로 부여할 가중치의 최소/최대 범위
        [SerializeField] private Vector2 m_WeightRange = new Vector2(0.5f, 2f);
        // 바이옴 경계(원호)에 굴곡을 주는 노이즈의 스케일. 클수록 더 잘게, 자주 굴곡진다.
        [SerializeField] private float m_BoundaryNoiseScale = 20f;
        // 바이옴 경계에 굴곡을 주는 노이즈의 세기. 0이면 가중 보로노이 경계가 원래의 원호 형태 그대로 유지된다.
        [SerializeField] private float m_BoundaryNoiseStrength = 0.003f;
        // 이 거리보다 멀리 있는 보로노이 정점은 영향력 계산에서 제외된다. (0~1 정규화 좌표 기준)
        [SerializeField] private float m_MaxInfluenceDistance = 0.6f;
        // 맵에 배치될 바이옴 목록. 앞선 바이옴의 배치 범위(고도/습도)가 먼저 검사되므로 순서가 곧 배치 우선순위다.
        [SerializeField] private MapBiome[] m_Biomes;
        // maxInfluenceDistance 이내에 정점이 없는 위치에 대신 사용할 기본 바이옴
        [SerializeField] private MapBiome m_DefaultBiome;

        // 마지막으로 생성된 맵 바이옴 데이터
        private MapData m_LastMapData;

        // 마지막으로 주입받은 시드. GenerateMapData가 호출되기 전까지는 유효하지 않다.
        private int seed = -1;

        // 마지막으로 생성된 맵 바이옴 데이터를 반환한다.
        public MapData LastMapData => m_LastMapData;
        // 설정된 바이옴 목록을 반환한다.
        public MapBiome[] Biomes => m_Biomes;
        // 마지막으로 주입받은 랜덤 시드를 반환한다.
        public int Seed => seed;

        // 주입받은 시드와 인스펙터 파라미터를 이용해 새로운 MapData를 생성하고 결과를 보관한다.
        public MapData GenerateMapData(int injectedSeed)
        {
            if (m_Biomes == null || m_Biomes.Length == 0)
            {
                Debug.LogError("MapDataGenerator: No biomes assigned.");
                return null;
            }

            seed = injectedSeed;

            MapData mapData = new MapData(seed, m_Biomes, m_DefaultBiome, m_WeightRange.x, m_WeightRange.y, m_BoundaryNoiseScale, m_BoundaryNoiseStrength, m_MaxInfluenceDistance);
            m_LastMapData = mapData;

            return mapData;
        }
    }
}
