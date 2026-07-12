using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

// 내브메시 경계 세그먼트 하나. 양 끝 월드 좌표와 걷기 가능 영역 바깥쪽을 가리키는 XZ 법선을 담는다.
public struct BoundarySegment
{
    public Vector3 Start;
    public Vector3 End;
    public Vector3 OutwardNormal;
}

// 런타임에 구워진 내브메시 삼각분할에서 걷기 불가 경계 세그먼트를 주기적으로 추출해 공간 해시로 보관하는 컴포넌트.
// 베이크(DynamicNavMeshBaker)는 수시로 완료되므로 이벤트 대신 주기 폴링을 쓰고, 삼각분할이 실제로 바뀌었을 때만
// 워커 스레드에서 정점 용접 → 경계 엣지 판정 → 폴리라인 병합 → 공간 해시 구축을 수행한다.
public class NavMeshBoundaryTracker : MonoBehaviour
{
    // 경계 추출 주기(초)
    [SerializeField, Min(0.5f)] private float _extractionInterval = 3f;
    // 정점 용접 허용 오차(m). CalculateTriangulation은 같은 위치의 정점을 중복으로 반환하므로 용접해야 경계를 찾을 수 있다.
    [SerializeField, Min(0.001f)] private float _weldTolerance = 0.05f;
    // 연속 엣지를 한 세그먼트로 병합할 수 있는 최대 방향 차이(도)
    [SerializeField, Range(0f, 89f)] private float _mergeAngleDegrees = 20f;
    // 병합된 세그먼트 하나의 최대 길이(m)
    [SerializeField, Min(1f)] private float _maxSegmentLength = 20f;
    // 공간 해시 셀 한 변 크기(m)
    [SerializeField, Min(1f)] private float _gridCellSize = 16f;

    // 최신 추출 결과: 세그먼트 배열과 XZ 공간 해시, 해시를 만들 때 쓴 셀 크기를 함께 보관한다
    private class ExtractionResult
    {
        public BoundarySegment[] Segments;
        public Dictionary<(int, int), List<int>> Grid;
        public float CellSize;
    }

    // 경계 엣지 하나: 용접된 정점 인덱스 쌍과 바깥 방향 법선
    private struct BoundaryEdge
    {
        public int A;
        public int B;
        public Vector3 Normal;
    }

    private ExtractionResult _result;
    private Coroutine _extractLoop;
    private int _lastVertexCount = -1;
    private int _lastIndexCount = -1;
    private int _lastVertexHash;
    private readonly HashSet<int> _queryDedupe = new HashSet<int>();

    // 추출이 한 번이라도 성공해 조회 가능한 데이터가 있는지
    public bool HasData => _result != null;

    // 비활성화 시 주기 추출을 멈춘다 (컴포넌트만 꺼져도 코루틴은 살아남으므로 직접 정리)
    private void OnDisable()
    {
        StopTracking();
    }

    // 주기 추출을 시작한다. 이미 추출 중이면 아무것도 하지 않는다.
    public void StartTracking()
    {
        if (_extractLoop != null) return;
        _extractLoop = StartCoroutine(HandleExtractLoop());
    }

    // 주기 추출을 멈춘다. 마지막 추출 결과는 보존되어 계속 조회할 수 있다.
    public void StopTracking()
    {
        if (_extractLoop == null) return;
        StopCoroutine(_extractLoop);
        _extractLoop = null;
    }

