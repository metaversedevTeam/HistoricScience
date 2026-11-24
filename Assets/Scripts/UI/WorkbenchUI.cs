using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 작업대 조합 UI — 인벤토리와 작업대 위치를 페이로드로 받아 열리는 관리형 UI (보유 아이템 표시, 5x5 격자 배치 조합, 도감 열기).
// 열려 있는 동안 카메라를 연구소 위로 올려 내려다보게 하고, 일시정지 화면과 같은 등장·퇴장 연출을 쓴다.
public class WorkbenchUI : OpenableUIBase<WorkbenchData>
{
    [Header("보유 아이템")]
    [SerializeField] private ItemSlotUI _slotPrefab;
    [SerializeField] private RectTransform _slotsContent;
    // 보유 종류가 적어도 빈 칸을 그려 보유 격자를 채우는 최소 칸 수 (디자인 기준 5열 x 6행)
    [SerializeField, Min(0)] private int _minSlotCount = 30;
    // 보유 중인 아이템 종류 수를 보여 줄 글자 (비워 두면 표시하지 않는다)
    [SerializeField] private TextMeshProUGUI _ownedKindsText;

    [Header("조합 격자")]
    [SerializeField] private RectTransform _craftingGrid;
    [SerializeField] private Image _resultIcon;
    // 결과 아이템 이름. 일치하는 조합법이 없으면 안내 문구를 보여 준다.
    [SerializeField] private TextMeshProUGUI _resultNameText;
    [SerializeField] private Button _craftButton;
    [SerializeField] private TextMeshProUGUI _warningText;

    [Header("도감")]
    [SerializeField] private Button _codexButton;
    // 도감 버튼으로 작업대 위에 띄울 도감 UI
    [SerializeField] private ItemCodexUI _codexPrefab;
    // 처음 획득한 아이템을 조합했을 때 띄울 아이템 정보 팝업 (도감과 같은 프리팹)
    [SerializeField] private ItemDetailPopupUI _detailPopupPrefab;

    [Header("툴팁")]
    // 보유 칸·조합 칸에 마우스를 올려 두면 아이템 시대·이름·설명을 띄울 툴팁 (비워 두면 띄우지 않는다)
    [SerializeField] private ItemTooltipUI _tooltip;

    [Header("헤더")]
    [SerializeField] private Button _closeButton;

    [Header("카메라")]
    // 열려 있는 동안 카메라를 연구소 바로 위 몇 m 높이로 올려 내려다보게 할지
    [SerializeField, Min(1f)] private float _viewHeight = 10f;

    [Header("연출")]
    // 열릴 때 요소들을 차례로 들여오고, 닫힐 때 거꾸로 내보내는 연출
    [SerializeField] private MainMenuIntroSequence _introSequence;

    private ResourceInventory _inventory;

    // 조합 결과를 획득한 위치로 넘길 작업대의 월드 좌표
    private Vector3 _workbenchWorldPosition;

    // 열려 있는 동안 연구소 위로 옮겨 둔 3인칭 카메라
    private ThirdPersonCamera _camera;

    // 이 작업대가 띄운 도감. 닫히기 전까지 다시 열지 않는다.
    private ItemCodexUI _openCodexUI;

    // 첫 조합 아이템을 알리려고 이 작업대가 띄운 정보 팝업. 닫히기 전까지 새로 띄우지 않는다.
    private ItemDetailPopupUI _openDetailPopup;

    // 첫 획득 여부와 도감 번호를 조회할 씬의 도감
    private ItemCodex _itemCodex;

    private CraftingSlotUI[] _craftingSlots;
    private readonly List<ItemSlotUI> _slots = new();
    private Coroutine _warningCoroutine;

    private void Awake()
    {
        InitializeCraftingSlots();
        _craftButton.onClick.AddListener(OnCraftButtonClick);
        _closeButton.onClick.AddListener(OnCloseButtonClick);
        _codexButton.onClick.AddListener(OnCodexButtonClick);
        _warningText.gameObject.SetActive(false);

        RefreshResultPreview();
    }

    // 조합 격자 하위의 모든 CraftingSlotUI를 수집해 배치 규칙과 변경 알림을 연결한다(격자 크기·모양 무관).
    private void InitializeCraftingSlots()
    {
        _craftingSlots = _craftingGrid.GetComponentsInChildren<CraftingSlotUI>(true);

        foreach (var slot in _craftingSlots)
        {
            // 프리팹에는 아이콘 이미지가 켜진 채 저장돼 있어 그대로 두면 빈 칸이 흰 사각형으로 보인다.
            slot.Clear();
            slot.SetPlacementRule(CanPlaceInCraftingGrid);
            slot.OnItemChanged += HandleCraftingGridChanged;
            slot.OnHoverEnter += HandleSlotHoverEnter;
            slot.OnHoverExit += HandleSlotHoverExit;
        }
    }

