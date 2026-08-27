using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 맵 관리 화면(Figma의 map-management-menu). 저장된 맵 슬롯을 최신순 목록으로 보여 주고
// 불러오기·삭제·새 맵 생성을 처리하며, 맵이 정해지면 인게임 씬으로 넘어간다.
public class MapManagementUI : MonoBehaviour
{
    // 맵 파일 저장/읽기를 담당하는 유틸리티
    [SerializeField] private MapSaveUtil _mapSaveUtil;

    [Header("목록")]
    [SerializeField] private MapListRowUI _rowPrefab;
    [SerializeField] private RectTransform _rowParent;
    // 저장된 맵이 하나도 없을 때만 켜지는 안내 문구
    [SerializeField] private TextMeshProUGUI _emptyText;

    [Header("버튼")]
    [SerializeField] private Button _createButton;
    [SerializeField] private Button _backButton;

    [Header("새 맵 생성")]
    // 새 맵 버튼을 눌렀을 때 이름과 시드를 받는 대화상자
    [SerializeField] private CreateNewMapDialogUI _createDialog;

    [Header("씬 전환")]
    // 맵이 정해졌을 때 넘어갈 인게임 씬 이름. 비워 두면 씬을 전환하지 않는다.
    [SerializeField] private string _ingameSceneName = "PlayTest";
    // 새로 만드는 맵 슬롯 이름의 앞부분. 뒤에 비어 있는 가장 작은 번호가 붙는다.
    [SerializeField] private string _newMapNamePrefix = "새 맵";

    // 뒤로가기를 눌러 메인 메뉴로 돌아가야 할 때 발생한다.
    public event Action BackRequested;

    private readonly List<MapListRowUI> _rows = new();
    private MapListRowUI _selectedRow;

    private void Awake()
    {
        _createButton.onClick.AddListener(HandleCreateClick);
        _backButton.onClick.AddListener(HandleBackClick);

        _createDialog.Confirmed += HandleCreateConfirmed;
        _createDialog.Canceled += HandleCreateCanceled;
    }

    private void OnDestroy()
    {
        _createDialog.Confirmed -= HandleCreateConfirmed;
        _createDialog.Canceled -= HandleCreateCanceled;
    }

    // 저장 폴더를 다시 훑어 목록을 채운다. 화면을 열 때마다 호출한다.
    public void Refresh()
    {
        _createDialog.Close();
        HandleClearRows();

        foreach ((string slot, DateTime savedAt) in HandleCollectSlots())
        {
            MapListRowUI row = Instantiate(_rowPrefab, _rowParent);
            row.Setup(slot, savedAt, HandleSelect, HandleLoad, HandleDelete);
            _rows.Add(row);
        }

        _emptyText.gameObject.SetActive(_rows.Count == 0);

        if (_rows.Count > 0)
            HandleSelect(_rows[0]);
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

    // 만들어 둔 줄을 모두 지운다.
    private void HandleClearRows()
    {
        foreach (MapListRowUI row in _rows)
            Destroy(row.gameObject);

        _rows.Clear();
        _selectedRow = null;
    }

    // 선택 표시를 누른 줄로 옮긴다.
    private void HandleSelect(MapListRowUI row)
    {
        if (_selectedRow == row) return;

        if (_selectedRow != null)
            _selectedRow.SetSelected(false);

        _selectedRow = row;
        _selectedRow.SetSelected(true);
    }

    // 선택한 슬롯의 맵 파일을 확인하고 인게임 씬으로 넘어간다.
    private void HandleLoad(MapListRowUI row)
    {
        if (_mapSaveUtil.TryReadMapFile(row.Slot) == null)
        {
            AudioManager.PlayError();
            Debug.LogError($"MapManagementUI: '{row.Slot}' 맵 파일을 열지 못했습니다.");
            Refresh();
            return;
        }

        AudioManager.PlayConfirm();
        HandleEnterIngameScene(row.Slot, isNewMap: false);
    }

    // 선택한 슬롯의 맵 파일을 지우고 목록을 갱신한다.
    private void HandleDelete(MapListRowUI row)
    {
        if (!_mapSaveUtil.TryDeleteMap(row.Slot))
        {
            AudioManager.PlayError();
            return;
        }

        AudioManager.PlayConfirm();
        Refresh();
    }

    // 새 맵 대화상자를 추천 이름과 이미 쓰이고 있는 슬롯 목록과 함께 연다.
    private void HandleCreateClick()
    {
        _createDialog.Open(HandleBuildNewSlotName(), HandleCollectSlots().Select(entry => entry.Slot));
    }

    // 대화상자에서 받은 이름과 시드로 맵을 만들고 바로 인게임 씬으로 넘어간다.
    private void HandleCreateConfirmed(string slot, int seed)
    {
        _createDialog.Close();

        if (!_mapSaveUtil.TryCreateNewMap(slot, seed))
        {
            AudioManager.PlayError();
            Debug.LogError($"MapManagementUI: '{slot}' 맵 생성에 실패했습니다.");
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
        BackRequested?.Invoke();
    }

    // "새 맵 1"처럼 접두사 뒤에 아직 쓰이지 않은 가장 작은 번호를 붙인 슬롯 이름을 만든다.
    private string HandleBuildNewSlotName()
    {
        var used = new HashSet<string>(HandleCollectSlots().Select(entry => entry.Slot));

        for (int number = 1; ; number++)
        {
            string candidate = $"{_newMapNamePrefix} {number}";
            if (!used.Contains(candidate)) return candidate;
        }
    }

    // 다음에 열 슬롯을 지정하고 인게임 씬으로 전환한다. isNewMap은 이번 진입이 맵을 새로 만든 세션인지를 뜻한다.
    private void HandleEnterIngameScene(string slot, bool isNewMap)
    {
        if (string.IsNullOrEmpty(_ingameSceneName))
        {
            Debug.LogWarning($"MapManagementUI: 인게임 씬 이름이 비어 있어 '{slot}' 슬롯으로 전환하지 않았습니다.");
            return;
        }

        // 튜토리얼 진입부 — 맵을 새로 만든 세션에서만 튜토리얼이 진행된다. 튜토리얼을 걷어낼 때는 이 세 줄만 지우면 된다.
        if (isNewMap)
            TutorialSession.MarkNewMapCreated();
        else
            TutorialSession.MarkExistingMapLoaded();

        IngameSceneManager.NextMapSlot = slot;
        SceneManager.LoadScene(_ingameSceneName);
    }
}
