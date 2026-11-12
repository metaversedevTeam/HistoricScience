using UnityEngine;

// 사각형을 시작 위치에서 끝 위치까지 쓸고 지나가게 한 뒤 잠시 쉬었다가 다시 반복하는 모션. 스캔 라인·반짝임 표현에 쓴다.
public class UIRectSweepMotion : MonoBehaviour
{
    // 쓸기 시작 위치(앵커 기준 위치)
    [SerializeField] private Vector2 _from = new Vector2(-1200f, 0f);

    // 쓸기가 끝나는 위치(앵커 기준 위치)
    [SerializeField] private Vector2 _to = new Vector2(1200f, 0f);

    // 한 번 쓸고 지나가는 데 걸리는 시간(초)
    [SerializeField, Min(0.01f)] private float _duration = 2.4f;

    // 한 번 쓸고 난 뒤 다음 쓸기까지 쉬는 시간(초)
    [SerializeField, Min(0f)] private float _interval = 3f;

    // 화면이 켜진 뒤 첫 쓸기를 시작하기까지 기다리는 시간(초)
    [SerializeField, Min(0f)] private float _startDelay;

    // 움직일 사각형
    private RectTransform _rect;

    // 화면이 켜진 뒤 지난 시간(초)
    private float _elapsed;

    private void Awake()
    {
        _rect = (RectTransform)transform;
    }

    private void OnEnable()
    {
        _elapsed = 0f;
        _rect.anchoredPosition = _from;
    }

    private void Update()
    {
        HandleSweep();
    }

    // 지난 시간을 한 주기(쓸기+쉬기) 안의 진행도로 바꿔, 가속·감속이 들어간 곡선으로 위치를 옮긴다.
    private void HandleSweep()
    {
        _elapsed += Time.unscaledDeltaTime;

        float local = _elapsed - _startDelay;
        if (local < 0f)
            return;

        float cycle = local % (_duration + _interval);
        float t = Mathf.Clamp01(cycle / _duration);
        float eased = t * t * (3f - 2f * t);
        _rect.anchoredPosition = Vector2.LerpUnclamped(_from, _to, eased);
    }
}
