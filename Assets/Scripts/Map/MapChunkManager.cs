using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using HistoricScience.Test;

// 무한 맵을 청크 단위로 나누어, 특정 청크 좌표에 MapChunkTerrain 프리팹을 소환해 지형을 그리거나 지우는 관리자
public class MapChunkManager : MonoBehaviour
{
    // 소환할 청크 프리팹. Terrain, TerrainCollider, TerrainPainter가 구성되어 있어야 한다.
    [SerializeField] private GameObject m_ChunkPrefab;

    // 이 맵의 모든 청크가 공유하는 맵 데이터. 무한 평면 전체를 담당하는 불변 객체라 하나를 모든 청크에 그대로 넘긴다.
    // 새 게임 시작이나 저장 파일 로드 시 SetMapData로 주입받는다.
    private MapData m_MapData;

    // 아직 소환되지 않은 저장 오브젝트 대기 목록. 청크가 구워질 때마다 그 청크의 스포너가 자기 영역의 항목을 꺼내 소환한다.
    private List<SavableEntry> m_PendingSavables;

    // 현재 소환되어 있는 청크 오브젝트를 청크 좌표 기준으로 보관한다.
    private readonly Dictionary<Vector2Int, GameObject> m_ActiveChunks = new Dictionary<Vector2Int, GameObject>();

    // 청크 하나가 월드에서 차지하는 XZ 크기 캐시. 모든 청크가 같은 프리팹에서 나오므로 한 번만 계산해 둔다.
    private Vector2? m_ChunkWorldSize;

    // 현재 소환되어 있는 청크들의 좌표. 청크 로더가 해제 대상을 고를 때 쓴다.
    public IReadOnlyCollection<Vector2Int> ActiveChunkCoordinates => m_ActiveChunks.Keys;

    // 청크 하나가 월드에서 차지하는 XZ 크기. 월드 좌표와 청크 좌표를 변환할 때 쓴다.
    public Vector2 ChunkWorldSize
    {
        get
        {
            if (m_ChunkWorldSize == null)
                m_ChunkWorldSize = HandleResolveChunkWorldSize();

            return m_ChunkWorldSize.Value;
        }
    }

    // 주어진 좌표에 청크가 소환되어 있는지 반환한다.
    public bool IsChunkActive(Vector2Int chunkCoordinate)
    {
        return m_ActiveChunks.ContainsKey(chunkCoordinate);
    }

    // 월드 좌표가 속한 청크 좌표를 반환한다. 청크는 이 매니저를 부모로 로컬 좌표에 배치되므로 매니저 위치를 기준으로 계산한다.
    public Vector2Int WorldToChunkCoordinate(Vector3 worldPosition)
    {
        Vector3 localPosition = worldPosition - transform.position;
        Vector2 chunkSize = ChunkWorldSize;

        return new Vector2Int(Mathf.FloorToInt(localPosition.x / chunkSize.x), Mathf.FloorToInt(localPosition.z / chunkSize.y));
    }

    // 프리팹 터레인에 데이터가 있으면 그 크기를, 없으면 TerrainPainter가 런타임에 새로 만들 기본 크기를 청크 크기로 쓴다.
    private Vector2 HandleResolveChunkWorldSize()
    {
        Terrain terrain = m_ChunkPrefab != null ? m_ChunkPrefab.GetComponent<Terrain>() : null;
        Vector3 size = terrain != null && terrain.terrainData != null ? terrain.terrainData.size : TerrainPainter.DefaultTerrainSize;

        return new Vector2(size.x, size.z);
    }

    // 주어진 좌표에 청크 프리팹을 소환하고 곧바로 지형을 굽는다. 이미 소환된 좌표면 기존 오브젝트를 그대로 반환한다.
    public GameObject DrawChunk(Vector2Int chunkCoordinate)
    {
        if (m_ActiveChunks.TryGetValue(chunkCoordinate, out GameObject existingChunk))
            return existingChunk;

        GameObject chunkObject = SpawnChunk(chunkCoordinate);
        if (chunkObject != null)
            PaintChunk(chunkCoordinate);

        return chunkObject;
    }

