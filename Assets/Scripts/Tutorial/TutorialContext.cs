using System;
using UnityEngine;

// 튜토리얼이 지켜볼 씬의 객체와 자주 쓰는 조회를 모아 두는 문맥.
// 다른 코드가 튜토리얼을 참조하는 일이 없도록, 필요한 참조는 모두 여기서 씬을 훑어 직접 찾는다.
public class TutorialContext : IDisposable
{
    // 돌 자원 소스 프리팹의 이름. 채집 안내에서 강조할 대상을 고르는 데만 쓰는 값이라, 못 찾으면 강조 없이 안내만 한다.
    private const string StoneSourcePrefabName = "Stone Source";

    // 선택·클릭 이벤트를 발행하는 플레이어 매니저
    public PlayerManager PlayerManager { get; private set; }

    // 채집·조합 결과가 쌓이는 인벤토리
    public ResourceInventory Inventory { get; private set; }

    // 조합법 힌트 공개를 알리는 도감
    public ItemCodex Codex { get; private set; }

    // 맵 로딩이 끝났는지 판정할 인게임 씬 매니저
    public IngameSceneManager SceneManager { get; private set; }

    // 선택된 유닛의 명령 버튼이 만들어지는 패널
    public TempCommandPanelUI CommandPanel { get; private set; }

    // 월드 좌표를 화면 좌표로 옮길 때와 카메라 이동을 재는 데 쓰는 카메라
    public Camera WorldCamera { get; private set; }

    // 카메라 이동·휠 확대를 담당하는 컨트롤러. 확대 배율이 이 오브젝트의 스케일로 표현된다.
    public CameraController CameraController { get; private set; }

    // 채집·건축 안내에서 개수를 세는 데 쓰는 돌 아이템
    public ItemData Stone { get; private set; }

    // 작업대 안내에서 만들게 할 좀돌날 아이템
    public ItemData Microblade { get; private set; }

    // 현재 선택된 오브젝트. 아무것도 선택되지 않았으면 null이다.
    public SelectableObject CurrentSelection { get; private set; }

    // 가장 가까운 시민을 잠시 들고 있는 캐시
    private TutorialTargetCache<Citizen> _nearestCitizen;

    // 가장 가까운 돌 자원 소스를 잠시 들고 있는 캐시
    private TutorialTargetCache<GatherableObject> _nearestStoneSource;

    // 씬에 지어져 있는 대장간·창고·근거지를 잠시 들고 있는 캐시
    private TutorialTargetCache<Lab> _lab;
    private TutorialTargetCache<Warehouse> _warehouse;
    private TutorialTargetCache<HomeBase> _homeBase;

    // 건물 선택 UI에서 마지막으로 찾아 둔 카드와 그 건물 이름
    private BuildingCardUI _cachedCard;
    private string _cachedCardName;

    // 건물 카드를 다시 찾을 시각
    private float _nextCardSearchTime;

    // 씬에서 튜토리얼에 필요한 참조를 모두 찾아 채운다. 없으면 안 되는 참조가 빠져 있으면 false를 반환한다.
    public bool TryResolve()
    {
        PlayerManager = UnityEngine.Object.FindFirstObjectByType<PlayerManager>();
        Inventory = UnityEngine.Object.FindFirstObjectByType<ResourceInventory>();
        Codex = UnityEngine.Object.FindFirstObjectByType<ItemCodex>();
        SceneManager = UnityEngine.Object.FindFirstObjectByType<IngameSceneManager>();
        CommandPanel = UnityEngine.Object.FindFirstObjectByType<TempCommandPanelUI>();
        WorldCamera = Camera.main;
        CameraController = UnityEngine.Object.FindFirstObjectByType<CameraController>();

        if (PlayerManager == null || Inventory == null || SceneManager == null)
        {
            Debug.LogWarning("TutorialContext: 인게임 씬에서 필요한 매니저를 찾지 못해 튜토리얼을 시작하지 않습니다.");
            return false;
        }

        if (CommandPanel == null)
            Debug.LogWarning("TutorialContext: 커맨드 패널을 찾지 못해 명령 버튼을 씬 전체에서 찾습니다.");

        Stone = FindItem("돌");
        Microblade = FindItem("좀돌날");

        _nearestCitizen = new TutorialTargetCache<Citizen>(SearchNearestCitizen);
        _nearestStoneSource = new TutorialTargetCache<GatherableObject>(SearchNearestStoneSource);
        _lab = new TutorialTargetCache<Lab>(UnityEngine.Object.FindFirstObjectByType<Lab>);
        _warehouse = new TutorialTargetCache<Warehouse>(UnityEngine.Object.FindFirstObjectByType<Warehouse>);
        _homeBase = new TutorialTargetCache<HomeBase>(UnityEngine.Object.FindFirstObjectByType<HomeBase>);

        PlayerManager.OnSelected += HandleSelected;
        PlayerManager.OnDeselected += HandleDeselected;
        return true;
    }

    // 구독을 모두 끊는다. 튜토리얼이 끝날 때 호출한다.
    public void Dispose()
    {
        if (PlayerManager == null) return;

        PlayerManager.OnSelected -= HandleSelected;
        PlayerManager.OnDeselected -= HandleDeselected;
    }

    // 현재 선택된 대상이 시민이면 그 시민을 돌려준다. 아니면 null이다.
    public Citizen SelectedCitizen => CurrentSelection != null ? CurrentSelection.GetComponent<Citizen>() : null;

    // 카메라에서 가장 가까운 시민을 돌려준다. (강조 대상용, 잠시 캐싱된다)
    public Citizen NearestCitizen => _nearestCitizen?.Get();

