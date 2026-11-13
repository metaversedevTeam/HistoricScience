using System.Collections.Generic;
using UnityEngine;
using System;

// 시민(Citizen)이 상호작용할 수 있는 개체가 구현하는 인터페이스. 시민이 앞에 서면 제공하는 명령 목록이 상호작용 명령 목록에 표시된다.
public interface ICommandable
{
    // 이 개체가 제공하는 상호작용 명령 목록을 반환한다.
    IReadOnlyList<CommandData> GetCommands();
}

// 명령 하나의 이름, 아이콘, 실행 콜백을 담는 데이터 클래스
public class CommandData
{
    public string Name { get; private set; }
    public Sprite Icon { get; private set; }
    // 명령을 실행할 때 부르는 콜백. 명령을 내린 플레이어를 받아 그 플레이어의 인벤토리 등을 쓴다.
    public Action<PlayerManager> OnExecute { get; private set; }

    // 명령 하나를 구성하는 데이터를 초기화한다.
    public CommandData(string name, Sprite icon, Action<PlayerManager> onExecute)
    {
        Name = name;
        Icon = icon;
        OnExecute = onExecute;
    }
}
