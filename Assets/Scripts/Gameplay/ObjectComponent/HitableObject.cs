using System;
using UnityEngine;

public class HitableObject : MonoBehaviour
{
    public event Action<bool> OnChangeHitState;

    public bool HitEnabled => _hitEnabled;
    public float HitRadius => HitEnabled ? _hitRadius : 0.0f;

    [SerializeField] private bool _hitEnabled = true;
    [SerializeField] private float _hitRadius = 0.7f;

    // 히트 활성 상태를 변경하고 변경 시 이벤트를 발행
    public void SetHitEnabled(bool value)
    {
        if(_hitEnabled != value) {
            _hitEnabled = value;
            OnChangeHitState?.Invoke(value);
        }
    }
}
