using UnityEngine;
using UnityEngine.Rendering;

// 코드만으로 만들어지는 기본 모래 먼지 벽 파티클 이펙트. 프리팹이나 머티리얼 에셋 없이 동작하며,
// 구매 에셋으로 교체하기 전까지 쓰는 기본 구현이다.
public class DustWallParticleEffect : BoundaryWallEffect
{
    // 지정하면 런타임 생성 머티리얼 대신 사용한다. 스탠드얼론 빌드에서는 Shader.Find가 셰이더 스트리핑으로
    // 실패할 수 있으므로, 빌드 전에 URP Particles/Unlit 머티리얼 에셋을 만들어 여기 할당하거나
    // Project Settings > Graphics > Always Included Shaders에 해당 셰이더를 추가해야 한다.
    [SerializeField] private Material _overrideMaterial;
    // 먼지 입자 색 (알파 포함)
    [SerializeField] private Color _dustColor = new Color(0.85f, 0.74f, 0.55f, 0.35f);
    // 세그먼트 1m당 초당 방출 입자 수
    [SerializeField, Min(0.1f)] private float _emissionRatePerMeter = 2f;
    // 입자 크기 최소/최대(m)
    [SerializeField, Min(0.1f)] private float _particleSizeMin = 2.5f;
    [SerializeField, Min(0.1f)] private float _particleSizeMax = 4.5f;
    // 입자 수명 최소/최대(초)
    [SerializeField, Min(0.1f)] private float _particleLifetimeMin = 1.5f;
    [SerializeField, Min(0.1f)] private float _particleLifetimeMax = 2.5f;
    // 벽 두께(m)
    [SerializeField, Min(0.1f)] private float _wallThickness = 1.5f;
    // 입자 상승 속도(m/s)
    [SerializeField] private float _upwardDrift = 1f;
    // 뭉게거림을 만드는 노이즈 강도
    [SerializeField, Min(0f)] private float _noiseStrength = 0.4f;

    private ParticleSystem _particleSystem;

    private static Texture2D s_softParticleTexture;
    private static Material s_runtimeMaterial;

    private void Awake()
    {
        HandleEnsureBuilt();
    }

    // 이펙트 방출을 시작한다
    public override void Play()
    {
        HandleEnsureBuilt();
        _particleSystem.Play();
    }

    // 방출만 멈추고 남은 입자는 자연 소멸시킨다 (페이드 아웃)
    public override void Stop()
    {
        if (_particleSystem != null)
            _particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    // 남은 입자까지 모두 사라져 재사용 가능한 상태인지
    public override bool IsFinished => _particleSystem == null || !_particleSystem.IsAlive(false);

    // 세그먼트 길이에 맞춰 방출 영역(Box 셰이프)과 초당 방출량을 조정한다
    protected override void HandleApplyLength(float length)
    {
        HandleEnsureBuilt();
        float clampedLength = Mathf.Max(length, 0.5f);

        ParticleSystem.ShapeModule shape = _particleSystem.shape;
        shape.scale = new Vector3(_wallThickness, 0.5f, clampedLength);

        ParticleSystem.EmissionModule emission = _particleSystem.emission;
        emission.rateOverTime = clampedLength * _emissionRatePerMeter;
    }

    // 파티클 시스템이 없으면 생성하고 모래 먼지 벽에 맞게 모듈을 구성한다. Awake 전에 호출돼도 안전하다.
    private void HandleEnsureBuilt()
    {
        if (_particleSystem != null) return;

        _particleSystem = GetComponent<ParticleSystem>();
        if (_particleSystem == null)
            _particleSystem = gameObject.AddComponent<ParticleSystem>();

        _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = _particleSystem.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // 풀링으로 재배치될 때 입자가 따라 미끄러지지 않게 한다
        main.startColor = _dustColor;
        main.startSize = new ParticleSystem.MinMaxCurve(_particleSizeMin, _particleSizeMax);
        main.startLifetime = new ParticleSystem.MinMaxCurve(_particleLifetimeMin, _particleLifetimeMax);
        main.startSpeed = 0f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 300;

        ParticleSystem.EmissionModule emission = _particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f; // 실제 값은 HandleApplyLength에서 길이에 비례해 설정한다

        ParticleSystem.ShapeModule shape = _particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(_wallThickness, 0.5f, 1f);

        ParticleSystem.VelocityOverLifetimeModule velocity = _particleSystem.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.y = _upwardDrift;

        ParticleSystem.NoiseModule noise = _particleSystem.noise;
        noise.enabled = true;
        noise.strength = _noiseStrength;
        noise.frequency = 0.2f;
        noise.scrollSpeed = 0.3f;

        // 입자별 알파를 0 → 1 → 0으로 굴려, 방출 시작/중지가 팝 없이 페이드처럼 보이게 한다
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = _particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = _particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.3f));

        ParticleSystemRenderer particleRenderer = GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.sortMode = ParticleSystemSortMode.Distance;
        particleRenderer.sharedMaterial = HandleResolveMaterial();
    }

    // 사용할 머티리얼을 정한다: 인스펙터 오버라이드가 우선, 없으면 공유 런타임 생성 머티리얼
    private Material HandleResolveMaterial()
    {
        if (_overrideMaterial != null) return _overrideMaterial;
        if (s_runtimeMaterial != null) return s_runtimeMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            Debug.LogWarning("URP Particles/Unlit 셰이더를 찾지 못해 기본 파티클 셰이더로 대체한다. " +
                             "빌드에서는 머티리얼 에셋을 _overrideMaterial에 할당하거나 Always Included Shaders에 추가해야 한다.");
            shader = Shader.Find("Particles/Standard Unlit");
        }

        Material material = new Material(shader);
        material.SetTexture("_BaseMap", HandleGetSoftParticleTexture());
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Surface", 1f); // Transparent
        material.SetFloat("_Blend", 0f);   // Alpha 블렌드
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;

        s_runtimeMaterial = material;
        return material;
    }

    // 소프트한 원형 그라데이션 파티클 텍스처를 생성해 캐싱한다
    private static Texture2D HandleGetSoftParticleTexture()
    {
        if (s_softParticleTexture != null) return s_softParticleTexture;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = alpha * alpha * (3f - 2f * alpha); // smoothstep으로 부드러운 가장자리
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        s_softParticleTexture = texture;
        return texture;
    }
}
