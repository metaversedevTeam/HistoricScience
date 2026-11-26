using System;
using HistoricScience.Test;
using UnityEngine;
using UnityEngine.InputSystem;

// 시민이 지을 건물의 배치 위치를 정하는 위치 지정 모드를 담당하는 컨트롤러. 화면 중앙 조준점이 가리키는 지면을 홀로그램이 따라가고,
// 좌클릭이나 F로 확정하면 자원을 소모해 그 자리에 바로 건물을 세운다. 우클릭이나 ESC로 취소한다.
public class BuildingPlacementController : MonoBehaviour
{
    // 겹침 판정 후보를 한 번에 담아 둘 버퍼 크기. 매 프레임 조회라 배열을 재사용하기 위해 필요하다.
    private const int k_MaxOverlapResults = 64;

    // 지금 위치 지정 중인 컨트롤러 개수. 파괴는 프레임 끝에 처리되므로, 취소한 프레임에도 ESC를 다른 곳(일시정지 등)이 받지 않게 막아 준다.
    public static int ActiveCount { get; private set; }

    // 배치가 끝나거나 취소되어 컨트롤러가 정리될 때 발생한다. 건물을 지었으면 true를 전달한다.
    public event Action<bool> OnFinished;

    [SerializeField] private Hologram _hologramPrefab;
    [SerializeField] private BuildCostUI _buildCostUiPrefab;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private Color _validColor = new Color(0.2f, 0.5f, 1f, 0.5f);
    [SerializeField] private Color _invalidColor = new Color(1f, 0.2f, 0.2f, 0.5f);
    // 겹침 판정 후보를 모을 때 건물 반경에 더해 줄 여유 거리. HitRadius가 콜라이더보다 큰 오브젝트도 후보에 들어오도록
    // 넉넉히 잡는다. 가장 큰 HitRadius(현재 연구소 5)보다 작으면 그 오브젝트와의 겹침을 놓칠 수 있다.
    [SerializeField] private float _hitableSearchPadding = 10f;
    // 조준점으로 건물을 놓을 수 있는 최대 거리(m). 3인칭 카메라는 시민 뒤에 있으므로 카메라와 시민 사이 거리까지 감안해 잡는다.
    [SerializeField] private float _maxPlacementDistance = 30f;

    private PlayerManager _playerManager;
    private IBuildable _buildable;
    private GameObject _buildingPrefab;
    private Transform _builderTransform;
    private HitableObject _builderHitable;
    private float _builderRadius;
    private float _buildingHitRadius;
    private float _footprintRadius;
    private readonly Collider[] _overlapBuffer = new Collider[k_MaxOverlapResults];
    private Hologram _hologram;
    private BuildCostUI _openBuildCostUI;
    private Camera _camera;
    private bool _isValidPosition;
    private bool _hasPosition;
    private bool _isFinished;
    private Vector3 _hologramWorldPosition;
    private Quaternion _hologramWorldRotation = Quaternion.identity;

    // 위치 지정 중인 컨트롤러로 집계한다.
    private void Awake()
    {
        ActiveCount++;
    }

    // 집계에서 빼고, 정리되지 않은 채 파괴되면 홀로그램과 비용 UI를 함께 정리한다.
    private void OnDestroy()
    {
        ActiveCount = Mathf.Max(0, ActiveCount - 1);

        // 씬이 내려가는 중일 수 있으므로 닫기 연출 없이 바로 정리한다.
        HandleCleanup(true);
    }

    // 지금 배치하려는 위치가 지을 수 있는 자리인지 여부
    public bool IsValidPosition => _hasPosition && _isValidPosition;

    // 건축에 필요한 자원을 모두 가지고 있는지 여부
    public bool HasEnoughResources => _buildable != null && HasEnoughResourcesFor(_buildable);

    // 배치 중인 건물의 이름
    public string BuildingName => _buildable != null ? _buildable.BuildingName : string.Empty;

    // 건물 선택 UI에서 고른 건물로 위치 지정 모드를 시작한다. builderHitable은 건물을 짓는 시민의 충돌 반경으로,
    // 시민이 서 있는 자리에 건물을 겹쳐 세우지 않는 데 쓴다. aimCamera는 조준점(화면 중앙) 레이를 쏠 카메라로, 비우면 메인 카메라를 쓴다.
    public void BeginPlacement(IBuildable buildable, GameObject buildingPrefab, PlayerManager playerManager, HitableObject builderHitable, Camera aimCamera)
    {
        _buildable = buildable;
        _buildingPrefab = buildingPrefab;
        _playerManager = playerManager;
        _builderHitable = builderHitable;
        _builderTransform = builderHitable != null ? builderHitable.transform : null;
        _builderRadius = builderHitable != null ? builderHitable.HitRadius : 0.5f;
        _camera = aimCamera != null ? aimCamera : Camera.main;
        _buildingHitRadius = ComputeBuildingHitRadius();

        _hologram = Instantiate(_hologramPrefab);
        _hologram.SetModel(buildable.BuildingModel);
        // 소환한 모델의 크기는 매 프레임 바뀌지 않으므로 판정에 쓸 반경을 여기서 한 번만 구해 둔다.
        _footprintRadius = ComputeFootprintRadius();

        var costData = new BuildCostData(_buildable.BuildCost, _playerManager.ResourceInventory);
        _openBuildCostUI = UIManager.Instance.OpenUI(_buildCostUiPrefab, costData);
    }

