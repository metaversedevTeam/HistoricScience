using UnityEngine;

// 하루 시간(0~1)을 진행시키며 Skybox 셰이더 그래프의 시간 값과 디렉셔널 라이트(낮엔 태양, 밤엔 달)의 방향·밝기·색을 함께 맞추는 컴포넌트
[ExecuteAlways]
public class SkyboxTimeOfDay : MonoBehaviour
{
    private static readonly int TimeOfDayId = Shader.PropertyToID("_TimeOfDay");
    private static readonly int SunOrbitYawId = Shader.PropertyToID("_SunOrbitYaw");
    private static readonly int SunOrbitTiltId = Shader.PropertyToID("_SunOrbitTilt");
    private static readonly int DayHorizonColorId = Shader.PropertyToID("_DayHorizonColor");
    private static readonly int NightHorizonColorId = Shader.PropertyToID("_NightHorizonColor");
    private static readonly int SunsetColorId = Shader.PropertyToID("_SunsetColor");

    // Skybox.hlsl의 노을 섞기 비율(0.3 + 0.7 * 태양 쪽 가중치)을 지평선 한 바퀴로 평균한 값. 태양 쪽 가중치 pow(cos²(φ/2), 3)의 평균은 5/16이다
    private const float HorizonSunsetWeight = 0.3f + 0.7f * (5f / 16f);

    [Header("Sky")]
    [Tooltip("Skybox 셰이더 그래프를 쓰는 머티리얼. 비워 두면 Lighting 설정의 스카이박스 머티리얼을 사용한다.")]
    [SerializeField] private Material skyboxMaterial;

    [Header("Time")]
    [Tooltip("하루 진행도. 0 = 자정, 0.25 = 일출, 0.5 = 정오, 0.75 = 일몰")]
    [Range(0f, 1f)]
    [SerializeField] private float timeOfDay = 0.35f;

    [Tooltip("플레이 중 시간이 저절로 흐르게 할지 여부")]
    [SerializeField] private bool autoAdvance = true;

    [Tooltip("하루(0 → 1)가 지나가는 데 걸리는 실제 시간(초)")]
    [Min(1f)]
    [SerializeField] private float dayLengthSeconds = 300f;

    [Header("Light")]
    [Tooltip("하늘의 태양·달 방향에 맞춰 회전시킬 디렉셔널 라이트. 비워 두면 Lighting 설정의 Sun Source를 사용한다.")]
    [SerializeField] private Light directionalLight;

    [SerializeField] private float sunIntensity = 1.2f;
    [SerializeField] private float moonIntensity = 0.15f;
    [SerializeField] private Gradient sunColorByHeight = CreateDefaultSunGradient();
    [SerializeField] private Color moonLightColor = new Color(0.55f, 0.65f, 0.9f);

    private Material runtimeSkybox;
    private Material originalSkybox;

    // 현재 하루 진행도(0~1)
    public float TimeOfDay => timeOfDay;

    // 하루 진행도를 직접 지정하고 하늘과 조명에 바로 반영한다.
    public void SetTimeOfDay(float value)
    {
        timeOfDay = Mathf.Repeat(value, 1f);
        ApplyAll();
    }

    // Skybox.hlsl의 SkySunDirection과 같은 식으로 태양 방향(하늘을 향하는 단위 벡터)을 구한다.
    public static Vector3 GetSunDirection(float time, float orbitYaw, float orbitTilt)
    {
        float angle = (time - 0.25f) * Mathf.PI * 2f;
        Vector3 local = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        return (Quaternion.Euler(0f, orbitYaw, 0f) * (Quaternion.Euler(orbitTilt, 0f, 0f) * local)).normalized;
    }

    // 지금 시간의 지평선 하늘 색을 구한다. Skybox.hlsl의 SkyGradient를 지평선 높이에서 방위 평균한 값이며 구름·해·달은 넣지 않는다
    // 스카이박스 머티리얼이 없거나 Skybox 셰이더 그래프가 아니면 false를 반환한다
    public bool TryGetHorizonSkyColor(out Color color)
    {
        color = Color.black;
        Material sky = GetActiveSkybox();
        if (sky == null || !sky.HasProperty(DayHorizonColorId))
        {
            return false;
        }

        float sunHeight = GetSunDirection(timeOfDay, sky.GetFloat(SunOrbitYawId), sky.GetFloat(SunOrbitTiltId)).y;
        float dayAmount = ShaderSmoothStep(-0.15f, 0.2f, sunHeight);
        float twilight = 1f - ShaderSmoothStep(0f, 0.3f, Mathf.Abs(sunHeight + 0.03f));

        // 셰이더는 선형 색 공간에서 섞으므로 같은 공간에서 계산한 뒤 머티리얼 색과 같은 공간으로 되돌린다
        bool isLinear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        Color dayHorizon = ToShaderSpace(sky.GetColor(DayHorizonColorId), isLinear);
        Color nightHorizon = ToShaderSpace(sky.GetColor(NightHorizonColorId), isLinear);
        Color sunset = ToShaderSpace(sky.GetColor(SunsetColorId), isLinear);

        Color horizon = Color.Lerp(nightHorizon, dayHorizon, dayAmount);
        Color mixed = Color.Lerp(horizon, sunset, twilight * HorizonSunsetWeight);
        mixed.a = 1f;
        color = isLinear ? mixed.gamma : mixed;
        return true;
    }

