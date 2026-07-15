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

    private NavMeshAgent  _agent;
    private Transform     _followTarget;
    private HitableObject _selfHitable;
    private float         _nextRepathTime;

    // NavMeshAgent와 HitableObject 컴포넌트를 캐싱
    private void Awake()
    {
        _agent       = GetComponent<NavMeshAgent>();
        _selfHitable = GetComponent<HitableObject>();
    }

    // 추적 대상이 있으면 매 프레임 따라가기 처리
    private void Update()
    {
        if (_followTarget != null)
            HandleFollow();
    }

    // 추적 대상의 경로를 주기적으로 재계산하고 멈춤 거리를 갱신하며 이동. 대상이 일시적으로 도달 불가해도
    // 추적을 버리지 않고 다음 주기에 재시도해, 내브메시가 뒤늦게 구워지거나 대상이 범위 안으로 돌아오면 이어서 따라간다.
    private void HandleFollow()
    {
        if (Time.time < _nextRepathTime)
            return;
        _nextRepathTime = Time.time + k_FollowRepathInterval;

        if (!EnsureOnNavMesh())
            return;

        if (!TryResolveDestination(_followTarget.position, out Vector3 destination))
            return;

        NavMeshPath path = new NavMeshPath();
        if (!_agent.CalculatePath(destination, path) || !IsPathUsable(path))
            return;

        _agent.stoppingDistance = GetStoppingDistance(_followTarget, destination);
        _agent.SetDestination(destination);
    }

    // 에이전트가 아직 내브메시 위에 있지 않으면 주변에서 가장 가까운 내브메시로 옮겨 놓는다. 내브메시가 동적으로
    // 늦게 구워지는 구조에서는 스폰 시점에 에이전트가 내브메시를 못 찾아 영구히 "오프메시" 상태로 남을 수 있어 필요하다.
    private bool EnsureOnNavMesh()
    {
        if (_agent.isOnNavMesh)
            return true;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, k_NavMeshWarpSearchRadius, NavMesh.AllAreas))
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
    private bool TryResolveDestination(Vector3 desired, out Vector3 resolved)
    {
        if (NavMesh.SamplePosition(desired, out NavMeshHit sampleHit, k_DestinationSampleRadius, NavMesh.AllAreas))
        {
            resolved = sampleHit.position;
            return true;
        }

        // Raycast는 선이 내브메시 가장자리에서 끊기면 그 지점을, 끝까지 이어지면 매핑된 도착점을 hit에 채운다.
        // 어느 쪽이든 위치가 유한하면 그대로 쓸 수 있고, 무한대면 투영 자체가 실패한 것이다.
        NavMesh.Raycast(transform.position, desired, out NavMeshHit rayHit, NavMesh.AllAreas);
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

    // 지정 위치로 이동; 위치가 내브메시 밖이면 갈 수 있는 가장 가까운 지점까지 이동하고, 이동 자체가 불가능하면 false 반환
    public bool Move(Vector2 targetPos)
    {
        _followTarget = null;

        if (!EnsureOnNavMesh())
            return false;

        float y = Terrain.activeTerrain != null
            ? Terrain.activeTerrain.SampleHeight(new Vector3(targetPos.x, 0f, targetPos.y))
            : 0f;
        Vector3 desired = new Vector3(targetPos.x, y, targetPos.y);

        if (!TryResolveDestination(desired, out Vector3 destination))
            return false;

        NavMeshPath path = new NavMeshPath();
        if (!_agent.CalculatePath(destination, path) || !IsPathUsable(path))
            return false;

        _agent.SetDestination(destination);
        return true;
    }

    // NavMeshAgent 경로를 초기화하고 추적 대상을 해제해 이동을 중지
    public void Stop()
    {
        _followTarget = null;
        _agent.ResetPath();
    }

    // 대상 Transform을 추적 시작; 대상이 내브메시 밖이어도 갈 수 있는 데까지 접근하며, 순환 체인이거나
    // 이동 자체가 불가능하면 false 반환
    public bool Move(Transform targetTransform)
    {
        if (targetTransform == null) return false;

        if (IsInFollowChain(targetTransform))
            return false;

        if (!EnsureOnNavMesh())
            return false;

        if (!TryResolveDestination(targetTransform.position, out Vector3 destination))
            return false;

        NavMeshPath path = new NavMeshPath();
        if (!_agent.CalculatePath(destination, path) || !IsPathUsable(path))
            return false;

        _followTarget = targetTransform;
        _nextRepathTime = 0f; // 새 명령은 다음 Update에서 즉시 경로를 계산한다
        return true;
    }
}
