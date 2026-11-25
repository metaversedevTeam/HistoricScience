using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 설정 화면. 메인 메뉴와 일시정지 화면의 OPTION 버튼이 UIManager로 여는 공용 창이며,
// 카메라 수평 감도와 청크 LOD0·LOD1 로딩 범위를 슬라이더로, 흑백 강조 사용 여부를 스위치로 바꿔 GameSettings에 바로 반영한다. (수직 감도는 수평의 절반으로 자동으로 맞춰진다)
// 일시정지 중(Time.timeScale = 0)에도 열리므로 페이드 연출은 실제 경과 시간으로 돈다.
public class SettingsUI : OpenableUIBase
{
    [Header("카메라 감도")]
    [SerializeField] private Slider _horizontalSlider;
    [SerializeField] private TMP_Text _horizontalValueText;

    [Header("청크 범위")]
    [SerializeField] private Slider _chunkLod0Slider;
    [SerializeField] private TMP_Text _chunkLod0ValueText;
    [SerializeField] private Slider _chunkLod1Slider;
    [SerializeField] private TMP_Text _chunkLod1ValueText;

    [Header("흑백 강조")]
    // 줄 전체를 누르면 흑백 강조를 켜고 끈다.
    [SerializeField] private Button _monochromeFocusButton;
    [SerializeField] private Image _monochromeFocusTrack;
    [SerializeField] private RectTransform _monochromeFocusKnob;
    [SerializeField] private TMP_Text _monochromeFocusValueText;
    // 손잡이가 트랙 중앙을 기준으로 좌우 끝까지 움직이는 거리
    [SerializeField] private float _monochromeFocusKnobTravel = 18f;
    [SerializeField] private Color _switchOnTrackColor = new Color(0.220f, 0.741f, 0.973f, 1f);
    [SerializeField] private Color _switchOffTrackColor = new Color(1f, 1f, 1f, 0.149f);
    [SerializeField] private Color _switchOnTextColor = new Color(0.220f, 0.741f, 0.973f, 1f);
    [SerializeField] private Color _switchOffTextColor = new Color(0.580f, 0.639f, 0.722f, 1f);

    [Header("버튼")]
    [SerializeField] private Button _resetButton;
    [SerializeField] private Button _closeButton;

    [Header("연출")]
    // 열고 닫을 때 투명도를 바꿀 그룹. 페이드 중에는 입력도 막는다.
    [SerializeField] private CanvasGroup _canvasGroup;

    // 페이드 인·아웃에 걸리는 시간(초)
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.15f;

    // 페이드 방향. 1이면 나타나는 중, -1이면 사라지는 중, 0이면 멈춰 있다.
    private int _fadeDirection;

    private void Awake()
    {
        _horizontalSlider.minValue = GameSettings.MinHorizontalLookSensitivity;
        _horizontalSlider.maxValue = GameSettings.MaxHorizontalLookSensitivity;
        _horizontalSlider.wholeNumbers = false;

        _chunkLod0Slider.minValue = GameSettings.MinChunkLoadRadius;
        _chunkLod0Slider.maxValue = GameSettings.MaxChunkLod0LoadRadius;
        _chunkLod0Slider.wholeNumbers = true;

        _chunkLod1Slider.minValue = GameSettings.MinChunkLoadRadius;
        _chunkLod1Slider.maxValue = GameSettings.MaxChunkLod1LoadRadius;
        _chunkLod1Slider.wholeNumbers = true;

        _horizontalSlider.onValueChanged.AddListener(HandleHorizontalChanged);
        _chunkLod0Slider.onValueChanged.AddListener(HandleChunkLod0Changed);
        _chunkLod1Slider.onValueChanged.AddListener(HandleChunkLod1Changed);
        _monochromeFocusButton.onClick.AddListener(HandleMonochromeFocusClick);
        _resetButton.onClick.AddListener(HandleResetClick);
        _closeButton.onClick.AddListener(HandleCloseClick);
    }

    private void Update()
    {
        HandleFade();
    }

    // 저장된 값으로 슬라이더를 맞춘 뒤 페이드 인을 시작한다.
    protected override void PlayOpenTransition()
    {
        HandleSyncFromSettings();

        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _fadeDirection = 1;
    }

    // 페이드 아웃을 시작한다. 다 사라지면 닫기를 마무리한다.
    protected override void PlayCloseTransition()
    {
        _canvasGroup.interactable = false;
        _fadeDirection = -1;
    }

    // 진행 중인 페이드를 멈춘다.
    protected override void StopTransition()
    {
        _fadeDirection = 0;
    }

    // 닫히고 나면 바뀐 설정을 디스크에 기록하고 다음 열기를 위해 투명도를 되돌린다.
    protected override void OnReturnToPool()
    {
        GameSettings.Save();

        _fadeDirection = 0;
        _canvasGroup.alpha = 1f;
        _canvasGroup.interactable = true;
    }

    // 저장된 수평 감도, 청크 범위, 흑백 강조 사용 여부를 이벤트 없이 화면에 반영한다.
    private void HandleSyncFromSettings()
    {
        float horizontal = GameSettings.HorizontalLookSensitivity;
        _horizontalSlider.SetValueWithoutNotify(horizontal);
        _horizontalValueText.text = FormatSensitivity(horizontal);

        HandleSyncChunkRadii();
        HandleSyncMonochromeFocus();
    }

