using System;
using UnityEngine;

public class SelectableObject : MonoBehaviour
{
    public event Action<PlayerManager> OnSelect;

    // 선택 이벤트를 발행하고 발행한 PlayerManager를 전달
    public void HandleSelect(PlayerManager playerManager)
    {
        OnSelect?.Invoke(playerManager);
    }
}
