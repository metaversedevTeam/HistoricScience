using UnityEngine;

//시드 기반으로 생성되지 않아 직접 저장해야하는 객체들 (ex)건물, 유닛, 인벤토리)
public interface ISavable
{
    //로드 시 어떤 프리팹을 소환할지 식별하는 키
    string PrefabId { get; }

    //현재 상태를 JSON 문자열로 캡처한다.
    string CaptureJson();

    //JSON 문자열로 상태를 복원한다.
    void ApplyJson(string json);
}