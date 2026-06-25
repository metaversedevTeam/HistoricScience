using UnityEngine;

public interface IMover
{
    bool Move(Vector2 targetPos);
    bool Move(Transform targetTransform);
}
