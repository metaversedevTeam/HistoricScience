using UnityEngine;

// 지형에 출력할 풀 한 종류의 모델, 크기, 분포 규칙을 담는 스크립터블 오브젝트.
// 바이옴 에셋의 풀 목록에 밀도와 함께 등록하면 그 바이옴 위에 출력된다. 밀도(빈도)는 바이옴마다 따로 정하고, 군락 모양과 높이 조건은 이 에셋이 정한다.
[CreateAssetMenu(fileName = "NewGrassType", menuName = "HistoricScience/Map/Grass Type")]
public class GrassType : ScriptableObject
{
    [Header("모델")]
    // 풀로 출력할 프리팹. MeshFilter/MeshRenderer가 있어야 하며, 머티리얼은 GPU Instancing을 켠 URP 셰이더여야 한다.
    [SerializeField] private GameObject m_Prefab;

    [Header("크기")]
    // 너비 배율 범위 (x=최소, y=최대). 인스턴스마다 이 범위에서 무작위로 정해진다.
    [SerializeField] private Vector2 m_WidthRange = new Vector2(0.8f, 1.2f);
    // 높이 배율 범위 (x=최소, y=최대)
    [SerializeField] private Vector2 m_HeightRange = new Vector2(0.8f, 1.2f);
    // 지면 경사에 맞춰 기울이는 정도 (0 = 항상 수직, 1 = 지면 법선 방향)
    [SerializeField, Range(0f, 1f)] private float m_AlignToGround = 0.5f;
    // 디테일 셀 안에서 위치를 흩뜨리는 정도 (0 = 셀 격자에 정렬, 1 = 셀 전체에 흩뿌림)
    [SerializeField, Range(0f, 1f)] private float m_PositionJitter = 1f;

    [Header("분포")]
    // 출력할 지형 표면 높이 범위(월드 Y, x=최소, y=최대). 기본값은 해수면 위 전체다.
    [SerializeField] private Vector2 m_SpawnHeightRange = new Vector2(12f, 100f);
    // 군락을 만드는 방식. 펄린은 구불구불한 덩어리, 보로노이는 흩어진 다각형 무리를 만든다.
    [SerializeField] private GrassDistributionMode m_DistributionMode = GrassDistributionMode.PerlinClump;
    // 군락 하나의 대략적인 크기(m). 보로노이 방식에서는 셀 하나의 평균 크기다. 클수록 풀이 넓은 덩어리로 뭉쳐 난다.
    [SerializeField, Min(0.1f)] private float m_ClumpSize = 20f;
    // 군락이 지면을 덮는 대략적인 비율. 보로노이 방식에서는 풀이 채워지는 셀의 비율이다. 1이면 군락 없이 바이옴 전체에 고르게 깔린다.
    [SerializeField, Range(0f, 1f)] private float m_ClumpCoverage = 1f;
    // 군락 가장자리에서 밀도가 줄어드는 폭. 펄린 방식은 노이즈 값, 보로노이 방식은 셀 크기 대비 경계까지의 거리 기준이다. 0이면 경계가 딱 끊긴다.
    [SerializeField, Range(0f, 0.5f)] private float m_ClumpEdgeSoftness = 0.1f;
    // 풀 종류마다 군락 노이즈 패턴이 겹치지 않도록 하는 구분 번호. 에셋을 만들 때 자동으로 정해지며, 바꾸면 이 풀의 배치가 달라진다.
    [SerializeField] private int m_NoiseSalt;

    public GameObject Prefab => m_Prefab;
    public float MinSpawnHeight => m_SpawnHeightRange.x;
    public float MaxSpawnHeight => m_SpawnHeightRange.y;
    public GrassDistributionMode DistributionMode => m_DistributionMode;
    public float ClumpSize => m_ClumpSize;
    public float ClumpCoverage => m_ClumpCoverage;
    public float ClumpEdgeSoftness => m_ClumpEdgeSoftness;
    public int NoiseSalt => m_NoiseSalt;

    // 에셋이 처음 만들어질 때 군락 노이즈 구분 번호를 무작위로 정한다.
    private void Reset()
    {
        m_NoiseSalt = Random.Range(1, int.MaxValue);
    }

    // 인스펙터에서 범위 값의 최소/최대가 뒤집히지 않도록 보정한다.
    private void OnValidate()
    {
        m_WidthRange.y = Mathf.Max(m_WidthRange.x, m_WidthRange.y);
        m_HeightRange.y = Mathf.Max(m_HeightRange.x, m_HeightRange.y);
        m_SpawnHeightRange.y = Mathf.Max(m_SpawnHeightRange.x, m_SpawnHeightRange.y);
    }

    // 이 풀 종류를 터레인 디테일로 출력할 GPU 인스턴싱 메시 프로토타입을 만든다. Unity API를 쓰므로 메인 스레드에서 호출해야 한다.
    public DetailPrototype CreateDetailPrototype()
    {
        return new DetailPrototype
        {
            prototype = m_Prefab,
            usePrototypeMesh = true,
            useInstancing = true,
            renderMode = DetailRenderMode.VertexLit,
            minWidth = m_WidthRange.x,
            maxWidth = m_WidthRange.y,
            minHeight = m_HeightRange.x,
            maxHeight = m_HeightRange.y,
            alignToGround = m_AlignToGround,
            positionJitter = m_PositionJitter,
            useDensityScaling = true,
        };
    }
}
