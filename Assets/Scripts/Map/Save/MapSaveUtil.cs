using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using HistoricScience.Test;

//MapSaveData의 조립과 JSON 파일 저장/읽기를 담당하는 유틸리티
public class MapSaveUtil : MonoBehaviour
{
    // 로드 시 저장된 시드로 MapData를 재생성할 때 사용할 제너레이터
    [SerializeField] private MapDataGenerator m_MapDataGenerator;

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

    // 시드만으로 빈 상태의 새 맵 저장 파일을 생성한다. 이미 슬롯이 있으면 덮어쓴다. 성공하면 True 반환
    public bool TryCreateNewMap(string slot, int seed)
    {
        MapSaveData saveData = new MapSaveData
        {
            Seed = seed,
            // 빈 문자열은 JsonUtility 파싱에 실패하므로, 내용 없는 유효한 JSON으로 채워 둔다.
            InventoryJson = "{}",
        };

        return TrySaveMap(saveData, slot);
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
