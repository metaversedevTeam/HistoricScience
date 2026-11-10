using UnityEngine;

// 흑백 화면 셰이더의 전역 강도를 조절해 화면을 컬러/흑백으로 전환하는 컴포넌트
// 어디서든 MonochromeController.Instance로 접근한다. 씬에 배치하지 않았더라도 처음 접근할 때 하나 만들어 준다
public class MonochromeController : MonoBehaviour
{
    // 현재 씬의 MonochromeController. 씬에 배치돼 있으면 그 인스턴스를 쓰고, 없으면 빈 게임오브젝트를 만들어 붙여 준다
    // (AudioManager, UIManager와 같은 지연 생성 방식)
    public static MonochromeController Instance
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = FindFirstObjectByType<MonochromeController>();
            if (_instance == null)
            {
                // Awake에서 _instance에 자신을 등록한다
                _instance = new GameObject(nameof(MonochromeController)).AddComponent<MonochromeController>();
            }

            return _instance;
        }
    }

    private static MonochromeController _instance;

    // 전역 흑백 강도를 마지막으로 적용한 컨트롤러. 다른 컨트롤러가 값을 넘겨받은 뒤에는 되돌리지 않는다
    private static MonochromeController _owner;

    [Tooltip("시작할 때 흑백 상태로 둘지 여부")]
    [SerializeField] private bool _startMonochrome = false;

    private float _currentIntensity;
    private float _targetIntensity;

    // 초당 강도 변화량. 전환을 시작할 때 전환 시간으로 계산한다
    private float _transitionSpeed;

    // 현재 흑백 전환이 목표로 하는 상태인지 여부
    public bool IsMonochrome => _targetIntensity > 0.5f;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }

        _targetIntensity = _startMonochrome ? 1f : 0f;
        _currentIntensity = _targetIntensity;
    }

    private void OnEnable()
    {
        // OnDisable에서 0으로 돌려놓은 전역 값을 현재 상태로 다시 맞춘다
        ApplyIntensity();
    }

    private void Update()
    {
        HandleTransition();
    }

    private void OnDisable()
    {
        // 컴포넌트가 꺼지거나 씬이 바뀌면 화면 색을 원래대로 돌려놓는다
        // 다른 컨트롤러가 이미 값을 넘겨받았다면 그 값을 덮어쓰지 않는다
        if (_owner != this)
        {
            return;
        }

        _owner = null;
        MonochromeEffect.SetIntensity(0f);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    // 흑백 화면을 켜거나 끈다. transitionDuration(초)이 0 이하면 즉시 전환한다
    public void SetMonochrome(bool isEnabled, float transitionDuration)
    {
        _targetIntensity = isEnabled ? 1f : 0f;

        if (transitionDuration <= 0f)
        {
            _currentIntensity = _targetIntensity;
            ApplyIntensity();
            return;
        }

        // 전환 도중에 다시 호출해도 남은 변화량을 transitionDuration 동안 진행한다
        _transitionSpeed = Mathf.Abs(_targetIntensity - _currentIntensity) / transitionDuration;
    }

    // 현재 상태를 반대로 바꾼다. transitionDuration(초)이 0 이하면 즉시 전환한다
    public void ToggleMonochrome(float transitionDuration)
    {
        SetMonochrome(!IsMonochrome, transitionDuration);
    }

    // 현재 강도를 목표 강도로 부드럽게 이동시킨다
    private void HandleTransition()
    {
        if (Mathf.Approximately(_currentIntensity, _targetIntensity))
        {
            return;
        }

        _currentIntensity = Mathf.MoveTowards(_currentIntensity, _targetIntensity, _transitionSpeed * Time.unscaledDeltaTime);
        ApplyIntensity();
    }

    // 현재 강도를 셰이더 전역 값에 반영한다
    private void ApplyIntensity()
    {
        _owner = this;
        MonochromeEffect.SetIntensity(_currentIntensity);
    }
}