    // HLSL smoothstep과 같은 식으로 edge0~edge1 사이를 부드럽게 0~1로 바꾼다
    private static float ShaderSmoothStep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    // 선형 색 공간 프로젝트면 머티리얼 색을 셰이더가 받는 선형 값으로 바꾼다
    private static Color ToShaderSpace(Color color, bool isLinear)
    {
        return isLinear ? color.linear : color;
    }

    private void OnEnable()
    {
        PrepareRuntimeSkybox();
        ApplyAll();
    }

    private void OnDisable()
    {
        ReleaseRuntimeSkybox();
    }

    private void OnValidate()
    {
        ApplyAll();
    }

    private void Update()
    {
        HandleTimeAdvance();
        ApplyAll();
    }

    // 플레이 중이고 자동 진행이 켜져 있으면 하루 진행도를 흘려보낸다. 치트의 시간 진행 속도 배율만큼 빨라진다.
    private void HandleTimeAdvance()
    {
        if (!Application.isPlaying || !autoAdvance)
        {
            return;
        }

        timeOfDay = Mathf.Repeat(timeOfDay + Time.deltaTime * TimeSpeedCheat.Multiplier / dayLengthSeconds, 1f);
    }

    // 하늘 머티리얼과 디렉셔널 라이트에 현재 시간을 반영한다.
    private void ApplyAll()
    {
        Material sky = GetActiveSkybox();
        if (sky == null || !sky.HasProperty(TimeOfDayId))
        {
            return;
        }

        sky.SetFloat(TimeOfDayId, timeOfDay);
        HandleLight(sky.GetFloat(SunOrbitYawId), sky.GetFloat(SunOrbitTiltId));
    }

    // 태양이 떠 있으면 태양 방향으로, 지면 달 방향으로 라이트를 돌리고 밝기와 색을 정한다.
    private void HandleLight(float orbitYaw, float orbitTilt)
    {
        Light targetLight = directionalLight != null ? directionalLight : RenderSettings.sun;
        if (targetLight == null)
        {
            return;
        }

        Vector3 sunDirection = GetSunDirection(timeOfDay, orbitYaw, orbitTilt);
        bool isDay = sunDirection.y >= 0f;
        Vector3 lightSourceDirection = isDay ? sunDirection : -sunDirection;
        targetLight.transform.rotation = Quaternion.LookRotation(-lightSourceDirection);

        // 지평선에 가까울수록 빛을 약하게 해 해·달이 바뀌는 순간 조명이 튀지 않게 한다.
        float height = Mathf.Clamp01(lightSourceDirection.y);
        float horizonFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(height / 0.15f));
        targetLight.intensity = (isDay ? sunIntensity : moonIntensity) * horizonFade;
        targetLight.color = isDay ? sunColorByHeight.Evaluate(height) : moonLightColor;
    }

    // 플레이 중에는 원본 머티리얼 에셋이 바뀌지 않도록 복제본을 만들어 스카이박스로 사용한다.
    private void PrepareRuntimeSkybox()
    {
        if (!Application.isPlaying || runtimeSkybox != null)
        {
            return;
        }

        Material source = skyboxMaterial != null ? skyboxMaterial : RenderSettings.skybox;
        if (source == null)
        {
            return;
        }

        originalSkybox = RenderSettings.skybox;
        runtimeSkybox = new Material(source) { name = source.name + " (Runtime)" };
        RenderSettings.skybox = runtimeSkybox;
    }

    // 런타임 복제 머티리얼을 정리하고 원래 스카이박스로 되돌린다.
    private void ReleaseRuntimeSkybox()
    {
        if (runtimeSkybox == null)
        {
            return;
        }

        if (RenderSettings.skybox == runtimeSkybox)
        {
            RenderSettings.skybox = originalSkybox;
        }

        Destroy(runtimeSkybox);
        runtimeSkybox = null;
    }

    // 지금 시간을 적용할 스카이박스 머티리얼을 고른다(플레이 중 복제본 → 지정 머티리얼 → Lighting 설정 순).
    private Material GetActiveSkybox()
    {
        if (runtimeSkybox != null)
        {
            return runtimeSkybox;
        }

        return skyboxMaterial != null ? skyboxMaterial : RenderSettings.skybox;
    }

    // 태양 높이(0 = 지평선, 1 = 천정)에 따른 기본 햇빛 색(노을 주황 → 한낮 흰색)을 만든다.
    private static Gradient CreateDefaultSunGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.55f, 0.3f), 0f),
                new GradientColorKey(new Color(1f, 0.85f, 0.7f), 0.25f),
                new GradientColorKey(new Color(1f, 0.97f, 0.92f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }
}
