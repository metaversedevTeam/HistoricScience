using System;
using UnityEngine;

// 강조 표시 한 건이 가리키는 대상과 표현 방식. 월드 오브젝트와 UI 요소 중 한쪽만 채워진다.
public readonly struct TutorialHighlightRequest
{
    // 강조할 월드 오브젝트. UI를 강조하는 경우에는 비어 있다.
    public readonly Transform WorldTarget;

    // 월드 오브젝트를 감쌀 반지름(월드 단위)
    public readonly float WorldRadius;

    // 강조할 UI 요소. 월드 오브젝트를 강조하는 경우에는 비어 있다.
    public readonly RectTransform UiTarget;

    // 강조 링 위에 지시 화살표를 띄울지 여부
    public readonly bool ShowArrow;

    // 강조 대상 바깥을 어둡게 덮을지 여부.
    // 화면을 넓게 봐야 하거나(자원 찾기) 다른 UI를 읽어야 하는(건물 목록) 단계에서는 꺼서 링만 보여 준다.
    public readonly bool ShowDim;

    // 대상과 표현 방식을 직접 지정해 강조 요청을 만든다.
    private TutorialHighlightRequest(Transform worldTarget, float worldRadius, RectTransform uiTarget, bool showArrow, bool showDim)
    {
        WorldTarget = worldTarget;
        WorldRadius = worldRadius;
        UiTarget = uiTarget;
        ShowArrow = showArrow;
        ShowDim = showDim;
    }

    // 월드 오브젝트를 강조하는 요청을 만든다.
    public static TutorialHighlightRequest World(Transform target, float radius = 1.6f, bool dim = true, bool showArrow = true)
    {
        return new TutorialHighlightRequest(target, radius, null, showArrow, dim);
    }

    // UI 요소를 강조하는 요청을 만든다.
    public static TutorialHighlightRequest Ui(RectTransform target, bool dim = true, bool showArrow = true)
    {
        return new TutorialHighlightRequest(null, 0f, target, showArrow, dim);
    }

    // 가리킬 대상이 아직 살아 있는지 여부
    public bool IsValid => WorldTarget != null || UiTarget != null;
}

// 강조 UI에 전달되는 페이로드 — 매 프레임 다시 물어볼 대상 제공자와, 이 UI가 그 아래에 머물러야 할 형제 오브젝트
public readonly struct TutorialHighlightData
{
    // 매 프레임 호출해 강조 대상을 다시 받아 오는 제공자. null을 돌려주면 그 프레임에는 아무것도 그리지 않는다.
    public readonly Func<TutorialHighlightRequest?> Provider;

    // 항상 이 오브젝트보다 아래에 그려지도록 형제 순서를 맞출 대상 (튜토리얼 대화창)
    public readonly Transform KeepBelow;

    // 대상 제공자와 위에 둘 형제 오브젝트로 페이로드를 구성한다.
    public TutorialHighlightData(Func<TutorialHighlightRequest?> provider, Transform keepBelow)
    {
        Provider = provider;
        KeepBelow = keepBelow;
    }
}
