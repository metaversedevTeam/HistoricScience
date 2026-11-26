using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 시민 유닛을 나타내며 플레이어의 WASD 입력으로 걷고, 앞의 상호작용 대상(IGatherable·ICommandable)에게 할 수 있는 명령 목록을 띄워 F로 실행하는 컴포넌트
public class Citizen : MonoBehaviour, ISavable
{
    [SerializeField] private Sprite _gatherCommandIcon;
    [SerializeField] private Sprite _buildCommandIcon;
    // 채집 대상 선택 모드일 때 마우스에 표시할 곡괭이 커서 텍스처
    [SerializeField] private Texture2D _gatherTargetingCursor;
    // 곡괭이 커서에서 실제 클릭 지점으로 쓸 텍스처 안의 좌표(픽셀). 기본값은 32x32 커서의 중앙이다.
    [SerializeField] private Vector2 _gatherTargetingCursorHotspot = new Vector2(16f, 16f);
    // 건물 선택 UI를 여는 키
    [SerializeField] private Key _buildMenuKey = Key.B;
    // 건물 선택 UI 프리팹
    [SerializeField] private BuildingSelectUI _buildingSelectUiPrefab;
    // 건물 선택 UI에 나열할, IBuildable을 구현한 건물 프리팹 목록
    [SerializeField] private List<GameObject> _buildablePrefabs;
    // 건물 선택 후 위치 지정 모드를 담당하는 컨트롤러 프리팹
    [SerializeField] private BuildingPlacementController _buildingPlacementControllerPrefab;
    // 저장/복원 기능을 제공하는 컴포지션. PrefabId는 인스펙터에서 설정한다.
    [SerializeField] private SavableHandler _savable = new();

    // 시민 모델의 애니메이터. 이동·채집 상태를 파라미터로 전달한다.
    [SerializeField] private Animator _animator;

    [Header("앞쪽 상호작용 대상")]
    // 앞의 상호작용 대상 오른쪽에 명령 목록을 띄울 UI 프리팹. 비워 두면 목록을 띄우지 않는다.
    [SerializeField] private InteractionCommandListUI _interactionListUiPrefab;
    // 시민 발밑에서 앞쪽으로 감지 구의 중심을 얼마나 내밀지(m)
    [SerializeField] private float _interactForwardDistance = 1.2f;
    // 감지 구의 중심 높이(m)
    [SerializeField] private float _interactHeight = 0.8f;
    // 감지 구의 반경(m)
    [SerializeField, Min(0.01f)] private float _interactRadius = 0.9f;
    // 상호작용 대상으로 볼 레이어
    [SerializeField] private LayerMask _interactLayers = ~(1 << 2);

    // 앞쪽 감지에 한 번에 받을 최대 충돌체 수
    private const int k_MaxInteractHits = 16;

    // IGatherable 대상에게 띄울 명령 이름
    private const string k_GatherCommandName = "채집";

    // 애니메이터 파라미터 이름. 매 프레임 문자열 비교를 피하기 위해 해시로 캐싱한다.
    private static readonly int k_IsMovingParam = Animator.StringToHash("IsMoving");
    private static readonly int k_IsGatheringParam = Animator.StringToHash("IsGathering");

    private IMover _mover;
    private Gatherer _gatherer;

    private PlayerManager _pendingGatherPlayer;
    private IGatherable _gatherTarget;
    private ResourceInventory _gatherInventory;

    // 이동 중인지 여부. IMover의 이동 시작·종료 이벤트로 갱신한다.
    private bool _isMoving;

    // 열려 있는 건물 선택 UI. 열려 있지 않으면 null이다.
    private BuildingSelectUI _openBuildingSelectUI;

    // 건물 선택 후 생성한 위치 지정 컨트롤러. 배치가 끝나면 스스로 파괴되므로 그 시점부터 null이 된다.
    private BuildingPlacementController _placementController;

