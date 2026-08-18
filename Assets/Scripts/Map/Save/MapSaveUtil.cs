using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;
using HistoricScience.Test;

//MapSaveData의 조립과 JSON 파일 저장/읽기, 그리고 이 맵의 월드 좌표 ↔ 정규화 맵 좌표 변환을 단독으로 관리하는 유틸리티
public class MapSaveUtil : MonoBehaviour
{
    // 씬에 하나만 두는 인스턴스. 런타임에 소환되는 프리팹은 씬 참조를 직렬화해 둘 수 없어 좌표 변환을 쓰려면 전역 접근 경로가 필요하다.
    public static MapSaveUtil Instance { get; private set; }

    // 로드 시 저장된 시드로 MapData를 재생성할 때 사용할 제너레이터
    [SerializeField] private MapDataGenerator m_MapDataGenerator;

    // 새 맵 생성 시 (0,0) 근처에 시작 시민으로 소환할 프리팹. ISavable을 구현한 Citizen 컴포넌트가 있어야 한다.
    [SerializeField] private Citizen m_InitialCitizenPrefab;
    // 시작 시민 위치를 찾을 때 (0,0)에서 걸을 수 있는 곳까지 검색할 반경 (정규화 맵 좌표 단위)
    [SerializeField] private float m_InitialCitizenSearchRadius = 2f;
    // 청크 TerrainPainter의 Map View Size와 반드시 같아야 하는, 월드 좌표와 맵 좌표를 환산할 때 쓰는 값
    [SerializeField, FormerlySerializedAs("m_OriginMapViewSize")] private float m_MapViewSize = 3f;
    // 청크 하나가 실제로 만들어내는 터레인의 가로/세로 크기(월드 단위)와 반드시 같아야 하는 값
    [SerializeField, FormerlySerializedAs("m_OriginTerrainSize")] private Vector2 m_ChunkTerrainSize = new Vector2(500f, 500f);

