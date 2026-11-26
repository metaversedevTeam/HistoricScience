using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// MapChunkManager를 이용해 등록된 추적 대상들 주변의 청크만 로딩하고, 모든 대상에게서 멀어진 청크는 메모리에서 해제하는 클래스.
// 대상에 가까운 LOD0 로딩 범위 안은 풀·자원·알파맵 블러까지 갖춘 LOD0로, 그 바깥 LOD1 로딩 범위까지는 원경용 LOD1로,
// 그 바깥 LOD2 로딩 범위까지는 알파맵을 칠하지 않는 최원경용 LOD2로 로딩한다.
// LOD0 청크는 LOD0 유지 범위(LOD0 로딩 범위 + 1) 밖으로 나가면 LOD1로, LOD0·LOD1 청크는 LOD1 유지 범위(LOD1 로딩 범위 + 1) 밖으로 나가면 LOD2로 낮추고,
// 모든 청크는 LOD2 유지 범위(LOD2 로딩 범위 + 1) 밖으로 나가면 해제한다.
// 추적 대상은 인스펙터로 미리 등록하거나 런타임에 AddTarget/RemoveTarget으로 바꿀 수 있다.
// 스스로 시작하지 않으며, 씬 진입점이 시드 주입을 마친 뒤 BeginLoadingAsync를 호출해야 한다. 초기 로딩은 LOD1 로딩 범위까지만 채우고 끝나며,
// 그 바깥 LOD2 청크는 이어지는 추적 루프가 플레이 중에 채운다. 이후 대상 위치를 주기적으로 다시 확인하며 로딩/해제를 이어 간다.
// 무거운 지형 계산은 MapChunkManager.DrawChunkAsync를 통해 백그라운드 스레드에서 여러 청크를 동시에 처리해 메인 스레드 프레임드랍을 막는다.
public class MapChunkLoader : MonoBehaviour
{
    // 청크를 실제로 소환/굽는 매니저
    [SerializeField] private MapChunkManager m_ChunkManager;
    // 해제된 청크에 남아 있던 유닛/건물을 저장 후 파괴하는 언로더. 비워 두면 오브젝트는 해제되지 않고 그대로 남는다.
    [SerializeField] private ChunkObjectUnloader m_ObjectUnloader;
    // 이 대상들 각각의 주변 청크를 로딩한다 (예: 메인 카메라, 조작 중인 유닛들)
    [SerializeField] private List<Transform> m_FollowTargets = new List<Transform>();
    // 추적 대상 위치를 다시 확인해 로딩/해제 목록을 갱신하는 주기(초)
    [SerializeField, Min(0.05f)] private float m_RefreshInterval = 0.5f;
    // 동시에 백그라운드 스레드에서 계산할 최대 청크 개수. 클수록 빨리 채워지지만 CPU 부하가 커진다.
    [SerializeField, Min(1)] private int m_MaxConcurrentLoads = 16;

    // 가장 가까운 대상 순서로 정렬해 둔 로딩 대기열. 아직 없는 청크의 새 로딩과 이미 있는 청크의 더 높은 LOD로 올리기가 함께 들어간다.
    private readonly Queue<Vector2Int> m_PendingChunks = new Queue<Vector2Int>();
    // 대기열에 있거나 지금 로딩 중인 좌표. 같은 청크를 중복으로 맡기거나 로딩 도중에 해제하는 것을 막는다.
    private readonly HashSet<Vector2Int> m_ReservedChunks = new HashSet<Vector2Int>();
    // 이번 갱신 시점에 로딩되어 있어야 하는 청크 좌표와, 그 청크가 갖춰야 하는 품질 단계
    private readonly Dictionary<Vector2Int, ChunkLod> m_DesiredChunks = new Dictionary<Vector2Int, ChunkLod>();
    // 추적 대상들이 속한 청크 좌표. 매 갱신마다 다시 채운다.
    private readonly List<Vector2Int> m_TargetCoordinates = new List<Vector2Int>();
    // 갱신 중에 좌표 목록을 담아 두는 버퍼. 순회 도중 컬렉션이 바뀌는 것을 막는다.
    private readonly List<Vector2Int> m_CoordinateBuffer = new List<Vector2Int>();
    // 오브젝트가 파괴될 때 진행 중인 로딩과 추적 루프를 멈추기 위한 취소 토큰
    private CancellationTokenSource m_Cancellation;
    // 마지막으로 계산해 알린 로딩 진행도(0~1). 같은 값을 반복해서 알리지 않기 위해 보관한다.
    private float m_LoadProgress;

