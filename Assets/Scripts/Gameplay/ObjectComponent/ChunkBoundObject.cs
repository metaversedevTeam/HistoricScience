using UnityEngine;

// 이 컴포넌트가 붙은 오브젝트를 청크 수명에 묶는 마커. 자기 위치의 청크가 해제되면 상태가 맵의 저장 오브젝트
// 대기 목록으로 돌아간 뒤 파괴되고, 그 청크가 다시 로딩될 때 ChunkSavableSpawner가 같은 상태로 다시 소환한다.
// 같은 오브젝트에 ISavable 구현체가 있어야 하고, 다시 소환할 프리팹이 SavablePrefabRegistry에 등록되어 있어야 한다.
public class ChunkBoundObject : MonoBehaviour
{
    // 상태 캡처에 사용할 같은 오브젝트의 ISavable 구현체
    private ISavable m_Savable;

    // 상태를 캡처할 ISavable을 캐싱한다. 없으면 청크가 해제될 때 되살릴 방법이 없으므로 곧바로 알린다.
    private void Awake()
    {
        m_Savable = GetComponent<ISavable>();

        if (m_Savable == null)
            Debug.LogError($"ChunkBoundObject: '{name}'에 ISavable 컴포넌트가 없어 청크 해제 시 상태를 저장할 수 없습니다.", this);
    }

    // 현재 상태를 저장 항목으로 캡처한다. 다시 소환할 수 없는 상태면 false를 반환해 파괴되지 않게 한다.
    public bool TryCaptureEntry(out SavableEntry entry)
    {
        entry = default;

        if (m_Savable == null)
            return false;

        if (string.IsNullOrEmpty(m_Savable.PrefabId))
        {
            Debug.LogError($"ChunkBoundObject: '{name}'의 PrefabId가 비어 있어 다시 소환할 수 없으므로 청크 해제에서 제외합니다.", this);
            return false;
        }

        entry = new SavableEntry
        {
            PrefabId = m_Savable.PrefabId,
            StateJson = m_Savable.CaptureJson(),
        };

        return true;
    }
}
