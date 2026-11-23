using System;
using UnityEngine;

// 선택 가능한 오브젝트임을 나타내고 선택 이벤트를 발행하는 컴포넌트
[Obsolete("PlayerManager의 선택 기능이 없어져 더 이상 쓰지 않는다. 상호작용은 Citizen 앞 대상의 명령 목록으로 한다.")]
public class SelectableObject : MonoBehaviour
{
    public event Action<PlayerManager> OnSelect;

    // 선택 이벤트를 발행하고 발행한 PlayerManager를 전달
    public void HandleSelect(PlayerManager playerManager)
    {
        OnSelect?.Invoke(playerManager);
    }
}