    // 마우스가 아이템 칸에 들어오면 툴팁 대기 시간을 센다.
    private void HandleSlotHoverEnter(IItemHoverSource source)
    {
        if (_tooltip != null) _tooltip.BeginHover(source);
    }

    // 마우스가 아이템 칸을 벗어나면 툴팁을 감춘다.
    private void HandleSlotHoverExit(IItemHoverSource source)
    {
        if (_tooltip != null) _tooltip.EndHover(source);
    }

    // 격자 배치가 바뀌면 결과 미리보기와 보유 목록에 남아 보이는 수량을 함께 갱신한다.
    private void HandleCraftingGridChanged()
    {
        RefreshResultPreview();

        // 열리기 전이나 풀로 돌아가는 중에는 채울 인벤토리가 없다.
        if (_inventory != null) PopulateSlots();
    }

    // 격자에 재료를 한 개 더 올릴 여유가 있는지 판정한다.
    private bool CanPlaceInCraftingGrid(ItemData item)
    {
        if (item == null || _inventory == null) return false;
        if (IsInfiniteResources()) return true;
        return GetRemainingCount(item) > 0;
    }

    // 치트 모드로 자원이 무한한 상태인지 확인한다. 이때는 재료가 실제로 차감되지 않으므로 보유량 제한을 두지 않는다.
    private bool IsInfiniteResources() => _inventory != null && _inventory.IsCheatModeEnabled;

    // 보유 목록에 남아 보이는 수량 — 보유량에서 조합 격자에 이미 올려둔 몫을 뺀 값.
    private int GetRemainingCount(ItemData item)
    {
        int placed = 0;
        foreach (var slot in _craftingSlots)
        {
            if (slot.Item == item) placed++;
        }

        return _inventory.Get(item) - placed;
    }

    // 주입받은 인벤토리를 구독하고 보유 격자를 구성한 뒤 카메라를 연구소 위로 옮긴다. 작업대 위치는 조합 결과의 획득 위치로도 쓴다.
    protected override void ApplyData(WorkbenchData data)
    {
        _inventory = data.Inventory;
        _workbenchWorldPosition = data.WorldPosition;
        _inventory.OnAddItem += HandleInventoryChanged;
        _inventory.OnRemoveItem += HandleInventoryChanged;
        _itemCodex = FindFirstObjectByType<ItemCodex>();
        PopulateSlots();
        RefreshResultPreview();
        HandleMoveCameraOverLab();
    }

    // 닫히기 시작하면 카메라를 먼저 돌려보내고, 등장 연출을 거꾸로 재생해 요소들을 내보낸 뒤 닫기를 마무리한다.
    protected override void PlayCloseTransition()
    {
        HandleReleaseCamera();
        if (_tooltip != null) _tooltip.Hide();

        if (_introSequence == null || !_introSequence.isActiveAndEnabled)
        {
            FinishClose();
            return;
        }

        _introSequence.PlayReverse(FinishClose);
    }

    // 인벤토리·도감 구독을 해제하고 카메라와 격자·경고 표시를 재사용 가능한 상태로 정리한다.
    protected override void OnReturnToPool()
    {
        HandleReleaseCamera();

        if (_openCodexUI != null)
        {
            _openCodexUI.OnFinishClose -= HandleCodexUIClosed;
            _openCodexUI = null;
        }

        if (_openDetailPopup != null)
        {
            _openDetailPopup.OnFinishClose -= HandleDetailPopupClosed;
            _openDetailPopup = null;
        }

        _itemCodex = null;

        if (_inventory != null)
        {
            _inventory.OnAddItem -= HandleInventoryChanged;
            _inventory.OnRemoveItem -= HandleInventoryChanged;
            _inventory = null;
        }

        foreach (var slot in _craftingSlots)
            slot.Clear();

        _warningText.gameObject.SetActive(false);
    }

    // 카메라를 작업대(연구소) 바로 위로 옮겨 아래를 내려다보게 한다. 화면 위쪽이 플레이어가 보던 방향이 되도록 방위각은 유지한다.
    private void HandleMoveCameraOverLab()
    {
        _camera = FindThirdPersonCamera();
        if (_camera == null) return;

        Vector3 position = _workbenchWorldPosition + Vector3.up * _viewHeight;
        Quaternion rotation = Quaternion.Euler(90f, _camera.Yaw, 0f);
        _camera.SetViewOverride(position, rotation);
    }

