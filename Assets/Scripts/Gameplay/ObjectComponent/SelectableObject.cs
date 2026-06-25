using System;
using UnityEngine;

public class SelectableObject : MonoBehaviour
{
    public event Action<PlayerManager> OnSelect;

    public void HandleSelect(PlayerManager playerManager)
    {
        OnSelect?.Invoke(playerManager);
    }
}
