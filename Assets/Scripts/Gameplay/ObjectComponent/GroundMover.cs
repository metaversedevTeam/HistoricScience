using System;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class GroundMover : MonoBehaviour, IMover
{
    // 에이전트가 내브메시 밖에 있을 때 주변에서 내브메시를 찾아볼 반경. 내브메시가 뒤늦게 구워지는 경우를 위한 값이라
    // 너무 크게 잡지 않는다. 이 범위 안에 내브메시가 없으면 실제로 이동할 수 없는 상태로 간주한다.
    private const float k_NavMeshWarpSearchRadius = 10f;

    // 목적지가 내브메시 위에 없을 때 주변에서 가장 가까운 내브메시 지점을 찾아볼 반경. Stone Source처럼
    // 카빙하는 오브젝트의 발밑 구멍(반경 약 2.5m)을 넉넉히 벗어날 수 있어야 한다.
    private const float k_DestinationSampleRadius = 10f;

    // 추적 중 경로 재계산 최소 간격(초). 동기 CalculatePath를 매 프레임 수행하지 않기 위한 값으로,
    // 도달 불가한 대상을 계속 재시도하는 비용도 이 간격으로 제한된다.
    private const float k_FollowRepathInterval = 0.25f;

    // 정지 판정에 쓰는 속도 제곱 임계값. 경로 끝에서 이 이하로 느려졌으면 실제로 멈춘 것으로 본다.
    private const float k_StoppedSqrVelocity = 0.01f;

    // 도착 판정 여유 거리(m). 로컬 회피에 밀리거나 감속 오차로 목적지에서 조금 벗어나 멈춰도 도착으로 인정한다.
    private const float k_ArrivalTolerance = 0.5f;

    // 추적 대상이 이만큼 움직였을 때만 경로를 다시 잡는다. 멈춰 있는 대상 옆에서 매 주기 경로를 새로 설정하면
    // 정지 판정이 계속 뒤집혀 이동 종료 이벤트가 반복 발생하므로 필요하다.
    private const float k_FollowRepathDistance = 0.25f;

    // 목적지 설정 후 이동 시작을 기다려주는 최대 시간(초). 이 안에 출발하지 않으면 이미 목적지에 서 있는
    // 것으로 보고 정지 판정을 시작한다. 경로만 잡히면 즉시 게이트가 열리므로 실제로는 거의 소모되지 않는다.
    private const float k_MoveStartGrace = 0.25f;

    // 목적지 XZ의 지면 높이를 구할 때 자신의 위치 기준 이 높이(m)만큼 위에서부터 아래로 레이를 쏜다. 터레인 최대 높이보다 충분히 커야 한다.
    private const float k_GroundRaycastUpDistance = 500f;

    // 지면 높이를 구하는 레이캐스트의 총 길이(m). 위 시작 높이를 지나 터레인 아래까지 닿을 만큼 충분히 커야 한다.
    private const float k_GroundRaycastLength = 1000f;

    // 실제 속도가 잠시 0으로 떨어져도 발소리를 바로 끊지 않고 기다려 주는 시간(초). 새 이동 명령으로 경로를 다시 잡는 동안
    // 한두 프레임 속도가 0이 되는데, 그때마다 소리를 끊으면 이동 명령이 연달아 들어올 때 발소리가 계속 처음부터 다시 재생된다.
    private const float k_WalkSfxStopGrace = 0.2f;

    // 방향 이동 입력을 이동으로 볼 최소 크기의 제곱. 아날로그 입력의 미세한 흔들림으로 걷기가 시작되지 않게 한다.
    private const float k_DirectMoveMinSqrInput = 0.0001f;

    // 벽 무시 이동을 끝내고 내브메시로 돌아올 때 가장 가까운 내브메시를 찾아볼 반경(m). 걸을 수 없는 지형 한가운데서 끝낼 수 있어 넉넉히 잡는다.
    private const float k_NoclipReturnSearchRadius = 50f;

    // 목적지의 지면 높이를 찾을 때 감지할 지면 레이어. 비워두면 Awake에서 "Ground" 레이어를 자동으로 찾는다.
    [SerializeField] private LayerMask _groundLayer;

    // 이동 속도에 곱해서 적용할 연구 보너스. 비워 두면 연구 보너스를 적용하지 않고 프리팹에 설정된 속도를 그대로 쓴다.
    [SerializeField] private ResearchBonusData _moveSpeedBonus;

    // 방향 이동 중 달릴 때 걷는 속도에 곱할 배율. FPVTest 브랜치의 걷기 5 / 달리기 9 비율을 따른다.
    [SerializeField, Min(1f)] private float _runSpeedMultiplier = 1.8f;

    // 이동하는 동안 계속 반복 재생할 걷는 소리. 비워 두면 발소리를 내지 않고, 처음 걸을 때 한 번 경고를 남긴다.
    [SerializeField] private AudioClip _walkSfx;

    // 걷는 소리에 적용할 볼륨 배율. AudioManager의 효과음 볼륨에 곱해진다.
    [SerializeField, Range(0f, 1f)] private float _walkSfxVolume = 1f;

    // 걷는 소리의 기본 재생 속도(피치). 1이면 클립 원본 속도로, 올리면 발걸음이 빨라진다.
    // 실제 재생 속도는 여기에 이동 속도 배율과 SetWalkSfxPitchScale로 지정한 배율을 곱해 구한다.
    [SerializeField, Range(0.1f, 3f)] private float _walkSfxPitch = 1f;

    // 새 이동 명령이 실제로 시작됐을 때 발생(Move()가 성공한 경우에만)
    public event Action OnMoveOrdered;

    // 요청한 목적지에 실제로 도달했을 때 발생
    public event Action OnArrived;

    // 이동이 어떤 방식으로든 끝나 멈췄을 때 발생(도착, 더 갈 수 없어 멈춤, Stop() 호출)
    public event Action OnMoveEnd;

    // 대기 중이던 콜백이 있는 이동이 새 Move() 명령으로 대체돼 취소됐을 때 발생
    public event Action OnMoveOrderReplaced;

    private NavMeshAgent  _agent;
    private Transform     _followTarget;
    private HitableObject _selfHitable;
    private float         _nextRepathTime;

    // 이동 명령이 살아 있는지. 이 값이 false면 종료 판정 자체를 하지 않는다.
    private bool    _hasMoveOrder;
    // 직전 프레임의 정지 여부. 움직임 → 정지로 바뀌는 순간에만 이벤트를 발생시키기 위한 값이다.
    private bool    _wasStopped;
    // 마지막으로 에이전트에 설정한 목적지(내브메시 투영 후)
    private Vector3 _requestedDestination;
    // 그 목적지가 요청한 지점 자체이고 경로도 끝까지 이어지는지. 도착과 "갈 수 있는 데까지만 감"을 구분한다.
    private bool    _destinationReachable;
    // 마지막 추적 경로를 계산할 때의 대상 위치. 대상이 충분히 움직였는지 판정하는 기준이다.
    private Vector3 _lastFollowSourcePos;
    // 목적지 설정 후 에이전트가 실제로 움직이기 시작했는지. 이 값이 false인 동안은 정지 판정을 하지 않는다.
    private bool    _hasStartedMoving;
    // 이동 시작을 기다려주는 시한. 이 시각을 넘기면 출발하지 않았어도 정지 판정을 시작한다.
    private float   _moveStartDeadline;
    // Move 호출자가 넘긴 도착 콜백. 그 호출로 시작된 이동에 대해서만 한 번 호출되고 비워진다.
    private Action  _pendingArrived;
    // Move(Vector2) 호출자가 넘긴 이동 종료 콜백. 그 호출로 시작된 이동에 대해서만 한 번 호출되고 비워진다.
    private Action  _pendingMoveEnd;
    // 이동 명령 세대 번호. Move나 Stop마다 증가시켜, 콜백을 호출하는 도중 새 명령이 들어온 경우를 구분한다.
    private int     _moveOrderId;
    // 연구 보너스를 곱하기 전의 기본 이동 속도. 보너스가 바뀔 때마다 이 값에서 다시 계산한다.
    private float   _baseSpeed;
    // 치트 배율을 곱하기 전의 기본 가속도. 빨라진 속도에 금방 도달하도록 치트 배율만큼 함께 키운다.
    private float   _baseAcceleration;
    // 프리팹에 설정된 에이전트의 기본 높이 오프셋. Y오프셋 치트는 이 값에 더해진다.
    private float   _defaultBaseOffset;
    // Y오프셋 치트로 transform이 발밑보다 더 떠 있는 높이(m). 발밑 위치를 구할 때 transform 높이에서 뺀다.
    private float   _heightOffset;
    // Y오프셋이 최댓값이라 transform을 월드 Y 최댓값에 직접 고정하고 있는지
    private bool    _isPinnedToMaxHeight;
    // 재생 중인 걷는 소리를 가리키는 손잡이. 발소리를 내고 있지 않으면 null이다.
    private AudioManager.LoopSfxHandle _walkSfxHandle;
    // 기본 재생 속도에 곱해 쓰는 배율. 밖에서 SetWalkSfxPitchScale로 바꾼다.
    private float   _walkSfxPitchScale = 1f;
    // 속도가 떨어졌을 때 발소리를 실제로 멈출 시각. 그 전에 다시 움직이면 소리를 끊지 않고 이어 간다.
    private float   _walkSfxStopTime;
    // 걷는 소리가 비어 있다고 이미 경고했는지. 걷는 동안 매 프레임 확인하므로 한 번만 알리기 위한 값이다.
    private bool    _hasWarnedMissingWalkSfx;
    // 방향 입력으로 직접 걷는 중인지. 이 이동은 경로를 쓰지 않아 에이전트 속도가 0이므로, 걷는 중인지는 이 값으로 판정한다.
    private bool    _isDirectMoving;
    // 마지막으로 방향 이동 입력을 받은 프레임. 입력이 끊긴 프레임을 찾아 방향 이동을 끝내는 기준이다.
    private int     _lastDirectMoveFrame;
    // 에이전트에 원래 설정돼 있던 자동 회전 여부. 방향 이동 동안 끈 회전을 경로 이동으로 돌아갈 때 되돌린다.
    private bool    _defaultUpdateRotation;
    // 방향 이동으로 달리는 중인지. 달리기 배율을 이동량과 MoveSpeedRatio에 반영하는 기준이다.
    private bool    _isRunning;
    // 벽 무시 치트로 에이전트의 위치 동기화를 끄고 transform을 직접 옮기고 있는지
    private bool    _isNoclipping;

    // NavMeshAgent와 HitableObject 컴포넌트를 캐싱하고 지면 레이어와 기본 이동 속도를 확보
    private void Awake()
    {
        _agent       = GetComponent<NavMeshAgent>();
        _selfHitable = GetComponent<HitableObject>();
        _baseSpeed   = _agent.speed;
        _baseAcceleration = _agent.acceleration;
        _defaultBaseOffset = _agent.baseOffset;
        _defaultUpdateRotation = _agent.updateRotation;

        HandleResolveGroundLayer();
    }

    // Ground 레이어가 미설정된 경우 자동으로 찾아 할당한다.
    private void HandleResolveGroundLayer()
    {
        if (_groundLayer.value != 0)
            return;

        int idx = LayerMask.NameToLayer("Ground");
        if (idx >= 0)
            _groundLayer = 1 << idx;
        else
            Debug.LogWarning("[GroundMover] 'Ground' 레이어를 찾을 수 없습니다. Inspector에서 직접 설정해주세요.");
    }

    // 활성화 시 자신을 내브메시 베이커의 추적 대상으로 등록해 주변 내브메시가 구워지게 하고, 연구 보너스와 치트 배율을 반영한다
    private void OnEnable()
    {
        if (DynamicNavMeshBaker.Instance != null)
            DynamicNavMeshBaker.Instance.AddTarget(transform);

        HandleSubscribeMoveSpeedBonus();
        MoveSpeedCheat.OnMultiplierChanged += ApplyMoveSpeed;
        ApplyMoveSpeed();
    }

    // 비활성화 시 내브메시 베이커의 추적 대상에서 자신을 제거하고 연구 보너스·치트 배율 구독과 내고 있던 걷는 소리를 정리한다
    private void OnDisable()
    {
        if (DynamicNavMeshBaker.Instance != null)
            DynamicNavMeshBaker.Instance.RemoveTarget(transform);

        HandleUnsubscribeMoveSpeedBonus();
        MoveSpeedCheat.OnMultiplierChanged -= ApplyMoveSpeed;
        StopWalkSfx();
    }

    // 인스펙터에서 값을 바꾸면 이미 나고 있는 발소리에도 바로 반영해, 플레이 중에 재생 속도를 들어 보며 맞출 수 있게 한다
    private void OnValidate()
    {
        ApplyWalkSfxPitch();
    }

    // 이동 속도 보너스가 지정되어 있으면 갱신 이벤트를 구독한다. 현재 합계는 OnEnable의 ApplyMoveSpeed에서 반영한다.
    private void HandleSubscribeMoveSpeedBonus()
    {
        if (_moveSpeedBonus == null) return;

        ResearchManager.Instance.OnBonusesChanged += ApplyMoveSpeed;
    }

    // 구독해 둔 연구 보너스 갱신 이벤트를 해제한다.
    private void HandleUnsubscribeMoveSpeedBonus()
    {
        if (_moveSpeedBonus == null) return;

        ResearchManager.Instance.OnBonusesChanged -= ApplyMoveSpeed;
    }

    // 기본 속도에 연구 보너스와 치트 배율을 곱해 에이전트 속도에 반영하고, 가속도는 치트 배율만큼 키운다.
    private void ApplyMoveSpeed()
    {
        float bonus = _moveSpeedBonus != null ? ResearchManager.Instance.GetMultiplier(_moveSpeedBonus) : 1f;
        float cheat = MoveSpeedCheat.Multiplier;

        _agent.speed        = _baseSpeed * bonus * cheat;
        _agent.acceleration = _baseAcceleration * cheat;
        ApplyWalkSfxPitch();
    }

    // 추적 대상이 있으면 따라가기를 처리하고, 매 프레임 이동 종료 여부와 걷는 소리 재생 여부를 갱신
    private void Update()
    {
        if (_followTarget != null)
            HandleFollow();

        HandleMoveEnd();
        HandleWalkSfx();
    }

    // 이번 프레임에 방향 이동 입력이 들어오지 않았으면 방향 이동을 끝낸다. 입력을 주는 쪽의 Update보다 늦게 판정하려고 LateUpdate에서 처리한다.
    private void LateUpdate()
    {
        if (_isDirectMoving && _lastDirectMoveFrame != Time.frameCount)
            EndDirectMove(true);

        HandleHeightOffset();
    }

    // Y오프셋 치트 값을 반영한다. 0~499는 에이전트 높이 오프셋(baseOffset)으로 띄우고, 최댓값이면 transform을 월드 Y 최댓값에 직접 고정한다.
    // 최댓값일 때 baseOffset으로 따라가면 땅 높이가 바뀔 때마다 매 프레임 baseOffset을 바꾸게 되는데, 그때마다 에이전트가 다시 배치되며
    // 그 프레임의 이동이 취소돼 제자리에 묶인다. 그래서 고정 높이는 baseOffset을 건드리지 않고 위치 동기화를 끈 채 직접 배치한다.
    private void HandleHeightOffset()
    {
        bool pinned = HeightOffsetCheat.IsPinnedToMax;
        if (pinned != _isPinnedToMaxHeight)
        {
            _isPinnedToMaxHeight = pinned;
            RefreshUpdatePosition();
        }

        // 고정 높이에서는 baseOffset을 기본값으로 두고, 아니면 치트 오프셋만큼 더한다. 값이 그대로면 다시 설정하지 않는다.
        float baseOffset = _defaultBaseOffset + (pinned ? 0f : HeightOffsetCheat.Offset);
        if (!Mathf.Approximately(_agent.baseOffset, baseOffset))
            _agent.baseOffset = baseOffset;

        if (_isNoclipping)
        {
            // 벽 무시 이동은 transform을 직접 옮기므로, 이번 프레임 발밑 높이 위에 원하는 만큼 다시 띄운다.
            float footY = GroundPosition.y;
            _heightOffset = pinned ? HeightOffsetCheat.MaxOffset - footY : HeightOffsetCheat.Offset;
            SetTransformHeight(footY + _heightOffset);
            return;
        }

        if (!pinned)
        {
            _heightOffset = HeightOffsetCheat.Offset;
            return;
        }

        // 위치 동기화를 끈 동안에도 에이전트는 내브메시 위에서 계속 이동하므로, 그 XZ를 따라가며 높이만 최댓값에 고정한다.
        Vector3 agentPosition = _agent.nextPosition;
        _heightOffset = HeightOffsetCheat.MaxOffset - agentPosition.y;
        transform.position = new Vector3(agentPosition.x, HeightOffsetCheat.MaxOffset, agentPosition.z);
    }

    // transform의 XZ는 그대로 두고 높이만 바꾼다.
    private void SetTransformHeight(float y)
    {
        Vector3 position = transform.position;
        position.y = y;
        transform.position = position;
    }

    // 벽 무시 이동이나 고정 높이처럼 transform을 직접 배치하는 동안에는 에이전트 위치 동기화를 끄고, 아니면 켠다.
    private void RefreshUpdatePosition()
    {
        _agent.updatePosition = !_isNoclipping && !_isPinnedToMaxHeight;
    }

    // Y오프셋 치트로 떠 있는 높이를 뺀 발밑 위치. 내브메시와 지면 질의는 떠 있는 transform 위치 대신 이 위치를 기준으로 한다.
    private Vector3 GroundPosition => transform.position - Vector3.up * _heightOffset;

    // 이번 프레임 동안 월드 XZ 방향으로 직접 걷는다. 걸을 수 없는 지형 쪽은 막힌 축을 빼고 미끄러지듯 걷고, 내브메시 밖으로는 에이전트가 나가지 않는다.
    public void MoveInDirection(Vector2 direction, bool isRunning = false)
    {
        if (direction.sqrMagnitude < k_DirectMoveMinSqrInput)
            return;

        bool ignoreWalls = WallIgnoreCheat.IsEnabled;
        if (!ignoreWalls)
            EndNoclip();

        if (!ignoreWalls && !EnsureOnNavMesh())
            return;

        if (!_isDirectMoving)
            BeginDirectMove();

        _lastDirectMoveFrame = Time.frameCount;
        SetRunning(isRunning);

        Vector2 clamped = Vector2.ClampMagnitude(direction, 1f);
        Vector3 delta   = new Vector3(clamped.x, 0f, clamped.y) * (_agent.speed * GetRunFactor() * Time.deltaTime);

        if (ignoreWalls)
        {
            MoveIgnoringWalls(delta);
            return;
        }

        delta = ResolveSlidingDelta(delta, CanStepTo);

        if (delta.sqrMagnitude > 0f)
            _agent.Move(delta);
    }

    // 벽 무시 치트로 걸을 수 없는 지형과 내브메시 경계를 무시하고 transform을 직접 옮긴다. 로딩된 지형 밖으로는 나가지 않으며,
    // 높이는 그 자리의 지면(물 위라면 수면)에 맞춘다.
    private void MoveIgnoringWalls(Vector3 delta)
    {
        BeginNoclip();

        delta = ResolveSlidingDelta(delta, CanNoclipTo);
        if (delta.sqrMagnitude <= 0f)
            return;

        Vector3 next = GroundPosition + delta;
        if (!TryGetLoadedSurfaceHeight(new Vector2(next.x, next.z), out float surfaceY))
            return;

        // 고정 높이라면 새 발밑 기준으로 띄울 높이를 곧바로 다시 구해, 이 프레임 중간에도 transform이 최댓값에서 벗어나지 않게 한다.
        float footY = surfaceY + _defaultBaseOffset;
        if (_isPinnedToMaxHeight)
            _heightOffset = HeightOffsetCheat.MaxOffset - footY;

        transform.position = new Vector3(next.x, footY + _heightOffset, next.z);

        // 에이전트 내부 위치도 최대한 따라오게 해, 벽 무시를 끝낼 때 내브메시로 돌아오는 거리를 줄인다. (내브메시 위로 고정된다)
        if (_agent.isOnNavMesh)
            _agent.nextPosition = transform.position;
    }

    // 벽 무시 이동을 시작한다. 에이전트가 transform을 내브메시 위로 끌어당기지 않도록 위치 동기화를 끈다.
    private void BeginNoclip()
    {
        if (_isNoclipping)
            return;

        _isNoclipping = true;
        RefreshUpdatePosition();
    }

    // 벽 무시 이동을 끝내고 내브메시 이동으로 돌아온다. 지금 발밑에서 가장 가까운 내브메시로 옮긴 뒤 위치 동기화를 되돌린다.
    private void EndNoclip()
    {
        if (!_isNoclipping)
            return;

        _isNoclipping = false;

        if (NavMesh.SamplePosition(GroundPosition, out NavMeshHit hit, k_NoclipReturnSearchRadius, NavMesh.AllAreas))
            _agent.Warp(hit.position);

        RefreshUpdatePosition();
    }

    // 방향 이동을 시작한다. 진행 중이던 경로 이동·추적은 대체된 것이므로 콜백을 버리고 경로를 지운 뒤, 바라보는 방향이 바뀌지 않게 자동 회전을 끈다.
    private void BeginDirectMove()
    {
        ReplaceMoveOrder();

        _followTarget         = null;
        _hasMoveOrder         = false;
        _wasStopped           = false;
        _hasStartedMoving     = false;
        _destinationReachable = false;
        // 벽 무시 이동으로 시작하면 에이전트가 아직 내브메시에 올라가 있지 않을 수 있다.
        if (_agent.isOnNavMesh)
            _agent.ResetPath();
        _agent.updateRotation = false;

        _isDirectMoving = true;
        OnMoveOrdered?.Invoke();
    }

    // 방향 이동 상태를 끝내고 자동 회전을 되돌린다. 입력이 끊겨 멈춘 경우에만 OnMoveEnd를 발생시키고, 새 경로 이동으로 바뀐 경우는 이동이 이어지므로 발생시키지 않는다.
    private void EndDirectMove(bool raiseMoveEnd)
    {
        if (!_isDirectMoving)
            return;

        _isDirectMoving       = false;
        _agent.updateRotation = _defaultUpdateRotation;
        SetRunning(false);

        if (raiseMoveEnd)
            OnMoveEnd?.Invoke();
    }

    // 달리기 상태를 바꾸고, 바뀌었으면 발소리 재생 속도에 바로 반영한다.
    private void SetRunning(bool isRunning)
    {
        if (_isRunning == isRunning)
            return;

        _isRunning = isRunning;
        ApplyWalkSfxPitch();
    }

    // 지금 이동 속도에 곱할 달리기 배율을 반환한다. 달리지 않으면 1이다.
    private float GetRunFactor()
    {
        return _isRunning ? _runSpeedMultiplier : 1f;
    }

    // 이번 프레임 이동량 중 canStep이 허락하는 부분만 남긴다. 그대로 갈 수 없으면 X·Z 축 하나씩만 따로 시도해 벽을 따라 미끄러지게 하고, 둘 다 막히면 움직이지 않는다.
    private static Vector3 ResolveSlidingDelta(Vector3 delta, Func<Vector3, bool> canStep)
    {
        if (canStep(delta))
            return delta;

        Vector3 deltaX = new Vector3(delta.x, 0f, 0f);
        if (canStep(deltaX))
            return deltaX;

        Vector3 deltaZ = new Vector3(0f, 0f, delta.z);
        if (canStep(deltaZ))
            return deltaZ;

        return Vector3.zero;
    }

    // 벽 무시 이동으로 delta만큼 갔을 때 몸 앞쪽 끝이 로딩된 지형 위에 있는지 판정한다. 걸을 수 있는지는 따지지 않는다.
    private bool CanNoclipTo(Vector3 delta)
    {
        if (delta.sqrMagnitude <= 0f)
            return false;

        Vector3 probe = GroundPosition + delta + delta.normalized * _agent.radius;
        return TryGetLoadedSurfaceHeight(new Vector2(probe.x, probe.z), out _);
    }

    // 월드 XZ 아래에 로딩된 지형(청크의 터레인 콜라이더)이 있으면 서 있을 높이를 구한다. 지면이 수면보다 낮으면 수면 높이를 쓴다.
    // 청크가 로딩되지 않은 곳에는 콜라이더가 없어 레이가 닿지 않으므로, 이 판정이 곧 "로딩된 맵 안인지"가 된다.
    private bool TryGetLoadedSurfaceHeight(Vector2 worldXZ, out float height)
    {
        if (!TryRaycastGround(worldXZ, out height))
            return false;

        MapData mapData = MapSaveUtil.Instance != null ? MapSaveUtil.Instance.CurrentMapData : null;
        if (mapData != null)
            height = Mathf.Max(height, mapData.SeaLevel);

        return true;
    }

    // 월드 XZ의 지면 높이를 위에서 아래로 레이캐스트해 구한다. 지면이 없으면 false를 반환한다.
    private bool TryRaycastGround(Vector2 worldXZ, out float height)
    {
        Vector3 origin = new Vector3(worldXZ.x, GroundPosition.y + k_GroundRaycastUpDistance, worldXZ.y);

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, k_GroundRaycastLength, _groundLayer))
        {
            height = hit.point.y;
            return true;
        }

        height = 0f;
        return false;
    }

    // 지금 위치에서 delta만큼 걸었을 때 몸 앞쪽 끝(에이전트 반경만큼 앞)이 걸을 수 있는 지형에 있는지 판정한다.
    private bool CanStepTo(Vector3 delta)
    {
        if (delta.sqrMagnitude <= 0f)
            return false;

        Vector3 probe = GroundPosition + delta + delta.normalized * _agent.radius;
        return IsWalkableWorldPosition(probe);
    }

    // 월드 위치가 맵 데이터 기준으로 걸을 수 있는 지형인지 판정한다. 맵 데이터를 아직 쓸 수 없으면 막지 않고, 내브메시 제한에만 맡긴다.
    private bool IsWalkableWorldPosition(Vector3 worldPosition)
    {
        MapSaveUtil mapSaveUtil = MapSaveUtil.Instance;
        if (mapSaveUtil == null)
            return true;

        MapData mapData = mapSaveUtil.CurrentMapData;
        if (mapData == null)
            return true;

        return mapData.IsWalkable(mapSaveUtil.WorldToMapPosition(worldPosition));
    }

    // 이동 명령이 살아 있는 동안 정지 상태로 바뀌는 순간에만 이벤트를 발생시킨다. 목적지에 실제로 닿았으면
    // OnArrived를 먼저 발생시키고, 어떤 경우든 OnMoveEnd를 이어서 발생시킨다.
    // 이동을 명령한 쪽에 넘겨받은 콜백은 이벤트보다 먼저 호출한다.
    private void HandleMoveEnd()
    {
        if (!_hasMoveOrder)
            return;

        if (!TryBeginStopCheck())
            return;

        bool stopped     = IsStopped();
        bool justStopped = stopped && !_wasStopped;
        _wasStopped = stopped;

        if (!justStopped)
            return;

        bool arrived = HasReachedDestination();

        // 구독자가 이벤트나 콜백 안에서 곧바로 새 이동을 명령할 수 있으므로 상태를 먼저 정리하고 호출한다.
        // 콜백은 이 이동에만 속하므로, 지금 세대를 기억해 두고 새 명령으로 세대가 바뀌면 호출하지 않는다.
        int orderId = _moveOrderId;

        // 추적 이동은 대상이 다시 멀어지면 계속 따라가야 하므로 명령을 유지한다. 위치 이동은 여기서 끝난다.
        if (_followTarget == null)
            _hasMoveOrder = false;

        // 콜백은 이 이동을 명령한 쪽의 후속 처리이므로 이벤트보다 먼저 호출한다. 이벤트를 먼저 발생시키면
        // 구독자가 그 안에서 새 이동을 명령했을 때, 조건이 이미 충족된 콜백이 무관한 제3자 때문에 취소된다.
        if (arrived)
            InvokePendingCallback(ref _pendingArrived, orderId);
        else if (_followTarget == null)
            // 도착하지 못한 채 명령이 끝났으므로 도착 콜백은 호출될 기회가 없다. 추적은 명령이 살아 있어
            // 막혀서 멈춘 뒤에도 다시 따라갈 수 있으므로, 그때는 버리지 않고 다음 도달까지 들고 간다.
            _pendingArrived = null;

        InvokePendingCallback(ref _pendingMoveEnd, orderId);

        if (arrived)
            OnArrived?.Invoke();

        OnMoveEnd?.Invoke();
    }

    // 이동 명령 세대가 그대로일 때만 대기 중인 콜백을 꺼내 비우고 호출한다. 세대가 바뀌었으면 그 콜백은 이미
    // 폐기됐거나 새 명령에 속한 것이므로 이 이동의 종료로 호출해서는 안 된다.
    private void InvokePendingCallback(ref Action pending, int orderId)
    {
        if (_moveOrderId != orderId)
            return;

        Action callback = pending;
        pending = null;
        callback?.Invoke();
    }

    // 새 이동 명령이 이전 명령을 대체할 때 호출한다. 이전 명령의 콜백을 버리고, 그 결과를 기다리던 쪽에 취소를 알린다.
    // 버릴 콜백이 없었으면 알려 줄 대상도 없으므로 이벤트를 발생시키지 않는다.
    private void ReplaceMoveOrder()
    {
        bool hadPendingCallback = _pendingArrived != null || _pendingMoveEnd != null;

        DiscardPendingCallbacks();

        if (hadPendingCallback)
            OnMoveOrderReplaced?.Invoke();
    }

    // 대기 중인 콜백을 호출하지 않고 버리며 이동 명령 세대를 넘긴다. 새 Move나 Stop으로 이전 이동이 무효가 될 때 호출한다.
    private void DiscardPendingCallbacks()
    {
        _moveOrderId++;
        _pendingArrived = null;
        _pendingMoveEnd = null;
    }

    // 정지 판정을 시작해도 되는지 확인한다. SetDestination 직후 몇 프레임은 경로 코너가 아직 준비되지 않아
    // remainingDistance가 0으로 나오는데, 그 상태는 "출발 전"과 "도착 후"가 구분되지 않아 그대로 판정하면
    // 이동하자마자 종료로 오인한다. 그래서 실제로 출발한 것이 확인될 때까지 판정을 미룬다.
    private bool TryBeginStopCheck()
    {
        if (_hasStartedMoving)
            return true;

        if (HasAgentStartedMoving())
        {
            _hasStartedMoving = true;
            return true;
        }

        // 이미 목적지에 서 있으면 영영 출발하지 않으므로, 유예 시간이 지나면 그대로 정지 판정으로 넘긴다.
        return Time.time >= _moveStartDeadline;
    }

    // 경로 계산이 끝나고 에이전트가 실제로 목적지를 향해 움직이기 시작했는지 판정
    private bool HasAgentStartedMoving()
    {
        if (!_agent.isOnNavMesh || _agent.pathPending)
            return false;

        if (_agent.velocity.sqrMagnitude > k_StoppedSqrVelocity)
            return true;

        // 남은 거리가 멈춤 거리를 넘었다는 건 경로 코너가 준비됐고 아직 갈 길이 남았다는 뜻이다.
        return _agent.hasPath && _agent.remainingDistance > _agent.stoppingDistance;
    }

    // 목적지를 새로 설정한 뒤 경로 코너가 준비될 때까지 정지 판정을 미루는 대기 상태로 되돌린다.
    private void BeginDestinationSettle()
    {
        _hasStartedMoving  = false;
        _moveStartDeadline = Time.time + k_MoveStartGrace;
    }

    // 에이전트가 이동을 끝내고 멈췄는지 판정. 경로가 사라졌거나, 경로 끝(멈춤 거리 안)에서 속도가 0에 수렴하면
    // 정지로 본다. 경로 도중에 다른 유닛에 막혀 잠시 멈춘 경우는 곧 다시 움직이므로 정지로 보지 않는다.
    private bool IsStopped()
    {
        if (!_agent.isOnNavMesh)
            return true;

        if (_agent.pathPending)
            return false;

        if (!_agent.hasPath)
            return true;

        if (_agent.remainingDistance > _agent.stoppingDistance)
            return false;

        return _agent.velocity.sqrMagnitude <= k_StoppedSqrVelocity;
    }

    // 멈춘 지점이 요청한 목적지인지 판정한다. 경로가 부분 경로였거나 목적지가 내브메시 밖이라 가장자리까지만
    // 간 경우를 도착과 구분하기 위해, 목적지까지 갈 수 있었는지와 실제 XZ 거리를 함께 본다.
    private bool HasReachedDestination()
    {
        if (!_destinationReachable)
            return false;

        Vector2 currentXZ     = new Vector2(transform.position.x, transform.position.z);
        Vector2 destinationXZ = new Vector2(_requestedDestination.x, _requestedDestination.z);

        return Vector2.Distance(currentXZ, destinationXZ) <= _agent.stoppingDistance + k_ArrivalTolerance;
    }

    // 추적 대상의 경로를 주기적으로 재계산한다. 대상이 일시적으로 도달 불가해도 추적을 버리지 않고 다음 주기에
    // 재시도해, 내브메시가 뒤늦게 구워지거나 대상이 범위 안으로 돌아오면 이어서 따라간다.
    private void HandleFollow()
    {
        if (Time.time < _nextRepathTime)
            return;
        _nextRepathTime = Time.time + k_FollowRepathInterval;

        if ((_followTarget.position - _lastFollowSourcePos).sqrMagnitude <= k_FollowRepathDistance * k_FollowRepathDistance)
            return;

        TrySetFollowDestination();
    }

    // 추적 대상 쪽으로 목적지와 멈춤 거리를 계산해 에이전트에 설정; 목적지를 잡지 못하면 false 반환
    private bool TrySetFollowDestination()
    {
        // 실패하면 다음 주기에 반드시 다시 시도하도록 기준 위치를 무효화해 둔다.
        _lastFollowSourcePos = Vector3.positiveInfinity;

        if (!EnsureOnNavMesh())
            return false;

        if (!TryResolveDestination(_followTarget.position, out Vector3 destination, out bool isExact))
            return false;

        NavMeshPath path = new NavMeshPath();
        if (!_agent.CalculatePath(destination, path) || !IsPathUsable(path))
            return false;

        _agent.stoppingDistance = GetStoppingDistance(_followTarget, destination);
        _agent.SetDestination(destination);

        _requestedDestination = destination;
        _destinationReachable = isExact && path.status == NavMeshPathStatus.PathComplete;
        _lastFollowSourcePos  = _followTarget.position;
        BeginDestinationSettle();
        return true;
    }

    // 에이전트가 아직 내브메시 위에 있지 않으면 주변에서 가장 가까운 내브메시로 옮겨 놓는다. 내브메시가 동적으로
    // 늦게 구워지는 구조에서는 스폰 시점에 에이전트가 내브메시를 못 찾아 영구히 "오프메시" 상태로 남을 수 있어 필요하다.
    private bool EnsureOnNavMesh()
    {
        if (_agent.isOnNavMesh){
            return true;}

        if (NavMesh.SamplePosition(GroundPosition, out NavMeshHit hit, k_NavMeshWarpSearchRadius, NavMesh.AllAreas))
        {
            _agent.Warp(hit.position);
            return true;
        }
        return false;
    }

    // 경로가 목적지까지 완전하지 않아도(PathPartial) 갈 수 있는 데까지는 이동을 시도한다. 경사가 너무 가파른
    // 지형처럼 일부만 막힌 경우, 유닛이 그 앞까지 실제로 걸어가서 멈추는 편이 아무 반응이 없는 것보다 명확하다.
    // 아예 경로가 없는 경우(PathInvalid)만 이동 불가로 취급한다.
    private static bool IsPathUsable(NavMeshPath path)
    {
        return path.status != NavMeshPathStatus.PathInvalid;
    }

    // 목적지를 내브메시 위 좌표로 투영한다. CalculatePath는 내브메시에서 조금만 벗어난 목적지(카빙된 구멍 안,
    // 베이크 범위 밖 등)도 매핑하지 못해 통째로 실패하므로, 먼저 주변에서 가장 가까운 내브메시 지점을 찾고,
    // 그마저 없으면 에이전트에서 목적지 방향으로 걸어서 닿을 수 있는 마지막 지점(내브메시 가장자리)을 반환해
    // 아무 행동도 하지 않는 대신 갈 수 있는 데까지는 이동하게 한다. EnsureOnNavMesh 이후에 호출해야 한다.
    // isExact는 요청한 지점 자체를 투영한 결과인지를 알려, 가장자리까지만 가는 경우를 도착으로 세지 않게 한다.
    private bool TryResolveDestination(Vector3 desired, out Vector3 resolved, out bool isExact)
    {
        if (NavMesh.SamplePosition(desired, out NavMeshHit sampleHit, k_DestinationSampleRadius, NavMesh.AllAreas))
        {
            resolved = sampleHit.position;
            isExact  = true;
            return true;
        }

        isExact = false;

        // Raycast는 선이 내브메시 가장자리에서 끊기면 그 지점을, 끝까지 이어지면 매핑된 도착점을 hit에 채운다.
        // 어느 쪽이든 위치가 유한하면 그대로 쓸 수 있고, 무한대면 투영 자체가 실패한 것이다.
        NavMesh.Raycast(GroundPosition, desired, out NavMeshHit rayHit, NavMesh.AllAreas);
        if (!float.IsInfinity(rayHit.position.x))
        {
            resolved = rayHit.position;
            return true;
        }

        resolved = desired;
        return false;
    }

    // 자신과 대상의 충돌 반경 합산으로 멈춤 거리를 계산한다. 목적지가 대상 위치에서 내브메시 위로 밀려나
    // 투영된 경우 그 오프셋만큼 빼서, 실제 대상 기준으로는 원래 의도한 거리에서 멈추게 한다.
    private float GetStoppingDistance(Transform target, Vector3 destination)
    {
        float selfRadius   = _selfHitable != null ? _selfHitable.HitRadius : 0f;
        var   targetHitable = target.GetComponent<HitableObject>();
        float targetRadius = targetHitable != null ? targetHitable.HitRadius : 0f;

        Vector2 targetXZ      = new Vector2(target.position.x, target.position.z);
        Vector2 destinationXZ = new Vector2(destination.x, destination.z);
        float projectionOffset = Vector2.Distance(targetXZ, destinationXZ);

        return Mathf.Max(0f, selfRadius + targetRadius - projectionOffset);
    }

    // start부터 루트까지 추적 체인을 순회해 자신이 포함되면 true 반환
    private bool IsInFollowChain(Transform start)
    {
        var current = start;
        while (current != null)
        {
            if (current == transform) return true;
            var mover = current.GetComponent<GroundMover>();
            current = mover?._followTarget;
        }
        return false;
    }

    // 새 이동 명령이 시작될 때 종료 판정 상태를 초기화한다. 이전 이동은 취소된 것이므로 종료 이벤트는 발생시키지 않는다.
    private void BeginMoveOrder()
    {
        _hasMoveOrder = true;
        _wasStopped   = false;
        BeginDestinationSettle();

        OnMoveOrdered?.Invoke();
    }

    // 에이전트가 실제로 움직이는 동안에만 걷는 소리가 나게 한다. 이동 명령의 시작·종료가 아니라 실제 속도를 기준으로 삼아,
    // 추적 중 대상 옆에 멈췄다가 다시 따라가는 경우나 다른 유닛에 막혀 잠시 멈춰 선 순간까지 소리가 실제 움직임과 어긋나지 않게 한다.
    // 다만 속도가 떨어졌다고 곧바로 끊지는 않는다. 그러면 이동 명령이 연달아 들어와 경로를 다시 잡는 한두 프레임마다
    // 소리가 끊겨, 이미 재생 중이던 발소리가 계속 처음부터 다시 재생되기 때문이다.
    private void HandleWalkSfx()
    {
        if (IsWalking())
        {
            _walkSfxStopTime = Time.time + k_WalkSfxStopGrace;
            PlayWalkSfx();
            return;
        }

        // 기다려 주는 시간 안에 다시 움직이면 소리를 그대로 이어 가고, 그 뒤에도 멈춰 있으면 실제로 멈춘다.
        if (Time.time < _walkSfxStopTime) return;

        StopWalkSfx();
    }

    // 발소리를 낼 만큼 에이전트가 실제로 움직이고 있는지 판정한다. 정지 판정과 같은 속도 임계값을 쓴다.
    private bool IsWalking()
    {
        if (_isDirectMoving)
            return true;

        if (!_agent.isOnNavMesh)
            return false;

        return _agent.velocity.sqrMagnitude > k_StoppedSqrVelocity;
    }

    // 걷는 소리의 기본 재생 속도에 곱할 배율을 지정한다. 발소리를 내고 있는 중이면 곧바로 반영된다.
    public void SetWalkSfxPitchScale(float scale)
    {
        _walkSfxPitchScale = Mathf.Max(0f, scale);
        ApplyWalkSfxPitch();
    }

    // 지금 재생 중인 걷는 소리에 계산된 재생 속도를 반영한다. 발소리를 내고 있지 않으면 다음에 시작할 때 적용된다.
    private void ApplyWalkSfxPitch()
    {
        if (_walkSfxHandle == null) return;

        AudioManager.SetLoopPitch(_walkSfxHandle, GetWalkSfxPitch());
    }

    // 인스펙터에 지정한 기본 재생 속도에 밖에서 지정한 배율과 이동 속도 배율을 곱해 실제 재생 속도를 구한다.
    private float GetWalkSfxPitch()
    {
        return _walkSfxPitch * _walkSfxPitchScale * MoveSpeedRatio;
    }

    // 프리팹에 설정된 기본 이동 속도 대비 지금 이동 속도의 배율. 연구 보너스·치트 배율·달리기 배율이 곱해진 값이라, 걸음이 빨라지면
    // 발소리와 걷는 애니메이션도 이 배율만큼 빨라진다. 기본 속도를 아직 알 수 없으면 배율을 따질 수 없으므로 1로 본다.
    public float MoveSpeedRatio
    {
        get
        {
            if (_baseSpeed <= 0f)
                return GetRunFactor();

            return _agent.speed / _baseSpeed * GetRunFactor();
        }
    }

    // 걷는 소리를 반복 재생하기 시작한다. 이미 내고 있으면 그대로 두므로, 움직이는 내내 매 프레임 불러도 소리가 처음부터 다시 시작되지 않는다.
    private void PlayWalkSfx()
    {
        if (_walkSfx == null)
        {
            WarnMissingWalkSfx();
            return;
        }
        if (_walkSfxHandle != null && _walkSfxHandle.IsPlaying) return;

        _walkSfxHandle = AudioManager.PlayLoop(_walkSfx, transform, _walkSfxVolume, GetWalkSfxPitch());
    }

    // 걷는 소리가 할당되지 않았다는 경고를 이 오브젝트에서 처음 한 번만 남긴다.
    private void WarnMissingWalkSfx()
    {
        if (_hasWarnedMissingWalkSfx) return;

        _hasWarnedMissingWalkSfx = true;
        Debug.LogWarning($"[{nameof(GroundMover)}] {name}의 {nameof(_walkSfx)}에 클립이 할당되지 않아 발소리를 내지 않는다.", this);
    }

    // 내고 있던 걷는 소리를 멈춘다.
    private void StopWalkSfx()
    {
        if (_walkSfxHandle == null) return;

        AudioManager.StopLoop(_walkSfxHandle);
        _walkSfxHandle = null;
    }

    // 목적지 XZ의 지면 높이를 아래로 레이캐스트해 구한다. 맵이 청크 터레인 여러 개로 이루어져 있어 Terrain.activeTerrain은
    // 목적지가 속하지 않은 다른 청크를 가리킬 수 있고, 그 청크의 SampleHeight는 가장자리 값으로 잘려 수십 m씩 어긋난 높이를
    // 돌려준다. 그 높이로는 목적지가 내브메시에서 수직으로 멀어져 NavMesh.SamplePosition이 실패하므로 반드시 레이캐스트로 구한다.
    // 지면을 찾지 못하면 자신의 현재 높이를 쓴다. 자신은 지면 위에 서 있으므로 0보다 실제 지면에 가깝다.
    private float GetGroundHeight(Vector2 targetPos)
    {
        return TryRaycastGround(targetPos, out float height) ? height : GroundPosition.y;
    }

    // 지정 위치로 이동; 위치가 내브메시 밖이면 갈 수 있는 가장 가까운 지점까지 이동하고, 이동 자체가 불가능하면 false 반환
    // onArrived/onMoveEnd는 이 호출로 시작된 이동에 대해서만 한 번 호출된다
    public bool Move(Vector2 targetPos, Action onArrived = null, Action onMoveEnd = null, float stoppingDistance = 0f)
    {
        // 명령이 들어온 시점에 이전 이동은 취소된 것이므로, 이 호출이 실패로 끝나더라도 이전 콜백을 되살리지 않는다.
        ReplaceMoveOrder();

        _followTarget = null;

        // 경로 이동은 내브메시 위에서만 할 수 있으므로 벽 무시 이동 중이었다면 내브메시로 돌아온다.
        EndNoclip();

        if (!EnsureOnNavMesh())
            return false;

        Vector3 desired = new Vector3(targetPos.x, GetGroundHeight(targetPos), targetPos.y);

        if (!TryResolveDestination(desired, out Vector3 destination, out bool isExact))
            return false;

        NavMeshPath path = new NavMeshPath();
        if (!_agent.CalculatePath(destination, path) || !IsPathUsable(path))
            return false;

        // 인자로 받은 멈춤 거리를 적용한다(기본 0). 직전 추적 이동에서 늘려 둔 값이 남아 있을 수 있으므로 항상 덮어쓴다.
        _agent.stoppingDistance = stoppingDistance;
        _agent.SetDestination(destination);

        _requestedDestination = destination;
        _destinationReachable = isExact && path.status == NavMeshPathStatus.PathComplete;
        _pendingArrived       = onArrived;
        _pendingMoveEnd       = onMoveEnd;
        // 방향 이동 중이었다면 이 경로 이동으로 이어지므로 이동 종료 없이 넘긴다. 실패한 경우는 방향 이동이 그대로 이어진다.
        EndDirectMove(false);
        BeginMoveOrder();
        return true;
    }

    // NavMeshAgent 경로를 초기화하고 추적 대상을 해제해 이동을 중지; 이동 중이었다면 OnMoveEnd와 대기 중인 이동 종료 콜백을 호출한다
    public void Stop()
    {
        bool   wasMoving       = _hasMoveOrder || _isDirectMoving;
        Action moveEndCallback = _pendingMoveEnd;

        EndDirectMove(false);

        // 목적지에 닿지 못한 채 끝났으므로 도착 콜백은 호출하지 않고 버린다.
        DiscardPendingCallbacks();

        _followTarget         = null;
        _hasMoveOrder         = false;
        _wasStopped           = false;
        _hasStartedMoving     = false;
        _destinationReachable = false;
        _agent.ResetPath();

        // HandleMoveEnd와 같은 이유로 콜백을 이벤트보다 먼저 호출한다. 이미 캡처해 둔 콜백이라 세대 확인이
        // 필요 없고, 도착 콜백 안에서 중지된 경우처럼 이동 명령이 먼저 정리됐어도 종료는 종료이므로 호출한다.
        moveEndCallback?.Invoke();

        if (wasMoving)
            OnMoveEnd?.Invoke();
    }

    // 대상 Transform을 추적 시작; 대상이 내브메시 밖이어도 갈 수 있는 데까지 접근하며, 순환 체인이거나
    // 이동 자체가 불가능하면 false 반환. onArrived는 대상에 처음 도달했을 때 한 번만 호출된다
    public bool Move(Transform targetTransform, Action onArrived = null)
    {
        ReplaceMoveOrder();

        if (targetTransform == null) return false;

        if (IsInFollowChain(targetTransform))
            return false;

        // 추적도 내브메시 경로 이동이므로 벽 무시 이동 중이었다면 내브메시로 돌아온다.
        EndNoclip();

        Transform previousTarget = _followTarget;
        _followTarget = targetTransform;

        if (!TrySetFollowDestination())
        {
            _followTarget = previousTarget;
            return false;
        }

        _nextRepathTime = Time.time + k_FollowRepathInterval;
        _pendingArrived = onArrived;
        EndDirectMove(false);
        BeginMoveOrder();
        return true;
    }
}