    // 카메라에서 가장 가까운 돌 자원 소스를 돌려준다. 근처에 없으면 null이다. (강조 대상용, 잠시 캐싱된다)
    public GatherableObject NearestStoneSource => _nearestStoneSource?.Get();

    // 맵 로딩이 끝나 튜토리얼을 시작해도 되는 상태인지 여부.
    // 맵 데이터가 만들어진 뒤에도 청크 로딩이 남아 있으므로, 로딩 화면이 걷힌 것까지 확인한다.
    public bool IsMapReady =>
        SceneManager != null &&
        SceneManager.MapData != null &&
        UnityEngine.Object.FindFirstObjectByType<LoadingScreenUI>() == null;

    // 휠 확대/축소가 실제로 반영되는 트랜스폼. 카메라 컨트롤러가 자기 스케일을 바꾸므로 그 오브젝트를 본다.
    // 컨트롤러를 찾지 못했으면 카메라 자신으로 대신한다.
    public Transform CameraRoot =>
        CameraController != null ? CameraController.transform : (WorldCamera != null ? WorldCamera.transform : null);

    // 씬에 지어져 있는 대장간. 아직 없으면 null이다. (강조·완료 판정용, 잠시 캐싱된다)
    public Lab Lab => _lab?.Get();

    // 씬에 지어져 있는 창고. 아직 없으면 null이다. (강조·완료 판정용, 잠시 캐싱된다)
    public Warehouse Warehouse => _warehouse?.Get();

    // 씬에 지어져 있는 근거지. 아직 없으면 null이다. (강조·완료 판정용, 잠시 캐싱된다)
    public HomeBase HomeBase => _homeBase?.Get();

    // 인벤토리에 지정한 자원을 몇 개 들고 있는지 센다.
    public int CountOwned(ResourceData resource)
    {
        return resource != null && Inventory != null ? Inventory.Get(resource) : 0;
    }

    // 건물 선택 UI가 열려 있으면 그 목록에서 이름이 같은 건물 카드를 찾는다. (강조 대상용, 잠시 캐싱된다)
    public BuildingCardUI FindBuildingCard(string buildingName)
    {
        if (_cachedCardName == buildingName && _cachedCard != null && Time.time < _nextCardSearchTime)
            return _cachedCard;

        if (_cachedCardName == buildingName && Time.time < _nextCardSearchTime)
            return null;

        _nextCardSearchTime = Time.time + 0.3f;
        _cachedCardName = buildingName;
        _cachedCard = SearchBuildingCard(buildingName);
        return _cachedCard;
    }

    // 아이템 목록에서 이름으로 아이템을 찾는다. 없으면 경고를 남기고 null을 돌려준다.
    private ItemData FindItem(string itemName)
    {
        if (Inventory == null || Inventory.ItemDataList == null) return null;

        foreach (ItemData item in Inventory.ItemDataList.Items)
        {
            if (item != null && item.Nmae == itemName)
                return item;
        }

        Debug.LogWarning($"TutorialContext: 아이템 '{itemName}'을 찾지 못했습니다. 관련 단계는 개수 안내 없이 진행됩니다.");
        return null;
    }

    // 열려 있는 건물 선택 UI의 카드 중 지정한 이름의 건물 카드를 찾는다. 없으면 null이다.
    private BuildingCardUI SearchBuildingCard(string buildingName)
    {
        foreach (BuildingCardUI card in UnityEngine.Object.FindObjectsByType<BuildingCardUI>(FindObjectsSortMode.None))
        {
            if (card.Buildable != null && card.Buildable.BuildingName == buildingName)
                return card;
        }

        return null;
    }

    // 카메라 위치에서 가장 가까운 시민을 씬에서 찾는다.
    private Citizen SearchNearestCitizen()
    {
        return FindNearest(UnityEngine.Object.FindObjectsByType<Citizen>(FindObjectsSortMode.None));
    }

    // 카메라 위치에서 가장 가까운 돌 자원 소스를 씬에서 찾는다. 이름으로 골라내므로 못 찾으면 null이다.
    private GatherableObject SearchNearestStoneSource()
    {
        GatherableObject[] all = UnityEngine.Object.FindObjectsByType<GatherableObject>(FindObjectsSortMode.None);

        GatherableObject nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (GatherableObject candidate in all)
        {
            if (!candidate.name.StartsWith(StoneSourcePrefabName, StringComparison.Ordinal)) continue;

            float distance = SquaredDistanceFromCamera(candidate.transform);
            if (distance >= nearestDistance) continue;

            nearestDistance = distance;
            nearest = candidate;
        }

        return nearest;
    }

    // 후보 중 카메라에서 가장 가까운 하나를 고른다.
    private T FindNearest<T>(T[] candidates) where T : Component
    {
        T nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (T candidate in candidates)
        {
            float distance = SquaredDistanceFromCamera(candidate.transform);
            if (distance >= nearestDistance) continue;

            nearestDistance = distance;
            nearest = candidate;
        }

        return nearest;
    }

    // 카메라와의 XZ 거리 제곱을 구한다. 카메라가 없으면 원점 기준으로 잰다.
    private float SquaredDistanceFromCamera(Transform target)
    {
        Vector3 origin = WorldCamera != null ? WorldCamera.transform.position : Vector3.zero;
        float dx = target.position.x - origin.x;
        float dz = target.position.z - origin.z;
        return dx * dx + dz * dz;
    }

    // 선택된 대상을 기억한다.
    private void HandleSelected(SelectableObject selected)
    {
        CurrentSelection = selected;
    }

    // 선택이 풀렸음을 기억한다.
    private void HandleDeselected()
    {
        CurrentSelection = null;
    }
}
