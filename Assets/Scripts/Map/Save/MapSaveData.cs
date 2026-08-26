using System;
using System.Collections.Generic;

//맵의 시드와 ISavable들을 파일로 저장하기 위한 포맷
[Serializable]
public class MapSaveData
{
    public int Seed;
    public string InventoryJson;
    public string CodexJson;
    public string ResearchJson;
    public string CameraJson;
    public List<SavableEntry> Savables = new();
}

//프리팹 식별자와 캡처된 상태 JSON 한 쌍. xz위치/y축 각도는 각 ISavable 구현체가 상태 JSON 안에 직접 포함한다.
[Serializable]
public struct SavableEntry
{
    public string PrefabId;
    public string StateJson;
}
