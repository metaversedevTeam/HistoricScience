using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 연구 하나의 상세 패널 — 썸네일, 연구 효과, 필요 자원을 보여주고 연구 버튼을 제공한다.
// 시대 제한이나 선행 연구에 걸린 연구는 내용만 보여주고 연구 버튼이 잠긴다.
public class ResearchDetailUI : OpenableUIBase<ResearchDetailData>
{
    [Header("헤더")]
    // "구석기 시대 연구" 형태로 시대 제한을 보여주는 줄
    [SerializeField] private TextMeshProUGUI _ageText;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private Button _closeButton;

    [Header("썸네일")]
    [SerializeField] private Image _thumbnail;

    [Header("연구 효과")]
    [SerializeField] private TextMeshProUGUI _effectsText;
    [SerializeField] private TextMeshProUGUI _descriptionText;

    [Header("필요 자원")]
    [SerializeField] private TextMeshProUGUI _costText;

    [Header("연구 버튼")]
    [SerializeField] private Button _actionButton;
    [SerializeField] private Image _actionFill;
    [SerializeField] private Image _actionOutline;
    [SerializeField] private Image _actionIcon;
    [SerializeField] private TextMeshProUGUI _actionText;

    [Header("버튼 아이콘")]
    [SerializeField] private Sprite _boltIcon;
    [SerializeField] private Sprite _checkIcon;
    [SerializeField] private Sprite _lockIcon;

    [Header("버튼 색상")]
    [SerializeField] private Color _readyFill = new Color32(0xF9, 0x73, 0x16, 0xFF);
    [SerializeField] private Color _readyOutline = new Color32(0x11, 0x18, 0x27, 0xFF);
    [SerializeField] private Color _readyText = new Color32(0x11, 0x18, 0x27, 0xFF);
    [SerializeField] private Color _disabledFill = new Color32(0x0A, 0x0F, 0x1C, 0xFF);
    [SerializeField] private Color _disabledOutline = new Color32(0x2A, 0x33, 0x45, 0xFF);
    [SerializeField] private Color _disabledText = new Color32(0x7B, 0x84, 0x96, 0xFF);
    [SerializeField] private Color _completedFill = new Color32(0x07, 0x1C, 0x14, 0xFF);
    [SerializeField] private Color _completedOutline = new Color32(0x16, 0xA3, 0x4A, 0xFF);
    [SerializeField] private Color _completedText = new Color32(0x16, 0xA3, 0x4A, 0xFF);

    [Header("자원 문구 색상")]
    // 자원이 충분할 때 붙는 표시의 색 (16진 RGB)
    [SerializeField] private string _enoughColorHex = "#22C55E";
    // 자원이 모자랄 때 붙는 표시의 색 (16진 RGB)
    [SerializeField] private string _lackingColorHex = "#EF4444";

    // 이번 열림 동안 표시 중인 연구
    private ResearchData _research;

    // 비용을 치를 인벤토리
    private ResourceInventory _inventory;

    // 상태를 읽어 오는 관리자
    private ResearchManager _manager;

    // 비용 문구를 매번 새로 만들지 않도록 재사용하는 버퍼
    private readonly StringBuilder _textBuilder = new();

    private void Awake()
    {
        _closeButton.onClick.AddListener(HandleCloseButtonClick);
        _actionButton.onClick.AddListener(HandleActionButtonClick);
    }

    private void OnEnable()
    {
        _manager = ResearchManager.Instance;
        _manager.OnCompleted += HandleResearchChanged;
        _manager.OnAgeChanged += HandleAgeChanged;
    }

    private void OnDisable()
    {
        if (_manager == null) return;

        _manager.OnCompleted -= HandleResearchChanged;
        _manager.OnAgeChanged -= HandleAgeChanged;
        _manager = null;
    }

    private void Update()
    {
        HandleSpaceShortcut();
    }

    // Space 키로도 연구할 수 있게 한다. 버튼이 잠겨 있으면 아무 일도 하지 않는다.
    private void HandleSpaceShortcut()
    {
        if (State != UIState.Open) return;
        if (Keyboard.current == null || !Keyboard.current.spaceKey.wasPressedThisFrame) return;
        if (!_actionButton.interactable) return;

        HandleActionButtonClick();
    }

    // 표시할 연구와 비용을 치를 인벤토리를 주입받고 패널 내용을 채운다.
    protected override void ApplyData(ResearchDetailData data)
    {
        _research = data.Research;
        _inventory = data.Inventory;

        ApplyStaticContent();
        RefreshDynamicContent();
    }

    // 풀로 돌아가기 전에 이번 열림 동안 들고 있던 참조를 비운다.
    protected override void OnReturnToPool()
    {
        _research = null;
        _inventory = null;
    }

    // 연구가 바뀌지 않는 한 그대로인 내용(제목, 썸네일, 효과, 설명)을 채운다.
    private void ApplyStaticContent()
    {
        if (_research == null) return;

        _ageText.text = $"{_research.RequiredAge.ToTabName()} 연구";
        _nameText.text = _research.ResearchName;

        bool hasThumbnail = _research.Thumbnail != null;
        _thumbnail.gameObject.SetActive(hasThumbnail);
        _thumbnail.sprite = _research.Thumbnail;

        _effectsText.text = BuildEffectsText();
        _descriptionText.text = _research.Description;
    }