    // 전역 접근 경로로 자신을 등록한다
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("MapSaveUtil: 씬에 인스턴스가 둘 이상 있어 먼저 등록된 쪽을 그대로 사용합니다.", this);
            return;
        }

        Instance = this;
    }

    // 씬을 벗어날 때 파괴된 자신이 전역 경로에 남지 않도록 등록을 해제한다
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // 터레인 한 청크가 출력하는 맵 영역의 한 변 길이 (정규화 맵 좌표). 청크를 굽는 쪽이 백그라운드 계산에 들어가기 전에 받아 간다.
    public float MapViewSize => m_MapViewSize;

    // 현재 열려 있는 맵의 데이터. GetMapData나 TryCreateNewMap으로 한 번 생성되기 전에는 null이다.
    public MapData CurrentMapData => m_MapDataGenerator != null ? m_MapDataGenerator.LastMapData : null;

    // 월드 좌표를 정규화 맵 좌표로 변환한다. 모든 청크가 같은 Map View Size와 터레인 크기를 쓰고 청크 좌표에 비례해 배치되므로,
    // 어느 청크 위인지 따질 필요 없이 월드 XZ만으로 결정되는 하나의 변환식이 된다. (청크 매니저가 원점에 있다고 가정한다)
    public Vector2 WorldToMapPosition(Vector3 worldPosition)
    {
        return new Vector2(worldPosition.x, worldPosition.z) / m_ChunkTerrainSize * m_MapViewSize;
    }

    // 정규화 맵 좌표를 월드 XZ 좌표로 변환한다. WorldToMapPosition의 역함수다.
    public Vector2 MapToWorldXZ(Vector2 mapPosition)
    {
        return mapPosition / m_MapViewSize * m_ChunkTerrainSize;
    }

    //맵 데이터와 저장되어야 하는 오브젝트들을 MapSaveData로 묶어 반환
    public MapSaveData GetSaveData(MapData mapData, ResourceInventory inventory, ItemCodex codex, List<ISavable> savables, CameraController cameraController)
    {
        MapSaveData saveData = new MapSaveData();
        saveData.Seed = mapData.Seed;
        saveData.InventoryJson = inventory.CaptureJson();

        if (codex != null)
            saveData.CodexJson = codex.CaptureJson();

        if (cameraController != null)
            saveData.CameraJson = cameraController.CaptureJson();

        foreach (ISavable savable in savables)
        {
            saveData.Savables.Add(new SavableEntry
            {
                PrefabId = savable.PrefabId,
                StateJson = savable.CaptureJson()
            });
        }

        return saveData;
    }

    // 저장된 시드로 MapData를 재생성해 반환한다.
    public MapData GetMapData(MapSaveData saveData)
    {
        if (m_MapDataGenerator == null)
        {
            Debug.LogError("MapSaveUtil: MapDataGenerator가 연결되지 않았습니다.");
            return null;
        }

        return m_MapDataGenerator.GenerateMapData(saveData.Seed);
    }

    // 시드만으로 새 맵 저장 파일을 생성한다. (0,0)에서 가장 가까운 걸을 수 있는 위치에 시작 시민을 두고 카메라도
    // 그 위치를 보도록 채워 넣는다. 이미 슬롯이 있으면 덮어쓴다. 성공하면 True 반환
    public bool TryCreateNewMap(string slot, int seed)
    {
        MapSaveData saveData = new MapSaveData
        {
            Seed = seed,
            // 빈 문자열은 JsonUtility 파싱에 실패하므로, 내용 없는 유효한 JSON으로 채워 둔다.
            InventoryJson = "{}",
        };

        HandlePlaceInitialCitizenAndCamera(saveData, seed);

        return TrySaveMap(saveData, slot);
    }

    // (0,0)에서 가장 가까운 걸을 수 있는 위치를 찾아 시작 시민의 저장 항목과 카메라 위치를 saveData에 채운다.
    private void HandlePlaceInitialCitizenAndCamera(MapSaveData saveData, int seed)
    {
        if (m_MapDataGenerator == null || m_InitialCitizenPrefab == null)
            return;

        MapData mapData = m_MapDataGenerator.GenerateMapData(seed);
        if (mapData == null)
            return;

        if (!mapData.TryGetNearestWalkablePosition(Vector2.zero, out Vector2 spawnMapPosition, m_InitialCitizenSearchRadius))
            Debug.LogWarning("MapSaveUtil: 검색 반경 안에서 걸을 수 있는 위치를 찾지 못해 (0,0) 위치에 시작 시민을 둡니다.");

        Vector2 spawnWorldPosition = MapToWorldXZ(spawnMapPosition);

        saveData.Savables.Add(new SavableEntry
        {
            PrefabId = m_InitialCitizenPrefab.PrefabId,
            StateJson = JsonUtility.ToJson(new InitialPositionState { X = spawnWorldPosition.x, Z = spawnWorldPosition.y }),
        });

        saveData.CameraJson = JsonUtility.ToJson(new InitialCameraState { Position = spawnWorldPosition });
    }

    // SavableHandler가 저장하는 것과 같은 필드만 담은, 시작 시민 위치 JSON 직렬화용 값 묶음
    [Serializable]
    private struct InitialPositionState
    {
        public float X;
        public float Z;
        public float YAngle;
    }

    // CameraController가 저장하는 것과 같은 필드만 담은, 시작 카메라 위치 JSON 직렬화용 값 묶음
    [Serializable]
    private struct InitialCameraState
    {
        public Vector2 Position;
    }

    //MapSaveData를 JSON 포맷으로 로컬에 저장 시도, 성공하면 True 반환
    public bool TrySaveMap(MapSaveData saveData, string slot)
    {
        try
        {
            string path = HandleGetSavePath(slot);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(saveData, true));
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"MapSaveUtil: '{slot}' 저장 실패 - {exception.Message}");
            return false;
        }
    }

    // 슬롯의 저장 파일을 읽어 MapSaveData로 반환한다. 파일이 없거나 읽기에 실패하면 null을 반환한다.
    public MapSaveData TryReadMapFile(string slot)
    {
        try
        {
            string path = HandleGetSavePath(slot);
            if (!File.Exists(path))
                return null;

            return JsonUtility.FromJson<MapSaveData>(File.ReadAllText(path));
        }
        catch (Exception exception)
        {
            Debug.LogError($"MapSaveUtil: '{slot}' 읽기 실패 - {exception.Message}");
            return null;
        }
    }

    // 슬롯의 저장 파일을 삭제 시도, 성공하면(파일이 원래 없던 경우 포함) True 반환
    public bool TryDeleteMap(string slot)
    {
        try
        {
            string path = HandleGetSavePath(slot);
            if (File.Exists(path))
                File.Delete(path);

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"MapSaveUtil: '{slot}' 삭제 실패 - {exception.Message}");
            return false;
        }
    }

    // 슬롯 이름으로 저장 파일의 전체 경로를 만든다. 파일명에 쓸 수 없는 문자가 있으면 예외를 던진다.
    private string HandleGetSavePath(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot) || slot.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException($"잘못된 슬롯 이름: '{slot}'");

        return Path.Combine(Application.persistentDataPath, "Saves", slot + ".json");
    }
}
