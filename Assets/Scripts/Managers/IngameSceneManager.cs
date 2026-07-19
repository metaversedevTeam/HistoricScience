using UnityEngine;

// 인게임 씬의 진입점. 이전 씬이 지정한 맵 파일을 열어 MapData를 만들고, 시드를 주입한 뒤 청크 로딩을 시작한다.
public class IngameSceneManager : MonoBehaviour
{
    // 이전 씬이 씬 전환 전에 설정하는, 인게임 씬에서 열 맵 파일의 슬롯 이름
    public static string NextMapSlot { get; set; }

    // NextMapSlot이 비어 있을 때(에디터에서 인게임 씬을 바로 실행한 경우 등) 대신 열 슬롯 이름
    [SerializeField] private string _fallbackSlot = "test";

    // 맵 파일 읽기와 MapData 재생성을 담당하는 유틸리티
    [SerializeField] private MapSaveUtil _mapSaveUtil;
    // 맵 시드를 주입할 청크 매니저
    [SerializeField] private MapChunkManager _chunkManager;
    // 시드 주입이 끝난 뒤 로딩을 시작시킬 청크 로더
    [SerializeField] private MapChunkLoader _chunkLoader;

    // 이번 씬에서 열린 맵 파일의 원본 데이터
    private MapSaveData _saveData;
    // 이번 씬의 맵 데이터
    private MapData _mapData;

    // 이번 씬의 맵 데이터를 반환한다. 씬 진입이 끝나기 전에는 null이다.
    public MapData MapData => _mapData;

    private async void Start()
    {
        if (!HandleOpenMapFile()) return;
        if (!HandleCreateMapData()) return;

        _chunkManager.SetSeed(_saveData.Seed);
        await _chunkLoader.BeginLoadingAsync();
    }

    // 이전 씬이 지정한 슬롯(없으면 폴백 슬롯)의 맵 파일을 열어 보관한다. 실패하면 false를 반환한다.
    private bool HandleOpenMapFile()
    {
        string slot = string.IsNullOrEmpty(NextMapSlot) ? _fallbackSlot : NextMapSlot;

        _saveData = _mapSaveUtil.TryReadMapFile(slot);
        if (_saveData == null)
        {
            Debug.LogError($"IngameSceneManager: '{slot}' 맵 파일을 열지 못해 씬 진입을 중단합니다.");
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
}
