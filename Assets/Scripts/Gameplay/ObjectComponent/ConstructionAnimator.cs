using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 건물이 지어질 때 메시를 아래에서 위로 서서히 드러내는 연출을 재생하는 컴포넌트.
// 연출 동안에만 렌더러의 머티리얼을 같은 텍스처를 쓰는 절단 셰이더 버전으로 바꿔 두고, 절단 높이를 바닥에서 꼭대기까지 올린 뒤 원래 머티리얼로 되돌린다.
// 저장 파일에서 복원되는 건물처럼 연출이 필요 없는 경우가 있으므로 스스로 시작하지 않으며, 건물을 소환한 쪽이 Play를 호출해야 재생된다.
public class ConstructionAnimator : MonoBehaviour
{
    private const string k_ConstructShaderName = "HistoricScience/BuildingConstruct";

    private static readonly int k_CutHeightId = Shader.PropertyToID("_CutHeight");
    private static readonly int k_EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
    private static readonly int k_EdgeColorId = Shader.PropertyToID("_EdgeColor");
    private static readonly int k_NoiseScaleId = Shader.PropertyToID("_NoiseScale");
    private static readonly int k_NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
    private static readonly int k_BumpMapId = Shader.PropertyToID("_BumpMap");
    private static readonly int k_MetallicGlossMapId = Shader.PropertyToID("_MetallicGlossMap");
    private static readonly int k_OcclusionMapId = Shader.PropertyToID("_OcclusionMap");
    private static readonly int k_EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // 원본 머티리얼마다 만들어 둔 절단 셰이더 버전. 진행도는 렌더러마다 프로퍼티 블록으로 따로 넣으므로 이 머티리얼 자체에는
    // 건물마다 달라질 값이 없어, 여러 건물이 같은 것을 돌려 쓴다.
    private static readonly Dictionary<Material, Material> s_ConstructMaterials = new();

    // 연출에 쓸 절단 셰이더. HistoricScience/BuildingConstruct를 지정한다.
    [SerializeField] private Shader _constructShader;
    // 연출을 걸 렌더러. 여기에 지정한 것만 아래에서부터 드러나고 나머지는 처음부터 완성된 모습으로 남는다.
    // 비워 두면 자식의 모든 메시 렌더러를 대상으로 삼는다.
    [SerializeField] private List<Renderer> _targetRenderers = new();
    // 절단 경계에 그릴 빛나는 띠의 두께(월드 단위)
    [SerializeField] private float _edgeWidth = 0.15f;
    // 절단 경계 띠의 색. 강하게 빛나도록 HDR로 지정한다.
    [SerializeField, ColorUsage(false, true)] private Color _edgeColor = new Color(2f, 1.3f, 0.5f);
    // 절단선을 울퉁불퉁하게 흔드는 노이즈의 촘촘함
    [SerializeField] private float _noiseScale = 4f;
    // 절단선을 흔드는 폭(월드 단위)
    [SerializeField] private float _noiseStrength = 0.15f;
    // 절단 높이의 시작과 끝에 더할 여유. 노이즈로 흔들린 경계까지 확실히 다 가리고 다 드러내도록 건물 높이 바깥으로 조금 넘긴다.
    [SerializeField] private float _heightMargin = 0.2f;
    // 시간에 따른 차오름 곡선. 기본은 일정한 속도다.
    [SerializeField] private AnimationCurve _progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private readonly List<Renderer> _renderers = new();
    private readonly List<Material[]> _originalMaterials = new();
    private MaterialPropertyBlock _propertyBlock;
    private Coroutine _playRoutine;
    private Action _onComplete;

    // 지금 건축 연출이 재생 중인지 여부
    public bool IsPlaying => _playRoutine != null;

    // 연출 도중에 비활성화되면 코루틴이 멈춰 건물이 반쯤 지어진 채로 남으므로, 곧바로 완성 상태로 되돌린다.
    private void OnDisable()
    {
        if (!IsPlaying) return;

        StopCoroutine(_playRoutine);
        _playRoutine = null;
        Finish();
    }

    // 플레이 모드를 다시 시작할 때 도메인 리로드를 꺼 두면 정적 캐시가 그대로 남아 이미 파괴된 머티리얼을 가리키므로, 시작 시 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearConstructMaterialCache()
    {
        s_ConstructMaterials.Clear();
    }

    // 건축 연출을 duration초 동안 재생하고, 끝나면 원래 머티리얼로 되돌린 뒤 onComplete를 호출한다.
    // 시간이 0 이하이거나 연출할 렌더러·셰이더가 없으면 연출 없이 곧바로 완료로 처리한다.
    public void Play(float duration, Action onComplete = null)
    {
        if (IsPlaying) return;

        CacheRenderers();

        if (duration <= 0f || _renderers.Count == 0 || !TryGetConstructShader(out Shader constructShader))
        {
            onComplete?.Invoke();
            return;
        }

        _onComplete = onComplete;
        // 머티리얼을 갈무리하기까지 한 프레임을 기다리는 동안 완성된 건물이 비치지 않도록 렌더러를 먼저 꺼 둔다.
        SetRenderersEnabled(false);
        _playRoutine = StartCoroutine(PlayRoutine(duration, constructShader));
    }

