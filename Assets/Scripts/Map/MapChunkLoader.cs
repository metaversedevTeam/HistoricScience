using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// MapChunkManager를 이용해 등록된 추적 대상들 주변의 청크만 로딩하고, 모든 대상에게서 멀어진 청크는 메모리에서 해제하는 클래스.
// 추적 대상은 인스펙터로 미리 등록하거나 런타임에 AddTarget/RemoveTarget으로 바꿀 수 있다.
// 스스로 시작하지 않으며, 씬 진입점이 시드 주입을 마친 뒤 BeginLoadingAsync를 호출해야 한다. 초기 로딩이 끝나면
// 대상 위치를 주기적으로 다시 확인하며 로딩/해제를 이어 간다.
// 무거운 지형 계산은 MapChunkManager.DrawChunkAsync를 통해 백그라운드 스레드에서 여러 청크를 동시에 처리해 메인 스레드 프레임드랍을 막는다.
public class MapChunkLoader : MonoBehaviour
{
    // 청크를 실제로 소환/굽는 매니저
    [SerializeField] private MapChunkManager m_ChunkManager;
    // 해제된 청크에 남아 있던 유닛/건물을 저장 후 파괴하는 언로더. 비워 두면 오브젝트는 해제되지 않고 그대로 남는다.
    [SerializeField] private ChunkObjectUnloader m_ObjectUnloader;
    // 이 대상들 각각의 주변 청크를 로딩한다 (예: 메인 카메라, 조작 중인 유닛들)
    [SerializeField] private List<Transform> m_FollowTargets = new List<Transform>();
    // 각 추적 대상이 속한 청크를 중심으로 이 반경(청크 단위) 안의 청크까지 로딩한다.
    [SerializeField, Min(0)] private int m_LoadRadius = 2;
    // 모든 추적 대상에게서 이 반경(청크 단위) 밖으로 벗어난 청크만 해제한다. 로딩 반경보다 커야 경계에서 로딩과 해제가 반복되지 않는다.
    [SerializeField, Min(1)] private int m_KeepRadius = 4;
    // 추적 대상 위치를 다시 확인해 로딩/해제 목록을 갱신하는 주기(초)
    [SerializeField, Min(0.05f)] private float m_RefreshInterval = 0.5f;
    // 동시에 백그라운드 스레드에서 계산할 최대 청크 개수. 클수록 빨리 채워지지만 CPU 부하가 커진다.
    [SerializeField, Min(1)] private int m_MaxConcurrentLoads = 16;

    // 가장 가까운 대상 순서로 정렬해 둔 로딩 대기열
    private readonly Queue<Vector2Int> m_PendingChunks = new Queue<Vector2Int>();
    // 대기열에 있거나 지금 로딩 중인 좌표. 같은 청크를 중복으로 맡기거나 로딩 도중에 해제하는 것을 막는다.
    private readonly HashSet<Vector2Int> m_ReservedChunks = new HashSet<Vector2Int>();
    // 이번 갱신 시점에 로딩되어 있어야 하는 청크 좌표 집합
    private readonly HashSet<Vector2Int> m_DesiredChunks = new HashSet<Vector2Int>();
    // 추적 대상들이 속한 청크 좌표. 매 갱신마다 다시 채운다.
    private readonly List<Vector2Int> m_TargetCoordinates = new List<Vector2Int>();
    // 갱신 중에 좌표 목록을 담아 두는 버퍼. 순회 도중 컬렉션이 바뀌는 것을 막는다.
    private readonly List<Vector2Int> m_CoordinateBuffer = new List<Vector2Int>();
    // 오브젝트가 파괴될 때 진행 중인 로딩과 추적 루프를 멈추기 위한 취소 토큰
    private CancellationTokenSource m_Cancellation;
    // 마지막으로 계산해 알린 로딩 진행도(0~1). 같은 값을 반복해서 알리지 않기 위해 보관한다.
    private float m_LoadProgress;

    // 등록된 청크 중 로딩이 끝난 청크의 비율(0~1)이 바뀔 때마다 알린다. 로딩 화면의 프로그래스 바가 이 값을 표시한다.
    public event Action<float> LoadProgressChanged;

    // 등록된 청크(추적 대상 주변에 로딩되어 있어야 하는 청크) 중 로딩이 끝난 청크의 비율(0~1)
    public float LoadProgress => m_LoadProgress;

    // 추적 루프와 진행 중인 로딩을 멈춘다.
    private void OnDestroy()
    {
        if (m_Cancellation == null)
            return;

        m_Cancellation.Cancel();
        m_Cancellation.Dispose();
        m_Cancellation = null;
    }

    // 인스펙터에서 값을 바꿀 때 해제 반경이 항상 로딩 반경보다 크도록 보정한다.
    private void OnValidate()
    {
        m_KeepRadius = Mathf.Max(m_KeepRadius, m_LoadRadius + 1);
    }

