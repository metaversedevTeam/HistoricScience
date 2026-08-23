using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

// 인게임 씬의 진입점. 이전 씬이 지정한 맵 파일을 열어 MapData를 만들고, 맵 데이터 주입과 청크 로딩,
// 인벤토리/Savable 복원까지 마친 뒤에는 현재 맵 상태를 다시 파일로 저장하는 역할도 맡는다.
public class IngameSceneManager : MonoBehaviour
{
    // 이전 씬이 씬 전환 전에 설정하는, 인게임 씬에서 열 맵 파일의 슬롯 이름
    public static string NextMapSlot { get; set; }

    // 맵 파일을 읽어 오는 데 성공한 시점에 표시할 로딩 진행도. 남은 구간은 청크 로딩 비율에 쓴다.
    private const float FileLoadProgress = 0.1f;

    // NextMapSlot이 비어 있을 때(에디터에서 인게임 씬을 바로 실행한 경우 등) 대신 열 슬롯 이름
    [SerializeField] private string _fallbackSlot = "test";

    // 맵 파일 읽기/쓰기와 MapData 재생성을 담당하는 유틸리티
    [SerializeField] private MapSaveUtil _mapSaveUtil;
    // 맵 데이터를 주입할 청크 매니저
    [SerializeField] private MapChunkManager _chunkManager;
    // 맵 데이터 주입이 끝난 뒤 로딩을 시작시킬 청크 로더
    [SerializeField] private MapChunkLoader _chunkLoader;
    // 저장/복원 대상 인벤토리
    [SerializeField] private ResourceInventory _resourceInventory;
    // 저장/복원 대상 도감
    [SerializeField] private ItemCodex _itemCodex;
    // 저장/복원 대상 카메라. 없어도 맵 저장/불러오기 자체는 계속 동작하며 카메라 위치만 복원되지 않는다.
    [SerializeField] private CameraController _cameraController;
    // 맵을 연 뒤 청크 초기 로딩이 끝날 때까지 띄울 로딩 화면 프리팹. 비어 있으면 로딩 화면 없이 씬에 진입한다.
    [SerializeField] private LoadingScreenUI _loadingScreenUIPrefab;

    // 이번 씬에서 열린 맵 파일의 원본 데이터
    private MapSaveData _saveData;
    // 이번 씬의 맵 데이터
    private MapData _mapData;
    // 이번 씬에서 열린 맵 파일의 슬롯 이름
    private string _currentSlot;
    // 아직 청크가 로드되지 않아 소환되지 못한 저장 오브젝트 대기 목록. 각 청크의 ChunkSavableSpawner가 자기 영역의 항목을 꺼내 소환한다.
    private List<SavableEntry> _pendingSavables;
    // 이번 씬 진입 동안 띄워 둔 로딩 화면 인스턴스
    private LoadingScreenUI _loadingScreen;

    // 이번 씬의 맵 데이터를 반환한다. 씬 진입이 끝나기 전에는 null이다.
    public MapData MapData => _mapData;

    // 이번 씬에서 열린 맵 파일의 슬롯 이름. 저장 대상 슬롯을 바깥에서 알아야 할 때(일시정지 화면 등) 쓴다.
    public string CurrentSlot => _currentSlot;

    private async void Start()
    {
        HandleShowLoadingScreen();
        await HandleEnterMapAsync();
        HandleHideLoadingScreen();
    }

    // 맵 파일을 열어 저장 상태를 복원하고, 등록된 청크의 초기 로딩이 끝날 때까지 기다린다. 도중에 실패하면 그 자리에서 중단한다.
    private async Task HandleEnterMapAsync()
    {
        if (!HandleOpenMapFile()) return;

        HandleReportFileLoadProgress();

        if (!HandleCreateMapData()) return;

        HandleRestoreInventory();
        HandleRestoreCodex();

        // 청크가 구워질 때마다 각 청크의 스포너가 자기 영역의 저장 오브젝트를 이 목록에서 꺼내 소환한다.
        _pendingSavables = _saveData.Savables != null ? new List<SavableEntry>(_saveData.Savables) : new List<SavableEntry>();
        _chunkManager.SetPendingSavables(_pendingSavables);

        // 청크마다 시드로 맵을 다시 만들지 않도록, 이미 만들어 둔 이 씬의 맵 데이터를 그대로 넘긴다.
        _chunkManager.SetMapData(_mapData);

        // 청크 로더가 카메라를 추적 대상으로 삼으므로, 저장된 위치 주변부터 로딩되도록 XZ를 먼저 복원한다.
        HandleRestoreCameraPosition();
        await _chunkLoader.BeginLoadingAsync();

        // 카메라 고도는 그 자리의 지형 높이로 정해지므로, 주변 지형이 실제로 존재하게 된 뒤 한 번 더 복원해 고도를 맞춘다.
        HandleRestoreCameraPosition();
    }

    // 맵을 열기 전에 로딩 화면을 띄워, 청크가 다 구워질 때까지 아직 비어 있는 월드가 보이지 않게 가린다.
    private void HandleShowLoadingScreen()
    {
        if (_loadingScreenUIPrefab == null)
        {
            Debug.LogWarning("IngameSceneManager: 로딩 화면 프리팹이 연결되지 않아 로딩 중 화면을 가리지 않습니다.", this);
            return;
        }

        _loadingScreen = Instantiate(_loadingScreenUIPrefab, UIManager.Instance.UIRoot);
        _loadingScreen.Show();

        // 청크가 하나씩 로딩될 때마다 등록된 청크 대비 비율을 받아 프로그래스 바에 반영한다.
        if (_chunkLoader != null)
            _chunkLoader.LoadProgressChanged += HandleReportChunkLoadProgress;
    }

