using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

// MapChunkManager를 이용해 0,0 청크를 기준으로 가까운 청크부터 순서대로 계속 로딩하는 클래스.
// 스스로 시작하지 않으며, 씬 진입점이 시드 주입을 마친 뒤 BeginLoadingAsync를 호출해야 한다.
// 무거운 지형 계산은 MapChunkManager.DrawChunkAsync를 통해 백그라운드 스레드에서 여러 청크를 동시에 처리해 메인 스레드 프레임드랍을 막고, 한번 로딩한 청크는 지우지 않는다.
public class MapChunkLoader : MonoBehaviour
{
    // 청크를 실제로 소환/굽는 매니저
    [SerializeField] private MapChunkManager m_ChunkManager;
    // 0,0 청크를 중심으로 이 반경(청크 단위) 안의 청크까지 로딩한다.
    [SerializeField, Min(0)] private int m_LoadRadius = 10;
    // 동시에 백그라운드 스레드에서 계산할 최대 청크 개수. 클수록 빨리 채워지지만 CPU 부하가 커진다.
    [SerializeField, Min(1)] private int m_MaxConcurrentLoads = 16;

    // 0,0에서 가까운 순서로 미리 정렬해 둔 로딩 대기열
    private Queue<Vector2Int> m_PendingChunks;

    // 로딩 대기열을 만들고 비동기 청크 로딩을 시작한다. 청크 시드가 주입된 뒤에 호출되어야 한다.
    public async Task BeginLoadingAsync()
    {
        if (m_ChunkManager == null)
        {
            Debug.LogError("MapChunkLoader: ChunkManager가 지정되지 않았습니다.");
            return;
        }

        m_PendingChunks = HandleBuildLoadQueue();
        await HandleLoadChunksAsync();
    }

    // 반경 안의 모든 청크 좌표를 0,0으로부터 가까운 순서로 정렬한 대기열로 만든다.
    private Queue<Vector2Int> HandleBuildLoadQueue()
    {
        List<Vector2Int> coordinates = new List<Vector2Int>();
        int radiusSqr = m_LoadRadius * m_LoadRadius;

        for (int y = -m_LoadRadius; y <= m_LoadRadius; y++)
        {
            for (int x = -m_LoadRadius; x <= m_LoadRadius; x++)
            {
                Vector2Int coordinate = new Vector2Int(x, y);
                if (coordinate.sqrMagnitude <= radiusSqr)
                    coordinates.Add(coordinate);
            }
        }

        coordinates.Sort((a, b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));
        return new Queue<Vector2Int>(coordinates);
    }

    // 대기열이 빌 때까지 최대 동시 개수만큼 청크 로딩을 백그라운드 스레드에 병렬로 맡기고, 하나가 끝날 때마다 다음 청크를 이어서 맡긴다.
    private async Task HandleLoadChunksAsync()
    {
        List<Task> runningLoads = new List<Task>();

        while (m_PendingChunks.Count > 0 || runningLoads.Count > 0)
        {
            while (runningLoads.Count < m_MaxConcurrentLoads && m_PendingChunks.Count > 0)
            {
                Vector2Int coordinate = m_PendingChunks.Dequeue();
                runningLoads.Add(m_ChunkManager.DrawChunkAsync(coordinate));
            }

            Task finishedLoad = await Task.WhenAny(runningLoads);
            runningLoads.Remove(finishedLoad);
            // 예외가 있었다면 여기서 다시 던져 드러나게 한다.
            await finishedLoad;
        }
    }
}
