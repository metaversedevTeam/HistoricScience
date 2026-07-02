using UnityEngine;

namespace HistoricScience.Test
{
    // 보로노이 다이어그램 기반의 MapData를 생성하고 보관하는 클래스
    public class MapDataGenerator : MonoBehaviour
    {
        // 생성할 보로노이 정점(영역)의 개수
        [SerializeField] private int m_RegionCount = 12;
        // 각 정점에 랜덤으로 부여할 가중치의 최소/최대 범위
        [SerializeField] private Vector2 m_WeightRange = new Vector2(0.5f, 2f);
        // true면 매번 랜덤 시드를 사용하고, false면 m_RandomSeed 값을 고정 시드로 사용
        [SerializeField] private bool m_UseRandomSeed = true;
        // m_UseRandomSeed가 false일 때 사용할 고정 랜덤 시드 값
        [SerializeField] private int m_RandomSeed = 0;
        // 바이옴 경계(원호)에 굴곡을 주는 노이즈의 스케일. 클수록 더 잘게, 자주 굴곡진다.
        [SerializeField] private float m_BoundaryNoiseScale = 20f;
        // 바이옴 경계에 굴곡을 주는 노이즈의 세기. 0이면 가중 보로노이 경계가 원래의 원호 형태 그대로 유지된다.
        [SerializeField] private float m_BoundaryNoiseStrength = 0.003f;
        // 이 거리보다 멀리 있는 보로노이 정점은 영향력 계산에서 제외된다. (0~1 정규화 좌표 기준)
        [SerializeField] private float m_MaxInfluenceDistance = 0.6f;
        // 맵에 배치될 바이옴 목록. 랜덤 영역 생성 시 이 목록에서 무작위로 선택된다.
        [SerializeField] private MapBiome[] m_Biomes;

        // 마지막으로 생성된 맵 바이옴 데이터
        private MapData m_LastMapData;

        private int seed = -1;

        // 마지막으로 생성된 맵 바이옴 데이터를 반환한다.
        public MapData LastMapData => m_LastMapData;
        // 설정된 바이옴 목록을 반환한다.
        public MapBiome[] Biomes => m_Biomes;

        // 시드와 파라미터를 이용해 새로운 MapData를 생성하고 결과를 보관한다.
        public MapData GenerateMapData(bool useRandom = true)
        {
            if (m_Biomes == null || m_Biomes.Length == 0)
            {
                Debug.LogError("MapDataGenerator: No biomes assigned.");
                return null;
            }

            if (useRandom)
                seed = m_UseRandomSeed ? System.Environment.TickCount : m_RandomSeed;

            MapData mapData = new MapData(seed, m_Biomes, m_RegionCount, m_WeightRange.x, m_WeightRange.y, m_BoundaryNoiseScale, m_BoundaryNoiseStrength, m_MaxInfluenceDistance);
            m_LastMapData = mapData;

            return mapData;
        }
    }
}