    // 청크 초기 로딩이 끝난 뒤 로딩 화면을 걷는다.
    private void HandleHideLoadingScreen()
    {
        if (_loadingScreen == null)
            return;

        // 로딩 화면이 사라진 뒤에도 추적 루프가 진행도를 계속 알리므로 여기서 연결을 끊는다.
        if (_chunkLoader != null)
            _chunkLoader.LoadProgressChanged -= HandleReportChunkLoadProgress;

        _loadingScreen.Hide();
    }

    // 맵 파일을 읽어 온 몫만큼 로딩 진행도를 먼저 채운다.
    private void HandleReportFileLoadProgress()
    {
        if (_loadingScreen == null)
            return;

        _loadingScreen.SetProgress(FileLoadProgress);
    }

    // 청크 로딩 비율을 전체 로딩 진행도로 바꿔 로딩 화면에 알린다. 맵 파일 읽기가 앞의 몫을 차지하므로 남은 구간에 대응시킨다.
    private void HandleReportChunkLoadProgress(float chunkLoadProgress)
    {
        if (_loadingScreen == null)
            return;

        _loadingScreen.SetProgress(FileLoadProgress + chunkLoadProgress * (1f - FileLoadProgress));
    }

    // 현재 맵 상태(맵 데이터, 인벤토리, 씬의 모든 ISavable)를 지정한 슬롯에 저장한다. 성공하면 true를 반환한다.
    public bool SaveMap(string slot)
    {
        if (_mapData == null)
        {
            Debug.LogError("IngameSceneManager: 맵 로드가 끝나기 전에는 저장할 수 없습니다.");
            return false;
        }

        if (_resourceInventory == null)
        {
            Debug.LogError("IngameSceneManager: ResourceInventory가 연결되지 않아 저장할 수 없습니다.");
            return false;
        }

        MapSaveData saveData = _mapSaveUtil.GetSaveData(_mapData, _resourceInventory, _itemCodex, HandleCollectSavables(), _cameraController);

        // 아직 청크가 로드되지 않아 소환되지 못한 오브젝트는 씬에 없으므로, 원본 항목을 그대로 이어 붙여 유실을 막는다.
        if (_pendingSavables != null)
            saveData.Savables.AddRange(_pendingSavables);

        return _mapSaveUtil.TrySaveMap(saveData, slot);
    }

    // 현재 열려 있는 슬롯에 그대로 저장한다. (에디터 테스트용)
    [ContextMenu("Save Current Slot")]
    private void SaveCurrentSlot()
    {
        if (SaveMap(_currentSlot))
            Debug.Log($"IngameSceneManager: '{_currentSlot}' 슬롯에 저장했습니다.");
    }

    // 이전 씬이 지정한 슬롯(없으면 폴백 슬롯)의 맵 파일을 열어 보관한다. 실패하면 false를 반환한다.
    private bool HandleOpenMapFile()
    {
        _currentSlot = string.IsNullOrEmpty(NextMapSlot) ? _fallbackSlot : NextMapSlot;

        _saveData = _mapSaveUtil.TryReadMapFile(_currentSlot);
        if (_saveData == null)
        {
            Debug.LogError($"IngameSceneManager: '{_currentSlot}' 맵 파일을 열지 못해 씬 진입을 중단합니다.");
            return false;
        }

        return true;
    }

    // 열린 맵 파일의 시드로 MapData를 생성해 보관한다. 실패하면 false를 반환한다.
    private bool HandleCreateMapData()
    {
        _mapData = _mapSaveUtil.GetMapData(_saveData);
        return _mapData != null;
    }

    // 저장된 인벤토리 상태를 복원한다.
    private void HandleRestoreInventory()
    {
        if (_resourceInventory == null || string.IsNullOrEmpty(_saveData.InventoryJson))
            return;

        _resourceInventory.ApplyJson(_saveData.InventoryJson);
    }

    // 저장된 도감 상태를 복원한다.
    private void HandleRestoreCodex()
    {
        if (_itemCodex == null || string.IsNullOrEmpty(_saveData.CodexJson))
            return;

        _itemCodex.ApplyJson(_saveData.CodexJson);
    }

    // 저장된 카메라 위치가 있으면 복원한다.
    private void HandleRestoreCameraPosition()
    {
        if (_cameraController == null || string.IsNullOrEmpty(_saveData.CameraJson))
            return;

        _cameraController.ApplyJson(_saveData.CameraJson);
    }

    // 씬에 살아 있는 모든 ISavable 구현체를 찾아 목록으로 만든다. 별도 필드로 저장되는 인벤토리·도감·카메라는 제외한다.
    private List<ISavable> HandleCollectSavables()
    {
        List<ISavable> savables = new();

        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (behaviour is ISavable savable
                && !ReferenceEquals(savable, _resourceInventory)
                && !ReferenceEquals(savable, _itemCodex)
                && !ReferenceEquals(savable, _cameraController))
                savables.Add(savable);
        }

        return savables;
    }
}
