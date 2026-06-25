using System;
using UnityEngine;

public class HitableObject : MonoBehaviour
{
    public event Action<bool> OnChangeHitState;

    public bool HitEnabled => _hitEnabled;
    public float HitRadius => HitEnabled ? _hitRadius : 0.0f;

    [SerializeField] private bool _hitEnabled = true;
    [SerializeField] private float _hitRadius = 0.7f;
    
    public void SetHitEnabled(bool value)
    {
        if(_hitEnabled != value) {
            _hitEnabled = value;
            OnChangeHitState?.Invoke(value);
        }
    }
}
