using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 맵 선택 화면. 저장된 맵을 최신순 사각형 카드 목록으로 보여 주고, 선택한 맵 시작·새 맵 만들기·삭제·뒤로가기를 처리한다.
// 맵이 정해지면 인게임 씬으로 넘어간다. 화면 등장 연출은 같은 오브젝트의 MainMenuIntroSequence가 맡는다.
public class MapSelectUI : MonoBehaviour
{
    // 맵 파일 저장/읽기를 담당하는 유틸리티
    [SerializeField] private MapSaveUtil _mapSaveUtil;

    [Header("목록")]
    // 맵 하나를 보여 줄 카드 프리팹
    [SerializeField] private MapSlotCardUI _cardPrefab;

    // 카드가 놓일 부모(세로 레이아웃)
    [SerializeField] private RectTransform _cardParent;

    // 목록을 맨 위로 되돌릴 스크롤 영역
    [SerializeField] private ScrollRect _scrollRect;

    // 저장된 맵이 하나도 없을 때만 켜지는 안내
    [SerializeField] private GameObject _emptyNotice;

    // 저장된 맵 개수 글자
    [SerializeField] private TMP_Text _countText;

    // 화면이 켜진 뒤 첫 카드가 등장하기까지의 지연(초)
    [SerializeField, Min(0f)] private float _cardIntroDelay = 0.45f;

    // 카드끼리 등장 시점의 간격(초)
    [SerializeField, Min(0f)] private float _cardIntroStagger = 0.05f;

    [Header("버튼")]
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _createButton;
    [SerializeField] private Button _deleteButton;
    [SerializeField] private Button _backButton;

    // 삭제 버튼 글자. 한 번 누르면 확인 문구로 바뀐다.
    [SerializeField] private TMP_Text _deleteLabel;

    // 삭제 버튼을 한 번 누른 뒤 다시 눌러야 실제로 지워지는 시간(초)
    [SerializeField, Min(0.1f)] private float _deleteConfirmWindow = 2.5f;

    // 삭제 버튼 평소 글자
    [SerializeField] private string _deleteText = "삭제";

    // 삭제 버튼 확인 글자
    [SerializeField] private string _deleteConfirmText = "한 번 더";

    [Header("새 맵 생성")]
    // 새 맵 버튼을 눌렀을 때 이름과 시드를 받는 대화상자
    [SerializeField] private CreateNewMapDialogUI _createDialog;

    [Header("씬 전환")]
    // 맵이 정해졌을 때 넘어갈 인게임 씬 이름. 비워 두면 씬을 전환하지 않는다.
    [SerializeField] private string _ingameSceneName = "Ingame";

    // 새로 만드는 맵 슬롯 이름의 앞부분. 뒤에 비어 있는 가장 작은 번호가 붙는다.
    [SerializeField] private string _newMapNamePrefix = "새 맵";

    // 뒤로가기를 눌러 메인 메뉴로 돌아가야 할 때 발생한다.
    public event Action BackRequested;

    // 지금 목록에 놓인 카드들
    private readonly List<MapSlotCardUI> _cards = new();

    // 지금 선택된 카드
    private MapSlotCardUI _selectedCard;

    // 삭제 확인 상태가 풀리는 시각(실제 시간 기준). 음수면 확인 상태가 아니다.
    private float _deleteArmedUntil = -1f;

    private void Awake()
    {
        _startButton.onClick.AddListener(HandleStartClick);
        _createButton.onClick.AddListener(HandleCreateClick);
        _deleteButton.onClick.AddListener(HandleDeleteClick);
        _backButton.onClick.AddListener(HandleBackClick);

        _createDialog.Confirmed += HandleCreateConfirmed;
        _createDialog.Canceled += HandleCreateCanceled;
    }

    private void OnDestroy()
    {
        _createDialog.Confirmed -= HandleCreateConfirmed;
        _createDialog.Canceled -= HandleCreateCanceled;
    }

    private void Update()
    {
        HandleDeleteArmTimeout();
    }

    // 저장 폴더를 다시 훑어 카드 목록을 채운다. 화면을 열 때마다 호출한다.
    public void Refresh()
    {
        _createDialog.Close();
        HandleClearCards();

        List<(string Slot, DateTime SavedAt)> slots = HandleCollectSlots();
        for (int i = 0; i < slots.Count; i++)
        {
            MapSlotCardUI card = Instantiate(_cardPrefab, _cardParent);
            card.Setup(i, slots[i].Slot, slots[i].SavedAt, _cardIntroDelay + _cardIntroStagger * i, HandleSelect, HandleLoad);
            _cards.Add(card);
        }

        _emptyNotice.SetActive(_cards.Count == 0);
        _countText.text = _cards.Count.ToString("00");

        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;

        if (_cards.Count > 0)
            HandleSelect(_cards[0]);

        HandleRefreshActionButtons();
    }

    // 저장 폴더의 맵 파일을 최신 저장순으로 모은다.
    private List<(string Slot, DateTime SavedAt)> HandleCollectSlots()
    {
        string savesDirectory = Path.Combine(Application.persistentDataPath, "Saves");
        if (!Directory.Exists(savesDirectory))
            return new List<(string, DateTime)>();

        return Directory.GetFiles(savesDirectory, "*.json")
            .Select(path => (Slot: Path.GetFileNameWithoutExtension(path), SavedAt: File.GetLastWriteTime(path)))
            .OrderByDescending(entry => entry.SavedAt)
            .ToList();
    }

