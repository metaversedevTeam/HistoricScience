using System.Collections.Generic;
using UnityEngine;
using HistoricScience.Test;

// 무한 맵을 청크 단위로 나누어, 특정 청크 좌표에 MapChunkTerrain 프리팹을 소환해 지형을 그리거나 지우는 관리자
public class MapChunkManager : MonoBehaviour
{
    // 소환할 청크 프리팹. Terrain, TerrainCollider, MapDataGenerator, TerrainPainter가 구성되어 있어야 한다.
    [SerializeField] private GameObject m_ChunkPrefab;

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

    // 이미 소환된 청크의 좌표를 터레인 페인터에 반영해 보로노이 지형을 굽고 월드 위치를 잡는다. useRandom을 항상 false로
    // 넘겨야 MapDataGenerator의 시드가 모든 청크 인스턴스에서 기본값으로 고정되어, 같은 무한 맵의 서로 다른 영역을
    // 이어서 보여주게 된다. (true로 넘기면 청크마다 랜덤 시드가 생겨 경계가 끊어진다)
    public void PaintChunk(Vector2Int chunkCoordinate)
    {
        if (!m_ActiveChunks.TryGetValue(chunkCoordinate, out GameObject chunkObject))
        {
            Debug.LogError($"MapChunkManager: {chunkCoordinate} 청크가 소환되어 있지 않습니다.");
            return;
        }

        TerrainPainter painter = chunkObject.GetComponent<TerrainPainter>();
        painter.SetChunkCoordinate(chunkCoordinate);
        painter.PaintVoronoiTerrain(false);

        HandlePositionChunk(chunkObject, chunkCoordinate);
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
