using System.Collections.Generic;
using HistoricScience.Test;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 건축할 건물의 배치 위치를 지정하는 위치 지정 모드를 담당하는 컨트롤러. 마우스로 홀로그램 위치를 옮기다 좌클릭으로 확정하면 선택 상태로 전환되어 건축/취소 명령을 제공한다.
public class BuildingPlacementController : SelectableObject, ICommandable
{
    // Positioning: 마우스로 자리를 고르는 중, Confirmed: 자리를 확정해 스스로가 선택된 상태, Building: 건축 명령 후 시민이 이동 중이라 선택이 이미 풀린 상태
    private enum PlacementState { Positioning, Confirmed, Building }

    // 겹침 판정 후보를 한 번에 담아 둘 버퍼 크기. 매 프레임 조회라 배열을 재사용하기 위해 필요하다.
    private const int k_MaxOverlapResults = 64;

    [SerializeField] private Hologram _hologramPrefab;
    [SerializeField] private BuildCostUI _buildCostUiPrefab;
    // 건축 명령 버튼에 표시할 아이콘
    [SerializeField] private Sprite _buildCommandIcon;
    // 취소 명령 버튼에 표시할 아이콘
    [SerializeField] private Sprite _cancelCommandIcon;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private Color _validColor = new Color(0.2f, 0.5f, 1f, 0.5f);
    [SerializeField] private Color _invalidColor = new Color(1f, 0.2f, 0.2f, 0.5f);
    // 겹침 판정 후보를 모을 때 건물 반경에 더해 줄 여유 거리. HitRadius가 콜라이더보다 큰 오브젝트도 후보에 들어오도록
    // 넉넉히 잡는다. 가장 큰 HitRadius(현재 연구소 5)보다 작으면 그 오브젝트와의 겹침을 놓칠 수 있다.
    [SerializeField] private float _hitableSearchPadding = 10f;

    private PlayerManager _playerManager;
    private IBuildable _buildable;
    private GameObject _buildingPrefab;
    private IMover _mover;
    private HitableObject _moverHitable;
    private Transform _builderTransform;
    private float _buildingHitRadius;
    private float _approachDistance;
    private float _footprintRadius;
    private readonly Collider[] _overlapBuffer = new Collider[k_MaxOverlapResults];
    private Hologram _hologram;
    private BuildCostUI _openBuildCostUI;
    private Camera _camera;
    private PlacementState _state;
    private bool _isValidPosition;
    private bool _isMovingToBuildSite;
    private Vector3 _hologramWorldPosition;
    private IReadOnlyList<CommandData> _commands;

    // 카메라를 캐싱하고 건축/취소 명령 목록을 생성
    private void Awake()
    {
        _camera = Camera.main;
        _commands = new List<CommandData>
        {
            new CommandData("건축", _buildCommandIcon, ExecuteBuild),
            new CommandData("취소", _cancelCommandIcon, ExecuteCancel),
        };
    }

    // 파괴 전 PlayerManager와 시민 이동 컴포넌트 구독을 해제
    private void OnDisable()
    {
        UnsubscribeFromPlayer();
        UnsubscribeFromMover();
    }

    // 건축 명령 후 시민이 배치 위치로 이동하고 있는 중인지 여부. 이동 중에는 시민에게 새 건축 명령을 받지 않기 위해 쓴다.
    public bool IsMovingToBuildSite => _isMovingToBuildSite;

    // 이 오브젝트가 제공하는 명령 목록을 반환한다.
    public IReadOnlyList<CommandData> GetCommands() => _commands;