    // 만들어 둔 카드를 모두 지운다.
    private void HandleClearCards()
    {
        foreach (MapSlotCardUI card in _cards)
            Destroy(card.gameObject);

        _cards.Clear();
        _selectedCard = null;
        HandleDisarmDelete();
    }

    // 선택 표시를 누른 카드로 옮긴다.
    private void HandleSelect(MapSlotCardUI card)
    {
        if (_selectedCard == card)
            return;

        if (_selectedCard != null)
            _selectedCard.SetSelected(false);

        _selectedCard = card;
        _selectedCard.SetSelected(true);

        HandleDisarmDelete();
        HandleRefreshActionButtons();
    }

    // 선택된 맵이 있을 때만 시작·삭제 버튼을 누를 수 있게 한다.
    private void HandleRefreshActionButtons()
    {
        bool hasSelection = _selectedCard != null;
        _startButton.interactable = hasSelection;
        _deleteButton.interactable = hasSelection;
    }

    // 선택한 맵으로 시작한다.
    private void HandleStartClick()
    {
        if (_selectedCard == null)
            return;

        HandleLoad(_selectedCard);
    }

    // 고른 슬롯의 맵 파일을 확인하고 인게임 씬으로 넘어간다.
    private void HandleLoad(MapSlotCardUI card)
    {
        if (_mapSaveUtil.TryReadMapFile(card.Slot) == null)
        {
            AudioManager.PlayError();
            Debug.LogError($"MapSelectUI: '{card.Slot}' 맵 파일을 열지 못했습니다.");
            Refresh();
            return;
        }

        AudioManager.PlayConfirm();
        HandleEnterIngameScene(card.Slot, isNewMap: false);
    }

    // 처음 누르면 확인 상태로 바꾸고, 확인 상태에서 다시 누르면 선택한 맵을 지운다.
    private void HandleDeleteClick()
    {
        if (_selectedCard == null)
            return;

        if (_deleteArmedUntil < 0f)
        {
            AudioManager.PlayButtonClick();
            _deleteArmedUntil = Time.unscaledTime + _deleteConfirmWindow;
            _deleteLabel.text = _deleteConfirmText;
            return;
        }

        HandleDisarmDelete();

        if (!_mapSaveUtil.TryDeleteMap(_selectedCard.Slot))
        {
            AudioManager.PlayError();
            return;
        }

        AudioManager.PlayConfirm();
        Refresh();
    }

    // 확인 시간이 지나도록 다시 누르지 않으면 삭제 확인 상태를 푼다.
    private void HandleDeleteArmTimeout()
    {
        if (_deleteArmedUntil >= 0f && Time.unscaledTime > _deleteArmedUntil)
            HandleDisarmDelete();
    }

    // 삭제 확인 상태를 풀고 버튼 글자를 되돌린다.
    private void HandleDisarmDelete()
    {
        _deleteArmedUntil = -1f;
        if (_deleteLabel != null)
            _deleteLabel.text = _deleteText;
    }

    // 새 맵 대화상자를 추천 이름과 이미 쓰이고 있는 슬롯 목록과 함께 연다.
    private void HandleCreateClick()
    {
        HandleDisarmDelete();
        _createDialog.Open(HandleBuildNewSlotName(), HandleCollectSlots().Select(entry => entry.Slot));
    }

    // 대화상자에서 받은 이름과 시드로 맵을 만들고 바로 인게임 씬으로 넘어간다.
    private void HandleCreateConfirmed(string slot, int seed)
    {
        _createDialog.Close();

        if (!_mapSaveUtil.TryCreateNewMap(slot, seed))
        {
            AudioManager.PlayError();
            Debug.LogError($"MapSelectUI: '{slot}' 맵 생성에 실패했습니다.");
            return;
        }

        AudioManager.PlayConfirm();
        HandleEnterIngameScene(slot, isNewMap: true);
    }

    // 대화상자에서 취소를 눌렀으므로 아무것도 만들지 않고 닫는다.
    private void HandleCreateCanceled()
    {
        _createDialog.Close();
    }

    // 메인 메뉴로 돌아가야 함을 알린다.
    private void HandleBackClick()
    {
        AudioManager.PlayCancel();
        HandleDisarmDelete();
        BackRequested?.Invoke();
    }

    // "새 맵 1"처럼 접두사 뒤에 아직 쓰이지 않은 가장 작은 번호를 붙인 슬롯 이름을 만든다.
    private string HandleBuildNewSlotName()
    {
        var used = new HashSet<string>(HandleCollectSlots().Select(entry => entry.Slot));

        for (int number = 1; ; number++)
        {
            string candidate = $"{_newMapNamePrefix} {number}";
            if (!used.Contains(candidate))
                return candidate;
        }
    }

    // 다음에 열 슬롯을 지정하고 인게임 씬으로 전환한다. isNewMap은 이번 진입이 맵을 새로 만든 세션인지를 뜻한다.
    private void HandleEnterIngameScene(string slot, bool isNewMap)
    {
        if (string.IsNullOrEmpty(_ingameSceneName))
        {
            Debug.LogWarning($"MapSelectUI: 인게임 씬 이름이 비어 있어 '{slot}' 슬롯으로 전환하지 않았습니다.");
            return;
        }

        // 튜토리얼 진입부 — 맵을 새로 만든 세션에서만 튜토리얼이 진행된다. 튜토리얼을 걷어낼 때는 이 네 줄만 지우면 된다.
        if (isNewMap)
            TutorialSession.MarkNewMapCreated();
        else
            TutorialSession.MarkExistingMapLoaded();

        IngameSceneManager.NextMapSlot = slot;
        SceneManager.LoadScene(_ingameSceneName);
    }
}
