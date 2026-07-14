using System;
using UnityEngine;

public class HitableObject : MonoBehaviour
{
    public event Action<bool> OnChangeHitState;

    public bool HitEnabled => _hitEnabled;
    public float HitRadius => HitEnabled ? _hitRadius : 0.0f;

    [SerializeField] private bool _hitEnabled = true;
    [SerializeField] private float _hitRadius = 0.7f;

    private const int GizmoCircleSegments = 32;

    // 히트 활성 상태를 변경하고 변경 시 이벤트를 발행
    public void SetHitEnabled(bool value)
    {
        if(_hitEnabled != value) {
            _hitEnabled = value;
            OnChangeHitState?.Invoke(value);
        }
    }

    // 선택 시 Scene 뷰에 히트 반경을 XZ 평면 원으로 표시
    private void OnDrawGizmosSelected()
    {
        if (HitRadius <= 0f) return;

        Gizmos.color = Color.red;
        DrawHitRadiusGizmo();
    }

    // HitRadius 크기의 원을 자신의 위치를 중심으로 XZ 평면에 그린다.
    private void DrawHitRadiusGizmo()
    {
        Vector3 center = transform.position;
        Vector3 prevPoint = center + new Vector3(HitRadius, 0f, 0f);

        for (int i = 1; i <= GizmoCircleSegments; i++)
        {
            float angle = i / (float)GizmoCircleSegments * Mathf.PI * 2f;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * HitRadius, 0f, Mathf.Sin(angle) * HitRadius);
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }
}
