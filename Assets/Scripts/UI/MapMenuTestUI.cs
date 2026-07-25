using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HistoricScience.Test
{
    // 새 맵 생성, 기존 맵 불러오기, 게임 종료 기능을 제공하는 테스트용 메인 메뉴 UI
    public class MapMenuTestUI : MonoBehaviour
    {
        // 맵 파일 저장/읽기를 담당하는 유틸리티
        [SerializeField] private MapSaveUtil _mapSaveUtil;

        // 생성/불러오기 대상 슬롯 이름을 입력받는 필드
        [SerializeField] private TMP_InputField _slotNameInput;
        // 새 맵 생성 시 사용할 시드를 입력받는 필드. 비워두면 무작위 시드를 사용한다.
        [SerializeField] private TMP_InputField _seedInput;
        // 저장된 맵 슬롯 목록을 보여주는 드롭다운
        [SerializeField] private TMP_Dropdown _existingSlotsDropdown;

        [SerializeField] private Button _createButton;
        [SerializeField] private Button _loadButton;
        [SerializeField] private Button _deleteButton;
        [SerializeField] private Button _quitButton;

        // 진행 결과나 오류를 알려주는 상태 텍스트
        [SerializeField] private TextMeshProUGUI _statusText;

        // 맵 생성/불러오기에 성공했을 때 전환할 인게임 씬 이름. 비워두면 씬을 전환하지 않고 상태 메시지만 표시한다.
        [SerializeField] private string _ingameSceneName;

        // 버튼 클릭을 연결하고 저장된 맵 목록을 채운다.
        private void Awake()
        {
            _createButton.onClick.AddListener(HandleCreateButtonClick);
            _loadButton.onClick.AddListener(HandleLoadButtonClick);
            _deleteButton.onClick.AddListener(HandleDeleteButtonClick);
            _quitButton.onClick.AddListener(HandleQuitButtonClick);
            _existingSlotsDropdown.onValueChanged.AddListener(HandleSlotSelected);

            HandleRefreshSlotList();
            HandleSyncSlotNameToSelection();
        }

        // 입력된 슬롯 이름으로 새 맵 파일을 생성한다. 이미 있으면 덮어쓴다.
        private void HandleCreateButtonClick()
        {
            string slot = _slotNameInput.text.Trim();
            if (string.IsNullOrEmpty(slot))
            {
                HandleShowStatus("슬롯 이름을 입력하세요.");
                return;
            }

            bool overwritten = _mapSaveUtil.TryReadMapFile(slot) != null;
            int seed = HandleResolveSeed();

            MapSaveData saveData = new MapSaveData
            {
                Seed = seed,
                // 빈 문자열은 JsonUtility 파싱에 실패하므로, 내용 없는 유효한 JSON으로 채워 둔다.
                InventoryJson = "{}",
            };

            if (!_mapSaveUtil.TrySaveMap(saveData, slot))
            {
                HandleShowStatus($"'{slot}' 맵 생성에 실패했습니다.");
                return;
            }

            HandleShowStatus($"'{slot}' 맵을 {(overwritten ? "덮어썼습니다" : "생성했습니다")}. (시드: {seed})");
            HandleRefreshSlotList();
            HandleEnterIngameScene(slot);
        }

        // 입력된 슬롯 이름의 맵 파일을 읽어 불러온다.
        private void HandleLoadButtonClick()
        {
            string slot = _slotNameInput.text.Trim();
            if (string.IsNullOrEmpty(slot))
            {
                HandleShowStatus("불러올 슬롯 이름을 입력하세요.");
                return;
            }

            MapSaveData saveData = _mapSaveUtil.TryReadMapFile(slot);
            if (saveData == null)
            {
                HandleShowStatus($"'{slot}' 맵 파일을 찾을 수 없습니다.");
                return;
            }

            HandleShowStatus($"'{slot}' 맵을 불러왔습니다. (시드: {saveData.Seed}, 오브젝트 {saveData.Savables.Count}개)");
            HandleEnterIngameScene(slot);
        }

        // 입력된 슬롯 이름의 맵 파일을 삭제한다.
        private void HandleDeleteButtonClick()
        {
            string slot = _slotNameInput.text.Trim();
            if (string.IsNullOrEmpty(slot))
            {
                HandleShowStatus("삭제할 슬롯 이름을 입력하세요.");
                return;
            }

            if (_mapSaveUtil.TryReadMapFile(slot) == null)
            {
                HandleShowStatus($"'{slot}' 맵 파일을 찾을 수 없습니다.");
                return;
            }

            if (!_mapSaveUtil.TryDeleteMap(slot))
            {
                HandleShowStatus($"'{slot}' 맵 삭제에 실패했습니다.");
                return;
            }

            HandleShowStatus($"'{slot}' 맵을 삭제했습니다.");
            _slotNameInput.text = string.Empty;
            HandleRefreshSlotList();
            HandleSyncSlotNameToSelection();
        }

        // 게임(플레이 모드)을 종료한다.
        private void HandleQuitButtonClick()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // 드롭다운에서 슬롯을 선택하면 이름 입력 필드에 채워준다.
        private void HandleSlotSelected(int index)
        {
            if (index < 0 || index >= _existingSlotsDropdown.options.Count)
                return;

            _slotNameInput.text = _existingSlotsDropdown.options[index].text;
        }

        // 드롭다운이 현재 가리키는 슬롯 이름을 입력 필드에 채운다. 목록이 비어 있으면 아무것도 하지 않는다.
        private void HandleSyncSlotNameToSelection()
        {
            if (_existingSlotsDropdown.options.Count == 0)
                return;

            _slotNameInput.text = _existingSlotsDropdown.options[_existingSlotsDropdown.value].text;
        }

        // 시드 입력 필드 값을 정수로 해석한다. 비어 있거나 잘못된 값이면 무작위 시드를 반환한다.
        private int HandleResolveSeed()
        {
            if (int.TryParse(_seedInput.text, out int seed))
                return seed;

            return Random.Range(int.MinValue, int.MaxValue);
        }

        // Saves 폴더를 스캔해 드롭다운의 슬롯 목록을 갱신한다.
        private void HandleRefreshSlotList()
        {
            _existingSlotsDropdown.ClearOptions();

            string savesDirectory = Path.Combine(Application.persistentDataPath, "Saves");
            if (!Directory.Exists(savesDirectory))
                return;

            var slots = Directory.GetFiles(savesDirectory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(name => name)
                .ToList();

            _existingSlotsDropdown.AddOptions(slots);
        }

        // 설정된 인게임 씬이 있으면 다음에 열 슬롯을 지정하고 씬을 전환한다.
        private void HandleEnterIngameScene(string slot)
        {
            if (string.IsNullOrEmpty(_ingameSceneName))
                return;

            IngameSceneManager.NextMapSlot = slot;
            SceneManager.LoadScene(_ingameSceneName);
        }

        // 상태 텍스트를 갱신한다.
        private void HandleShowStatus(string message)
        {
            _statusText.text = message;
        }
    }
}