    private void Update()
    {
        if (_isFinished || _buildable == null) return;

        HandlePositioning();
    }

    // 조준점이 가리키는 지면으로 홀로그램을 옮기고 배치 가능 여부에 따라 색을 갱신하며, 확정·취소 입력을 처리한다.
    // 창고·일시정지처럼 조작을 막는 UI가 떠 있는 동안에는 입력을 받지 않는다.
    private void HandlePositioning()
    {
        if (UIManager.Instance.HasBlockingUI) return;

        if (IsCancelPressed())
        {
            CancelPlacement();
            return;
        }

        _hasPosition = TryRaycastGround(out Vector3 groundPoint, out TerrainPainter terrainPainter);
        _hologram.gameObject.SetActive(_hasPosition);
        if (!_hasPosition) return;

        _hologramWorldPosition = groundPoint;
        _hologramWorldRotation = ComputeFacingRotation(groundPoint);
        _hologram.transform.SetPositionAndRotation(groundPoint, _hologramWorldRotation);

        _isValidPosition = IsPositionBuildable(groundPoint, terrainPainter);
        _hologram.SetValid(_isValidPosition && HasEnoughResources, _validColor, _invalidColor);

        if (IsConfirmPressed())
            HandleTryBuild();
    }

    // 우클릭이나 ESC가 이번 프레임에 눌렸는지 확인한다. Alt로 커서를 풀어 둔 동안의 우클릭은 취소로 보지 않는다.
    private bool IsCancelPressed()
    {
        bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool rightClick = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame && !IsCursorFree();
        return escape || rightClick;
    }

    // 좌클릭이나 상호작용 키(F)가 이번 프레임에 눌렸는지 확인한다. Alt로 커서를 풀어 UI를 누르는 동안의 좌클릭은 배치 확정으로 보지 않는다.
    private bool IsConfirmPressed()
    {
        bool leftClick = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !IsCursorFree();
        bool interactKey = _playerManager != null && _playerManager.IsInteractPressed;
        return leftClick || interactKey;
    }

    // 커서가 화면 중앙에 잠겨 있지 않은지(Alt를 누르고 있는 등) 확인한다.
    private static bool IsCursorFree()
    {
        return Cursor.lockState != CursorLockMode.Locked;
    }