    // 반경 안(XZ 거리 기준)의 경계 세그먼트를 results에 담고 개수를 반환한다. results는 먼저 비운다.
    public int GetSegmentsInRadius(Vector3 position, float radius, List<BoundarySegment> results)
    {
        results.Clear();
        ExtractionResult result = _result;
        if (result == null) return 0;

        float inv = 1f / result.CellSize;
        int minX = Mathf.FloorToInt((position.x - radius) * inv);
        int maxX = Mathf.FloorToInt((position.x + radius) * inv);
        int minZ = Mathf.FloorToInt((position.z - radius) * inv);
        int maxZ = Mathf.FloorToInt((position.z + radius) * inv);
        float sqrRadius = radius * radius;

        _queryDedupe.Clear();
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                if (!result.Grid.TryGetValue((x, z), out List<int> cell)) continue;

                foreach (int index in cell)
                {
                    if (!_queryDedupe.Add(index)) continue;

                    BoundarySegment segment = result.Segments[index];
                    if (HandleSqrDistanceToSegmentXZ(position, segment.Start, segment.End) <= sqrRadius)
                        results.Add(segment);
                }
            }
        }

        return results.Count;
    }

    // 주기적으로 삼각분할을 떠서, 지난번과 달라졌을 때만 워커 스레드로 경계를 추출하는 루프
    private IEnumerator HandleExtractLoop()
    {
        while (true)
        {
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            if (HandleHasMeshChanged(triangulation.vertices, triangulation.indices))
            {
                // 워커에서 인스펙터 필드를 직접 읽지 않도록 값을 로컬로 캡처한다
                float weldTolerance = _weldTolerance;
                float cosMergeAngle = Mathf.Cos(_mergeAngleDegrees * Mathf.Deg2Rad);
                float maxSegmentLength = _maxSegmentLength;
                float gridCellSize = _gridCellSize;
                Task<ExtractionResult> task = Task.Run(() => HandleExtract(
                    triangulation.vertices, triangulation.indices,
                    weldTolerance, cosMergeAngle, maxSegmentLength, gridCellSize));

                while (!task.IsCompleted)
                    yield return null;

                if (task.Status == TaskStatus.RanToCompletion)
                {
                    _result = task.Result;
                }
                else
                {
                    Debug.LogWarning($"내브메시 경계 추출에 실패했다: {task.Exception?.GetBaseException()}");
                    _lastVertexCount = -1; // 다음 주기에 다시 시도한다
                }
            }

            yield return new WaitForSeconds(_extractionInterval);
        }
    }

    // 정점/인덱스 개수와 샘플링 정점 해시로 삼각분할이 지난번과 달라졌는지 싸게 판별한다.
    // 베이커가 경계를 양자화해 같은 볼륨을 다시 굽는 동안은 결과가 동일하므로 대부분의 주기는 여기서 걸러진다.
    private bool HandleHasMeshChanged(Vector3[] vertices, int[] indices)
    {
        int hash = HandleComputeVertexHash(vertices);
        if (vertices.Length == _lastVertexCount && indices.Length == _lastIndexCount && hash == _lastVertexHash)
            return false;

        _lastVertexCount = vertices.Length;
        _lastIndexCount = indices.Length;
        _lastVertexHash = hash;
        return true;
    }

    // 일정 간격으로 샘플링한 정점들의 FNV 해시를 계산한다
    private static int HandleComputeVertexHash(Vector3[] vertices)
    {
        unchecked
        {
            int hash = (int)2166136261;
            int step = Mathf.Max(1, vertices.Length / 64);
            for (int i = 0; i < vertices.Length; i += step)
            {
                Vector3 v = vertices[i];
                hash = (hash * 16777619) ^ v.x.GetHashCode();
                hash = (hash * 16777619) ^ v.y.GetHashCode();
                hash = (hash * 16777619) ^ v.z.GetHashCode();
            }
            return hash;
        }
    }

    // 워커 스레드 진입점: 용접 → 경계 엣지 수집 → 폴리라인 병합 → 공간 해시 구축. Unity API를 호출하지 않는다.
    private static ExtractionResult HandleExtract(Vector3[] vertices, int[] indices, float weldTolerance, float cosMergeAngle, float maxSegmentLength, float gridCellSize)
    {
        int[] remap = HandleWeldVertices(vertices, weldTolerance);
        List<BoundaryEdge> edges = HandleCollectBoundaryEdges(vertices, indices, remap);
        List<BoundarySegment> segments = HandleBuildSegments(vertices, edges, cosMergeAngle, maxSegmentLength);

        return new ExtractionResult
        {
            Segments = segments.ToArray(),
            Grid = HandleBuildGrid(segments, gridCellSize),
            CellSize = gridCellSize,
        };
    }

    // 정점을 허용 오차 격자로 양자화하고 인접 셀까지 검색해, 같은 위치의 중복 정점을 하나의 대표 인덱스로 병합한다
    private static int[] HandleWeldVertices(Vector3[] vertices, float tolerance)
    {
        int[] remap = new int[vertices.Length];
        var cellToCanonical = new Dictionary<(long, long, long), int>(vertices.Length);
        float inv = 1f / tolerance;
        float sqrTolerance = tolerance * tolerance;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            long cx = (long)Mathf.Floor(v.x * inv);
            long cy = (long)Mathf.Floor(v.y * inv);
            long cz = (long)Mathf.Floor(v.z * inv);

            int canonical = -1;
            for (long ox = -1; ox <= 1 && canonical < 0; ox++)
            {
                for (long oy = -1; oy <= 1 && canonical < 0; oy++)
                {
                    for (long oz = -1; oz <= 1 && canonical < 0; oz++)
                    {
                        if (!cellToCanonical.TryGetValue((cx + ox, cy + oy, cz + oz), out int existing)) continue;

                        // 같은 셀 안이면 거리 검사 없이 병합해도 오차가 허용 범위 수준이다
                        bool sameCell = ox == 0 && oy == 0 && oz == 0;
                        if (sameCell || (vertices[existing] - v).sqrMagnitude <= sqrTolerance)
                            canonical = existing;
                    }
                }
            }

            if (canonical < 0)
            {
                cellToCanonical[(cx, cy, cz)] = i;
                canonical = i;
            }
            remap[i] = canonical;
        }

        return remap;
    }

    // 용접된 인덱스 기준으로 삼각형을 순회해, 정확히 한 번만 쓰인 엣지(경계)와 그 바깥 방향 법선을 수집한다
    private static List<BoundaryEdge> HandleCollectBoundaryEdges(Vector3[] vertices, int[] indices, int[] remap)
    {
        var edgeInfo = new Dictionary<(int, int), (int count, int opposite)>(indices.Length);

        for (int t = 0; t + 2 < indices.Length; t += 3)
        {
            int a = remap[indices[t]];
            int b = remap[indices[t + 1]];
            int c = remap[indices[t + 2]];
            if (a == b || b == c || c == a) continue; // 용접으로 퇴화한 삼각형은 무시

            HandleCountEdge(edgeInfo, a, b, c);
            HandleCountEdge(edgeInfo, b, c, a);
            HandleCountEdge(edgeInfo, c, a, b);
        }

        var boundary = new List<BoundaryEdge>();
        foreach (KeyValuePair<(int, int), (int count, int opposite)> pair in edgeInfo)
        {
            if (pair.Value.count != 1) continue;

            Vector3 va = vertices[pair.Key.Item1];
            Vector3 vb = vertices[pair.Key.Item2];
            Vector3 normal = Vector3.Cross(Vector3.up, vb - va);
            normal.y = 0f;
            if (normal.sqrMagnitude < 1e-8f) continue; // 수직에 가까운 엣지는 방향을 정할 수 없어 제외
            normal.Normalize();

            // 삼각형의 반대쪽 정점(걷기 영역 안쪽)을 향하면 뒤집어 항상 바깥을 가리키게 한다
            Vector3 mid = (va + vb) * 0.5f;
            Vector3 toOpposite = vertices[pair.Value.opposite] - mid;
            if (normal.x * toOpposite.x + normal.z * toOpposite.z > 0f)
                normal = -normal;

            boundary.Add(new BoundaryEdge { A = pair.Key.Item1, B = pair.Key.Item2, Normal = normal });
        }

        return boundary;
    }

    // 엣지 사용 횟수를 누적하고, 처음 본 엣지면 마주보는 정점을 기억해 둔다
    private static void HandleCountEdge(Dictionary<(int, int), (int count, int opposite)> edgeInfo, int a, int b, int opposite)
    {
        (int, int) key = a < b ? (a, b) : (b, a);
        if (edgeInfo.TryGetValue(key, out (int count, int opposite) info))
            edgeInfo[key] = (info.count + 1, info.opposite);
        else
            edgeInfo[key] = (1, opposite);
    }

    // 경계 엣지들을 정점 연결로 이어 체인(폴리라인)을 만들고, 각 체인을 병합해 세그먼트 목록을 만든다
    private static List<BoundarySegment> HandleBuildSegments(Vector3[] vertices, List<BoundaryEdge> edges, float cosMergeAngle, float maxSegmentLength)
    {
        var vertexToEdges = new Dictionary<int, List<int>>();
        for (int i = 0; i < edges.Count; i++)
        {
            HandleAddIncidence(vertexToEdges, edges[i].A, i);
            HandleAddIncidence(vertexToEdges, edges[i].B, i);
        }

        var segments = new List<BoundarySegment>();
        bool[] visited = new bool[edges.Count];
        var chainVerts = new List<int>();
        var chainEdges = new List<int>();

        // 1차: 끝점/분기점(차수 != 2)에서 시작하는 열린 체인
        for (int i = 0; i < edges.Count; i++)
        {
            if (visited[i]) continue;

            int degreeA = vertexToEdges[edges[i].A].Count;
            int degreeB = vertexToEdges[edges[i].B].Count;
            if (degreeA == 2 && degreeB == 2) continue;

            int startVertex = degreeA != 2 ? edges[i].A : edges[i].B;
            HandleWalkChain(edges, vertexToEdges, visited, startVertex, i, chainVerts, chainEdges);
            HandleCanonicalizeOpenChain(vertices, chainVerts, chainEdges);
            HandleMergeChain(vertices, edges, chainVerts, chainEdges, cosMergeAngle, maxSegmentLength, segments);
        }

        // 2차: 남은 엣지는 모두 닫힌 루프
        for (int i = 0; i < edges.Count; i++)
        {
            if (visited[i]) continue;

            HandleWalkChain(edges, vertexToEdges, visited, edges[i].A, i, chainVerts, chainEdges);
            HandleCanonicalizeLoop(vertices, chainVerts, chainEdges);
            HandleMergeChain(vertices, edges, chainVerts, chainEdges, cosMergeAngle, maxSegmentLength, segments);
        }

        return segments;
    }

    // 정점 → 인접 경계 엣지 목록에 엣지를 추가한다
    private static void HandleAddIncidence(Dictionary<int, List<int>> vertexToEdges, int vertex, int edgeIndex)
    {
        if (!vertexToEdges.TryGetValue(vertex, out List<int> list))
        {
            list = new List<int>(2);
            vertexToEdges[vertex] = list;
        }
        list.Add(edgeIndex);
    }

    // startVertex에서 firstEdge를 따라 분기점/끝점/방문한 엣지/시작점 복귀를 만날 때까지 진행하며 정점·엣지 나열을 수집한다
    private static void HandleWalkChain(List<BoundaryEdge> edges, Dictionary<int, List<int>> vertexToEdges, bool[] visited, int startVertex, int firstEdge, List<int> chainVerts, List<int> chainEdges)
    {
        chainVerts.Clear();
        chainEdges.Clear();

        int currentVertex = startVertex;
        int currentEdge = firstEdge;
        chainVerts.Add(currentVertex);

        while (true)
        {
            visited[currentEdge] = true;
            chainEdges.Add(currentEdge);

            BoundaryEdge edge = edges[currentEdge];
            int nextVertex = edge.A == currentVertex ? edge.B : edge.A;
            chainVerts.Add(nextVertex);

            if (nextVertex == startVertex) break; // 닫힌 루프 완주

            List<int> incident = vertexToEdges[nextVertex];
            if (incident.Count != 2) break; // 끝점 또는 분기점

            int nextEdge = incident[0] == currentEdge ? incident[1] : incident[0];
            if (visited[nextEdge]) break;

            currentVertex = nextVertex;
            currentEdge = nextEdge;
        }
    }

    // 열린 체인의 진행 방향을 양 끝점 위치 비교로 고정한다. 순회 순서가 달라져도 병합 결과(세그먼트 경계)가
    // 같아야 재추출 후에도 같은 세그먼트가 같은 키로 식별된다.
    private static void HandleCanonicalizeOpenChain(Vector3[] vertices, List<int> chainVerts, List<int> chainEdges)
    {
        Vector3 first = vertices[chainVerts[0]];
        Vector3 last = vertices[chainVerts[chainVerts.Count - 1]];
        if (HandleComparePositions(last, first) < 0)
        {
            chainVerts.Reverse();
            chainEdges.Reverse();
        }
    }

    // 닫힌 루프의 시작 정점을 위치가 가장 작은 정점으로 회전시키고 진행 방향도 위치 기준으로 고정한다
    private static void HandleCanonicalizeLoop(Vector3[] vertices, List<int> chainVerts, List<int> chainEdges)
    {
        int count = chainEdges.Count;

        int minIndex = 0;
        for (int i = 1; i < count; i++)
        {
            if (HandleComparePositions(vertices[chainVerts[i]], vertices[chainVerts[minIndex]]) < 0)
                minIndex = i;
        }

        if (minIndex != 0)
        {
            var rotatedVerts = new List<int>(count + 1);
            var rotatedEdges = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                rotatedVerts.Add(chainVerts[(minIndex + i) % count]);
                rotatedEdges.Add(chainEdges[(minIndex + i) % count]);
            }
            rotatedVerts.Add(rotatedVerts[0]);

            chainVerts.Clear();
            chainVerts.AddRange(rotatedVerts);
            chainEdges.Clear();
            chainEdges.AddRange(rotatedEdges);
        }

        if (HandleComparePositions(vertices[chainVerts[1]], vertices[chainVerts[count - 1]]) > 0)
        {
            chainVerts.Reverse();
            chainEdges.Reverse();
        }
    }

    // 위치를 x, y, z 순 사전식으로 비교한다 (체인 방향/시작점 고정용)
    private static int HandleComparePositions(Vector3 a, Vector3 b)
    {
        if (a.x != b.x) return a.x < b.x ? -1 : 1;
        if (a.y != b.y) return a.y < b.y ? -1 : 1;
        if (a.z != b.z) return a.z < b.z ? -1 : 1;
        return 0;
    }

    // 체인 안에서 시위(현) 방향과의 각도 차이와 최대 길이 제한을 만족하는 연속 엣지를 하나의 세그먼트로 병합한다
    private static void HandleMergeChain(Vector3[] vertices, List<BoundaryEdge> edges, List<int> chainVerts, List<int> chainEdges, float cosMergeAngle, float maxSegmentLength, List<BoundarySegment> output)
    {
        int runStart = 0;
        Vector3 normalSum = edges[chainEdges[0]].Normal;

        for (int i = 1; i < chainEdges.Count; i++)
        {
            Vector3 runStartPos = vertices[chainVerts[runStart]];
            Vector3 edgeStartPos = vertices[chainVerts[i]];
            Vector3 edgeEndPos = vertices[chainVerts[i + 1]];

            Vector3 chordDir = (edgeStartPos - runStartPos).normalized;
            Vector3 edgeDir = (edgeEndPos - edgeStartPos).normalized;
            bool withinAngle = Vector3.Dot(chordDir, edgeDir) >= cosMergeAngle;
            bool withinLength = (edgeEndPos - runStartPos).magnitude <= maxSegmentLength;

            if (withinAngle && withinLength)
            {
                normalSum += edges[chainEdges[i]].Normal;
                continue;
            }

            output.Add(HandleMakeSegment(runStartPos, edgeStartPos, normalSum));
            runStart = i;
            normalSum = edges[chainEdges[i]].Normal;
        }

        output.Add(HandleMakeSegment(vertices[chainVerts[runStart]], vertices[chainVerts[chainEdges.Count]], normalSum));
    }

    // 병합 구간의 법선 합을 정규화해 세그먼트를 만든다. 법선 합이 상쇄되면 세그먼트 방향에서 다시 구한다.
    private static BoundarySegment HandleMakeSegment(Vector3 start, Vector3 end, Vector3 normalSum)
    {
        normalSum.y = 0f;
        Vector3 normal = normalSum.sqrMagnitude > 1e-8f
            ? normalSum.normalized
            : Vector3.Cross(Vector3.up, (end - start).normalized);

        return new BoundarySegment { Start = start, End = end, OutwardNormal = normal };
    }

    // 각 세그먼트의 XZ AABB가 걸치는 셀마다 인덱스를 등록해 공간 해시를 만든다
    private static Dictionary<(int, int), List<int>> HandleBuildGrid(List<BoundarySegment> segments, float cellSize)
    {
        var grid = new Dictionary<(int, int), List<int>>();
        float inv = 1f / cellSize;

        for (int i = 0; i < segments.Count; i++)
        {
            BoundarySegment segment = segments[i];
            int minX = Mathf.FloorToInt(Mathf.Min(segment.Start.x, segment.End.x) * inv);
            int maxX = Mathf.FloorToInt(Mathf.Max(segment.Start.x, segment.End.x) * inv);
            int minZ = Mathf.FloorToInt(Mathf.Min(segment.Start.z, segment.End.z) * inv);
            int maxZ = Mathf.FloorToInt(Mathf.Max(segment.Start.z, segment.End.z) * inv);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (!grid.TryGetValue((x, z), out List<int> cell))
                    {
                        cell = new List<int>();
                        grid[(x, z)] = cell;
                    }
                    cell.Add(i);
                }
            }
        }

        return grid;
    }

    // 점에서 세그먼트까지의 XZ 평면 거리 제곱을 계산한다
    private static float HandleSqrDistanceToSegmentXZ(Vector3 point, Vector3 a, Vector3 b)
    {
        float px = point.x - a.x;
        float pz = point.z - a.z;
        float dx = b.x - a.x;
        float dz = b.z - a.z;

        float lenSq = dx * dx + dz * dz;
        float t = lenSq > 1e-6f ? Mathf.Clamp01((px * dx + pz * dz) / lenSq) : 0f;

        float cx = a.x + dx * t - point.x;
        float cz = a.z + dz * t - point.z;
        return cx * cx + cz * cz;
    }

    // 선택 시 추출된 경계 세그먼트(시안)와 바깥 법선(마젠타)을 씬 뷰에 표시한다
    private void OnDrawGizmosSelected()
    {
        if (_result == null) return;

        Vector3 lift = Vector3.up * 0.5f;
        foreach (BoundarySegment segment in _result.Segments)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(segment.Start + lift, segment.End + lift);

            Gizmos.color = Color.magenta;
            Vector3 mid = (segment.Start + segment.End) * 0.5f + lift;
            Gizmos.DrawLine(mid, mid + segment.OutwardNormal);
        }
    }
}
