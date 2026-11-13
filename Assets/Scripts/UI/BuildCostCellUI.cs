using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 건물 선택 UI 상세 패널에서 필요 자원 하나를 사각형 칸으로 보여 주는 UI. 자원 아이콘·이름·보유/필요 수량을 적고,
// 칸 아래 막대가 충족 비율만큼 차오른다. 보유량이 모자라면 수량·막대·테두리를 경고 색으로 바꾼다.
public class BuildCostCellUI : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _countText;

    // 충족 비율만큼 가로로 차오를 막대(피벗이 왼쪽이어야 한다)
    [SerializeField] private Image _bar;

    // 부족할 때 경고 색으로 바뀔 칸 테두리
    [SerializeField] private Graphic _border;

    [Header("색")]
    [SerializeField] private Color _sufficientColor = new Color32(0xEB, 0xF0, 0xFF, 0xFF);
    [SerializeField] private Color _insufficientColor = new Color32(0xFF, 0x5A, 0x4D, 0xFF);
    [SerializeField] private Color _sufficientBarColor = new Color32(0x4A, 0xDE, 0x80, 0xFF);
    [SerializeField] private Color _normalBorderColor = new Color(1f, 1f, 1f, 0.12f);
    [SerializeField] private Color _insufficientBorderColor = new Color32(0xFF, 0x5A, 0x4D, 0x80);

    // 막대가 목표 길이까지 차오르는 데 걸리는 시간(초)
    [SerializeField, Min(0.01f)] private float _barDuration = 0.6f;

    // 막대가 다 찼을 때의 비율(0~1)
    private float _barTarget;

    // 막대 연출이 시작된 뒤 지난 시간(초)
    private float _barElapsed;

    private void Update()
    {
        HandleBarGrow();
    }

    // 자원과 보유/필요 수량을 칸에 반영한다. animateBar가 true면 막대 연출을 처음부터 시작하고, false면 막대를 바로 목표 길이로 맞춘다.
    public void Setup(ResourceData resource, int needed, int owned, bool animateBar = true)
    {
        bool sufficient = owned >= needed;

        _icon.sprite = resource != null ? resource.IconSprite : null;
        _icon.color = _icon.sprite != null ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.6f);
        _nameText.text = resource != null ? resource.Nmae : "";

        _countText.text = $"{owned}<size=60%><color=#94A3B8> / {needed}</color></size>";
        _countText.color = sufficient ? _sufficientColor : _insufficientColor;

        _bar.color = sufficient ? _sufficientBarColor : _insufficientColor;
        _border.color = sufficient ? _normalBorderColor : _insufficientBorderColor;

        _barTarget = needed > 0 ? Mathf.Clamp01((float)owned / needed) : 1f;
        _barElapsed = animateBar ? 0f : _barDuration;
        HandleApplyBar(animateBar ? 0f : 1f);
    }

    // 막대를 목표 길이까지 감속하며 늘린다.
    private void HandleBarGrow()
    {
        if (_barElapsed >= _barDuration)
            return;

        _barElapsed += Time.unscaledDeltaTime;
        HandleApplyBar(Mathf.Clamp01(_barElapsed / _barDuration));
    }

    // 막대 연출 진행도(0~1)를 막대 길이에 반영한다.
    private void HandleApplyBar(float t)
    {
        float eased = 1f - Mathf.Pow(1f - t, 4f);
        _bar.rectTransform.localScale = new Vector3(_barTarget * eased, 1f, 1f);
    }
}