    // 절단 높이를 바닥에서 꼭대기까지 올리며 건물을 차오르게 하고, 다 차면 완성 상태로 정리한다.
    private IEnumerator PlayRoutine(float duration, Shader constructShader)
    {
        // BottomMaterialController처럼 Start에서 머티리얼을 바꿔 놓는 컴포넌트가 있으므로, 한 프레임 기다렸다가 최종 머티리얼을 갈무리한다.
        // 지금 갈무리하면 그 컴포넌트가 나중에 넣을 바닥 머티리얼을 연출이 끝나며 덮어써 버린다.
        yield return null;

        if (!TryComputeHeightRange(out float bottomY, out float topY))
        {
            _playRoutine = null;
            Finish();
            yield break;
        }

        ApplyConstructMaterials(constructShader);
        SetCutHeight(bottomY);
        SetRenderersEnabled(true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = _progressCurve.Evaluate(Mathf.Clamp01(elapsed / duration));
            SetCutHeight(Mathf.LerpUnclamped(bottomY, topY, progress));
            yield return null;
        }

        _playRoutine = null;
        Finish();
    }

    // 연출을 끝내고 원래 머티리얼로 되돌린 뒤 완료 콜백을 부른다. 중간에 비활성화돼 끊긴 경우에도 같은 정리를 거친다.
    private void Finish()
    {
        RestoreOriginalMaterials();
        SetRenderersEnabled(true);

        Action onComplete = _onComplete;
        _onComplete = null;
        onComplete?.Invoke();
    }

    // 연출 대상이 될 렌더러를 모은다. 인스펙터에서 지정한 목록이 있으면 그것만 쓰고, 비어 있으면 자식의 모든 메시 렌더러를 대상으로 삼는다.
    private void CacheRenderers()
    {
        _renderers.Clear();

        if (_targetRenderers.Count > 0)
        {
            foreach (var renderer in _targetRenderers)
                AddRendererIfValid(renderer);

            return;
        }

        foreach (var renderer in GetComponentsInChildren<Renderer>(false))
            AddRendererIfValid(renderer);
    }

    // 연출을 걸 수 있는 렌더러면 대상 목록에 담는다. 파티클처럼 머티리얼을 바꾸면 안 되는 렌더러와, 원래부터 꺼져 있어 보이지 않는 렌더러는 제외한다.
    // 꺼져 있던 렌더러까지 담으면 연출이 끝날 때 함께 켜 버려 원래 숨겨 두었던 부분이 드러난다.
    // 같은 렌더러가 두 번 담기면 머티리얼을 되돌릴 때 짝이 어긋나므로 중복도 걸러낸다.
    private void AddRendererIfValid(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled) return;
        if (renderer is not (MeshRenderer or SkinnedMeshRenderer)) return;
        if (_renderers.Contains(renderer)) return;

