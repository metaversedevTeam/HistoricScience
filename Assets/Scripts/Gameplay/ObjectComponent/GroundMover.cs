using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class GroundMover : MonoBehaviour, IMover
{
    private NavMeshAgent _agent;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    public bool Move(Vector2 targetPos)
    {
        Vector3 destination = new Vector3(targetPos.x, 0f, targetPos.y);

        NavMeshPath path = new NavMeshPath();
        if (!_agent.CalculatePath(destination, path) || path.status != NavMeshPathStatus.PathComplete)
            return false;

        _agent.SetDestination(destination);
        return true;
    }
}
