using UnityEngine;

// PrefabId 없이 자기 상태만 캡처/복원하는 저장 부품. 같은 오브젝트의 ISavable이 자신의 JSON 안에 함께 담아 저장하므로,
// 독립된 저장 오브젝트로 취급되지 않도록 ISavable을 상속하지 않는다.
public interface IStatePart
{
    // 현재 상태를 JSON 문자열로 캡처한다.
    string CaptureJson();

    // JSON 문자열로 상태를 복원한다.
    void ApplyJson(string json);
}
