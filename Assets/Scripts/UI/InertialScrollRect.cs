using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 휠 입력을 즉시 이동이 아니라 관성 속도로 바꿔 주는 스크롤 뷰.
// 기본 ScrollRect는 휠을 굴린 만큼 목록을 그 자리에서 옮기지만, 이 클래스는 같은 거리만큼의 속도를 실어 주고
// ScrollRect가 드래그에 쓰는 감속 곡선(Deceleration Rate)으로 서서히 멈추게 한다. 연속으로 굴리면 속도가 쌓여 더 빨리 넘어간다.
public class InertialScrollRect : ScrollRect
{
    // 휠 입력을 받아 콘텐츠를 옮기는 대신 속도만 더한다. 실제 이동은 ScrollRect가 매 프레임 감속하며 처리한다.
    public override void OnScroll(PointerEventData eventData)
    {
        // 관성이 꺼져 있으면 속도를 실어 줘도 다음 프레임에 0이 되므로 기본 동작(즉시 이동)에 맡긴다.
        if (!inertia || !IsActive())
        {
            base.OnScroll(eventData);
            return;
        }

        velocity += ToScrollVelocity(eventData.scrollDelta);
    }

    // 휠 입력량을 이번에 실어 줄 속도로 환산한다.
    private Vector2 ToScrollVelocity(Vector2 scrollDelta)
    {
        return ToScrollAxis(scrollDelta) * (scrollSensitivity * VelocityScale);
    }

    // 지수 감속(v × rate^t)을 적분하면 이동 거리가 v ÷ ln(1/rate)이 되므로,
    // 그 역수를 곱해 두면 휠 한 칸이 기본 ScrollRect와 똑같은 거리를 이동한다. (감속 곡선만 달라진다)
    private float VelocityScale => -Mathf.Log(Mathf.Clamp(decelerationRate, 0.0001f, 0.9999f));

    // 기본 ScrollRect와 같은 규칙으로 휠 입력을 이 스크롤이 실제로 쓰는 축에 맞춘다.
    // (휠 방향은 UI 좌표계와 반대이고, 한 축만 쓰는 스크롤은 반대 축 입력도 받아 준다)
    private Vector2 ToScrollAxis(Vector2 scrollDelta)
    {
        Vector2 delta = scrollDelta;
        delta.y *= -1f;

        if (vertical && !horizontal)
        {
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) delta.y = delta.x;
            delta.x = 0f;
        }

        if (horizontal && !vertical)
        {
            if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x)) delta.x = delta.y;
            delta.y = 0f;
        }

        return delta;
    }
}
