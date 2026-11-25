using UnityEngine;

// 카메라에서 먼 물체를 안개 색과 섞는 거리 안개를 켜고, 시작 거리·끝 거리·색을 런타임에 조절하는 컴포넌트
// URP의 선형(Linear) 안개를 사용하므로 URP Lit·Unlit·지형·셰이더 그래프 머티리얼에 모두 적용된다
// 시작 거리(거리1)부터 섞이기 시작해 끝 거리(거리2)부터는 원래 색과 관계없이 안개 색만 보인다
// 어디서든 DistanceFogController.Instance로 접근한다. 씬에 배치하지 않았더라도 처음 접근할 때 하나 만들어 준다
public class DistanceFogController : MonoBehaviour
{
    // 끝 거리가 시작 거리보다 최소한 이만큼은 멀도록 유지한다 (같으면 섞는 비율 계산이 0으로 나뉜다)
    private const float MinDistanceGap = 0.01f;

    // 현재 씬의 DistanceFogController. 씬에 배치돼 있으면 그 인스턴스를 쓰고, 없으면 빈 게임오브젝트를 만들어 붙여 준다
    public static DistanceFogController Instance
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = FindFirstObjectByType<DistanceFogController>();
            if (_instance == null)
            {
                // Awake에서 _instance에 자신을 등록한다
                _instance = new GameObject(nameof(DistanceFogController)).AddComponent<DistanceFogController>();
            }

            return _instance;
        }
    }

    private static DistanceFogController _instance;

    [Tooltip("거리1: 이 거리보다 먼 물체부터 안개 색과 섞이기 시작한다")]
    [SerializeField, Min(0f)] private float _startDistance = 80f;

    [Tooltip("거리2: 이 거리보다 먼 물체는 원래 색과 관계없이 안개 색으로만 보인다")]
    [SerializeField, Min(0f)] private float _endDistance = 300f;

    [Tooltip("색깔1: 먼 물체와 섞이는 안개 색")]
    [SerializeField] private Color _fogColor = new Color(0.75f, 0.8f, 0.85f, 1f);

    // 이 컴포넌트가 켜지기 전의 안개 설정. 꺼질 때 되돌린다
    private bool _previousFog;
    private FogMode _previousFogMode;
    private float _previousStartDistance;
    private float _previousEndDistance;
    private Color _previousFogColor;

    // 거리1: 안개가 섞이기 시작하는 카메라로부터의 거리
    public float StartDistance
    {
        get => _startDistance;
        set => SetDistances(value, _endDistance);
    }

    // 거리2: 물체가 완전히 안개 색이 되는 카메라로부터의 거리
    public float EndDistance
    {
        get => _endDistance;
        set => SetDistances(_startDistance, value);
    }

    // 색깔1: 먼 물체와 섞이는 안개 색
    public Color FogColor
    {
        get => _fogColor;
        set
        {
            _fogColor = value;
            ApplyFog();
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
    }

    private void OnEnable()
    {
        SaveSceneFog();
        ApplyFog();
    }

    private void OnDisable()
    {
        // 컴포넌트가 꺼지거나 씬이 바뀌면 씬의 원래 안개 설정으로 돌려놓는다
        RestoreSceneFog();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void OnValidate()
    {
        // 인스펙터에서 값을 바꿔도 끝 거리가 시작 거리보다 멀도록 맞추고 바로 반영한다
        _endDistance = Mathf.Max(_endDistance, _startDistance + MinDistanceGap);

        if (isActiveAndEnabled)
        {
            ApplyFog();
        }
    }

    // 시작 거리(거리1)와 끝 거리(거리2)를 한 번에 설정한다. 끝 거리는 시작 거리보다 멀게 보정된다
    public void SetDistances(float startDistance, float endDistance)
    {
        _startDistance = Mathf.Max(0f, startDistance);
        _endDistance = Mathf.Max(endDistance, _startDistance + MinDistanceGap);
        ApplyFog();
    }

    // 현재 값을 씬의 안개 설정(RenderSettings)에 반영한다. 컴포넌트가 꺼져 있으면 값만 저장해 둔다
    private void ApplyFog()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = _startDistance;
        RenderSettings.fogEndDistance = _endDistance;
        RenderSettings.fogColor = _fogColor;
    }

    // 이 컴포넌트가 덮어쓰기 전의 씬 안개 설정을 저장한다
    private void SaveSceneFog()
    {
        _previousFog = RenderSettings.fog;
        _previousFogMode = RenderSettings.fogMode;
        _previousStartDistance = RenderSettings.fogStartDistance;
        _previousEndDistance = RenderSettings.fogEndDistance;
        _previousFogColor = RenderSettings.fogColor;
    }

    // 저장해 둔 씬 안개 설정으로 되돌린다
    private void RestoreSceneFog()
    {
        RenderSettings.fog = _previousFog;
        RenderSettings.fogMode = _previousFogMode;
        RenderSettings.fogStartDistance = _previousStartDistance;
        RenderSettings.fogEndDistance = _previousEndDistance;
        RenderSettings.fogColor = _previousFogColor;
    }
}