    // 추적 대상 주변 청크를 채우고, 초기 로딩이 끝나면 대상을 계속 따라다니는 추적 루프를 시작한다. 청크 시드가 주입된 뒤에 호출되어야 한다.
    public async Task BeginLoadingAsync()
    {
        if (m_ChunkManager == null)
        {
            Debug.LogError("MapChunkLoader: ChunkManager가 지정되지 않았습니다.");
            return;
        }

        if (m_Cancellation != null)
        {
            Debug.LogWarning("MapChunkLoader: 이미 로딩이 시작되어 있어 요청을 무시합니다.");
            return;
        }

        // 청크 로딩 자체는 언로더 없이도 동작하므로 중단하지 않고, 유닛/건물이 남는다는 사실만 시작 시점에 한 번 알린다.
        if (m_ObjectUnloader == null)
            Debug.LogError("MapChunkLoader: ObjectUnloader가 지정되지 않아 해제된 청크의 유닛/건물이 씬에 그대로 남습니다.");

        // 해제 반경이 로딩 반경보다 작으면 경계의 청크가 로딩과 해제를 무한히 반복하므로 최소한 한 칸 크게 맞춘다.
        m_KeepRadius = Mathf.Max(m_KeepRadius, m_LoadRadius + 1);

        m_Cancellation = new CancellationTokenSource();
        CancellationToken token = m_Cancellation.Token;

        if (!HandleCollectTargetCoordinates())
        {
            Debug.LogError("MapChunkLoader: 추적 대상이 하나도 없어 청크를 로딩할 수 없습니다.");
            return;
        }

        HandleCollectDesiredChunks();
        HandleEnqueueMissingChunks();
        await HandleLoadPendingChunksAsync(token);

        HandleTrackTargets(token);
    }

    // 추적 대상을 추가해 그 주변 청크가 로딩되게 한다. 이미 등록된 대상이면 무시한다.
    public void AddTarget(Transform target)
    {
        if (target == null || m_FollowTargets.Contains(target)) return;

        m_FollowTargets.Add(target);
    }

    // 추적 대상을 제거한다. 이 대상 때문에 로딩되어 있던 청크는 다음 갱신에서 해제된다.
    public void RemoveTarget(Transform target)
    {
        if (target == null) return;

        m_FollowTargets.Remove(target);
    }

