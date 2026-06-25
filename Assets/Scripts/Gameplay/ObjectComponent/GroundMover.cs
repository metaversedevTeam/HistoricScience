using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class GroundMover : MonoBehaviour, IMover
{
    private NavMeshAgent _agent;
    private Transform    _followTarget;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        if (_followTarget != null)
            HandleFollow();
    }

    private void HandleFollow()
    {
        Vector3 destination = _followTarget.position;
        NavMeshPath path = new NavMeshPath();
        if (!_agent.CalculatePath(destination, path) || path.status != NavMeshPathStatus.PathComplete)
        {
            _followTarget = null;
            return;
        }

        _agent.SetDestination(destination);
    }

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

    public bool Move(Transform targetTransform)
    {
        if (targetTransform == null) return false;

        NavMeshPath path = new NavMeshPath();
        if (!_agent.CalculatePath(targetTransform.position, path) || path.status != NavMeshPathStatus.PathComplete)
            return false;

        _followTarget = targetTransform;
        return true;
    }
}