        _renderers.Add(renderer);
    }

    // 캐싱해 둔 렌더러를 한꺼번에 켜거나 끈다. 머티리얼을 갈무리하기까지 한 프레임 동안 건물을 완전히 감추는 데 쓴다.
    private void SetRenderersEnabled(bool isEnabled)
    {
        foreach (var renderer in _renderers)
        {
            if (renderer != null)
                renderer.enabled = isEnabled;
        }
    }

    // 연출에 쓸 셰이더를 얻는다. 인스펙터에 지정돼 있지 않으면 이름으로 찾아 보고, 그마저 없으면 연출을 건너뛰도록 false를 반환한다.
    private bool TryGetConstructShader(out Shader constructShader)
    {
        constructShader = _constructShader != null ? _constructShader : Shader.Find(k_ConstructShaderName);

        if (constructShader == null)
            Debug.LogWarning($"[ConstructionAnimator] 건축 연출 셰이더({k_ConstructShaderName})를 찾지 못해 연출을 건너뜁니다.", this);

        return constructShader != null;
    }

    // 모은 렌더러 전체를 감싸는 월드 높이 범위를 구한다. 절단 높이가 여기서 시작해 여기서 끝난다.
    private bool TryComputeHeightRange(out float bottomY, out float topY)
    {
        bottomY = 0f;
        topY = 0f;

        Bounds bounds = default;
        bool hasBounds = false;
        foreach (var renderer in _renderers)
        {
            if (renderer == null) continue;

            if (hasBounds)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            else
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
        }

        if (!hasBounds)
            return false;

        bottomY = bounds.min.y - _heightMargin;
        topY = bounds.max.y + _heightMargin;
        return true;
    }

    // 각 렌더러의 머티리얼을 절단 셰이더 버전으로 바꾸고, 되돌릴 수 있도록 원래 머티리얼 배열을 보관한다.
    private void ApplyConstructMaterials(Shader constructShader)
    {
        _originalMaterials.Clear();
        _propertyBlock ??= new MaterialPropertyBlock();

        foreach (var renderer in _renderers)
        {
            // 되돌릴 때 렌더러와 짝을 맞춰야 하므로, 사라진 렌더러 자리도 빈 배열로 채워 개수를 맞춘다.
            if (renderer == null)
            {
                _originalMaterials.Add(Array.Empty<Material>());
                continue;
            }

            // sharedMaterials는 호출할 때마다 새 배열을 돌려주므로, 그대로 보관해 두어도 이후의 교체에 영향받지 않는다.
            Material[] originals = renderer.sharedMaterials;
            _originalMaterials.Add(originals);

            var constructMaterials = new Material[originals.Length];
            for (int i = 0; i < originals.Length; i++)
                constructMaterials[i] = GetConstructMaterial(originals[i], constructShader);

            renderer.sharedMaterials = constructMaterials;
        }
    }

    // 보관해 둔 원래 머티리얼을 되돌리고 연출용으로 넣었던 프로퍼티 블록을 비운다.
    private void RestoreOriginalMaterials()
    {
        for (int i = 0; i < _renderers.Count && i < _originalMaterials.Count; i++)
        {
            Renderer renderer = _renderers[i];
            if (renderer == null) continue;

            renderer.sharedMaterials = _originalMaterials[i];
            // 프로퍼티 블록이 남아 있으면 그 렌더러만 SRP Batcher에서 빠지므로, 연출이 끝나면 통째로 비운다.
            renderer.SetPropertyBlock(null);
        }

        _originalMaterials.Clear();
    }

    // 이번 프레임의 절단 높이와 경계 연출 설정을 모든 렌더러에 넣는다. 절단 머티리얼은 건물끼리 공유하므로 머티리얼이 아니라 프로퍼티 블록으로 넘긴다.
    private void SetCutHeight(float cutHeight)
    {
        foreach (var renderer in _renderers)
        {
            if (renderer == null) continue;

            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(k_CutHeightId, cutHeight);
            _propertyBlock.SetFloat(k_EdgeWidthId, _edgeWidth);
            _propertyBlock.SetColor(k_EdgeColorId, _edgeColor);
            _propertyBlock.SetFloat(k_NoiseScaleId, _noiseScale);
            _propertyBlock.SetFloat(k_NoiseStrengthId, _noiseStrength);
            renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    // 원본과 같은 텍스처·색을 가지되 셰이더만 절단 셰이더로 바꾼 머티리얼을 돌려준다. 한 번 만든 것은 캐시해 두고 다시 쓴다.
    private static Material GetConstructMaterial(Material original, Shader constructShader)
    {
        if (original == null)
            return null;

        if (s_ConstructMaterials.TryGetValue(original, out Material cached) && cached != null)
            return cached;

        // 원본을 복사한 뒤 셰이더만 갈아 끼우면 이름이 같은 프로퍼티(_BaseMap·_BaseColor 등)의 값이 그대로 넘어와, 건축 중에도 완성된 건물과 같은 텍스처가 보인다.
        var constructMaterial = new Material(original) { shader = constructShader };
        constructMaterial.name = $"{original.name} (Construct)";
        SyncTextureKeywords(constructMaterial);

        s_ConstructMaterials[original] = constructMaterial;
        return constructMaterial;
    }

    // 셰이더를 바꿔도 원본의 키워드 설정이 그대로 남아 실제 텍스처 유무와 어긋날 수 있으므로, 텍스처가 꽂혀 있는지 보고 직접 켜고 끈다.
    private static void SyncTextureKeywords(Material material)
    {
        SetKeyword(material, "_NORMALMAP", material.GetTexture(k_BumpMapId) != null);
        SetKeyword(material, "_METALLICSPECGLOSSMAP", material.GetTexture(k_MetallicGlossMapId) != null);
        SetKeyword(material, "_OCCLUSIONMAP", material.GetTexture(k_OcclusionMapId) != null);
        SetKeyword(material, "_EMISSION", material.GetColor(k_EmissionColorId).maxColorComponent > 0f);
    }

    // 머티리얼의 셰이더 키워드를 켜거나 끈다.
    private static void SetKeyword(Material material, string keyword, bool isEnabled)
    {
        if (isEnabled)
            material.EnableKeyword(keyword);
        else
            material.DisableKeyword(keyword);
    }
}