    // 건물 배치 모드가 진행 중인지 여부. 배치 중에는 F키가 배치 확정으로 쓰이므로 앞 대상 명령 목록이 쉰다.
    public bool IsPlacing => _placementController != null;

    // 앞쪽 감지 결과를 담아 둘 재사용 버퍼
    private readonly Collider[] _interactHits = new Collider[k_MaxInteractHits];

    // 명령 목록 UI에 넘길 명령 이름을 담아 둘 재사용 목록
    private readonly List<string> _interactCommandNames = new();

    // 이 시민이 만들어 둔 명령 목록 UI. 처음 대상을 만났을 때 만든다.
    private InteractionCommandListUI _interactionListUI;

    // 지금 명령 목록을 띄우고 있는 상호작용 대상(IGatherable·ICommandable을 가진 오브젝트). 없으면 null이다.
    private GameObject _interactionTarget;

    // 명령 목록을 띄우는 상호작용 대상이 바뀔 때 새 대상을 알린다. 목록이 사라지면 null을 넘긴다.
    public event System.Action<GameObject> OnInteractionTargetChanged;

    // 지금 명령 목록을 띄우고 있는 상호작용 대상. 없으면 null이다.
    public GameObject InteractionTarget => _interactionTarget;

    // IMover, Gatherer 컴포넌트를 캐싱
    private void Awake()
    {
        _mover = GetComponent<IMover>();
        _gatherer = GetComponent<Gatherer>();
    }

    // 이동 시작·종료 이벤트를 구독
    private void OnEnable()
    {
        _mover.OnMoveOrdered += HandleMoveOrdered;
        _mover.OnMoveEnd += HandleMoveEnd;
    }

    // 이동 이벤트와 건물 선택 UI 구독을 해제하고 명령 목록을 감춘다
    private void OnDisable()
    {
        _mover.OnMoveOrdered -= HandleMoveOrdered;
        _mover.OnMoveEnd -= HandleMoveEnd;
        UnsubscribeFromBuildingSelectUI();
        CancelBuildingPlacement();

        if (_interactionListUI != null)
            _interactionListUI.Hide();
        SetInteractionTarget(null);
    }

    // 시민이 사라지면 이 시민이 만든 명령 목록 UI도 함께 없앤다.
    private void OnDestroy()
    {
        if (_interactionListUI != null)
            Destroy(_interactionListUI.gameObject);
    }

    // 선택 여부와 관계없이 WASD로 걷고, 채집 대상이 지정되어 있으면 매 프레임 채집을 시도하며, 그 결과를 애니메이션에 반영
    private void Update()
    {
        HandleMoveInput();
        HandleGathering();
        HandleAnimation();
        HandleBuildMenuInput();
        HandleInteractionTarget();
    }

    // 앞에 IGatherable이나 ICommandable을 가진 대상이 있으면 그 오른쪽에 명령 목록을 띄우고, 없으면 감춘다.
    // 건물 배치 중에는 F키가 배치 확정으로 쓰이므로, 조작을 막는 다른 UI가 열려 있을 때는 그 UI가 앞에 서므로 목록을 감추고 쉰다.
    private void HandleInteractionTarget()
    {
        if (_interactionListUiPrefab == null) return;

        bool isSuspended = IsPlacing || UIManager.Instance.HasBlockingUI;
        Collider target = isSuspended ? null : FindFrontInteractionTarget();
        if (target == null)
        {
            if (_interactionListUI != null)
                _interactionListUI.Hide();
            SetInteractionTarget(null);
            return;
        }

        SetInteractionTarget(GetInteractableOwner(target));
        CollectCommandNames(target);
        EnsureInteractionListUI().Show(target, _interactCommandNames);

        PlayerManager player = PlayerManager.Instance;
        if (player != null && player.IsInteractPressed)
            ExecuteInteractionCommand(target, _interactionListUI.SelectedIndex, player);
    }

