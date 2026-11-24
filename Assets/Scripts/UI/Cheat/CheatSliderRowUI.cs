using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 치트 관리 목록에 놓이는 슬라이더 한 줄. 이동속도 배율처럼 범위 안에서 값을 고르는 치트에 쓴다.
// 값은 지정한 간격으로 맞춰지고, 오른쪽에 "x2.5" 형태로 현재 배율을 보여 준다.
public class CheatSliderRowUI : MonoBehaviour
{
    [Header("구성")]
    [SerializeField] private Slider _slider;

    [Header("문구")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _valueText;

    // 슬라이더 값을 맞출 간격. 0 이하면 맞추지 않는다.
    [SerializeField, Min(0f)] private float _step = 0.5f;

    // 값이 바뀌었을 때 호출할 콜백. 값을 실제로 적용하는 일은 치트 관리 UI가 한다.
    private Action<float> _onValueChanged;

    private void Awake()
    {
        _slider.onValueChanged.AddListener(HandleSliderValueChanged);
    }

    // 줄에 표시할 문구, 값 범위, 값이 바뀌었을 때 호출할 콜백을 지정한다.
    public void Setup(string title, string description, float min, float max, Action<float> onValueChanged)
    {
        _titleText.text = title;
        _descriptionText.text = description;
        _slider.minValue = min;
        _slider.maxValue = max;
        _onValueChanged = onValueChanged;
    }

    // 콜백을 부르지 않고 슬라이더와 배율 표시를 현재 값으로 맞춘다.
    public void SetValue(float value)
    {
        _slider.SetValueWithoutNotify(value);
        UpdateValueText(_slider.value);
    }

    // 슬라이더를 움직이면 간격에 맞춘 값으로 고정하고 표시를 갱신한 뒤 콜백을 호출한다.
    private void HandleSliderValueChanged(float value)
    {
        float snapped = Snap(value);
        if (!Mathf.Approximately(snapped, value))
            _slider.SetValueWithoutNotify(snapped);

        UpdateValueText(snapped);
        _onValueChanged?.Invoke(snapped);
    }

    // 값을 최솟값 기준으로 _step 간격에 맞춰 반올림한다.
    private float Snap(float value)
    {
        if (_step <= 0f) return value;

        float steps = Mathf.Round((value - _slider.minValue) / _step);
        return Mathf.Clamp(_slider.minValue + steps * _step, _slider.minValue, _slider.maxValue);
    }

    // 배율 표시 문구를 갱신한다.
    private void UpdateValueText(float value)
    {
        _valueText.text = $"x{value:0.#}";
    }
}