    // 주 카메라에 붙은 3인칭 카메라를 찾고, 없으면 씬에서 찾는다.
    private ThirdPersonCamera FindThirdPersonCamera()
    {
        Camera main = Camera.main;
        if (main != null && main.TryGetComponent(out ThirdPersonCamera onMain)) return onMain;
        return FindFirstObjectByType<ThirdPersonCamera>();
    }

    // 옮겨 둔 카메라를 3인칭 시점으로 돌려보낸다.
    private void HandleReleaseCamera()
    {
        if (_camera == null) return;

        _camera.ClearViewOverride();
        _camera = null;
    }

    // 인벤토리 변경 시 보유 격자를 갱신한다.
    private void HandleInventoryChanged(ItemData item, int newCount) => PopulateSlots();

    // 닫기 버튼 클릭 시 UI를 닫는다.
    private void OnCloseButtonClick() => Close();

    // 도감 버튼 클릭 시 작업대 위에 도감을 연다. 이미 이 작업대가 연 도감이 떠 있으면 다시 열지 않는다.
    private void OnCodexButtonClick()
    {
        if (_openCodexUI != null || _inventory == null || State != UIState.Open) return;

        if (_codexPrefab == null)
        {
            Debug.LogWarning($"WorkbenchUI({name}): 도감 UI 프리팹이 설정되지 않아 도감을 열 수 없습니다.", this);
            return;
        }

        _openCodexUI = UIManager.Instance.OpenUI(_codexPrefab, _inventory);
        _openCodexUI.OnFinishClose += HandleCodexUIClosed;
    }

    // 도감이 닫히면 구독을 해제해 다음에 다시 열 수 있게 한다.
    private void HandleCodexUIClosed(IManagedUI ui)
    {
        ui.OnFinishClose -= HandleCodexUIClosed;
        if (ReferenceEquals(ui, _openCodexUI)) _openCodexUI = null;
    }

    // 보유 중인 자원으로 격자를 채우고, 남는 칸은 빈 칸으로 그린다.
    // 조합 격자에 올려둔 재료는 아직 소비되지 않았지만 보유 목록에서는 빠진 것처럼 보여 준다.
    // 치트 모드에서는 차감되지 않는 실제 보유량을 표시한다.
    private void PopulateSlots()
    {
        bool isInfinite = IsInfiniteResources();
        int filledCount = 0;
        foreach (var item in _inventory.ItemDataList.Items)
        {
            if (!ShouldShowInOwnedList(item, isInfinite)) continue;

            var count = isInfinite ? _inventory.Get(item) : GetRemainingCount(item);
            GetOrCreateSlot(filledCount).Setup(item, count);
            filledCount++;
        }

        int slotCount = Mathf.Max(_minSlotCount, filledCount);
        for (int i = filledCount; i < slotCount; i++)
            GetOrCreateSlot(i).SetupEmpty();

        // 보유 종류가 줄어 남는 슬롯은 격자에서 감춘다.
        for (int i = slotCount; i < _slots.Count; i++)
            _slots[i].gameObject.SetActive(false);

        if (_ownedKindsText != null) _ownedKindsText.text = $"{filledCount}종";
    }

    // 보유 목록에 보여 줄 아이템인지 판정한다.
    // 일반 모드: 남은 수량이 있는 아이템은 시대와 무관하게 전부. 치트 모드: 보유한 아이템은 전부, 보유하지 않았어도 nature 자원은 전부.
    private bool ShouldShowInOwnedList(ItemData item, bool isInfinite)
    {
        if (isInfinite) return item.Age == Age.nature || _inventory.GetStored(item) > 0;
        return GetRemainingCount(item) > 0;
    }

    // index번째 슬롯을 켜서 반환하고, 아직 없으면 새로 만들어 재사용 목록에 넣는다.
    private ItemSlotUI GetOrCreateSlot(int index)
    {
        while (_slots.Count <= index)
        {
            var slot = Instantiate(_slotPrefab, _slotsContent);
            slot.OnHoverEnter += HandleSlotHoverEnter;
            slot.OnHoverExit += HandleSlotHoverExit;
            _slots.Add(slot);
        }

        _slots[index].gameObject.SetActive(true);
        return _slots[index];
    }

