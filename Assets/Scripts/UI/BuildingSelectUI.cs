using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 건물 목록을 받아 아이콘 버튼으로 나열하고, 건물 선택·닫기를 콜백으로 알리는 건물 선택 UI
public class BuildingSelectUI : OpenableUIBase<IReadOnlyList<IBuildable>>
{
    // 건물 아이콘 버튼 클릭 시 선택된 건물을 알리는 콜백
    public event Action<IBuildable> OnBuildingSelected;

    // 건물을 하나도 고르지 않은 채 UI가 닫혔을 때 알리는 콜백
    public event Action OnClosedWithoutSelection;

    [SerializeField] private BuildingIconButtonUI _buildingIconButtonPrefab;
    [SerializeField] private RectTransform _content;
    [SerializeField] private Button _closeButton;

    // 이번 열림 동안 건물을 하나라도 선택했는지 여부
    private bool _hasSelectedBuilding;

    private void Awake()
    {
        _closeButton.onClick.AddListener(OnCloseButtonClick);
    }

    // 전달받은 건물 목록으로 아이콘 버튼들을 채운다.
    protected override void ApplyData(IReadOnlyList<IBuildable> data)
    {
        _hasSelectedBuilding = false;
        PopulateButtons(data);
    }

    // 풀 반납 전 생성된 버튼들을 정리하고, 선택 없이 닫힌 경우 콜백을 발행한다.
    protected override void OnReturnToPool()
    {
        ClearButtons();

        if (!_hasSelectedBuilding)
            OnClosedWithoutSelection?.Invoke();
    }

    // 닫기 버튼 클릭 시 UI를 닫는다.
    private void OnCloseButtonClick() => Close();

    // 기존 버튼을 제거하고 건물 목록마다 아이콘 버튼을 새로 생성한다.
    private void PopulateButtons(IReadOnlyList<IBuildable> buildables)
    {
        ClearButtons();
        foreach (var buildable in buildables)
        {
            var button = Instantiate(_buildingIconButtonPrefab, _content);
            button.Setup(buildable);
            button.Button.onClick.AddListener(() => HandleBuildingClicked(buildable));
        }
    }

    // Content 아래의 아이콘 버튼을 모두 제거한다.
    private void ClearButtons()
    {
        for (int i = _content.childCount - 1; i >= 0; i--)
            Destroy(_content.GetChild(i).gameObject);
    }

    // 건물 아이콘 버튼 클릭 시 선택 상태를 기록하고 선택 콜백을 발행한다.
    private void HandleBuildingClicked(IBuildable buildable)
    {
        _hasSelectedBuilding = true;
        OnBuildingSelected?.Invoke(buildable);
    }
}
