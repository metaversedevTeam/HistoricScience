using UnityEngine;

// 건축 위치 지정 중 표시되는 홀로그램의 형태와 배치 가능 여부에 따른 색상을 관리하는 컴포넌트
public class Hologram : MonoBehaviour
{
    [SerializeField] private MeshFilter _meshFilter;
    [SerializeField] private MeshRenderer _meshRenderer;

    private MaterialPropertyBlock _propertyBlock;

    private void Awake()
    {
        _propertyBlock = new MaterialPropertyBlock();
    }

    // 표시할 건물의 형태(메시)를 설정한다.
    public void SetMesh(Mesh mesh)
    {
        _meshFilter.sharedMesh = mesh;
    }

    // 배치 가능 여부에 따라 홀로그램 색상을 반영한다.
    public void SetValid(bool isValid, Color validColor, Color invalidColor)
    {
        _meshRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor("_BaseColor", isValid ? validColor : invalidColor);
        _meshRenderer.SetPropertyBlock(_propertyBlock);
    }
}
