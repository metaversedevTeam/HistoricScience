using System.Collections.Generic;
using UnityEngine;

// 활성 청크 밖으로 벗어난 ChunkBoundObject들을 상태 캡처 후 파괴해, 청크와 함께 메모리에서 내리는 컴포넌트.
// 캡처된 상태는 MapChunkManager의 저장 오브젝트 대기 목록으로 돌아가므로, 그 청크가 다시 로딩되면
// ChunkSavableSpawner가 같은 상태로 다시 소환하고, 다시 소환되기 전에 맵을 저장해도 항목이 유실되지 않는다.
public class ChunkObjectUnloader : MonoBehaviour
{
    // 청크 좌표 판정과 대기 목록 반환에 사용할 청크 매니저
    [SerializeField] private MapChunkManager m_ChunkManager;

    // 파괴 대상을 모아 두는 버퍼. 탐색 도중 오브젝트를 파괴하지 않도록 한 번 모은 뒤에 처리한다.
    private readonly List<ChunkBoundObject> m_UnloadBuffer = new List<ChunkBoundObject>();

    // 활성 청크에 속하지 않은 청크 종속 오브젝트를 모두 저장한 뒤 파괴한다.
    public void UnloadObjectsOutsideActiveChunks()
    {
        if (m_ChunkManager == null)
        {
            Debug.LogError("ChunkObjectUnloader: ChunkManager가 지정되지 않았습니다.");
            return;
        }

        HandleCollectObjectsOutsideActiveChunks();

        foreach (ChunkBoundObject boundObject in m_UnloadBuffer)
        {
            // 저장하지 못한 오브젝트는 파괴하면 되살릴 수 없으므로 그대로 남겨 둔다.
            if (!HandleStoreObject(boundObject))
                continue;

            Destroy(boundObject.gameObject);
        }
    }

    // 씬의 청크 종속 오브젝트 중 소환되어 있는 청크 밖에 있는 것들을 버퍼에 모은다.
    private void HandleCollectObjectsOutsideActiveChunks()
    {
        m_UnloadBuffer.Clear();

        foreach (ChunkBoundObject boundObject in FindObjectsByType<ChunkBoundObject>(FindObjectsSortMode.None))
        {
            Vector2Int chunkCoordinate = m_ChunkManager.WorldToChunkCoordinate(boundObject.transform.position);
            if (m_ChunkManager.IsChunkActive(chunkCoordinate))
                continue;

            m_UnloadBuffer.Add(boundObject);
        }
    }

    // 오브젝트의 상태를 캡처해 매니저의 대기 목록으로 되돌린다. 캡처나 등록에 실패하면 false를 반환한다.
    private bool HandleStoreObject(ChunkBoundObject boundObject)
    {
        if (!boundObject.TryCaptureEntry(out SavableEntry entry))
            return false;

        return m_ChunkManager.TryAddPendingSavable(entry);
    }
}