    // 명령 목록에서 선택된 명령을 실행한다. 목록은 '채집'(IGatherable일 때)이 먼저, 그 뒤에 ICommandable 명령이 같은 순서로 놓인다.
    private void ExecuteInteractionCommand(Collider target, int index, PlayerManager player)
    {
        if (index < 0) return;

        var gatherable = target.GetComponentInParent<IGatherable>();
        if (gatherable != null)
        {
            if (index == 0)
            {
                ExecuteGatherCommand(gatherable, player);
                return;
            }
            index--;
        }

        var commandable = target.GetComponentInParent<ICommandable>();
        if (commandable == null) return;

        var commands = commandable.GetCommands();
        if (index >= commands.Count) return;

        AudioManager.PlayButtonClick();
        commands[index].OnExecute?.Invoke(player);
    }

    // 앞의 채집 대상을 그 자리에서 채집한다. 진행 중이던 채집과 건축 위치 지정을 먼저 정리한다.
    private void ExecuteGatherCommand(IGatherable gatherable, PlayerManager player)
    {
        if (gatherable is not Component) return;

        CancelGathering();
        CancelBuildingPlacement();
        AudioManager.PlayButtonClick();
        BeginGathering(gatherable, player.ResourceInventory);
    }

    // 시민 앞쪽 감지 구 안에서 IGatherable이나 ICommandable을 가진, 구 중심에 가장 가까운 충돌체를 찾는다. 자기 자신은 제외한다.
    private Collider FindFrontInteractionTarget()
    {
        Vector3 center = transform.position + Vector3.up * _interactHeight + transform.forward * _interactForwardDistance;
        int count = Physics.OverlapSphereNonAlloc(center, _interactRadius, _interactHits, _interactLayers, QueryTriggerInteraction.Collide);

        Collider best = null;
        float bestSqrDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider hit = _interactHits[i];
            if (hit.transform.IsChildOf(transform)) continue;
            if (!IsInteractable(hit)) continue;

            float sqrDistance = (hit.ClosestPoint(center) - center).sqrMagnitude;
            if (sqrDistance >= bestSqrDistance) continue;

            best = hit;
            bestSqrDistance = sqrDistance;
        }
        return best;
    }

    // 명령 목록을 띄우는 대상이 바뀌었을 때만 기억해 두고 OnInteractionTargetChanged로 알린다.
    // 대상이 파괴되어도 null로 바뀐 것을 알릴 수 있도록, 유니티의 null 비교 대신 참조로 비교한다.
    private void SetInteractionTarget(GameObject target)
    {
        if (ReferenceEquals(_interactionTarget, target)) return;

        _interactionTarget = target;
        OnInteractionTargetChanged?.Invoke(target);
    }

    // 충돌체에서 위로 올라가며 IGatherable이나 ICommandable을 가진 오브젝트를 찾는다. 둘 다 있으면 IGatherable 쪽을 쓴다.
    private static GameObject GetInteractableOwner(Collider hit)
    {
        if (hit.GetComponentInParent<IGatherable>() is Component gatherable)
            return gatherable.gameObject;

        return hit.GetComponentInParent<ICommandable>() is Component commandable ? commandable.gameObject : null;
    }

    // 충돌체나 그 부모에 IGatherable 또는 ICommandable이 있는지 확인한다.
    private static bool IsInteractable(Collider hit)
    {
        return hit.GetComponentInParent<IGatherable>() != null || hit.GetComponentInParent<ICommandable>() != null;
    }

    // 대상이 IGatherable이면 '채집'을, ICommandable이면 그 명령 이름들을 차례로 모은다.
    private void CollectCommandNames(Collider target)
    {
        _interactCommandNames.Clear();

        if (target.GetComponentInParent<IGatherable>() != null)
            _interactCommandNames.Add(k_GatherCommandName);

        var commandable = target.GetComponentInParent<ICommandable>();
        if (commandable == null) return;

        foreach (var command in commandable.GetCommands())
            _interactCommandNames.Add(command.Name);
    }

    // 명령 목록 UI가 아직 없으면 UI 루트 맨 뒤(창 형태 UI 아래)에 만들어 둔다.
    private InteractionCommandListUI EnsureInteractionListUI()
    {
        if (_interactionListUI != null) return _interactionListUI;

        _interactionListUI = Instantiate(_interactionListUiPrefab, UIManager.Instance.UIRoot);
        _interactionListUI.transform.SetAsFirstSibling();
        return _interactionListUI;
    }

    // 에디터에서 시민을 선택하면 앞쪽 상호작용 감지 범위를 보여 준다.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.56f, 0.83f, 1f, 0.6f);
        Vector3 center = transform.position + Vector3.up * _interactHeight + transform.forward * _interactForwardDistance;
        Gizmos.DrawWireSphere(center, _interactRadius);
    }

    // 선택 여부와 관계없이 플레이어의 WASD 입력 방향으로 직접 걷고, Shift를 누르고 있으면 달린다. 입력이 끊기면 이동 컴포넌트가 스스로 멈춘다.
    // 걷기 시작하면 OnMoveOrdered를 거쳐 진행 중이던 채집이 취소되고, 대상 추적 이동도 이 이동으로 대체된다.
    private void HandleMoveInput()
    {
        PlayerManager player = PlayerManager.Instance;
        if (player == null) return;

        Vector2 direction = player.MoveDirection;
        if (direction == Vector2.zero) return;

        _mover.MoveInDirection(direction, player.IsRunning);
    }

    // 우클릭한 대상이 IGatherable이면 채집을 시작하고, 아니면 대상을 추적하며 진행 중이던 채집과 채집 타겟 지정 대기, 건축 위치 지정을 취소한다.
    // 이동은 WASD로만 하므로 빈 지면을 우클릭한 것은 무시한다.
    [System.Obsolete("PlayerManager의 선택 기능이 없어져 더 이상 쓰지 않는다. 상호작용은 앞 대상의 명령 목록(휠로 고르고 F로 실행)으로 한다.")]
    private void HandleRightClick(Vector2 pos, ClickableObject clickable)
    {
        if (clickable == null) return;

        CancelGatherTargeting();
        CancelGathering();
        CancelBuildingPlacement();

        var gatherable = clickable.GetComponent<IGatherable>();
        if (gatherable != null)
            BeginGathering(gatherable, PlayerManager.Instance.ResourceInventory);
        else
            _mover.Move(clickable.transform);
    }

    // 채집 명령을 실행해 다음 좌클릭 대상을 채집 대상으로 지정하는 모드로 진입한다.
    [System.Obsolete("PlayerManager의 선택 기능이 없어져 더 이상 쓰지 않는다. 상호작용은 앞 대상의 명령 목록(휠로 고르고 F로 실행)으로 한다.")]
    private void BeginGatherTargeting()
    {
        if (PlayerManager.Instance == null) return;

        CancelGatherTargeting();

        _pendingGatherPlayer = PlayerManager.Instance;
        _pendingGatherPlayer.OnMouseLeftClick += HandleGatherTargetClick;
        ApplyGatherTargetingCursor(true);
    }

    // 대기 중이던 채집 타겟 지정 모드에서 좌클릭 결과를 받아 대상이 유효하면 채집을 시작한다.
    [System.Obsolete("PlayerManager의 선택 기능이 없어져 더 이상 쓰지 않는다. 상호작용은 앞 대상의 명령 목록(휠로 고르고 F로 실행)으로 한다.")]
    private void HandleGatherTargetClick(Vector2 pos, ClickableObject clickable)
    {
        var chosenPlayer = _pendingGatherPlayer;
        CancelGatherTargeting();

        var gatherable = clickable != null ? clickable.GetComponent<IGatherable>() : null;
        if (gatherable == null) return;

        BeginGathering(gatherable, chosenPlayer.ResourceInventory);
    }

    // 지정한 대상을 채집 대상으로 설정한다. 대상에게 다가가지 않고 지금 서 있는 자리에서 채집하며, 채집 범위 밖이면 HandleGathering이 채집을 취소한다.
    private void BeginGathering(IGatherable gatherable, ResourceInventory inventory)
    {
        // 채집 타겟 지정을 거쳐 들어온 경우까지 포함해 여기서 건축 위치 지정을 정리한다.
        CancelBuildingPlacement();

        _gatherTarget = gatherable;
        _gatherInventory = inventory;
    }

    // 채집 타겟 지정 대기 상태를 취소하고 구독을 해제한다.
    [System.Obsolete("PlayerManager의 선택 기능이 없어져 더 이상 쓰지 않는다. 상호작용은 앞 대상의 명령 목록(휠로 고르고 F로 실행)으로 한다.")]
    private void CancelGatherTargeting()
    {
        if (_pendingGatherPlayer == null) return;

        _pendingGatherPlayer.OnMouseLeftClick -= HandleGatherTargetClick;
        _pendingGatherPlayer = null;
        ApplyGatherTargetingCursor(false);
    }

    // 채집 대상 선택 모드에 진입하면 곡괭이 커서를, 빠져나오면 시스템 기본 커서를 표시한다.
    [System.Obsolete("PlayerManager의 선택 기능이 없어져 더 이상 쓰지 않는다. 상호작용은 앞 대상의 명령 목록(휠로 고르고 F로 실행)으로 한다.")]
    private void ApplyGatherTargetingCursor(bool isTargeting)
    {
        if (_gatherTargetingCursor == null) return;

        Texture2D texture = isTargeting ? _gatherTargetingCursor : null;
        Vector2 hotspot = isTargeting ? _gatherTargetingCursorHotspot : Vector2.zero;
        Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
    }

    // 채집 대상이 유효하면 Gatherer로 채집을 시도하고, 대상이 파괴되었거나 제자리에서 닿지 않는 거리면 채집을 취소한다.
    private void HandleGathering()
    {
        if (_gatherTarget == null) return;

        bool isDestroyed = _gatherTarget is Component component && component == null;
        if (isDestroyed || !_gatherer.IsInRange(_gatherTarget))
        {
            CancelGathering();
            return;
        }

        _gatherer.TryGather(_gatherTarget, _gatherInventory);
    }

    // 진행 중인 채집 대상과 캐싱된 인벤토리 참조를 비운다. Gatherer의 채집 진행도 함께 끊어 다시 채집할 때 채집 시간을 처음부터 기다리게 한다.
    private void CancelGathering()
    {
        _gatherTarget = null;
        _gatherInventory = null;
        _gatherer?.CancelGather();
    }

    // 이동이 시작되면 이동 중으로 표시하고 진행 중이던 채집을 취소한다. 건축 명령처럼 시민 밖에서 내려온 이동 명령도 이 이벤트를 거치므로,
    // 채집 대상이 남은 채로 다른 곳에 도착해 채집 애니메이션이 다시 재생되는 것을 여기서 막는다.
    // 채집은 이동 없이 제자리에서 하므로, 채집 중 이동 명령이 들어오면 채집을 멈춘다.
    private void HandleMoveOrdered()
    {
        _isMoving = true;
        CancelGathering();
    }

    // 이동이 끝나면 이동 중 표시를 해제한다.
    private void HandleMoveEnd()
    {
        _isMoving = false;
    }

    // 현재 이동·채집 상태를 애니메이터 파라미터로 전달한다. 채집은 쿨타임 때문에 실제 채집 성공 여부가 끊기므로,
    // 대상까지 이동을 끝내고 채집 대상을 들고 있는 상태를 채집 중으로 본다.
    private void HandleAnimation()
    {
        if (_animator == null) return;

        _animator.SetBool(k_IsMovingParam, _isMoving);
        _animator.SetBool(k_IsGatheringParam, !_isMoving && _gatherTarget != null);

        // 연구로 걸음이 빨라진 만큼 걷는 동작도 빠르게 재생해 발이 미끄러져 보이지 않게 한다.
        // 애니메이터 전체 속도라 이동 중이 아닐 때는 원래 속도로 되돌려, 채집 같은 다른 동작은 영향을 받지 않게 한다.
        _animator.speed = _isMoving ? _mover.MoveSpeedRatio : 1f;
    }

    // 건축 키(B)가 눌렸고 조작을 막는 UI나 진행 중인 배치가 없으면 건물 선택 UI를 연다.
    private void HandleBuildMenuInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard[_buildMenuKey].wasPressedThisFrame) return;
        if (UIManager.Instance.HasBlockingUI || IsPlacing || _openBuildingSelectUI != null) return;

        OpenBuildingSelectUI();
    }

    // 건물 선택 UI를 열고 선택·닫기 결과를 구독한다.
    private void OpenBuildingSelectUI()
    {
        PlayerManager player = PlayerManager.Instance;
        if (player == null) return;

        UnsubscribeFromBuildingSelectUI();

        var selectData = new BuildingSelectData(CollectBuildables(), player.ResourceInventory);
        _openBuildingSelectUI = UIManager.Instance.OpenUI(_buildingSelectUiPrefab, selectData);
        _openBuildingSelectUI.OnBuildingSelected += HandleBuildingSelected;
        _openBuildingSelectUI.OnFinishClose += HandleBuildingSelectUIClosed;
    }

    // 인스펙터에 등록된 건물 프리팹 중 IBuildable을 구현한 것만 골라 반환한다.
    private IReadOnlyList<IBuildable> CollectBuildables()
    {
        var buildables = new List<IBuildable>();
        foreach (var prefab in _buildablePrefabs)
        {
            if (prefab != null && prefab.TryGetComponent(out IBuildable buildable))
                buildables.Add(buildable);
        }
        return buildables;
    }

    // 건물 선택 UI에서 건물이 선택되면 선택 UI를 닫고 위치 지정 모드를 시작한다.
    private void HandleBuildingSelected(IBuildable buildable)
    {
        PlayerManager player = PlayerManager.Instance;
        if (player == null) return;

        _openBuildingSelectUI.Close();

        _placementController = Instantiate(_buildingPlacementControllerPrefab);
        _placementController.BeginPlacement(buildable, (buildable as Component)?.gameObject, player, GetComponent<HitableObject>(), Camera.main);
    }

    // 진행 중이던 건축 위치 지정을 취소해 홀로그램과 위치 지정 모드를 정리한다.
    private void CancelBuildingPlacement()
    {
        if (_placementController == null) return;

        _placementController.CancelPlacement();
    }

    // 건물 선택 UI가 닫히면 구독을 해제한다.
    private void HandleBuildingSelectUIClosed(IManagedUI ui)
    {
        UnsubscribeFromBuildingSelectUI();
    }

    // 열려 있는 건물 선택 UI의 이벤트 구독을 해제하고 참조를 비운다.
    private void UnsubscribeFromBuildingSelectUI()
    {
        if (_openBuildingSelectUI == null) return;

        _openBuildingSelectUI.OnBuildingSelected -= HandleBuildingSelected;
        _openBuildingSelectUI.OnFinishClose -= HandleBuildingSelectUIClosed;
        _openBuildingSelectUI = null;
    }

    public string PrefabId => _savable.PrefabId;

    // 현재 상태를 JSON 문자열로 캡처한다.
    public string CaptureJson() => _savable.CaptureJson(transform);

    // JSON 문자열로 상태를 복원하고 다음 프레임에 지면 높이로 맞춘다.
    public void ApplyJson(string json)
    {
        _savable.ApplyJson(transform, json);
        StartCoroutine(HandleSnapToGroundNextFrame());
    }

    // 한 프레임 대기한 뒤 GroundSnapper가 있으면 지면 높이로 맞춘다.
    private IEnumerator HandleSnapToGroundNextFrame()
    {
        yield return null;
        if (TryGetComponent(out GroundSnapper snapper))
            snapper.SnapToGround();
    }
}