    // 주어진 좌표에 청크 프리팹을 소환만 하고 아직 굽지는 않는다. 굽기 전에 청크 오브젝트를 추가로 구성해야 할 때 사용한다.
    public GameObject SpawnChunk(Vector2Int chunkCoordinate)
    {
        if (m_ActiveChunks.TryGetValue(chunkCoordinate, out GameObject existingChunk))
            return existingChunk;

        if (m_ChunkPrefab == null || m_ChunkPrefab.GetComponent<TerrainPainter>() == null)
        {
            Debug.LogError("MapChunkManager: ChunkPrefab이 지정되지 않았거나 TerrainPainter가 없습니다.");
            return null;
        }

        GameObject chunkObject = Instantiate(m_ChunkPrefab, transform);
        chunkObject.name = $"{m_ChunkPrefab.name}_{chunkCoordinate.x}_{chunkCoordinate.y}";

        m_ActiveChunks[chunkCoordinate] = chunkObject;
        return chunkObject;
    }

    // 이후 소환/굽기되는 청크들에 주입할 맵 데이터를 설정한다. 이미 구워진 청크에는 소급 적용되지 않으므로 청크를 그리기 전에 호출해야 한다.
    public void SetMapData(MapData mapData)
    {
        m_MapData = mapData;
    }

    // 청크가 구워질 때 각 청크 영역의 저장 오브젝트를 소환하는 데 쓸 대기 목록을 설정한다. 소환된 항목은 목록에서 제거되므로 호출자와 목록을 공유한다.
    public void SetPendingSavables(List<SavableEntry> pendingSavables)
    {
        m_PendingSavables = pendingSavables;
    }

    // 청크가 해제되어 사라지는 오브젝트의 저장 항목을 대기 목록으로 되돌린다. 그 청크가 다시 구워지면 스포너가 이 항목으로 다시 소환한다.
    // 목록이 설정되지 않아 항목을 맡아 둘 곳이 없으면 false를 반환하므로, 호출자는 이때 오브젝트를 파괴해서는 안 된다.
    public bool TryAddPendingSavable(SavableEntry entry)
    {
        if (m_PendingSavables == null)
        {
            Debug.LogError("MapChunkManager: 저장 오브젝트 대기 목록이 설정되지 않아 항목을 되돌릴 수 없습니다.");
            return false;
        }

        m_PendingSavables.Add(entry);
        return true;
    }

    // 이미 소환된 청크의 좌표를 터레인 페인터에 반영해 보로노이 지형을 굽고 월드 위치를 잡는다. 모든 청크에 같은 맵 데이터(m_MapData)를
    // 주입하므로, 각 청크는 같은 무한 맵의 서로 다른 영역을 이어서 보여주게 된다.
    public void PaintChunk(Vector2Int chunkCoordinate)
    {
        if (!HandleCanPaint(chunkCoordinate, out GameObject chunkObject))
            return;

        TerrainPainter painter = chunkObject.GetComponent<TerrainPainter>();
        painter.SetChunkCoordinate(chunkCoordinate);
        painter.PaintVoronoiTerrain(m_MapData);

        HandlePositionChunk(chunkObject, chunkCoordinate);
        HandleSpawnSavables(chunkObject);
    }

    // DrawChunk의 비동기 버전. 무거운 지형 계산을 백그라운드 스레드에서 수행해 메인 스레드를 막지 않는다. 이미 소환된 좌표면 기존 오브젝트를 그대로 반환한다.
    public async Task<GameObject> DrawChunkAsync(Vector2Int chunkCoordinate)
    {
        if (m_ActiveChunks.TryGetValue(chunkCoordinate, out GameObject existingChunk))
            return existingChunk;

        GameObject chunkObject = SpawnChunk(chunkCoordinate);
        if (chunkObject != null)
            await PaintChunkAsync(chunkCoordinate);

        return chunkObject;
    }

