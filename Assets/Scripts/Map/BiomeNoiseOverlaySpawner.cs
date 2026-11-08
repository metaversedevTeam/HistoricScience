using UnityEngine;

// 에디터에서 실행할 때만 시작 시점에 BiomeNoiseOverlayDebugger를 소환해 맵 청크 관리자에 연결하는 컴포넌트. 빌드에서는 아무것도 소환하지 않는다.
public class BiomeNoiseOverlaySpawner : MonoBehaviour
{
    // 소환한 디버거가 감시할 맵 청크 관리자. 비어 있으면 씬에서 찾아 쓴다.
    [SerializeField] private MapChunkManager m_ChunkManager;

#if UNITY_EDITOR
    // 소환한 디버거 오브젝트 이름
    private const string k_DebuggerObjectName = "BiomeNoiseOverlayDebugger";

    private void Start()
    {
        HandleSpawnDebugger();
    }

    // 디버거 오브젝트를 이 오브젝트의 자식으로 만들고 맵 청크 관리자를 연결한다.
    private void HandleSpawnDebugger()
    {
        MapChunkManager chunkManager = m_ChunkManager != null ? m_ChunkManager : FindFirstObjectByType<MapChunkManager>();
        if (chunkManager == null)
        {
            Debug.LogError("BiomeNoiseOverlaySpawner: 맵 청크 관리자를 찾지 못해 노이즈 오버레이 디버거를 소환하지 않습니다.", this);
            return;
        }

        // 비활성 상태로 만들어 OnEnable이 관리자 연결 전에 실행되지 않게 한 뒤, 관리자를 지정하고 켠다.
        GameObject debuggerObject = new GameObject(k_DebuggerObjectName);
        debuggerObject.SetActive(false);
        debuggerObject.transform.SetParent(transform, false);

        BiomeNoiseOverlayDebugger debugger = debuggerObject.AddComponent<BiomeNoiseOverlayDebugger>();
        debugger.SetChunkManager(chunkManager);

        debuggerObject.SetActive(true);
    }
#endif
}
