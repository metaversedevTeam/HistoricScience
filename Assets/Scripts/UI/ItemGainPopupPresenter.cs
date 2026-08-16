using UnityEngine;

// ResourceInventory에 월드 좌표와 함께 아이템이 추가되면, 그 위치에 획득 팝업을 띄우는 표시 담당 컴포넌트
public class ItemGainPopupPresenter : MonoBehaviour
{
    // 구독할 인벤토리. 비어 있으면 같은 오브젝트의 ResourceInventory를 찾아 쓴다.
    [SerializeField] private ResourceInventory _inventory;

    // 획득 시 띄울 팝업 UI 프리팹. 비어 있으면 팝업을 띄우지 않는다.
    [SerializeField] private GatherPopupUI _popupPrefab;

    // 팝업이 뜰 기준 위치의 오프셋(획득한 월드 좌표 기준). 유닛 머리 위쪽에 뜨도록 y를 올려 쓴다.
    [SerializeField] private Vector3 _worldOffset = new Vector3(0f, 2f, 0f);

    private void Awake()
    {
        ResolveInventory();

        if (_popupPrefab == null)
            Debug.LogWarning("ItemGainPopupPresenter: 팝업 프리팹이 연결되지 않아 획득 팝업이 표시되지 않습니다.", this);
    }

    // 구독할 인벤토리를 찾는다. 인스펙터 지정 → 같은 오브젝트 → 씬 전체 순으로 찾고, 그래도 없으면 경고를 남긴다.
    private void ResolveInventory()
    {
        if (_inventory == null)
            _inventory = GetComponent<ResourceInventory>();

        if (_inventory == null)
            _inventory = FindFirstObjectByType<ResourceInventory>();

        if (_inventory == null)
            Debug.LogWarning("ItemGainPopupPresenter: 구독할 ResourceInventory를 찾지 못해 획득 팝업이 표시되지 않습니다.", this);
    }

    private void OnEnable()
    {
        if (_inventory != null)
            _inventory.OnAddItemAt += HandleItemAdded;
    }

    private void OnDisable()
    {
        if (_inventory != null)
            _inventory.OnAddItemAt -= HandleItemAdded;
    }

    // 획득한 아이템의 아이콘과 개수를 획득 위치 위쪽 캔버스 좌표에 잠깐 띄운다. 풀링 없이 매번 생성하고, 팝업이 스스로 파괴된다.
    private void HandleItemAdded(ItemData item, int amount, Vector3 worldPosition)
    {
        if (_popupPrefab == null || item == null) return;

        var popup = Instantiate(_popupPrefab, UIManager.Instance.UIRoot);
        // 창 형태의 UI들 아래에 그려지도록 캔버스의 맨 뒤로 보낸다.
        popup.transform.SetAsFirstSibling();
        popup.Show(item, amount, worldPosition + _worldOffset);
    }
}
