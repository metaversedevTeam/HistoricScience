using System;
using UnityEngine;

public class SelectableObject : MonoBehaviour
{
    public event Action OnSelect;

    public void HandleSelect()
    {
        OnSelect?.Invoke();
    }
}
