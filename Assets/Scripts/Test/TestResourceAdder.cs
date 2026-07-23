using UnityEngine;

namespace HistoricScience.Test
{
    // 인스펙터에 지정한 아이템을 컨텍스트 메뉴로 ResourceInventory에 추가하거나 차감하는 테스트용 컴포넌트
    public class TestResourceAdder : MonoBehaviour
    {
        // 아이템을 추가/차감할 대상 인벤토리
        [SerializeField] private ResourceInventory _inventory;
        // 추가/차감할 아이템 데이터
        [SerializeField] private ItemData _itemData;
        // 적용할 수량. 양수면 추가하고 음수면 그 절댓값만큼 차감한다.
        [SerializeField] private int _amount = 1;

        // 수량 부호에 따라 아이템을 인벤토리에 추가하거나 차감한다.
        [ContextMenu("Apply Amount")]
        private void ApplyAmount()
        {
            if (_inventory == null)
            {
                Debug.LogError("TestResourceAdder: ResourceInventory가 연결되지 않았습니다.");
                return;
            }

            if (_itemData == null)
            {
                Debug.LogError("TestResourceAdder: ItemData가 연결되지 않았습니다.");
                return;
            }

            if (_amount > 0)
                AddItem();
            else
                RemoveItem();
        }

        // 설정된 아이템을 _amount만큼 인벤토리에 추가한다.
        private void AddItem()
        {
            _inventory.Add(_itemData, _amount);
            Debug.Log($"TestResourceAdder: '{_itemData.name}' {_amount}개를 추가했습니다. (현재 {_inventory.Get(_itemData)}개)");
        }

        // 설정된 아이템을 _amount의 절댓값만큼 인벤토리에서 차감한다.
        private void RemoveItem()
        {
            int removeAmount = -_amount;
            if (_inventory.Remove(_itemData, removeAmount))
                Debug.Log($"TestResourceAdder: '{_itemData.name}' {removeAmount}개를 차감했습니다. (현재 {_inventory.Get(_itemData)}개)");
            else
                Debug.LogWarning($"TestResourceAdder: '{_itemData.name}' {removeAmount}개 차감에 실패했습니다. (현재 {_inventory.Get(_itemData)}개)");
        }
    }
}
