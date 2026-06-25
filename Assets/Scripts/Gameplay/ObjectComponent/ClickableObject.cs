using System;
using UnityEngine;

public class ClickableObject : MonoBehaviour
{
    public event Action OnClick;

    public void HandleClick()
    {
        OnClick?.Invoke();
    }
}