    // 건물 선택 UI에서 고른 건물로 위치 지정 모드를 시작한다. mover는 건축 확정 시 배치 위치까지 걸어갈 시민의 이동 컴포넌트,
    // moverHitable은 그 시민의 충돌 반경으로, 건물의 반경과 합산해 건물 중심이 아닌 근처에서 멈추게 하는 데 쓰인다.
    public void BeginPlacement(IBuildable buildable, GameObject buildingPrefab, PlayerManager playerManager, IMover mover, HitableObject moverHitable)
    {
        _buildable = buildable;
        _buildingPrefab = buildingPrefab;
        _playerManager = playerManager;
        _mover = mover;
        _moverHitable = moverHitable;
        _state = PlacementState.Positioning;

        // 배치 자리가 시민과 겹치는지 매 프레임 판정하는 데 쓴다. Hitable은 시민 본체에 붙어 있으므로 그 Transform이 곧 시민의 위치다.
        _builderTransform = moverHitable != null ? moverHitable.transform : (mover as Component)?.transform;
        _buildingHitRadius = ComputeBuildingHitRadius();
        _approachDistance = ComputeApproachDistance();

        _hologram = Instantiate(_hologramPrefab);
        _hologram.SetModel(buildable.BuildingModel);
        // 소환한 모델의 크기는 매 프레임 바뀌지 않으므로 판정에 쓸 반경을 여기서 한 번만 구해 둔다.
        _footprintRadius = ComputeFootprintRadius();
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

    // 홀로그램 중심과 건물 크기를 반영한 반경 안에 걸을 수 없는 지형이 있는지, 건축할 시민이 그 자리에 너무 가까이 서 있지는 않은지,
    // 그리고 이미 자리를 차지한 다른 오브젝트와 겹치지는 않는지로 배치 가능 여부를 판정한다.
    private bool IsPositionBuildable(Vector3 worldPosition, TerrainPainter terrainPainter)
    {
        if (terrainPainter == null || terrainPainter.CurrentMapData == null || _buildable.BuildingModel == null)
            return false;

        if (IsTooCloseToBuilder(worldPosition))
            return false;

        if (OverlapsOtherHitable(worldPosition))
            return false;

        Vector2 mapPosition = terrainPainter.WorldToMapPosition(worldPosition);
        float mapRadius = terrainPainter.WorldToMapDistance(_footprintRadius);

        return !terrainPainter.CurrentMapData.HasUnwalkableWithin(mapPosition, mapRadius);
    }

    // 건축할 시민이 배치 자리에 너무 가까이 서 있는지 판정한다. 시민은 건축 시 건물과 자신의 반경을 합한 거리(_approachDistance)까지만
    // 다가와 멈추므로, 그보다 가까운 자리는 건물이 시민을 덮치는 위치라 건축 불가로 본다.
    private bool IsTooCloseToBuilder(Vector3 worldPosition)
    {
        if (_builderTransform == null || _approachDistance <= 0f)
            return false;

        Vector2 builderXZ = new Vector2(_builderTransform.position.x, _builderTransform.position.z);
        Vector2 targetXZ = new Vector2(worldPosition.x, worldPosition.z);

        return Vector2.Distance(builderXZ, targetXZ) < _approachDistance;
    }

    // 배치 자리에 지을 건물이 다른 HitableObject와 겹치는지 판정한다. 지면은 항상 겹치므로 제외하고 주변 콜라이더를 모은 뒤,
    // 콜라이더가 자식에 달린 경우가 있어 부모까지 거슬러 올라가 HitableObject를 찾고 두 반경의 합으로 겹침을 판정한다.
    // 건축을 맡은 시민 자신은 IsTooCloseToBuilder가 따로 판정하므로 여기서는 제외한다.
    private bool OverlapsOtherHitable(Vector3 worldPosition)
    {
        float searchRadius = _buildingHitRadius + _hitableSearchPadding;
        int count = Physics.OverlapSphereNonAlloc(worldPosition, searchRadius, _overlapBuffer, ~_groundLayer, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            var hitable = _overlapBuffer[i].GetComponentInParent<HitableObject>();
            if (hitable == null || hitable == _moverHitable)
                continue;

            if (IsWithinCombinedRadius(worldPosition, hitable))
                return true;
        }
        return false;
    }

    // 배치 자리와 대상의 XZ 거리가 두 HitRadius의 합보다 가까운지, 즉 서로 겹치는지 판정한다.
    private bool IsWithinCombinedRadius(Vector3 worldPosition, HitableObject hitable)
    {
        float combinedRadius = _buildingHitRadius + hitable.HitRadius;
        if (combinedRadius <= 0f)
            return false;

        Vector2 targetXZ = new Vector2(worldPosition.x, worldPosition.z);
        Vector2 hitableXZ = new Vector2(hitable.transform.position.x, hitable.transform.position.z);

        return Vector2.Distance(targetXZ, hitableXZ) < combinedRadius;
    }

    // 홀로그램에 소환된 모델의 크기에서 배치 판정에 쓸 반경(월드 단위)을 계산한다. SetModel 이후에 호출해야 한다.
    private float ComputeFootprintRadius()
    {
        Bounds bounds = _hologram.ModelBounds;
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

    // 건축 명령 실행 — 즉시 짓지 않고 시민을 배치 위치 근처까지 걸어가게 하며, 도착하면 HandleArrivedAtBuildSite가 실제 건축을 수행한다.
    // 이동이 시작되면 건축 위치 지정 UI(홀로그램·명령 버튼·비용 패널)는 비활성화한다.
    private void ExecuteBuild()
    {
        if (_isMovingToBuildSite) return;

        Vector2 destination = new Vector2(_hologramWorldPosition.x, _hologramWorldPosition.z);
        bool started = _mover.Move(destination, HandleArrivedAtBuildSite, HandleMoveEndAtBuildSite, _approachDistance);
        if (!started)
        {
            Debug.LogWarning("[BuildingPlacementController] 건축 위치로 이동할 수 없습니다.");
            return;
        }

        _isMovingToBuildSite = true;
        // 이 Move 호출 자체도 이전 명령을 대체하며 이벤트를 발생시키므로, 스스로를 취소하지 않도록 호출이 끝난 뒤에 구독한다.
        _mover.OnMoveOrderReplaced += HandleMoveOrderReplaced;
        HidePlacementUI();
    }

    // 지을 건물 프리팹의 Hitable 반경을 읽어 온다. 없으면 0으로 보고 반경 기반 판정을 건너뛰게 한다.
    private float ComputeBuildingHitRadius()
    {
        HitableObject buildingHitable = _buildingPrefab != null ? _buildingPrefab.GetComponent<HitableObject>() : null;
        return buildingHitable != null ? buildingHitable.HitRadius : 0f;
    }

    // 지을 건물과 이동하는 시민의 Hitable 반경을 합산해, 건물 중심이 아니라 그 언저리에서 멈추게 할 거리를 계산한다.
    // 이 거리는 배치 위치가 시민에게 너무 가까운지를 판정하는 기준으로도 쓰인다. ComputeBuildingHitRadius 이후에 호출해야 한다.
    private float ComputeApproachDistance()
    {
        float moverRadius = _moverHitable != null ? _moverHitable.HitRadius : 0f;
        return _buildingHitRadius + moverRadius;
    }

    // 이동이 시작되면 건축 UI(명령 버튼·비용 패널)를 숨긴다. 홀로그램은 실제로 건축이 완료될 때(FinishPlacement)까지 배치 위치 표시로 남겨둔다.
    // 도착 판정은 계속 대기해야 하므로 컨트롤러 자신은 파괴하지 않는다.
    // 스스로 선택을 해제하는 것이므로, 해제 시 배치를 취소해버리는 HandleDeselected 구독은 먼저 해제해 둔다.
    // 이후 플레이어가 다른 대상을 선택할 수 있으므로, 정리 시 그 선택까지 풀어버리지 않도록 상태를 Building으로 옮긴다.
    private void HidePlacementUI()
    {
        _playerManager.OnDeselected -= HandleDeselected;
        _state = PlacementState.Building;

        if (_openBuildCostUI != null)
        {
            _openBuildCostUI.Close();
            _openBuildCostUI = null;
        }

        _playerManager.DeselectExternally();
    }

    // 시민이 배치 위치에 실제로 도착했을 때 호출 — 자원을 확인해 소모하고 건물을 소환한다.
    private void HandleArrivedAtBuildSite()
    {
        _isMovingToBuildSite = false;

        if (!HasEnoughResources())
        {
            Debug.LogWarning("[BuildingPlacementController] 자원이 부족해 건축할 수 없습니다.");
            return;
        }

        SpendResources();
        Instantiate(_buildingPrefab, _hologramWorldPosition, Quaternion.identity);

        FinishPlacement();
    }

    // 이동이 도착 없이 끝난 경우(길이 막혀 멈춤 등) 경고를 남기고 배치를 정리한다. 이 시점에는 이미 선택이 풀려 컨트롤러를 다시 조작할 수 없으므로,
    // 소환해 둔 홀로그램을 남겨두지 않고 컨트롤러와 함께 파괴한다.
    // 도착에 성공한 경우에는 HandleArrivedAtBuildSite가 먼저 실행되며 플래그를 이미 내려두므로 여기서는 무시한다.
    private void HandleMoveEndAtBuildSite()
    {
        if (!_isMovingToBuildSite) return;

        _isMovingToBuildSite = false;
        Debug.LogWarning("[BuildingPlacementController] 건축 위치까지 이동하지 못했습니다.");
        FinishPlacement();
    }

    // 시민에게 새 이동·채집 명령이 내려와 건축 위치로 가던 이동이 취소됐을 때 호출 — 도착할 방법이 사라졌으므로 홀로그램과 컨트롤러를 정리한다.
    // 시민은 이미 새 명령을 수행 중이므로 그 이동을 멈추지 않도록 플래그를 먼저 내리고 FinishPlacement를 호출한다.
    private void HandleMoveOrderReplaced()
    {
        if (!_isMovingToBuildSite) return;

        _isMovingToBuildSite = false;
        FinishPlacement();
    }

    // 취소 명령 실행 — 아무것도 짓지 않은 채 위치 지정 모드를 종료한다. 이동이 시작되면 UI가 비활성화되어 이 시점에는 아직 이동 전이다.
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

    // 위치 지정 모드를 취소한다. 건축 명령 후 시민이 배치 위치로 이동 중인 단계에서는 그 이동까지 멈춰버리지 않도록 취소를 무시한다.
    public void CancelPlacement()
    {
        if (_state == PlacementState.Building) return;

        FinishPlacement();
    }

    // 홀로그램과 열려 있는 건설 비용 UI를 정리하고, 확정 단계(스스로가 선택 상태)였다면 선택도 해제한 뒤 컨트롤러 자신도 파괴한다.
    // 확정 전(ESC 취소)에는 원래 선택돼 있던 시민 등을 건드리지 않도록 선택 해제를 건너뛴다.
    private void FinishPlacement()
    {
        UnsubscribeFromPlayer();
        UnsubscribeFromMover();

        if (_isMovingToBuildSite)
        {
            _isMovingToBuildSite = false;
            _mover.Stop();
        }

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

    // 구독 중인 시민 이동 컴포넌트의 이벤트를 해제한다.
    private void UnsubscribeFromMover()
    {
        if (_mover == null) return;
        _mover.OnMoveOrderReplaced -= HandleMoveOrderReplaced;
    }

    // 마우스 포인터가 UI 위에 있는지 확인한다.
    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