    // 격자 배치와 일치하는 조합법을 찾아 재료를 소비하고 결과 아이템을 지급한다.
    private void OnCraftButtonClick()
    {
        var placed = BuildPlacedPattern();
        var result = FindMatchingRecipe(placed);
        if (result == null)
        {
            ShowWarning("조합법이 없습니다.");
            return;
        }

        var needed = placed.CountItems();
        foreach (var (resource, count) in needed)
        {
            if (!_inventory.Has(resource, count))
            {
                ShowWarning("재료가 부족합니다.");
                return;
            }
        }

        foreach (var (resource, count) in needed)
            _inventory.Remove(resource, count);

        // 획득하는 순간 도감에 등록되므로, 처음 얻는 아이템인지는 지급하기 전에 확인해 둔다.
        bool isFirstAcquire = _itemCodex != null && !_itemCodex.IsDiscovered(result);

        _inventory.Add(result, 1, _workbenchWorldPosition);
        AudioManager.PlayConfirm();

        foreach (var slot in _craftingSlots)
            slot.Clear();

        if (isFirstAcquire) OpenDetailPopup(result);
    }

    // 처음 조합한 아이템의 정보 팝업을 작업대 위에 띄운다. 도감에서 감춘 아이템이거나 이미 떠 있는 팝업이 있으면 띄우지 않는다.
    private void OpenDetailPopup(ItemData item)
    {
        if (_openDetailPopup != null || State != UIState.Open) return;

        int codexNumber = _itemCodex.GetCodexNumber(item);
        if (codexNumber <= 0) return;

        if (_detailPopupPrefab == null)
        {
            Debug.LogWarning($"WorkbenchUI({name}): 아이템 정보 팝업 프리팹이 설정되지 않아 첫 획득 정보를 띄울 수 없습니다.", this);
            return;
        }

        _openDetailPopup = UIManager.Instance.OpenUI(_detailPopupPrefab, new ItemDetailData(item, codexNumber, isNew: true));
        _openDetailPopup.OnFinishClose += HandleDetailPopupClosed;
    }

    // 정보 팝업이 닫히면 구독을 해제해 다음 첫 조합 때 다시 띄울 수 있게 한다.
    private void HandleDetailPopupClosed(IManagedUI ui)
    {
        ui.OnFinishClose -= HandleDetailPopupClosed;
        if (ReferenceEquals(ui, _openDetailPopup)) _openDetailPopup = null;
    }

    // 격자의 점유 슬롯에서 현재 배치 패턴을 만든다.
    private CraftingPattern BuildPlacedPattern()
    {
        var cells = new List<(Vector2Int coord, ResourceData item)>();
        foreach (var slot in _craftingSlots)
        {
            if (slot.Item != null)
                cells.Add((slot.Coord, slot.Item));
        }
        return CraftingPattern.FromCells(cells);
    }

    // 현재 배치와 (위치까지) 일치하는 조합법의 결과 아이템을 반환한다. 없으면 null.
    private ItemData FindMatchingRecipe(CraftingPattern placed)
    {
        if (placed.IsEmpty) return null;
        // 열기 전에도 격자 변경 이벤트가 올 수 있어 인벤토리가 없으면 판정을 건너뛴다.
        if (_inventory == null) return null;

        foreach (var item in _inventory.ItemDataList.Items)
        {
            if (item.HasRecipe && placed.Matches(item.ToPattern()))
                return item;
        }
        return null;
    }

    // 현재 배치로 만들어질 아이템을 결과 칸에 미리 보여준다. 맞는 조합법이 없으면 빈 칸과 안내 문구를 둔다.
    private void RefreshResultPreview()
    {
        ItemData result = FindMatchingRecipe(BuildPlacedPattern());
        bool hasResult = result != null;

        _resultIcon.gameObject.SetActive(hasResult);
        if (_resultNameText != null) _resultNameText.text = hasResult ? result.Nmae : "일치하는 조합법 없음";
        if (!hasResult) return;

        _resultIcon.sprite = result.IconSprite;
        _resultIcon.color = result.IconSprite != null ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
    }

    // 경고 메시지를 표시하고 2초 후 자동으로 숨긴다.
    private void ShowWarning(string message)
    {
        AudioManager.PlayError();

        _warningText.text = message;
        _warningText.gameObject.SetActive(true);

        if (_warningCoroutine != null) StopCoroutine(_warningCoroutine);
        _warningCoroutine = StartCoroutine(HideWarningAfterDelay(2f));
    }

    // 지정한 시간 뒤에 경고 텍스트를 숨기는 코루틴.
    private IEnumerator HideWarningAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _warningText.gameObject.SetActive(false);
    }
}

// 작업대 UI에 전달되는 페이로드 — 재료·결과를 주고받을 인벤토리와, 조합 결과의 획득 위치로 쓸 작업대의 월드 좌표
public readonly struct WorkbenchData
{
    public readonly ResourceInventory Inventory;
    public readonly Vector3 WorldPosition;

    // 인벤토리와 작업대 월드 좌표로 페이로드를 구성한다.
    public WorkbenchData(ResourceInventory inventory, Vector3 worldPosition)
    {
        Inventory = inventory;
        WorldPosition = worldPosition;
    }
}