    // 각 추적 대상이 속한 청크를 중심으로 이 반경(청크 단위) 안의 청크를 LOD0로 로딩한다. 설정 화면에서 바꾼 값을 갱신마다 다시 읽는다.
    private int Lod0LoadRadius => GameSettings.ChunkLod0LoadRadius;

    // 각 추적 대상이 속한 청크를 중심으로 이 반경(청크 단위) 안에서 LOD0 반경 밖의 청크를 LOD1로 로딩한다. 항상 LOD0 반경 이상이다.
    private int Lod1LoadRadius => GameSettings.ChunkLod1LoadRadius;

    // 각 추적 대상이 속한 청크를 중심으로 이 반경(청크 단위) 안의 청크까지 로딩하며, LOD1 반경 밖은 LOD2로 로딩한다. 항상 LOD1 반경보다 1 이상 크다.
    private int Lod2LoadRadius => GameSettings.ChunkLod2LoadRadius;

    // 모든 추적 대상에게서 이 반경(청크 단위) 밖으로 벗어난 LOD0 청크를 LOD1로 낮춘다. LOD0 로딩 반경 + 1이다.
    private int Lod0KeepRadius => GameSettings.ChunkLod0KeepRadius;

    // 모든 추적 대상에게서 이 반경(청크 단위) 밖으로 벗어난 LOD0·LOD1 청크를 LOD2로 낮춘다. LOD1 로딩 반경 + 1이다.
    private int Lod1KeepRadius => GameSettings.ChunkLod1KeepRadius;

    // 모든 추적 대상에게서 이 반경(청크 단위) 밖으로 벗어난 청크를 해제한다. LOD2 로딩 반경 + 1이다.
    private int Lod2KeepRadius => GameSettings.ChunkLod2KeepRadius;

    // LOD1 로딩 범위 안의 등록된 청크 중 로딩이 끝난 청크의 비율(0~1)이 바뀔 때마다 알린다. 로딩 화면의 프로그래스 바가 이 값을 표시한다.
    public event Action<float> LoadProgressChanged;

    // LOD1 로딩 범위 안의 등록된 청크(맵을 열기 전에 로딩되어 있어야 하는 청크) 중 로딩이 끝난 청크의 비율(0~1)
    public float LoadProgress => m_LoadProgress;

    private void OnDestroy()
    {
        StopLoading();
    }