    // 취소될 때까지 주기적으로 추적 대상 위치를 다시 확인해, 멀어진 청크는 해제하고 새로 필요해진 청크는 로딩한다.
    private async void HandleTrackTargets(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(Mathf.RoundToInt(m_RefreshInterval * 1000f), token);

                if (!HandleCollectTargetCoordinates())
                    continue;

                HandleCollectDesiredChunks();
                HandleUnloadDistantChunks();
                HandleUnloadDistantObjects();
                HandleEnqueueMissingChunks();

                await HandleLoadPendingChunksAsync(token);
            }
        }
        catch (OperationCanceledException)
        {
            // 오브젝트가 파괴되어 추적을 멈춘 것이므로 정상 종료로 본다.
        }
    }

    // 파괴된 대상을 목록에서 정리하고 남은 대상들이 속한 청크 좌표를 다시 모은다. 유효한 대상이 하나도 없으면 false를 반환한다.
    private bool HandleCollectTargetCoordinates()
    {
        m_TargetCoordinates.Clear();

        for (int i = m_FollowTargets.Count - 1; i >= 0; i--)
        {
            if (m_FollowTargets[i] == null)
            {
                m_FollowTargets.RemoveAt(i);
                continue;
            }

            Vector2Int coordinate = m_ChunkManager.WorldToChunkCoordinate(m_FollowTargets[i].position);
            if (!m_TargetCoordinates.Contains(coordinate))
                m_TargetCoordinates.Add(coordinate);
        }

        return m_TargetCoordinates.Count > 0;
    }

    // 각 추적 대상 주변 로딩 반경 안의 청크 좌표를 모두 모아, 이번 갱신에 로딩되어 있어야 하는 집합을 만든다.
    private void HandleCollectDesiredChunks()
    {
        m_DesiredChunks.Clear();
        int loadRadiusSqr = m_LoadRadius * m_LoadRadius;

        foreach (Vector2Int targetCoordinate in m_TargetCoordinates)
        {
            for (int y = -m_LoadRadius; y <= m_LoadRadius; y++)
            {
                for (int x = -m_LoadRadius; x <= m_LoadRadius; x++)
                {
                    if (x * x + y * y > loadRadiusSqr)
                        continue;

                    m_DesiredChunks.Add(new Vector2Int(targetCoordinate.x + x, targetCoordinate.y + y));
                }
            }
        }
    }

    // 모든 추적 대상에게서 해제 반경 밖으로 벗어난 청크를 지운다. 지금 로딩 중인 청크는 계산이 끝난 뒤 다음 갱신에서 처리한다.
    private void HandleUnloadDistantChunks()
    {
        m_CoordinateBuffer.Clear();
        int keepRadiusSqr = m_KeepRadius * m_KeepRadius;

        foreach (Vector2Int coordinate in m_ChunkManager.ActiveChunkCoordinates)
        {
            if (m_ReservedChunks.Contains(coordinate))
                continue;

            if (HandleGetSquaredDistanceToTargets(coordinate) <= keepRadiusSqr)
                continue;

            m_CoordinateBuffer.Add(coordinate);
        }

        foreach (Vector2Int coordinate in m_CoordinateBuffer)
            m_ChunkManager.EraseChunk(coordinate);
    }

    // 청크 해제로 소환된 청크 밖에 남게 된 유닛/건물을 저장한 뒤 파괴한다. 청크를 지운 직후에 호출해야 한다.
    private void HandleUnloadDistantObjects()
    {
        // 언로더가 없다는 것은 로딩을 시작할 때 이미 알렸으므로, 갱신마다 같은 로그를 반복하지 않는다.
        if (m_ObjectUnloader == null)
            return;

        m_ObjectUnloader.UnloadObjectsOutsideActiveChunks();
    }

    // 아직 소환되지도, 로딩 중이지도 않은 청크를 가장 가까운 대상에서 가까운 순서로 대기열에 넣는다.
    private void HandleEnqueueMissingChunks()
    {
        m_CoordinateBuffer.Clear();

        foreach (Vector2Int coordinate in m_DesiredChunks)
        {
            if (m_ChunkManager.IsChunkActive(coordinate) || m_ReservedChunks.Contains(coordinate))
                continue;

            m_CoordinateBuffer.Add(coordinate);
        }

        m_CoordinateBuffer.Sort((a, b) => HandleGetSquaredDistanceToTargets(a).CompareTo(HandleGetSquaredDistanceToTargets(b)));

        foreach (Vector2Int coordinate in m_CoordinateBuffer)
        {
            m_PendingChunks.Enqueue(coordinate);
            m_ReservedChunks.Add(coordinate);
        }

        HandleRefreshLoadProgress();
    }

    // 등록된 청크 중 로딩이 끝난 청크의 비율을 다시 계산해, 값이 바뀌었으면 알린다. 대기열을 채운 뒤와 청크 하나가 로딩될 때마다 호출한다.
    private void HandleRefreshLoadProgress()
    {
        int registeredCount = m_DesiredChunks.Count;
        int loadedCount = 0;

        foreach (Vector2Int coordinate in m_DesiredChunks)
        {
            // 대기열에 있거나 지금 백그라운드에서 계산 중인 청크는 아직 로딩이 끝나지 않은 것으로 센다.
            if (!m_ReservedChunks.Contains(coordinate) && m_ChunkManager.IsChunkActive(coordinate))
                loadedCount++;
        }

        float progress = registeredCount > 0 ? loadedCount / (float)registeredCount : 1f;
        if (Mathf.Approximately(progress, m_LoadProgress))
            return;

        m_LoadProgress = progress;
        LoadProgressChanged?.Invoke(progress);
    }

    // 주어진 청크 좌표에서 가장 가까운 추적 대상까지의 거리 제곱(청크 단위)을 반환한다. 대상이 없으면 int.MaxValue를 반환한다.
    private int HandleGetSquaredDistanceToTargets(Vector2Int coordinate)
    {
        int nearestSqr = int.MaxValue;

        foreach (Vector2Int targetCoordinate in m_TargetCoordinates)
        {
            int dx = coordinate.x - targetCoordinate.x;
            int dy = coordinate.y - targetCoordinate.y;
            int distanceSqr = dx * dx + dy * dy;

            if (distanceSqr < nearestSqr)
                nearestSqr = distanceSqr;
        }

        return nearestSqr;
    }

    // 대기열이 빌 때까지 최대 동시 개수만큼 청크 로딩을 백그라운드 스레드에 병렬로 맡기고, 하나가 끝날 때마다 다음 청크를 이어서 맡긴다.
    private async Task HandleLoadPendingChunksAsync(CancellationToken token)
    {
        Dictionary<Task, Vector2Int> runningLoads = new Dictionary<Task, Vector2Int>();

        while (m_PendingChunks.Count > 0 || runningLoads.Count > 0)
        {
            if (token.IsCancellationRequested)
                return;

            while (runningLoads.Count < m_MaxConcurrentLoads && m_PendingChunks.Count > 0)
            {
                Vector2Int coordinate = m_PendingChunks.Dequeue();
                runningLoads.Add(m_ChunkManager.DrawChunkAsync(coordinate), coordinate);
            }

            Task finishedLoad = await Task.WhenAny(runningLoads.Keys);
            m_ReservedChunks.Remove(runningLoads[finishedLoad]);
            runningLoads.Remove(finishedLoad);

            HandleRefreshLoadProgress();

            // 예외가 있었다면 여기서 다시 던져 드러나게 한다.
            await finishedLoad;
        }
    }
}
