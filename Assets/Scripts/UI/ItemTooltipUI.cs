using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 아이템 칸 위에 마우스를 일정 시간 올려 두면 커서 오른쪽에 아이템의 시대·이름·설명을 띄우는 툴팁.
// 누르거나 끄는 중(드래그 포함)에는 감추고, 칸의 아이템이 사라지거나 칸이 꺼지면 함께 감춘다.
public class ItemTooltipUI : MonoBehaviour
{
    [SerializeField] private RectTransform _panel;
    [SerializeField] private TextMeshProUGUI _ageText;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    // 설명이 비어 있는 아이템에 대신 표시할 문구
    [SerializeField, TextArea(2, 4)] private string _emptyDescription = "아직 기록되지 않은 유물입니다.";

    [Header("표시")]
    // 마우스를 몇 초 동안 올려 두어야 툴팁을 띄울지
    [SerializeField, Min(0f)] private float _hoverDelay = 1f;
    // 커서 위치에서 툴팁 왼쪽 위 모서리까지의 화면 픽셀 간격
    [SerializeField] private Vector2 _cursorOffset = new(20f, -8f);
    // 시대별 글자색. 목록에 없는 시대는 시대 글자의 기본색을 쓴다.
    [SerializeField] private List<AgeColorEntry> _ageColors = new()
    {
        new AgeColorEntry(Age.nature, new Color32(0xB5, 0xB5, 0xB5, 0xFF)),
        new AgeColorEntry(Age.Paleolithic, new Color32(0xE3, 0xA5, 0x6C, 0xFF)),
        new AgeColorEntry(Age.Neolithic, new Color32(0xE0, 0x7E, 0x5C, 0xFF)),
        new AgeColorEntry(Age.bronzeAge, new Color32(0x6C, 0xC7, 0xB4, 0xFF)),
    };

    private IItemHoverSource _source;
    private ItemData _shownItem;
    private float _hoverTime;
    private Color _defaultAgeColor;

    private void Awake()
    {
        _defaultAgeColor = _ageText.color;

        // 커서 좌표(부모 피벗 기준)를 그대로 쓰도록 앵커를 부모 피벗에, 피벗을 왼쪽 위 모서리에 맞춘다.
        if (_panel.parent is RectTransform parent)
        {
            _panel.anchorMin = parent.pivot;
            _panel.anchorMax = parent.pivot;
        }
        _panel.pivot = new Vector2(0f, 1f);

        SetVisible(false);
    }

    private void OnDisable() => Hide();

    private void Update()
    {
        if (_source == null) return;

        HandleHoverTimer();
        if (_shownItem != null) HandleFollowCursor();
    }

    // 마우스가 아이템 칸에 들어왔을 때 호출해 대기 시간을 새로 센다.
    public void BeginHover(IItemHoverSource source)
    {
        _source = source;
        _hoverTime = 0f;
        HideContent();
    }

    // 마우스가 아이템 칸을 벗어났을 때 호출한다. 다른 칸으로 이미 옮겨 갔다면 무시한다.
    public void EndHover(IItemHoverSource source)
    {
        if (!ReferenceEquals(_source, source)) return;
        Hide();
    }

    // 추적 중인 칸을 잊고 툴팁을 감춘다.
    public void Hide()
    {
        _source = null;
        _hoverTime = 0f;
        HideContent();
    }

    // 대기 시간을 누적해 다 차면 띄우고, 눌림·빈 칸·꺼진 칸이면 다시 처음부터 기다린다.
    private void HandleHoverTimer()
    {
        ItemData item = GetHoveredItem();
        bool isPressed = Mouse.current != null && Mouse.current.leftButton.isPressed;

        if (item == null || isPressed)
        {
            _hoverTime = 0f;
            HideContent();
            return;
        }

        // 칸의 아이템이 바뀌면(드롭으로 교체 등) 새 아이템 기준으로 다시 기다린다.
        if (_shownItem != null && _shownItem != item)
        {
            _hoverTime = 0f;
            HideContent();
        }

        if (_shownItem != null) return;

        _hoverTime += Time.unscaledDeltaTime;
        if (_hoverTime >= _hoverDelay) ShowContent(item);
    }

    // 추적 중인 칸이 살아 있고 켜져 있으면 그 칸의 아이템을, 아니면 null을 돌려준다.
    private ItemData GetHoveredItem()
    {
        if (_source is Behaviour behaviour && (behaviour == null || !behaviour.isActiveAndEnabled)) return null;
        return _source.Item;
    }

    // 아이템의 시대·이름·설명을 채우고 툴팁을 커서 옆에 띄운다.
    private void ShowContent(ItemData item)
    {
        _shownItem = item;

        _ageText.text = item.Age.ToShortName();
        _ageText.color = GetAgeColor(item.Age);
        _nameText.text = item.Nmae;
        _descriptionText.text = string.IsNullOrWhiteSpace(item.Description) ? _emptyDescription : item.Description;

        SetVisible(true);
        _panel.SetAsLastSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
        HandleFollowCursor();
    }

    // 표시 중인 내용을 지우고 패널을 감춘다(추적 중인 칸은 유지).
    private void HideContent()
    {
        _shownItem = null;
        SetVisible(false);
    }

    // 툴팁 패널을 켜거나 끈다.
    private void SetVisible(bool visible)
    {
        if (_panel != null && _panel.gameObject.activeSelf != visible) _panel.gameObject.SetActive(visible);
    }

    // 툴팁을 커서 오른쪽에 붙이고, 부모 영역 밖으로 나가면 안쪽으로 밀어 넣는다.
    private void HandleFollowCursor()
    {
        if (Mouse.current == null) return;

        var parent = _panel.parent as RectTransform;
        Canvas canvas = _panel.GetComponentInParent<Canvas>();
        if (parent == null || canvas == null) return;

        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        float scale = canvas.rootCanvas.scaleFactor;
        Vector2 screenPoint = Mouse.current.position.ReadValue() + _cursorOffset * scale;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, camera, out Vector2 localPoint)) return;

        _panel.anchoredPosition = ClampInsideParent(parent, localPoint);
    }

    // 왼쪽 위 모서리 기준 위치를 부모 영역 안에 툴팁이 모두 들어오도록 보정한다.
    private Vector2 ClampInsideParent(RectTransform parent, Vector2 localPoint)
    {
        Rect bounds = parent.rect;
        Vector2 size = _panel.rect.size;

        // 앵커를 부모 피벗에 맞춰 두었으므로 로컬 좌표가 곧 앵커 기준 위치다.
        float x = Mathf.Clamp(localPoint.x, bounds.xMin, bounds.xMax - size.x);
        float y = Mathf.Clamp(localPoint.y, bounds.yMin + size.y, bounds.yMax);
        return new Vector2(x, y);
    }

    // 시대에 맞는 글자색을 찾는다. 등록되지 않은 시대는 기본색을 쓴다.
    private Color GetAgeColor(Age age)
    {
        foreach (var entry in _ageColors)
        {
            if (entry.Age == age) return entry.Color;
        }
        return _defaultAgeColor;
    }

    // 시대 하나와 그 시대 글자색의 짝
    [Serializable]
    private struct AgeColorEntry
    {
        public Age Age;
        public Color Color;

        // 시대와 글자색으로 항목을 만든다.
        public AgeColorEntry(Age age, Color color)
        {
            Age = age;
            Color = color;
        }
    }
}
