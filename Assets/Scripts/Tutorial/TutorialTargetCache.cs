using System;
using UnityEngine;

// 강조 대상을 찾는 씬 검색을 매 프레임 반복하지 않도록, 찾은 결과를 잠시 들고 있는 캐시.
// 강조 제공자는 매 프레임 호출되지만 실제 검색은 정해진 간격으로만 다시 한다.
public class TutorialTargetCache<T> where T : Component
{
    // 실제로 씬을 훑어 대상을 찾는 함수
    private readonly Func<T> _search;

    // 다시 검색하기까지 기다리는 시간(초)
    private readonly float _interval;

    // 마지막으로 찾아 둔 대상. 파괴되었으면 null이 된다.
    private T _cached;

    // 다음에 검색을 다시 할 시각
    private float _nextSearchTime;

    // 검색 함수와 재검색 간격으로 캐시를 만든다.
    public TutorialTargetCache(Func<T> search, float interval = 0.4f)
    {
        _search = search;
        _interval = interval;
    }

    // 들고 있는 대상을 돌려준다. 간격이 지났을 때만 다시 찾으므로, 대상이 없는 동안에도 매 프레임 씬을 훑지 않는다.
    public T Get()
    {
        if (Time.time >= _nextSearchTime)
        {
            _nextSearchTime = Time.time + _interval;
            _cached = _search();
        }

        // 찾아 둔 사이에 파괴된 대상은 없는 것으로 본다.
        return _cached != null ? _cached : null;
    }

    // 들고 있던 대상을 버려 다음 조회에서 곧바로 다시 찾게 한다.
    public void Invalidate()
    {
        _cached = null;
        _nextSearchTime = 0f;
    }
}