    // 흑백 강조 스위치의 트랙 색, 손잡이 위치, ON/OFF 표시를 저장된 값에 맞춘다.
    private void HandleSyncMonochromeFocus()
    {
        bool isOn = GameSettings.MonochromeFocusEnabled;

        _monochromeFocusTrack.color = isOn ? _switchOnTrackColor : _switchOffTrackColor;

        Vector2 knobPosition = _monochromeFocusKnob.anchoredPosition;
        knobPosition.x = isOn ? _monochromeFocusKnobTravel : -_monochromeFocusKnobTravel;
        _monochromeFocusKnob.anchoredPosition = knobPosition;

        _monochromeFocusValueText.text = isOn ? "ON" : "OFF";
        _monochromeFocusValueText.color = isOn ? _switchOnTextColor : _switchOffTextColor;
    }

    // 흑백 강조 줄을 누르면 사용 여부를 뒤집어 저장하고 스위치 표시를 갱신한다.
    private void HandleMonochromeFocusClick()
    {
        AudioManager.PlayConfirm();

        GameSettings.MonochromeFocusEnabled = !GameSettings.MonochromeFocusEnabled;
        HandleSyncMonochromeFocus();
    }

    // 저장된 청크 LOD0·LOD1 로딩 범위를 이벤트 없이 두 슬라이더와 값 표시에 반영한다.
    private void HandleSyncChunkRadii()
    {
        int lod0 = GameSettings.ChunkLod0LoadRadius;
        int lod1 = GameSettings.ChunkLod1LoadRadius;

        _chunkLod0Slider.SetValueWithoutNotify(lod0);
        _chunkLod0ValueText.text = FormatChunkRadius(lod0);
        _chunkLod1Slider.SetValueWithoutNotify(lod1);
        _chunkLod1ValueText.text = FormatChunkRadius(lod1);
    }

    // LOD0 로딩 범위 슬라이더가 움직이면 저장하고, LOD1 로딩 범위가 밀려 올라갔을 수 있으므로 두 슬라이더를 다시 맞춘다.
    private void HandleChunkLod0Changed(float value)
    {
        GameSettings.ChunkLod0LoadRadius = Mathf.RoundToInt(value);
        HandleSyncChunkRadii();
    }

    // LOD1 로딩 범위 슬라이더가 움직이면 저장하고, LOD0 로딩 범위가 밀려 내려갔을 수 있으므로 두 슬라이더를 다시 맞춘다.
    private void HandleChunkLod1Changed(float value)
    {
        GameSettings.ChunkLod1LoadRadius = Mathf.RoundToInt(value);
        HandleSyncChunkRadii();
    }

    // 수평 감도 슬라이더가 움직이면 0.05 단위로 맞춰 저장하고 표시를 갱신한다.
    private void HandleHorizontalChanged(float value)
    {
        float snapped = SnapSensitivity(value);
        GameSettings.HorizontalLookSensitivity = snapped;
        _horizontalSlider.SetValueWithoutNotify(snapped);
        _horizontalValueText.text = FormatSensitivity(snapped);
    }

    // 수평 감도, 청크 범위, 흑백 강조 사용 여부를 기본값으로 되돌린다.
    private void HandleResetClick()
    {
        AudioManager.PlayConfirm();

        GameSettings.HorizontalLookSensitivity = GameSettings.DefaultHorizontalLookSensitivity;
        GameSettings.ChunkLod0LoadRadius = GameSettings.DefaultChunkLod0LoadRadius;
        GameSettings.ChunkLod1LoadRadius = GameSettings.DefaultChunkLod1LoadRadius;
        GameSettings.MonochromeFocusEnabled = GameSettings.DefaultMonochromeFocusEnabled;
        HandleSyncFromSettings();
    }

    // 닫기 버튼은 창을 닫기만 하므로 닫기 효과음(ManagedUIBase)만 울린다.
    private void HandleCloseClick() => Close();

    // 페이드 방향에 따라 투명도를 실제 경과 시간으로 바꾸고, 끝나면 열기·닫기를 마무리한다.
    private void HandleFade()
    {
        if (_fadeDirection == 0)
            return;

        float step = Time.unscaledDeltaTime / _fadeDuration * _fadeDirection;
        _canvasGroup.alpha = Mathf.Clamp01(_canvasGroup.alpha + step);

        if (_fadeDirection > 0 && _canvasGroup.alpha >= 1f)
        {
            _fadeDirection = 0;
            _canvasGroup.interactable = true;
            FinishOpen();
        }
        else if (_fadeDirection < 0 && _canvasGroup.alpha <= 0f)
        {
            _fadeDirection = 0;
            FinishClose();
        }
    }

    // 감도를 0.05 단위로 반올림한다.
    private static float SnapSensitivity(float value)
    {
        return Mathf.Round(value * 20f) / 20f;
    }

    // 감도를 "x1.00" 꼴의 문자열로 바꾼다.
    private static string FormatSensitivity(float value)
    {
        return $"x{value:0.00}";
    }

    // 청크 범위를 숫자 문자열로 바꾼다. 값 표시 폰트(LiberationSans)에 한글이 없어 단위는 붙이지 않는다.
    private static string FormatChunkRadius(int value)
    {
        return value.ToString();
    }
}
