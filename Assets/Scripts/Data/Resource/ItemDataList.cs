using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "아이템 목록", menuName = "스크립터블 오브젝트/자원/아이템 목록", order = int.MinValue + 1)]
public class ItemDataList : ScriptableObject
{
    public IReadOnlyList<ItemData> Items => _items;

    [SerializeField] private List<ItemData> _items = new();

    // 다음에 부여할 ID (한번 증가한 값은 되돌아오지 않음)
    #pragma warning disable CS0414   //사용되지 않는 변수 경고 비활성화
    [SerializeField, HideInInspector] private int _nextId = 1;
}
