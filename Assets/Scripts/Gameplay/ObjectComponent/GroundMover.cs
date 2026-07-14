using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class GroundMover : MonoBehaviour, IMover
{
    // 에이전트가 내브메시 밖에 있을 때 주변에서 내브메시를 찾아볼 반경. 내브메시가 뒤늦게 구워지는 경우를 위한 값이라
    // 너무 크게 잡지 않는다. 이 범위 안에 내브메시가 없으면 실제로 이동할 수 없는 상태로 간주한다.
    private const float k_NavMeshWarpSearchRadius = 10f;

    private NavMeshAgent  _agent;
    private Transform     _followTarget;
    private HitableObject _selfHitable;

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

    // 추적 대상의 경로를 재계산하고 멈춤 거리를 갱신하며 이동
    private void HandleFollow()
    {
        if (!EnsureOnNavMesh())
        {
            _followTarget = null;
            return;
        }

        Vector3 destination = _followTarget.position;
        NavMeshPath path = new NavMeshPath();
        if (!_agent.CalculatePath(destination, path) || !IsPathUsable(path))
        {
            _followTarget = null;
            return;
        }

        _agent.stoppingDistance = GetStoppingDistance(_followTarget);
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

    // 자신과 대상의 충돌 반경 합산으로 멈춤 거리 계산
    private float GetStoppingDistance(Transform target)
    {
        float selfRadius   = _selfHitable != null ? _selfHitable.HitRadius : 0f;
        var   targetHitable = target.GetComponent<HitableObject>();
        float targetRadius = targetHitable != null ? targetHitable.HitRadius : 0f;
        return selfRadius + targetRadius;
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

    // 지정 위치로 이동; NavMesh 경로가 없으면 false 반환
    public bool Move(Vector2 targetPos)
    {
        _followTarget = null;

        if (!EnsureOnNavMesh())
            return false;

        float y = Terrain.activeTerrain != null
            ? Terrain.activeTerrain.SampleHeight(new Vector3(targetPos.x, 0f, targetPos.y))
            : 0f;
        Vector3 destination = new Vector3(targetPos.x, y, targetPos.y);

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

    // 대상 Transform을 추적 시작; 순환 체인이나 도달 불가면 false 반환
    public bool Move(Transform targetTransform)
    {
        if (targetTransform == null) return false;

        if (IsInFollowChain(targetTransform))
            return false;

        if (!EnsureOnNavMesh())
            return false;

        NavMeshPath path = new NavMeshPath();
        if (!_agent.CalculatePath(targetTransform.position, path) || !IsPathUsable(path))
            return false;

        _followTarget = targetTransform;
        return true;
    }
}
