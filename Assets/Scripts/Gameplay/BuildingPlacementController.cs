using System.Collections.Generic;
using HistoricScience.Test;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 건축할 건물의 배치 위치를 지정하는 위치 지정 모드를 담당하는 컨트롤러. 마우스로 홀로그램 위치를 옮기다 좌클릭으로 확정하면 선택 상태로 전환되어 건축/취소 명령을 제공한다.
public class BuildingPlacementController : SelectableObject, ICommandable
{
    private enum PlacementState { Positioning, Confirmed }

    [SerializeField] private Hologram _hologramPrefab;
    [SerializeField] private BuildCostUI _buildCostUiPrefab;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private Color _validColor = new Color(0.2f, 0.5f, 1f, 0.5f);
    [SerializeField] private Color _invalidColor = new Color(1f, 0.2f, 0.2f, 0.5f);

    private PlayerManager _playerManager;
    private IBuildable _buildable;
    private GameObject _buildingPrefab;
    private Hologram _hologram;
    private BuildCostUI _openBuildCostUI;
    private Camera _camera;
    private PlacementState _state;
    private bool _isValidPosition;
    private Vector3 _hologramWorldPosition;
    private IReadOnlyList<CommandData> _commands;

    // 카메라를 캐싱하고 건축/취소 명령 목록을 생성
    private void Awake()
    {
        _camera = Camera.main;
        _commands = new List<CommandData>
        {
            new CommandData("건축", null, ExecuteBuild),
            new CommandData("취소", null, ExecuteCancel),
        };
    }

    // 파괴 전 PlayerManager 구독을 해제
    private void OnDisable()
    {
        UnsubscribeFromPlayer();
    }

    // 이 오브젝트가 제공하는 명령 목록을 반환한다.
    public IReadOnlyList<CommandData> GetCommands() => _commands;

    // 건물 선택 UI에서 고른 건물로 위치 지정 모드를 시작한다.
    public void BeginPlacement(IBuildable buildable, GameObject buildingPrefab, PlayerManager playerManager)
    {
        _buildable = buildable;
        _buildingPrefab = buildingPrefab;
        _playerManager = playerManager;
        _state = PlacementState.Positioning;

        _hologram = Instantiate(_hologramPrefab);
        _hologram.SetMesh(buildable.BuildingMesh);
    }

    // 위치 지정 단계에서만 매 프레임 홀로그램 추적 처리를 수행
    private void Update()
    {
        if (_state == PlacementState.Positioning)
            HandlePositioning();
    }