    // PaintChunk의 비동기 버전. 알파맵/높이맵 계산만 Task.Run으로 백그라운드 스레드에 맡기고, Terrain에 적용하는 부분은 메인 스레드로 돌아와 처리한다.
    public async Task PaintChunkAsync(Vector2Int chunkCoordinate)
    {
        if (!HandleCanPaint(chunkCoordinate, out GameObject chunkObject))
            return;

        TerrainPainter painter = chunkObject.GetComponent<TerrainPainter>();
        painter.SetChunkCoordinate(chunkCoordinate);

        var context = painter.PrepareForPaint(m_MapData);
        if (context == null)
            return;

        var result = await Task.Run(() => painter.ComputePaint(context.Value));

        // 백그라운드 계산 중에 이 청크가 해제되었을 수 있으므로, 이미 파괴된 터레인을 건드리지 않도록 다시 확인한다.
        if (chunkObject == null || !m_ActiveChunks.TryGetValue(chunkCoordinate, out GameObject currentChunk) || currentChunk != chunkObject)
            return;

        painter.ApplyPaint(result);

        HandlePositionChunk(chunkObject, chunkCoordinate);
        HandleSpawnSavables(chunkObject);
    }

    // 주어진 좌표에 소환된 청크가 있으면 오브젝트와 인메모리 터레인 데이터를 파괴해 지운다.
    public void EraseChunk(Vector2Int chunkCoordinate)
    {
        if (!m_ActiveChunks.TryGetValue(chunkCoordinate, out GameObject chunkObject))
            return;

        HandleDestroyChunkTerrainData(chunkObject);
        Destroy(chunkObject);
        m_ActiveChunks.Remove(chunkCoordinate);
    }

    // 굽기에 필요한 청크 오브젝트와 맵 데이터가 모두 준비되었는지 확인하고, 준비된 청크 오브젝트를 넘겨준다.
    private bool HandleCanPaint(Vector2Int chunkCoordinate, out GameObject chunkObject)
    {
        if (!m_ActiveChunks.TryGetValue(chunkCoordinate, out chunkObject))
        {
            Debug.LogError($"MapChunkManager: {chunkCoordinate} 청크가 소환되어 있지 않습니다.");
            return false;
        }

        if (m_MapData == null)
        {
            Debug.LogError("MapChunkManager: 맵 데이터가 주입되지 않아 청크를 구울 수 없습니다. SetMapData를 먼저 호출해야 합니다.");
            return false;
        }

        return true;
    }

    // 방금 구워져 위치까지 잡힌 청크 영역에 속한 저장 오브젝트들을 청크의 스포너로 소환한다. 대기 목록이나 스포너가 없으면 아무것도 하지 않는다.
    private void HandleSpawnSavables(GameObject chunkObject)
    {
        if (m_PendingSavables == null || m_PendingSavables.Count == 0)
            return;

        ChunkSavableSpawner spawner = chunkObject.GetComponent<ChunkSavableSpawner>();
        if (spawner != null)
            spawner.SpawnSavables(m_PendingSavables);
    }

    // 청크 좌표와 실제로 구워진 터레인 크기를 이용해 청크를 월드 공간의 올바른 위치로 옮긴다.
    private void HandlePositionChunk(GameObject chunkObject, Vector2Int chunkCoordinate)
    {
        Terrain terrain = chunkObject.GetComponent<Terrain>();
        if (terrain == null || terrain.terrainData == null)
            return;

        Vector3 size = terrain.terrainData.size;
        chunkObject.transform.localPosition = new Vector3(chunkCoordinate.x * size.x, 0f, chunkCoordinate.y * size.z);
    }

    // 청크의 인메모리 터레인 데이터를 파괴한다. 에셋으로 저장되지 않으므로 GameObject 파괴만으로는 해제되지 않아 메모리가 샐 수 있다.
    private void HandleDestroyChunkTerrainData(GameObject chunkObject)
    {
        Terrain terrain = chunkObject.GetComponent<Terrain>();
        if (terrain != null && terrain.terrainData != null)
            Destroy(terrain.terrainData);
    }

    // 매니저가 파괴될 때 아직 지워지지 않은 모든 청크의 터레인 데이터를 정리한다.
    private void OnDestroy()
    {
        foreach (KeyValuePair<Vector2Int, GameObject> chunk in m_ActiveChunks)
            HandleDestroyChunkTerrainData(chunk.Value);
    }
}
