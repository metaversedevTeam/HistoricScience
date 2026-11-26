using UnityEngine;

// 카메라에서 먼 물체를 안개 색과 섞는 거리 안개를 켜고, 시작 거리·끝 거리·색을 런타임에 조절하는 컴포넌트
// 거리는 카메라 정면 방향 깊이가 아니라 카메라까지의 실제 거리(반경)로 잰다. 화면 가장자리에서도 같은 거리면 같은 만큼 안개가 덮인다
// URP 내장 안개는 깊이로 재므로 끄고, 값을 전역 셰이더 값으로 넘긴다. 불투명 물체는 렌더러의 전체 화면 패스(RadialFog.shader)가,
// 그 뒤에 그려지는 반투명 물체(Water)는 각자의 셰이더가 RadialFog.hlsl로 같은 식을 적용한다
// 시작 거리(거리1)부터 섞이기 시작해 끝 거리(거리2)부터는 원래 색과 관계없이 안개 색만 보인다
// 어디서든 DistanceFogController.Instance로 접근한다. 씬에 배치하지 않았더라도 처음 접근할 때 하나 만들어 준다
public class DistanceFogController : MonoBehaviour
{
    // 끝 거리가 시작 거리보다 최소한 이만큼은 멀도록 유지한다 (같으면 섞는 비율 계산이 0으로 나뉜다)
    private const float MinDistanceGap = 0.01f;

    // RadialFog.hlsl이 읽는 전역 셰이더 값. x: 시작 거리, y: 끝 거리, z: 1 / (끝 - 시작), w: 켜짐 여부(0 또는 1)
    private static readonly int RadialFogParamsId = Shader.PropertyToID("_RadialFogParams");
    private static readonly int RadialFogColorId = Shader.PropertyToID("_RadialFogColor");

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

    // 이 컴포넌트가 켜지기 전의 내장 안개 켜짐 여부. 꺼질 때 되돌린다
    private bool _previousFog;

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
        // 컴포넌트가 꺼지거나 씬이 바뀌면 반경 안개를 끄고 씬의 원래 내장 안개 설정으로 돌려놓는다
        // 전역 셰이더 값은 씬이 바뀌어도 남으므로 반드시 꺼 둔다
        Shader.SetGlobalVector(RadialFogParamsId, Vector4.zero);
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

    // 현재 값을 반경 안개의 전역 셰이더 값에 반영한다. 컴포넌트가 꺼져 있으면 값만 저장해 둔다
    private void ApplyFog()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        // 내장 안개가 켜져 있으면 깊이 기준 안개가 반경 안개 위에 한 번 더 덮이므로 끈다
        RenderSettings.fog = false;

        float inverseRange = 1f / (_endDistance - _startDistance);
        Shader.SetGlobalVector(RadialFogParamsId, new Vector4(_startDistance, _endDistance, inverseRange, 1f));
        // Shader.SetGlobalColor는 선형 색 공간이어도 감마 → 선형 변환을 하지 않으므로, 내장 안개와 같은 색이 나오도록 직접 바꿔 넘긴다
        Color shaderColor = QualitySettings.activeColorSpace == ColorSpace.Linear ? _fogColor.linear : _fogColor;
        Shader.SetGlobalVector(RadialFogColorId, shaderColor);
    }

    // 이 컴포넌트가 끄기 전의 씬 내장 안개 켜짐 여부를 저장한다
    private void SaveSceneFog()
    {
        _previousFog = RenderSettings.fog;
    }

    // 저장해 둔 씬 내장 안개 켜짐 여부로 되돌린다
    private void RestoreSceneFog()
    {
        RenderSettings.fog = _previousFog;
    }
}