    // 보유량과 연구 상태에 따라 달라지는 내용(필요 자원 문구, 연구 버튼)을 다시 그린다.
    private void RefreshDynamicContent()
    {
        if (_research == null) return;

        _costText.text = BuildCostText();
        ApplyActionButton();
    }

    // 이 연구가 주는 보너스를 한 줄에 하나씩 담은 여러 줄 문자열로 만든다.
    // 글머리 기호는 쓰지 않는다. 본문 폰트에 점 문자의 글리프가 없어 빈 자리만 남기 때문이다.
    private string BuildEffectsText()
    {
        _textBuilder.Clear();

        foreach (ResearchBonusEntry entry in _research.Bonuses)
        {
            if (entry.Bonus == null) continue;

            if (_textBuilder.Length > 0) _textBuilder.Append('\n');
            _textBuilder.Append(entry.Bonus.Format(entry.Value));
        }

        return _textBuilder.ToString();
    }

    // 필요 자원 목록을 "이름  수량  충족 여부" 형태의 여러 줄 문자열로 만든다.
    private string BuildCostText()
    {
        _textBuilder.Clear();

        foreach (ResearchCostEntry cost in _research.Costs)
        {
            if (cost.Resource == null) continue;

            bool enough = _inventory != null && _inventory.Has(cost.Resource, cost.Count);
            int owned = _inventory != null ? _inventory.Get(cost.Resource) : 0;
            string mark = enough
                ? $"<color={_enoughColorHex}>충족</color>"
                : $"<color={_lackingColorHex}>부족 ({owned}/{cost.Count})</color>";

            if (_textBuilder.Length > 0) _textBuilder.Append('\n');
            _textBuilder.Append($"{cost.Resource.Nmae}  {cost.Count}  {mark}");
        }

        if (_textBuilder.Length == 0)
            _textBuilder.Append("필요한 자원이 없습니다.");

        return _textBuilder.ToString();
    }

    // 연구 상태와 보유 자원에 맞춰 연구 버튼의 색·아이콘·문구·활성 여부를 정한다.
    private void ApplyActionButton()
    {
        ResearchState state = _manager != null ? _manager.GetState(_research) : ResearchState.AgeLocked;

        switch (state)
        {
            case ResearchState.Completed:
                ApplyAction(_completedFill, _completedOutline, _completedText, _checkIcon, "연구 완료", false);
                return;

            case ResearchState.AgeLocked:
                ApplyAction(_disabledFill, _disabledOutline, _disabledText, _lockIcon,
                    $"{_research.RequiredAge.ToTabName()}부터 연구 가능", false);
                return;

            case ResearchState.PrerequisiteLocked:
                ApplyAction(_disabledFill, _disabledOutline, _disabledText, _lockIcon, "선행 연구 필요", false);
                return;
        }

        bool affordable = _manager != null && _manager.CanAfford(_research, _inventory);
        if (!affordable)
        {
            ApplyAction(_disabledFill, _disabledOutline, _disabledText, _lockIcon, "자원 부족", false);
            return;
        }

        ApplyAction(_readyFill, _readyOutline, _readyText, _boltIcon, "연구 시작 (Space)", true);
    }

    // 연구 버튼의 배경·테두리·아이콘·문구·활성 여부를 한 번에 반영한다.
    private void ApplyAction(Color fill, Color outline, Color text, Sprite icon, string label, bool interactable)
    {
        _actionFill.color = fill;
        _actionOutline.color = outline;
        _actionText.color = text;
        _actionText.text = label;

        _actionIcon.sprite = icon;
        _actionIcon.color = text;
        _actionIcon.gameObject.SetActive(icon != null);

        _actionButton.interactable = interactable;
    }

    // 닫기 버튼을 누르면 UI를 닫는다. (ESC로 닫는 UIManager 경로와 동일한 동작)
    private void HandleCloseButtonClick() => Close();

    // 연구 버튼을 누르면 연구를 완료하고 버튼 상태를 다시 그린다.
    private void HandleActionButtonClick()
    {
        if (_manager == null || _research == null) return;

        _manager.TryResearch(_research, _inventory);
        RefreshDynamicContent();
    }

    // 연구가 끝나면 버튼과 자원 문구를 다시 그린다.
    private void HandleResearchChanged(ResearchData _) => RefreshDynamicContent();

    // 시대가 바뀌면 시대 제한 판정이 달라지므로 버튼을 다시 그린다.
    private void HandleAgeChanged(Age _) => RefreshDynamicContent();
}

// 연구 상세 패널을 열 때 넘기는 페이로드 (표시할 연구, 비용을 치를 인벤토리)
public readonly struct ResearchDetailData
{
    public readonly ResearchData Research;
    public readonly ResourceInventory Inventory;

    public ResearchDetailData(ResearchData research, ResourceInventory inventory)
    {
        Research = research;
        Inventory = inventory;
    }
}
