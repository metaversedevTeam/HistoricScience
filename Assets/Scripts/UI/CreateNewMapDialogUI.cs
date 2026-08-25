using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 새 맵 생성 대화상자(Figma의 create-new-map-dialog). 맵 관리 화면 위에 떠서 맵 이름과 시드를 받고,
// 생성을 누르면 정리된 입력값을 바깥(맵 관리 화면)에 넘긴다. 저장 자체는 하지 않는다.
public class CreateNewMapDialogUI : MonoBehaviour
{
    [Header("입력")]
    [SerializeField] private TMP_InputField _mapNameInput;
    [SerializeField] private TMP_InputField _seedInput;

    [Header("버튼")]
    [SerializeField] private Button _createButton;
    [SerializeField] private Button _cancelButton;

    // 생성을 눌렀을 때 맵 이름과 시드를 담아 발생한다. 시드 칸을 비워 두면 무작위로 뽑은 값이 실린다.
    public event Action<string, int> Confirmed;
    // 취소를 눌러 대화상자를 닫아야 할 때 발생한다.
    public event Action Canceled;

    // 이미 저장되어 있어 다시 쓸 수 없는 슬롯 이름들. Open을 부를 때마다 새로 채워진다.
    private readonly HashSet<string> _usedSlots = new(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        _createButton.onClick.AddListener(HandleCreateClick);
        _cancelButton.onClick.AddListener(HandleCancelClick);
        _mapNameInput.onValueChanged.AddListener(HandleMapNameChanged);
    }

    private void OnDestroy()
    {
        _createButton.onClick.RemoveListener(HandleCreateClick);
        _cancelButton.onClick.RemoveListener(HandleCancelClick);
        _mapNameInput.onValueChanged.RemoveListener(HandleMapNameChanged);
    }

    // 추천 이름과 이미 쓰이고 있는 슬롯 목록을 받아 대화상자를 연다.
    public void Open(string suggestedName, IEnumerable<string> usedSlots)
    {
        _usedSlots.Clear();
        if (usedSlots != null)
        {
            foreach (string slot in usedSlots)
                _usedSlots.Add(slot);
        }

        // 입력 칸을 만지기 전에 켜 둬야 Awake에서 건 리스너가 첫 값 변경부터 반응한다.
        gameObject.SetActive(true);

        _mapNameInput.text = suggestedName;
        _seedInput.text = string.Empty;

        HandleRefreshCreateInteractable();
        _mapNameInput.Select();
    }

    // 대화상자를 닫는다. 맵을 실제로 만들었든 취소했든 바깥에서 호출한다.
    public void Close()
    {
        gameObject.SetActive(false);
    }

    // 입력한 이름이 슬롯으로 쓸 수 있는지(빈 값·파일명 금지 문자·중복) 검사한다.
    private bool HandleIsMapNameValid()
    {
        string mapName = _mapNameInput.text.Trim();

        if (string.IsNullOrEmpty(mapName)) return false;
        if (mapName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;

        return !_usedSlots.Contains(mapName);
    }

    // 이름이 쓸 수 없는 값이면 생성 버튼을 잠가, 눌러도 아무 일이 없는 상황을 막는다.
    private void HandleRefreshCreateInteractable()
    {
        _createButton.interactable = HandleIsMapNameValid();
    }

    // 시드 칸의 값을 정수 시드로 바꾼다. 비워 두면 무작위 시드를 뽑고, 정수 범위를 넘으면 잘라서 쓴다.
    private int HandleResolveSeed()
    {
        string seedText = _seedInput.text.Trim();

        if (string.IsNullOrEmpty(seedText))
            return UnityEngine.Random.Range(int.MinValue, int.MaxValue);

        if (long.TryParse(seedText, out long parsed))
            return unchecked((int)parsed);

        return UnityEngine.Random.Range(int.MinValue, int.MaxValue);
    }

    // 이름을 고칠 때마다 생성 버튼의 잠금 상태를 다시 맞춘다.
    private void HandleMapNameChanged(string value)
    {
        HandleRefreshCreateInteractable();
    }

    // 생성 요청을 정리된 이름·시드와 함께 알린다.
    private void HandleCreateClick()
    {
        if (!HandleIsMapNameValid()) return;

        Confirmed?.Invoke(_mapNameInput.text.Trim(), HandleResolveSeed());
    }

    // 취소 요청을 알린다.
    private void HandleCancelClick()
    {
        Canceled?.Invoke();
    }
}
