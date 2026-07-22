using System.Collections.Generic;
using UnityEngine;

// 인게임 씬의 진입점. 이전 씬이 지정한 맵 파일을 열어 MapData를 만들고, 시드 주입과 청크 로딩,
// 인벤토리/Savable 복원까지 마친 뒤에는 현재 맵 상태를 다시 파일로 저장하는 역할도 맡는다.
public class IngameSceneManager : MonoBehaviour
{
    // 이전 씬이 씬 전환 전에 설정하는, 인게임 씬에서 열 맵 파일의 슬롯 이름
    public static string NextMapSlot { get; set; }

    // NextMapSlot이 비어 있을 때(에디터에서 인게임 씬을 바로 실행한 경우 등) 대신 열 슬롯 이름
    [SerializeField] private string _fallbackSlot = "test";

    // 맵 파일 읽기/쓰기와 MapData 재생성을 담당하는 유틸리티
    [SerializeField] private MapSaveUtil _mapSaveUtil;
    // 맵 시드를 주입할 청크 매니저
    [SerializeField] private MapChunkManager _chunkManager;
    // 시드 주입이 끝난 뒤 로딩을 시작시킬 청크 로더
    [SerializeField] private MapChunkLoader _chunkLoader;
    // 저장/복원 대상 인벤토리
    [SerializeField] private ResourceInventory _resourceInventory;

    // 이번 씬에서 열린 맵 파일의 원본 데이터
    private MapSaveData _saveData;
    // 이번 씬의 맵 데이터
    private MapData _mapData;
    // 이번 씬에서 열린 맵 파일의 슬롯 이름
    private string _currentSlot;
    // 아직 청크가 로드되지 않아 소환되지 못한 저장 오브젝트 대기 목록. 각 청크의 ChunkSavableSpawner가 자기 영역의 항목을 꺼내 소환한다.
    private List<SavableEntry> _pendingSavables;

    // 이번 씬의 맵 데이터를 반환한다. 씬 진입이 끝나기 전에는 null이다.
    public MapData MapData => _mapData;

    private async void Start()
    {
        if (!HandleOpenMapFile()) return;
        if (!HandleCreateMapData()) return;

        HandleRestoreInventory();

        // 청크가 구워질 때마다 각 청크의 스포너가 자기 영역의 저장 오브젝트를 이 목록에서 꺼내 소환한다.
        _pendingSavables = _saveData.Savables != null ? new List<SavableEntry>(_saveData.Savables) : new List<SavableEntry>();
        _chunkManager.SetPendingSavables(_pendingSavables);

        _chunkManager.SetSeed(_saveData.Seed);
        await _chunkLoader.BeginLoadingAsync();
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

        MapSaveData saveData = _mapSaveUtil.GetSaveData(_mapData, _resourceInventory, HandleCollectSavables());

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

    // 씬에 살아 있는 모든 ISavable 구현체를 찾아 목록으로 만든다. 별도 필드로 저장되는 인벤토리는 제외한다.
    private List<ISavable> HandleCollectSavables()
    {
        List<ISavable> savables = new();

        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (behaviour is ISavable savable && !ReferenceEquals(savable, _resourceInventory))
                savables.Add(savable);
        }

        return savables;
    }
}
