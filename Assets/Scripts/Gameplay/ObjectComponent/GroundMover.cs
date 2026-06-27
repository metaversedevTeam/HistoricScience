using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class GroundMover : MonoBehaviour, IMover
{
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
        Vector3 destination = _followTarget.position;
        NavMeshPath path = new NavMeshPath();
        if (!_agent.CalculatePath(destination, path) || path.status != NavMeshPathStatus.PathComplete)
        {
            _followTarget = null;
            return;
        }

        _agent.stoppingDistance = GetStoppingDistance(_followTarget);
        _agent.SetDestination(destination);
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

        float y = Terrain.activeTerrain != null
            ? Terrain.activeTerrain.SampleHeight(new Vector3(targetPos.x, 0f, targetPos.y))
            : 0f;
        Vector3 destination = new Vector3(targetPos.x, y, targetPos.y);

        NavMeshPath path = new NavMeshPath();
        if (!_agent.CalculatePath(destination, path) || path.status != NavMeshPathStatus.PathComplete)
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

        NavMeshPath path = new NavMeshPath();
        if (!_agent.CalculatePath(targetTransform.position, path) || path.status != NavMeshPathStatus.PathComplete)
            return false;

        _followTarget = targetTransform;
        return true;
    }
}
