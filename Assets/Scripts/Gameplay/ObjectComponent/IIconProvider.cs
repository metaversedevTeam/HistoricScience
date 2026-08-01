using UnityEngine;

// 아이콘 스프라이트를 제공하는 오브젝트가 구현하는 인터페이스
public interface IIconProvider
{
    Sprite Icon { get; }
}