    // 마우스 아래 지면으로 홀로그램을 옮기고 배치 가능 여부에 따라 색을 갱신하며, 유효한 위치에서의 좌클릭을 확정으로, ESC를 취소로 처리한다.
    private void HandlePositioning()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CancelPlacement();
            return;
        }

        if (!TryRaycastGround(out Vector3 groundPoint, out TerrainPainter terrainPainter))
            return;

        _hologramWorldPosition = groundPoint;
        _hologram.transform.position = groundPoint;

        _isValidPosition = IsPositionBuildable(groundPoint, terrainPainter);
        _hologram.SetValid(_isValidPosition, _validColor, _invalidColor);

        bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        if (clicked && _isValidPosition && !IsPointerOverUI())
            ConfirmPlacement();
    }

    // 마우스 아래 Ground 레이어를 레이캐스트해 지면 위치와 그 지점을 관리하는 TerrainPainter를 반환한다.
    private bool TryRaycastGround(out Vector3 point, out TerrainPainter terrainPainter)
    {
        point = default;
        terrainPainter = null;

        if (_camera == null || Mouse.current == null)
            return false;

        Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _groundLayer))
            return false;

        point = hit.point;
        terrainPainter = hit.collider.GetComponentInParent<TerrainPainter>();
        return true;
    }

    // 홀로그램 중심과 건물 크기를 반영한 반경 안에 걸을 수 없는 지형이 있는지로 배치 가능 여부를 판정한다.
    private bool IsPositionBuildable(Vector3 worldPosition, TerrainPainter terrainPainter)
    {
        if (terrainPainter == null || terrainPainter.CurrentMapData == null || _buildable.BuildingMesh == null)
            return false;

        Vector2 mapPosition = terrainPainter.WorldToMapPosition(worldPosition);
        float mapRadius = terrainPainter.WorldToMapDistance(HandleGetFootprintRadius());

        return !terrainPainter.CurrentMapData.HasUnwalkableWithin(mapPosition, mapRadius);
    }

    // 건물 메시의 바운드에서 배치 판정에 쓸 반경(월드 단위)을 계산한다.
    private float HandleGetFootprintRadius()
    {
        Bounds bounds = _buildable.BuildingMesh.bounds;
        return Mathf.Max(bounds.extents.x, bounds.extents.z);
    }

    // 현재 홀로그램 위치를 확정해 선택 상태로 전환하고, 카메라가 그 위치를 중심으로 이동하도록 만들며, 건설 비용 UI를 연다.
    private void ConfirmPlacement()
    {
        _state = PlacementState.Confirmed;
        _playerManager.OnDeselected += HandleDeselected;

        transform.position = _hologramWorldPosition;
        _playerManager.SelectExternally(this);

        var costData = new BuildCostData(_buildable.BuildCost, _playerManager.ResourceInventory);
        _openBuildCostUI = UIManager.Instance.OpenUI(_buildCostUiPrefab, costData);
    }

    // 확정 후 다른 대상을 선택하는 등으로 선택이 풀리면 배치를 취소한다.
    private void HandleDeselected()
    {
        CancelPlacement();
    }

    // 건축 명령 실행 — 자원이 충분하면 소모하고 실제 건물을 배치한 뒤 위치 지정 모드를 종료한다.
    private void ExecuteBuild()
    {
        if (!HasEnoughResources())
        {
            Debug.LogWarning("[BuildingPlacementController] 자원이 부족해 건축할 수 없습니다.");
            return;
        }

        SpendResources();
        Instantiate(_buildingPrefab, _hologramWorldPosition, Quaternion.identity);

        FinishPlacement();
    }

    // 취소 명령 실행 — 아무것도 짓지 않고 위치 지정 모드를 종료한다.
    private void ExecuteCancel()
    {
        FinishPlacement();
    }

    // 필요한 자원을 모두 보유하고 있는지 확인한다.
    private bool HasEnoughResources()
    {
        foreach (var cost in _buildable.BuildCost)
        {
            if (!_playerManager.ResourceInventory.Has(cost.Key, cost.Value))
                return false;
        }
        return true;
    }

    // 필요한 자원을 모두 차감한다.
    private void SpendResources()
    {
        foreach (var cost in _buildable.BuildCost)
            _playerManager.ResourceInventory.Remove(cost.Key, cost.Value);
    }

    // 위치 지정 모드를 취소한다.
    private void CancelPlacement()
    {
        FinishPlacement();
    }

    // 홀로그램과 열려 있는 건설 비용 UI를 정리하고, 확정 단계(스스로가 선택 상태)였다면 선택도 해제한 뒤 컨트롤러 자신도 파괴한다.
    // 확정 전(ESC 취소)에는 원래 선택돼 있던 시민 등을 건드리지 않도록 선택 해제를 건너뛴다.
    private void FinishPlacement()
    {
        UnsubscribeFromPlayer();

        if (_hologram != null)
            Destroy(_hologram.gameObject);

        if (_openBuildCostUI != null)
        {
            _openBuildCostUI.Close();
            _openBuildCostUI = null;
        }

        if (_state == PlacementState.Confirmed)
            _playerManager.DeselectExternally();

        Destroy(gameObject);
    }

    // 구독 중인 PlayerManager 이벤트를 해제한다.
    private void UnsubscribeFromPlayer()
    {
        if (_playerManager == null) return;
        _playerManager.OnDeselected -= HandleDeselected;
    }

    // 마우스 포인터가 UI 위에 있는지 확인한다.
    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
