using UnityEngine;
using UnityEngine.UI;

// 일시정지 기호(세로 막대 두 개)와 재생 기호(오른쪽을 향한 삼각형) 사이를 오가며 그리는 UI 그래픽.
// 왼쪽 막대는 삼각형의 왼쪽 절반(사다리꼴)으로, 오른쪽 막대는 오른쪽 절반(작은 삼각형)으로 꼭짓점이 옮겨 간다.
[RequireComponent(typeof(CanvasRenderer))]
public class PausePlayIconGraphic : MaskableGraphic
{
    // 일시정지 기호의 두 막대 사이 간격(캔버스 단위)
    [SerializeField, Min(0f)] private float _barGap = 22f;

    // 0이면 일시정지 기호, 1이면 재생 기호
    [SerializeField, Range(0f, 1f)] private float _morph;

    // 일시정지(0)에서 재생(1)으로 바뀐 정도. 바꾸면 메시를 다시 만든다.
    public float Morph
    {
        get => _morph;
        set
        {
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(clamped, _morph))
                return;

            _morph = clamped;
            SetVerticesDirty();
        }
    }

    // 두 기호의 꼭짓점을 진행도만큼 섞어 사각형 두 개로 메시를 만든다.
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = GetPixelAdjustedRect();
        float left = r.xMin;
        float right = r.xMax;
        float top = r.yMax;
        float bottom = r.yMin;
        float midX = r.center.x;
        float midY = r.center.y;
        float halfGap = Mathf.Min(_barGap, r.width) * 0.5f;
        float t = _morph;

        // 삼각형을 가운데 세로선에서 자르면 그 선 위 높이는 전체의 절반이 된다.
        float cutTop = midY + r.height * 0.25f;
        float cutBottom = midY - r.height * 0.25f;

        // 왼쪽 막대 → 삼각형 왼쪽 절반(사다리꼴)
        AddQuad(vh,
            new Vector2(left, bottom),
            new Vector2(left, top),
            Vector2.Lerp(new Vector2(midX - halfGap, top), new Vector2(midX, cutTop), t),
            Vector2.Lerp(new Vector2(midX - halfGap, bottom), new Vector2(midX, cutBottom), t));

        // 오른쪽 막대 → 삼각형 오른쪽 절반(오른쪽 두 꼭짓점이 끝점 하나로 모인다)
        AddQuad(vh,
            Vector2.Lerp(new Vector2(midX + halfGap, bottom), new Vector2(midX, cutBottom), t),
            Vector2.Lerp(new Vector2(midX + halfGap, top), new Vector2(midX, cutTop), t),
            Vector2.Lerp(new Vector2(right, top), new Vector2(right, midY), t),
            Vector2.Lerp(new Vector2(right, bottom), new Vector2(right, midY), t));
    }

#if UNITY_EDITOR
    // 인스펙터에서 값을 바꾸면 바로 다시 그린다.
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif

    // 시계 방향으로 주어진 네 꼭짓점으로 사각형 하나를 메시에 추가한다.
    private void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        int start = vh.currentVertCount;
        Color32 col = color;

        vh.AddVert(a, col, Vector4.zero);
        vh.AddVert(b, col, Vector4.zero);
        vh.AddVert(c, col, Vector4.zero);
        vh.AddVert(d, col, Vector4.zero);

        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }
}
