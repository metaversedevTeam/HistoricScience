using System.Collections.Generic;
using UnityEngine;
using System;

// 명령 목록을 제공하는 오브젝트가 구현하는 인터페이스
public interface ICommandable
{
    // 이 오브젝트가 제공하는 명령 목록을 반환한다.
    IReadOnlyList<CommandData> GetCommands();
}

// 명령 버튼 하나의 이름, 아이콘, 실행 콜백을 담는 데이터 클래스
public class CommandData
{
    public string Name { get; private set; }
    public Sprite Icon { get; private set; }
    public Action OnExecute { get; private set; }

    // 명령 버튼 하나를 구성하는 데이터를 초기화한다.
    public CommandData(string name, Sprite icon, Action onExecute)
    {
        Name = name;
        Icon = icon;
        OnExecute = onExecute;
    }
}
