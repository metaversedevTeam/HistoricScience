using UnityEngine;

// 주변의 IGatherable 대상을 채집하여 ResourceInventory에 결과를 추가하는 컴포넌트
public class Gatherer : MonoBehaviour
{
    [SerializeField] private float _gatherRange = 3f;

    private const int GizmoCircleSegments = 32;


    // 지정한 IGatherable 대상이 채집 범위 안에 있는지 확인한다. 대상에 HitableObject가 있다면 HitRadius만큼 범위를 늘려준다.
    private bool IsInRange(IGatherable target)
    {
        if (target is not Component component) return false;

        var targetHitable = component.GetComponent<HitableObject>();
        float extraRange = targetHitable != null ? targetHitable.HitRadius : 0f;
        return IsInRange(component.transform.position, extraRange);
    }

    private bool IsInRange(Vector3 pos, float extraRange = 0f)
    {
        Vector2 myPos = new Vector2(transform.position.x, transform.position.z);
        Vector2 targetPos = new Vector2(pos.x, pos.z);
        return Vector2.Distance(myPos, targetPos) <= _gatherRange + extraRange;
    }

    // 대상을 채집하여 결과 아이템을 인벤토리에 추가한다. 범위 밖이거나 채집 불가 상태면 false를 반환한다.
    public bool TryGather(IGatherable target, ResourceInventory inventory)
    {
        if (!IsInRange(target)) return false;
        if (!target.CanGather()) return false;

        var (isSuccess, itemType, count) = target.OnGather();
        if (isSuccess && itemType != null && count > 0)
            inventory?.Add(itemType, count);

        return true;
    }
    
    // 선택 시 Scene 뷰에 채집 반경을 XZ 평면 원으로 표시
    private void OnDrawGizmosSelected()
    {
        if (_gatherRange <= 0f) return;

        Gizmos.color = Color.red;
        DrawHitRadiusGizmo();
    }

    // _gatherRange 크기의 원을 자신의 위치를 중심으로 XZ 평면에 그린다.
    private void DrawHitRadiusGizmo()
    {
        Vector3 center = transform.position;
        Vector3 prevPoint = center + new Vector3(_gatherRange, 0f, 0f);
        Gizmos.color = Color.green;

        for (int i = 1; i <= GizmoCircleSegments; i++)
        {
            float angle = i / (float)GizmoCircleSegments * Mathf.PI * 2f;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * _gatherRange, 0f, Mathf.Sin(angle) * _gatherRange);
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }
}
