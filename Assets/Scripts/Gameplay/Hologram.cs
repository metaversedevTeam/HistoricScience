using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 건축 위치 지정 중 표시되는 홀로그램. 건물의 모델 오브젝트를 그대로 인스턴스해 형태를 만들고, 그 재질을 홀로그램 재질로 바꿔
// 배치 가능 여부를 색으로 표시하는 컴포넌트
public class Hologram : MonoBehaviour
{
    // 소환한 모델의 모든 렌더러에 덮어씌울 반투명 홀로그램 재질
    [SerializeField] private Material _hologramMaterial;

    private MaterialPropertyBlock _propertyBlock;
    private readonly List<Renderer> _renderers = new();
    private GameObject _modelInstance;
    private Bounds _modelBounds;

    // 소환한 모델 전체를 감싸는 크기(홀로그램 로컬 기준). 배치 판정에 쓸 건물 크기를 계산하는 데 쓴다.
    public Bounds ModelBounds => _modelBounds;

    private void Awake()
    {
        _propertyBlock = new MaterialPropertyBlock();
    }

    // 표시할 건물의 모델 오브젝트를 인스턴스해 홀로그램의 형태로 삼는다. 원본의 스케일·회전을 그대로 이어받으므로 실제로 지어질 모습과 같은 크기로 보인다.
    public void SetModel(GameObject modelSource)
    {
        ClearModel();
        if (modelSource == null) return;

        _modelInstance = Instantiate(modelSource, transform);
        _modelInstance.SetActive(true);

        StripColliders(_modelInstance);
        ApplyHologramMaterial(_modelInstance);

        _modelBounds = CalculateModelBounds();
    }

    // 배치 가능 여부에 따라 홀로그램 색상을 반영한다.
    public void SetValid(bool isValid, Color validColor, Color invalidColor)
    {
        Color color = isValid ? validColor : invalidColor;
        foreach (var renderer in _renderers)
        {
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    // 이전에 소환해 둔 모델을 파괴하고 캐싱한 렌더러 목록을 비운다.
    private void ClearModel()
    {
        if (_modelInstance != null)
            Destroy(_modelInstance);

        _modelInstance = null;
        _renderers.Clear();
        _modelBounds = new Bounds(Vector3.zero, Vector3.zero);
    }

    // 소환한 모델의 콜라이더를 모두 제거한다. 홀로그램은 아직 실체가 없는 미리보기라, 남겨 두면 배치 가능 여부 판정에 스스로가 걸린다.
    // Destroy는 프레임 끝에야 반영되므로, 소환 직후의 판정까지 확실히 비켜 가도록 먼저 꺼 둔다.
    private void StripColliders(GameObject modelInstance)
    {
        foreach (var collider in modelInstance.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
            Destroy(collider);
        }
    }

    // 소환한 모델의 모든 렌더러 재질을 홀로그램 재질로 바꾸고, 색상 변경에 쓸 수 있도록 렌더러를 캐싱한다.
    private void ApplyHologramMaterial(GameObject modelInstance)
    {
        modelInstance.GetComponentsInChildren(true, _renderers);

        foreach (var renderer in _renderers)
        {
            // 서브메시가 여럿인 모델은 재질 수가 맞지 않으면 일부가 원래 재질로 남으므로, 기존 재질 수만큼 홀로그램 재질을 채워 넣는다.
            var materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = _hologramMaterial;

            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    // 소환한 모델의 모든 렌더러를 합친 크기를 홀로그램 로컬 공간 기준으로 계산한다.
    private Bounds CalculateModelBounds()
    {
        if (_renderers.Count == 0)
            return new Bounds(Vector3.zero, Vector3.zero);

        Bounds bounds = _renderers[0].bounds;
        for (int i = 1; i < _renderers.Count; i++)
            bounds.Encapsulate(_renderers[i].bounds);

        bounds.center -= transform.position;
        return bounds;
    }
}
