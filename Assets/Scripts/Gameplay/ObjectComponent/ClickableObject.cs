using System;
using UnityEngine;

public class ClickableObject : MonoBehaviour
{
    public event Action<PlayerManager> OnClick;

    // 클릭 이벤트를 발행하고 발행한 PlayerManager를 전달
    public void HandleClick(PlayerManager playerManager)
    {
        OnClick?.Invoke(playerManager);
    }
}
