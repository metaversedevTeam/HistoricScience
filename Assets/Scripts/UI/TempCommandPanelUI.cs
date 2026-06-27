using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// 선택된 유닛의 ICommandable 명령 목록을 하단 패널에 버튼으로 표시하는 UI
public class TempCommandPanelUI : MonoBehaviour
{
    [SerializeField] private PlayerManager _playerManager;
    [SerializeField] private Transform _buttonContainer;

    private readonly List<GameObject> _activeButtons = new();

    private string _dbg = "waiting...";

    private void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            bool over = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            string sel = EventSystem.current?.currentSelectedGameObject?.name ?? "none";
            _dbg = $"overUI:{over} sel:{sel}";
            Debug.Log($"[CommandPanelUI] click — {_dbg}");
        }
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 700, 30), $"[DBG] {_dbg}  pos:{Mouse.current?.position.ReadValue()}");
    }

    private void OnEnable()
    {
        _playerManager.OnSelected   += HandleSelected;
        _playerManager.OnDeselected += HandleDeselected;
    }

    private void OnDisable()
    {
        _playerManager.OnSelected   -= HandleSelected;
        _playerManager.OnDeselected -= HandleDeselected;
    }

    // 선택된 오브젝트의 ICommandable 명령 목록을 읽어 버튼을 생성한다.
    private void HandleSelected(SelectableObject selected)
    {
        ClearButtons();
        var commandable = selected.GetComponent<ICommandable>();
        if (commandable == null) return;

        foreach (var cmd in commandable.GetCommands())
            CreateButton(cmd);
    }

    // 패널을 비우고 모든 버튼을 제거한다.
    private void HandleDeselected()
    {
        ClearButtons();
    }

    // CommandData 하나에 대응하는 버튼을 생성해 컨테이너에 추가한다.
    private void CreateButton(CommandData cmd)
    {
        var btnGO = new GameObject(cmd.Name);
        btnGO.transform.SetParent(_buttonContainer, false);

        var rect = btnGO.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(100f, 100f);

        var img = btnGO.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        var btn = btnGO.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = new Color(0.15f, 0.15f, 0.15f);
        colors.highlightedColor = new Color(0.35f, 0.35f, 0.35f);
        colors.pressedColor     = new Color(0.05f, 0.05f, 0.05f);
        btn.colors = colors;
        btn.onClick.AddListener(() => cmd.OnExecute?.Invoke());

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        var labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = cmd.Name;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize  = 16f;
        tmp.color     = Color.white;

        _activeButtons.Add(btnGO);
    }

    // 생성된 버튼 오브젝트를 모두 파괴한다.
    private void ClearButtons()
    {
        foreach (var go in _activeButtons)
            Destroy(go);
        _activeButtons.Clear();
    }
}
