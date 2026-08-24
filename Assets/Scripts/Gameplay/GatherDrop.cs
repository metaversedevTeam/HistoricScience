using System;
using UnityEngine;

// 채집 시 나올 수 있는 후보 아이템 하나의 종류, 개수, 추첨 가중치를 담는 클래스
[Serializable]
public class GatherDrop
{
    [SerializeField] private ItemData _itemData;
    [SerializeField, Min(1)] private int _amount = 1;
    // 다른 후보들과 비교되는 상대적인 추첨 비율. 0이면 절대 나오지 않는다.
    [SerializeField, Min(0f)] private float _weight = 1f;

    public ItemData ItemData => _itemData;
    public int Amount => _amount;
    public float Weight => _weight;

    // 추첨 후보로 쓸 수 있는 설정인지 확인한다.
    public bool IsValid()
    {
        return _itemData != null && _amount > 0 && _weight > 0f;
    }
}
