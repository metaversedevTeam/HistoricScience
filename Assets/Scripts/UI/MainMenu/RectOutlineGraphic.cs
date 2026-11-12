using UnityEngine;
using UnityEngine.UI;

// 가운데가 빈 사각형 테두리만 그리는 UI 그래픽. 스프라이트 없이 두께 값만으로 선 굵기를 정한다.
[RequireComponent(typeof(CanvasRenderer))]
public class RectOutlineGraphic : MaskableGraphic
{
    // 테두리 선의 두께(캔버스 단위)
    [SerializeField, Min(0f)] private float _thickness = 2f;

    // 테두리 선의 두께. 바꾸면 메시를 다시 만든다.
    public float Thickness
    {
        get => _thickness;
        set
        {
            _thickness = Mathf.Max(0f, value);
            SetVerticesDirty();
        }
    }

    // 위·아래·왼쪽·오른쪽 네 변을 각각 사각형 하나씩으로 채워 테두리 메시를 만든다.
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = GetPixelAdjustedRect();
        float t = Mathf.Min(_thickness, rect.width * 0.5f, rect.height * 0.5f);
        if (t <= 0f)
            return;

        AddQuad(vh, rect.xMin, rect.yMax - t, rect.xMax, rect.yMax);
        AddQuad(vh, rect.xMin, rect.yMin, rect.xMax, rect.yMin + t);
        AddQuad(vh, rect.xMin, rect.yMin + t, rect.xMin + t, rect.yMax - t);
        AddQuad(vh, rect.xMax - t, rect.yMin + t, rect.xMax, rect.yMax - t);
    }

#if UNITY_EDITOR
    // 인스펙터에서 두께를 바꾸면 바로 다시 그린다.
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif

    // 두 모서리 좌표로 정해지는 사각형 하나를 메시에 추가한다.
    private void AddQuad(VertexHelper vh, float x0, float y0, float x1, float y1)
    {
        int start = vh.currentVertCount;
        Color32 c = color;

        vh.AddVert(new Vector3(x0, y0), c, Vector4.zero);
        vh.AddVert(new Vector3(x0, y1), c, Vector4.zero);
        vh.AddVert(new Vector3(x1, y1), c, Vector4.zero);
        vh.AddVert(new Vector3(x1, y0), c, Vector4.zero);

        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }
}