    // 화면 중앙 조준점에서 Ground 레이어를 레이캐스트해 지면 위치와 그 지점을 관리하는 TerrainPainter를 반환한다.
    private bool TryRaycastGround(out Vector3 point, out TerrainPainter terrainPainter)
    {
        point = default;
        terrainPainter = null;

        if (_camera == null)
            return false;

        Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, _maxPlacementDistance, _groundLayer, QueryTriggerInteraction.Ignore))
            return false;

        point = hit.point;
        terrainPainter = hit.collider.GetComponentInParent<TerrainPainter>();
        return true;
    }

    // 배치 위치에서 플레이어(건물을 짓는 시민, 없으면 카메라)를 바라보는 방향에 y축 180도를 더한 회전을 계산한다. 두 위치가 XZ상 겹치면 직전 회전을 유지한다.
    private Quaternion ComputeFacingRotation(Vector3 buildingPosition)
    {
        Transform viewer = _builderTransform != null ? _builderTransform : (_camera != null ? _camera.transform : null);
        if (viewer == null)
            return _hologramWorldRotation;

        Vector3 toViewer = viewer.position - buildingPosition;
        toViewer.y = 0f;
        if (toViewer.sqrMagnitude < 0.0001f)
            return _hologramWorldRotation;

        return Quaternion.LookRotation(toViewer, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);
    }

    // 홀로그램 중심과 건물 크기를 반영한 반경 안에 걸을 수 없는 지형이 있는지, 시민이 그 자리에 서 있지는 않은지,
    // 그리고 이미 자리를 차지한 다른 오브젝트와 겹치지는 않는지로 배치 가능 여부를 판정한다.
    private bool IsPositionBuildable(Vector3 worldPosition, TerrainPainter terrainPainter)
    {
        if (terrainPainter == null || terrainPainter.CurrentMapData == null || _buildable.BuildingModel == null)
            return false;

        if (IsOverlappingBuilder(worldPosition))
            return false;

        if (OverlapsOtherHitable(worldPosition))
            return false;

        Vector2 mapPosition = terrainPainter.WorldToMapPosition(worldPosition);
        float mapRadius = terrainPainter.WorldToMapDistance(_footprintRadius);

        return !terrainPainter.CurrentMapData.HasUnwalkableWithin(mapPosition, mapRadius);
    }

    // 시민이 배치 자리 안에 서 있어 건물이 시민을 덮치는지 판정한다.
    private bool IsOverlappingBuilder(Vector3 worldPosition)
    {
        if (_builderTransform == null)
            return false;

        float combinedRadius = _buildingHitRadius + _builderRadius;
        if (combinedRadius <= 0f)
            return false;

        Vector2 builderXZ = new Vector2(_builderTransform.position.x, _builderTransform.position.z);
        Vector2 targetXZ = new Vector2(worldPosition.x, worldPosition.z);

        return Vector2.Distance(builderXZ, targetXZ) < combinedRadius;
    }

    // 배치 자리에 지을 건물이 다른 HitableObject와 겹치는지 판정한다. 지면은 항상 겹치므로 제외하고 주변 콜라이더를 모은 뒤,
    // 콜라이더가 자식에 달린 경우가 있어 부모까지 거슬러 올라가 HitableObject를 찾고 두 반경의 합으로 겹침을 판정한다.
    // 건물을 짓는 시민 자신은 IsOverlappingBuilder가 따로 판정하므로 여기서는 제외한다.
    private bool OverlapsOtherHitable(Vector3 worldPosition)
    {
        float searchRadius = _buildingHitRadius + _hitableSearchPadding;
        int count = Physics.OverlapSphereNonAlloc(worldPosition, searchRadius, _overlapBuffer, ~_groundLayer, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            var hitable = _overlapBuffer[i].GetComponentInParent<HitableObject>();
            if (hitable == null || hitable == _builderHitable)
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

    // 지을 건물 프리팹의 Hitable 반경을 읽어 온다. 없으면 0으로 보고 반경 기반 판정을 건너뛰게 한다.
    private float ComputeBuildingHitRadius()
    {
        HitableObject buildingHitable = _buildingPrefab != null ? _buildingPrefab.GetComponent<HitableObject>() : null;
        return buildingHitable != null ? buildingHitable.HitRadius : 0f;
    }

    // 자리가 유효하고 자원이 충분하면 자원을 소모해 그 자리에 건물을 세우고 배치 모드를 끝낸다. 아니면 아무것도 하지 않는다.
    private void HandleTryBuild()
    {
        if (!_isValidPosition)
            return;

        if (!HasEnoughResources)
        {
            Debug.LogWarning("[BuildingPlacementController] 자원이 부족해 건축할 수 없습니다.");
            return;
        }

        SpendResources();
        GameObject building = Instantiate(_buildingPrefab, _hologramWorldPosition, _hologramWorldRotation);
        StartConstructionAnimation(building);
        AudioManager.PlayConfirm();

        FinishPlacement(true);
    }

    // 소환한 건물에 건축 연출 컴포넌트가 있으면 건물의 건설 시간 동안 아래에서 위로 드러나는 연출을 재생시킨다.
    // 연출은 건물 스스로 끝까지 진행하므로, 컨트롤러는 이 호출 뒤 곧바로 정리돼도 된다.
    private void StartConstructionAnimation(GameObject building)
    {
        var constructionAnimator = building.GetComponent<ConstructionAnimator>();
        if (constructionAnimator == null) return;

        constructionAnimator.Play(_buildable.BuildTime);
    }

    // 지정한 건물에 필요한 자원을 모두 보유하고 있는지 확인한다.
    private bool HasEnoughResourcesFor(IBuildable buildable)
    {
        if (_playerManager == null) return false;

        foreach (var cost in buildable.BuildCost)
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

    // 아무것도 짓지 않은 채 위치 지정 모드를 종료한다.
    public void CancelPlacement()
    {
        FinishPlacement(false);
    }

    // 홀로그램과 건설 비용 UI를 정리하고 종료를 알린 뒤 컨트롤러 자신도 파괴한다.
    private void FinishPlacement(bool isBuilt)
    {
        if (_isFinished) return;
        _isFinished = true;

        HandleCleanup(false);
        OnFinished?.Invoke(isBuilt);
        Destroy(gameObject);
    }

    // 소환해 둔 홀로그램과 열어 둔 건설 비용 UI를 정리한다. immediate면 비용 UI를 연출 없이 닫는다. 여러 번 호출돼도 안전하다.
    private void HandleCleanup(bool immediate)
    {
        if (_hologram != null)
        {
            Destroy(_hologram.gameObject);
            _hologram = null;
        }

        if (_openBuildCostUI != null)
        {
            _openBuildCostUI.Close(immediate);
            _openBuildCostUI = null;
        }
    }
}
