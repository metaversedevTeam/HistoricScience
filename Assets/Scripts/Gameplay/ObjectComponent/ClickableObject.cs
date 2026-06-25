using System;
using UnityEngine;

public class ClickableObject : MonoBehaviour
{
    public event Action<PlayerManager> OnClick;

    public void HandleClick(PlayerManager playerManager)
    {
        OnClick?.Invoke(playerManager);
    }
}
