using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// UI 그래픽의 스텐실 사용을 바꿔 화면에 구멍을 뚫는 연출에 쓴다. 구멍 쪽은 색을 그리지 않고 스텐실 비트만 켜며,
// 지워지는 쪽은 그 비트가 켜진 곳에는 그려지지 않는다. uGUI Mask가 쓰는 낮은 비트와 겹치지 않도록 가장 높은 비트를 쓴다.
[RequireComponent(typeof(Graphic))]
public class UIStencilCutout : MonoBehaviour, IMaterialModifier
{
    // 구멍 표시에 쓰는 스텐실 비트
    private const int CutoutBit = 128;

    // 구멍을 뚫는 쪽인지(true), 구멍에서 지워지는 쪽인지(false)
    [SerializeField] private bool _isHole;

    // 이 그래픽에 쓰도록 만들어 둔 스텐실 재질
    private Material _material;

    // 재질을 바꿀 그래픽
    private Graphic _graphic;

    private void OnEnable()
    {
        HandleRefreshGraphic();
    }

    private void OnDisable()
    {
        HandleReleaseMaterial();
        HandleRefreshGraphic();
    }

    // 구멍을 뚫는 쪽인지 지워지는 쪽인지 정하고, 그래픽이 재질을 다시 만들게 한다.
    public void Setup(bool isHole)
    {
        _isHole = isHole;
        HandleRefreshGraphic();
    }

    // 그래픽이 그릴 재질을 구멍 뚫기용 또는 지워지기용 스텐실 재질로 바꿔 돌려준다.
    public Material GetModifiedMaterial(Material baseMaterial)
    {
        if (!isActiveAndEnabled)
            return baseMaterial;

        Material created = _isHole
            ? StencilMaterial.Add(baseMaterial, CutoutBit, StencilOp.Replace, CompareFunction.Always, 0, CutoutBit, CutoutBit)
            : StencilMaterial.Add(baseMaterial, CutoutBit, StencilOp.Keep, CompareFunction.NotEqual, ColorWriteMask.All, CutoutBit, 0);

        HandleReleaseMaterial();
        _material = created;
        return _material;
    }

    // 만들어 둔 스텐실 재질을 공용 재질 목록에 돌려준다.
    private void HandleReleaseMaterial()
    {
        if (_material == null)
            return;

        StencilMaterial.Remove(_material);
        _material = null;
    }

    // 그래픽이 다음 그리기 전에 재질을 다시 고르게 한다.
    private void HandleRefreshGraphic()
    {
        if (_graphic == null)
            _graphic = GetComponent<Graphic>();

        if (_graphic != null)
            _graphic.SetMaterialDirty();
    }
}
