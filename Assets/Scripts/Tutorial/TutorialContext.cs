using System;
using UnityEngine;

// 튜토리얼이 지켜볼 씬의 객체와 자주 쓰는 조회를 모아 두는 문맥.
// 다른 코드가 튜토리얼을 참조하는 일이 없도록, 필요한 참조는 모두 여기서 씬을 훑어 직접 찾는다.
public class TutorialContext : IDisposable
{
    // 이동·상호작용 입력을 전달하는 플레이어 매니저
    public PlayerManager PlayerManager { get; private set; }

    // 채집·조합 결과가 쌓이는 인벤토리
    public ResourceInventory Inventory { get; private set; }

    // 조합법 힌트 공개를 알리는 도감
    public ItemCodex Codex { get; private set; }

    // 맵 로딩이 끝났는지 판정할 인게임 씬 매니저
    public IngameSceneManager SceneManager { get; private set; }

    // 월드 좌표를 화면 좌표로 옮길 때와 시점 회전을 재는 데 쓰는 카메라
    public Camera WorldCamera { get; private set; }

    // 플레이어 시민을 따라가는 3인칭 카메라 리그. 휠 확대 배율이 이 오브젝트의 스케일로 표현된다.
    public CameraController CameraController { get; private set; }

    // 채집·건축 안내에서 개수를 세는 데 쓰는 돌 아이템
    public ItemData Stone { get; private set; }

    // 작업대 안내에서 만들게 할 좀돌날 아이템
    public ItemData Microblade { get; private set; }

    // 시민이 앞 대상 옆에 띄우는 명령 목록 UI를 잠시 들고 있는 캐시. 시민이 처음 대상을 만날 때 만들어지므로 그 전에는 null이다.
    private TutorialTargetCache<InteractionCommandListUI> _commandList;

    // 씬에 지어져 있는 대장간·창고·근거지를 잠시 들고 있는 캐시
    private TutorialTargetCache<Lab> _lab;
    private TutorialTargetCache<Warehouse> _warehouse;
    private TutorialTargetCache<HomeBase> _homeBase;

    // 씬에서 튜토리얼에 필요한 참조를 모두 찾아 채운다. 없으면 안 되는 참조가 빠져 있으면 false를 반환한다.
    public bool TryResolve()
    {
        PlayerManager = UnityEngine.Object.FindFirstObjectByType<PlayerManager>();
        Inventory = UnityEngine.Object.FindFirstObjectByType<ResourceInventory>();
        Codex = UnityEngine.Object.FindFirstObjectByType<ItemCodex>();
        SceneManager = UnityEngine.Object.FindFirstObjectByType<IngameSceneManager>();
        WorldCamera = Camera.main;
        CameraController = UnityEngine.Object.FindFirstObjectByType<CameraController>();

        if (PlayerManager == null || Inventory == null || SceneManager == null)
        {
            Debug.LogWarning("TutorialContext: 인게임 씬에서 필요한 매니저를 찾지 못해 튜토리얼을 시작하지 않습니다.");
            return false;
        }

        Stone = FindItem("돌");
        Microblade = FindItem("좀돌날");

        _commandList = new TutorialTargetCache<InteractionCommandListUI>(UnityEngine.Object.FindFirstObjectByType<InteractionCommandListUI>);
        _lab = new TutorialTargetCache<Lab>(UnityEngine.Object.FindFirstObjectByType<Lab>);
        _warehouse = new TutorialTargetCache<Warehouse>(UnityEngine.Object.FindFirstObjectByType<Warehouse>);
        _homeBase = new TutorialTargetCache<HomeBase>(UnityEngine.Object.FindFirstObjectByType<HomeBase>);
        return true;
    }

    // 튜토리얼이 끝날 때 호출한다. 지금은 따로 끊어 둘 구독이 없다.
    public void Dispose()
    {
    }

    // 카메라가 따라가는 플레이어 시민을 돌려준다. 아직 찾지 못했으면 null이다.
    public Citizen FollowedCitizen
    {
        get
        {
            Transform target = CameraController != null ? CameraController.FollowTarget : null;
            return target != null ? target.GetComponent<Citizen>() : null;
        }
    }

    // 시민 앞 대상 옆에 뜨는 명령 목록 UI. 아직 만들어지지 않았으면 null이다. (잠시 캐싱된다)
    public InteractionCommandListUI CommandList => _commandList?.Get();

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

    // 건물 선택 UI(B)가 열려 있는지 여부
    public bool IsBuildingSelectOpen => UnityEngine.Object.FindFirstObjectByType<BuildingSelectUI>() != null;

    // 건물 자리를 고르는 위치 지정 모드 중인지 여부
    public bool IsPlacingBuilding => BuildingPlacementController.ActiveCount > 0;

    // 작업대 UI가 열려 있는지 여부
    public bool IsWorkbenchOpen => UnityEngine.Object.FindFirstObjectByType<WorkbenchUI>() != null;

    // 창고 UI가 열려 있는지 여부
    public bool IsWarehouseOpen => UnityEngine.Object.FindFirstObjectByType<WarehouseUI>() != null;

    // 도감 UI가 열려 있는지 여부
    public bool IsCodexOpen => UnityEngine.Object.FindFirstObjectByType<ItemCodexUI>() != null;

    // 씬에 지어져 있는 대장간. 아직 없으면 null이다. (완료 판정용, 잠시 캐싱된다)
    public Lab Lab => _lab?.Get();

    // 씬에 지어져 있는 창고. 아직 없으면 null이다. (완료 판정용, 잠시 캐싱된다)
    public Warehouse Warehouse => _warehouse?.Get();

    // 씬에 지어져 있는 근거지. 아직 없으면 null이다. (완료 판정용, 잠시 캐싱된다)
    public HomeBase HomeBase => _homeBase?.Get();

    // 인벤토리에 지정한 자원을 몇 개 들고 있는지 센다.
    public int CountOwned(ResourceData resource)
    {
        return resource != null && Inventory != null ? Inventory.Get(resource) : 0;
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
}
