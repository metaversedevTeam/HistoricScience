using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using HistoricScience.Test;

// 무한 맵을 청크 단위로 나누어, 특정 청크 좌표에 MapChunkTerrain 프리팹을 소환해 지형을 그리거나 지우는 관리자
public class MapChunkManager : MonoBehaviour
{
    // 소환할 청크 프리팹. Terrain, TerrainCollider, MapDataGenerator, TerrainPainter가 구성되어 있어야 한다.
    [SerializeField] private GameObject m_ChunkPrefab;

    // 이 맵의 모든 청크에 공통으로 주입할 맵 시드. 새 게임 시작이나 저장 파일 로드 시 SetSeed로 갱신한다.
    [SerializeField] private int m_Seed = 0;

    // 아직 소환되지 않은 저장 오브젝트 대기 목록. 청크가 구워질 때마다 그 청크의 스포너가 자기 영역의 항목을 꺼내 소환한다.
    private List<SavableEntry> m_PendingSavables;

    // 현재 소환되어 있는 청크 오브젝트를 청크 좌표 기준으로 보관한다.
    private readonly Dictionary<Vector2Int, GameObject> m_ActiveChunks = new Dictionary<Vector2Int, GameObject>();

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

    // 주어진 좌표에 청크 프리팹을 소환만 하고 아직 굽지는 않는다. 굽기 전에 MapDataGenerator 등을 추가로 구성해야 할 때 사용한다.
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

    // 이후 소환/굽기되는 청크들에 주입할 맵 시드를 설정한다. 이미 구워진 청크에는 소급 적용되지 않으므로 청크를 그리기 전에 호출해야 한다.
    public void SetSeed(int seed)
    {
        m_Seed = seed;
    }

    // 청크가 구워질 때 각 청크 영역의 저장 오브젝트를 소환하는 데 쓸 대기 목록을 설정한다. 소환된 항목은 목록에서 제거되므로 호출자와 목록을 공유한다.
    public void SetPendingSavables(List<SavableEntry> pendingSavables)
    {
        m_PendingSavables = pendingSavables;
    }

    // 이미 소환된 청크의 좌표를 터레인 페인터에 반영해 보로노이 지형을 굽고 월드 위치를 잡는다. 모든 청크에 같은 맵 시드(m_Seed)를
    // 주입하므로, 각 청크는 같은 무한 맵의 서로 다른 영역을 이어서 보여주게 된다.
    public void PaintChunk(Vector2Int chunkCoordinate)
    {
        if (!m_ActiveChunks.TryGetValue(chunkCoordinate, out GameObject chunkObject))
        {
            Debug.LogError($"MapChunkManager: {chunkCoordinate} 청크가 소환되어 있지 않습니다.");
            return;
        }

        TerrainPainter painter = chunkObject.GetComponent<TerrainPainter>();
        painter.SetChunkCoordinate(chunkCoordinate);
        painter.PaintVoronoiTerrain(m_Seed);

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
        if (!m_ActiveChunks.TryGetValue(chunkCoordinate, out GameObject chunkObject))
        {
            Debug.LogError($"MapChunkManager: {chunkCoordinate} 청크가 소환되어 있지 않습니다.");
            return;
        }

        TerrainPainter painter = chunkObject.GetComponent<TerrainPainter>();
        painter.SetChunkCoordinate(chunkCoordinate);

        var context = painter.PrepareForPaint(m_Seed);
        if (context == null)
            return;

        var result = await Task.Run(() => painter.ComputePaint(context.Value));
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