    // 추적 루프와 진행 중인 로딩을 멈춘다. 이미 백그라운드에 맡긴 청크는 끝까지 돌지만 그 뒤로 새 청크는 맡기지 않는다. (로딩 취소 시에도 쓴다)
    public void StopLoading()
    {
        if (m_Cancellation == null)
            return;

        m_Cancellation.Cancel();
        m_Cancellation.Dispose();
        m_Cancellation = null;
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

        m_Cancellation = new CancellationTokenSource();
        CancellationToken token = m_Cancellation.Token;

        if (!HandleCollectTargetCoordinates())
        {
            Debug.LogError("MapChunkLoader: 추적 대상이 하나도 없어 청크를 로딩할 수 없습니다.");
            return;
        }

        // 맵을 여는 데 필요한 LOD1 로딩 범위까지만 기다리고, 그 바깥 LOD2 청크는 추적 루프의 첫 갱신에서 플레이 중에 채운다.
        HandleCollectDesiredChunks();
        HandleEnqueueMissingChunks(ChunkLod.Lod1);
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
    // 초기 로딩에서 건너뛴 LOD2 청크를 곧바로 채우도록 첫 갱신은 기다리지 않고 바로 실행한다.
    private async void HandleTrackTargets(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (HandleCollectTargetCoordinates())
                {
                    HandleCollectDesiredChunks();
                    HandleUnloadDistantChunks();
                    HandleReduceDistantChunks();
                    HandleUnloadDistantObjects();
                    HandleEnqueueMissingChunks(ChunkLod.Lod2);

                    await HandleLoadPendingChunksAsync(token);
                }

                await Task.Delay(Mathf.RoundToInt(m_RefreshInterval * 1000f), token);
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

    // 각 추적 대상 주변 LOD2 로딩 반경 안의 청크 좌표를 모두 모아, 이번 갱신에 로딩되어 있어야 하는 청크와 품질 단계를 정한다.
    // 한 청크를 여러 대상이 두면 그중 가장 높은 품질(가장 작은 LOD)을 고른다.
    private void HandleCollectDesiredChunks()
    {
        m_DesiredChunks.Clear();
        int lod0RadiusSqr = Lod0LoadRadius * Lod0LoadRadius;
        int lod1RadiusSqr = Lod1LoadRadius * Lod1LoadRadius;
        int lod2Radius = Lod2LoadRadius;
        int lod2RadiusSqr = lod2Radius * lod2Radius;

        foreach (Vector2Int targetCoordinate in m_TargetCoordinates)
        {
            for (int y = -lod2Radius; y <= lod2Radius; y++)
            {
                for (int x = -lod2Radius; x <= lod2Radius; x++)
                {
                    int distanceSqr = x * x + y * y;
                    if (distanceSqr > lod2RadiusSqr)
                        continue;

                    ChunkLod lod = HandleGetLodForDistance(distanceSqr, lod0RadiusSqr, lod1RadiusSqr);
                    Vector2Int coordinate = new Vector2Int(targetCoordinate.x + x, targetCoordinate.y + y);
                    if (!m_DesiredChunks.TryGetValue(coordinate, out ChunkLod existingLod) || lod < existingLod)
                        m_DesiredChunks[coordinate] = lod;
                }
            }
        }
    }

    // 추적 대상까지의 거리 제곱이 LOD0·LOD1 반경 제곱 안이면 그 LOD를, 둘 다 벗어나면 LOD2를 반환한다. 로딩 범위와 유지 범위 판정에 함께 쓴다.
    private static ChunkLod HandleGetLodForDistance(int distanceSqr, int lod0RadiusSqr, int lod1RadiusSqr)
    {
        if (distanceSqr <= lod0RadiusSqr)
            return ChunkLod.Lod0;

        if (distanceSqr <= lod1RadiusSqr)
            return ChunkLod.Lod1;

        return ChunkLod.Lod2;
    }

    // 모든 추적 대상에게서 LOD0 유지 반경 밖으로 벗어난 LOD0 청크는 LOD1로, LOD1 유지 반경 밖으로 벗어난 LOD0·LOD1 청크는 LOD2로 낮춘다.
    // 다시 계산하지 않고 풀과 자원만 지운다. 지금 로딩 중인 청크는 다음 갱신에서 처리한다.
    private void HandleReduceDistantChunks()
    {
        int lod0KeepRadiusSqr = Lod0KeepRadius * Lod0KeepRadius;
        int lod1KeepRadiusSqr = Lod1KeepRadius * Lod1KeepRadius;

        // 순회 중 컬렉션이 바뀌지 않도록 좌표를 먼저 모아 두고, 낮출 단계는 낮출 때 다시 계산한다.
        m_CoordinateBuffer.Clear();
        foreach (Vector2Int coordinate in m_ChunkManager.ActiveChunkCoordinates)
        {
            if (!m_ReservedChunks.Contains(coordinate))
                m_CoordinateBuffer.Add(coordinate);
        }

        foreach (Vector2Int coordinate in m_CoordinateBuffer)
        {
            if (!m_ChunkManager.TryGetChunkLod(coordinate, out ChunkLod currentLod))
                continue;

            ChunkLod allowedLod = HandleGetLodForDistance(HandleGetSquaredDistanceToTargets(coordinate), lod0KeepRadiusSqr, lod1KeepRadiusSqr);
            if (currentLod < allowedLod)
                m_ChunkManager.ReduceChunk(coordinate, allowedLod);
        }
    }

    // 모든 추적 대상에게서 LOD2 유지 반경 밖으로 벗어난 청크를 지운다. 지금 로딩 중인 청크는 계산이 끝난 뒤 다음 갱신에서 처리한다.
    private void HandleUnloadDistantChunks()
    {
        m_CoordinateBuffer.Clear();
        int keepRadius = Lod2KeepRadius;
        int keepRadiusSqr = keepRadius * keepRadius;

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

    // 아직 소환되지 않은 청크와 정해진 품질보다 낮은 LOD로 소환되어 있는 청크를, 로딩 중이 아니면 가장 가까운 대상에서 가까운 순서로 대기열에 넣는다.
    // 정해진 품질이 maxLod보다 낮은(더 먼) 청크는 넣지 않는다. 초기 로딩에서 LOD2 청크를 뒤로 미룰 때 쓴다.
    private void HandleEnqueueMissingChunks(ChunkLod maxLod)
    {
        m_CoordinateBuffer.Clear();

        foreach (KeyValuePair<Vector2Int, ChunkLod> desired in m_DesiredChunks)
        {
            if (desired.Value > maxLod)
                continue;

            if (m_ReservedChunks.Contains(desired.Key))
                continue;

            if (m_ChunkManager.TryGetChunkLod(desired.Key, out ChunkLod currentLod) && currentLod <= desired.Value)
                continue;

            m_CoordinateBuffer.Add(desired.Key);
        }

        m_CoordinateBuffer.Sort((a, b) => HandleGetSquaredDistanceToTargets(a).CompareTo(HandleGetSquaredDistanceToTargets(b)));

        foreach (Vector2Int coordinate in m_CoordinateBuffer)
        {
            m_PendingChunks.Enqueue(coordinate);
            m_ReservedChunks.Add(coordinate);
        }

        HandleRefreshLoadProgress();
    }

    // 맵을 여는 데 필요한 LOD1 로딩 범위 안의 등록된 청크 중 로딩이 끝난 청크의 비율을 다시 계산해, 값이 바뀌었으면 알린다.
    // 플레이 중에 채우는 LOD2 청크는 세지 않는다. 대기열을 채운 뒤와 청크 하나가 로딩될 때마다 호출한다.
    private void HandleRefreshLoadProgress()
    {
        int registeredCount = 0;
        int loadedCount = 0;

        foreach (KeyValuePair<Vector2Int, ChunkLod> desired in m_DesiredChunks)
        {
            if (desired.Value > ChunkLod.Lod1)
                continue;

            registeredCount++;

            // 대기열에 있거나 지금 백그라운드에서 계산 중인 청크는 아직 로딩이 끝나지 않은 것으로 센다.
            if (!m_ReservedChunks.Contains(desired.Key) && m_ChunkManager.IsChunkActive(desired.Key))
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
                runningLoads.Add(HandleStartChunkLoad(coordinate), coordinate);
            }

            Task finishedLoad = await Task.WhenAny(runningLoads.Keys);
            m_ReservedChunks.Remove(runningLoads[finishedLoad]);
            runningLoads.Remove(finishedLoad);

            HandleRefreshLoadProgress();

            // 예외가 있었다면 여기서 다시 던져 드러나게 한다.
            await finishedLoad;
        }
    }

    // 대기열에서 꺼낸 청크가 이미 소환되어 있으면 이번 갱신에서 정한 품질로 올리고, 없으면 그 품질로 새로 그리기 시작한다.
    // 대기 중에 범위에서 빠진 청크는 가장 가벼운 LOD2로 그린다(이미 소환되어 있으면 올리지 않는다).
    private Task HandleStartChunkLoad(Vector2Int coordinate)
    {
        ChunkLod lod = m_DesiredChunks.TryGetValue(coordinate, out ChunkLod desiredLod) ? desiredLod : ChunkLod.Lod2;

        if (m_ChunkManager.IsChunkActive(coordinate))
            return m_ChunkManager.UpgradeChunkAsync(coordinate, lod);

        return m_ChunkManager.DrawChunkAsync(coordinate, lod);
    }
}
