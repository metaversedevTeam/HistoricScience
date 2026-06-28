using System;
using UnityEngine;

// 클릭 가능한 오브젝트임을 나타내고 클릭 이벤트를 발행하는 컴포넌트
public class ClickableObject : MonoBehaviour
{
    public event Action<PlayerManager> OnClick;

    // 클릭 이벤트를 발행하고 발행한 PlayerManager를 전달
    public void HandleClick(PlayerManager playerManager)
    {
        OnClick?.Invoke(playerManager);
    }
}
