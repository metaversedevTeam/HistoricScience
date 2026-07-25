using System.Collections.Generic;
using UnityEngine;

// 청크 터레인이 구워진 뒤, 저장 파일에서 읽은 Savable 대기 목록 중 이 청크 영역에 속한 것들을 소환해 상태를 복원하는 컴포넌트
public class ChunkSavableSpawner : MonoBehaviour
{
    // 청크 영역 판정에 사용할 대상 터레인
    [SerializeField] private Terrain m_Terrain;
    // PrefabId로 소환할 프리팹을 찾는 레지스트리
    [SerializeField] private SavablePrefabRegistry m_Registry;

    // 대기 목록에서 이 청크 영역 안의 항목들을 꺼내 소환하고 상태를 복원한다. 처리된 항목은 목록에서 제거되어 다시 소환되지 않는다.
    public void SpawnSavables(List<SavableEntry> pendingEntries)
    {
        if (m_Terrain == null || m_Terrain.terrainData == null || m_Registry == null || pendingEntries == null)
            return;

        for (int i = pendingEntries.Count - 1; i >= 0; i--)
        {
            if (!SavableHandler.TryReadPositionXZ(pendingEntries[i].StateJson, out Vector2 positionXZ))
            {
                Debug.LogError($"ChunkSavableSpawner: 상태 JSON에서 위치를 읽지 못해 '{pendingEntries[i].PrefabId}' 복원을 건너뜁니다.");
                pendingEntries.RemoveAt(i);
                continue;
            }

            if (!HandleContains(positionXZ))
                continue;

            // 시민처럼 청크 사이를 오갈 수 있는 오브젝트이므로 청크의 자식으로 만들지 않는다.
            m_Registry.SpawnSavable(pendingEntries[i]);
            pendingEntries.RemoveAt(i);
        }
    }

    // 주어진 xz 월드 좌표가 이 청크 터레인의 영역 안인지 판정한다. 경계에 걸친 좌표가 이웃 청크와 중복 판정되지 않도록 최소 경계는 포함하고 최대 경계는 제외한다.
    private bool HandleContains(Vector2 positionXZ)
    {
        Vector3 origin = m_Terrain.transform.position;
        Vector3 size = m_Terrain.terrainData.size;

        return positionXZ.x >= origin.x && positionXZ.x < origin.x + size.x
            && positionXZ.y >= origin.z && positionXZ.y < origin.z + size.z;
    }
}
