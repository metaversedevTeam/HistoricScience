using System.Collections.Generic;
using UnityEngine;

// MeshRenderer가 붙은 오브젝트의 메시 구조(간선·정점·정점 노멀·삼각형 노멀)를 씬 뷰 기즈모로 확인하는 에디터 전용 테스트 컴포넌트
[DisallowMultipleComponent]
public class MeshGizmoVisualizer : MonoBehaviour
{
    [Header("표시 조건")]
    [Tooltip("켜면 오브젝트가 선택됐을 때만, 끄면 항상 기즈모를 그린다.")]
    [SerializeField] private bool _drawOnlyWhenSelected = true;

    [Header("표시 항목")]
    [SerializeField] private bool _drawEdges = true;
    [SerializeField] private bool _drawVertices = true;
    [SerializeField] private bool _drawVertexNormals = true;
    [SerializeField] private bool _drawTriangleNormals = true;

    [Header("색상")]
    [SerializeField] private Color _edgeColor = new Color(0f, 1f, 1f, 0.6f);
    [SerializeField] private Color _vertexColor = Color.yellow;
    [SerializeField] private Color _vertexNormalColor = Color.green;
    [SerializeField] private Color _triangleNormalColor = Color.magenta;

    [Header("크기")]
    [Tooltip("정점 표시용 구의 반지름(월드 단위).")]
    [SerializeField, Min(0f)] private float _vertexSize = 0.02f;
    [Tooltip("노멀 선의 길이(월드 단위).")]
    [SerializeField, Min(0f)] private float _normalLength = 0.15f;

    [Header("성능 제한")]
    [Tooltip("그릴 최대 삼각형 개수. 이보다 많은 메시는 앞쪽 삼각형만 그린다.")]
    [SerializeField, Min(1)] private int _maxTriangleCount = 4000;
    [Tooltip("그릴 최대 정점 개수. 이보다 많은 메시는 앞쪽 정점만 그린다.")]
    [SerializeField, Min(1)] private int _maxVertexCount = 4000;

#if UNITY_EDITOR
    private Mesh _cachedMesh;
    private bool _cacheFailed;
    private Vector3[] _vertices;
    private Vector3[] _normals;
    private int[] _triangles;
    private readonly HashSet<long> _drawnEdges = new HashSet<long>();
#endif

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (_drawOnlyWhenSelected) return;
        HandleDrawMeshGizmos();
#endif
    }

    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        if (!_drawOnlyWhenSelected) return;
        HandleDrawMeshGizmos();
#endif
    }

#if UNITY_EDITOR
    // 캐시된 메시 데이터로 켜져 있는 표시 항목들을 그린다
    private void HandleDrawMeshGizmos()
    {
        if (!HandleRefreshMeshCache()) return;

        Matrix4x4 localToWorld = transform.localToWorldMatrix;
        int indexLimit = Mathf.Min(_triangles.Length, _maxTriangleCount * 3);

        if (_drawEdges) HandleDrawEdges(localToWorld, indexLimit);
        if (_drawTriangleNormals) HandleDrawTriangleNormals(localToWorld, indexLimit);
        if (_drawVertices) HandleDrawVertices(localToWorld);
        if (_drawVertexNormals) HandleDrawVertexNormals(localToWorld);
    }

    // MeshRenderer/MeshFilter에서 메시를 찾아 정점·노멀·삼각형 배열을 캐시한다. 그릴 수 없으면 false를 돌려준다.
    private bool HandleRefreshMeshCache()
    {
        if (GetComponent<MeshRenderer>() == null) return false;

        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter == null) return false;

        Mesh mesh = filter.sharedMesh;
        if (mesh == null) return false;

        // 같은 메시라면 이전 결과를 그대로 쓴다. 읽기에 실패한 메시도 매 프레임 재시도하지 않는다.
        if (mesh == _cachedMesh) return !_cacheFailed;

        _cachedMesh = mesh;
        _cacheFailed = true;
        _vertices = null;
        _normals = null;
        _triangles = null;

        // Read/Write Enabled가 꺼진 임포트 메시는 플레이 중 정점 접근이 막히므로 에디트 모드에서만 읽는다
        if (!mesh.isReadable && Application.isPlaying)
        {
            Debug.LogWarning($"[MeshGizmoVisualizer] '{mesh.name}' 메시는 Read/Write Enabled가 꺼져 있어 플레이 모드에서는 표시할 수 없습니다. 모델 임포트 설정에서 Read/Write Enabled를 켜주세요.", this);
            return false;
        }

        _vertices = mesh.vertices;
        _normals = mesh.normals;
        _triangles = mesh.triangles;

        if (_vertices.Length == 0 || _triangles.Length == 0)
        {
            Debug.LogWarning($"[MeshGizmoVisualizer] '{mesh.name}' 메시의 정점·삼각형을 읽을 수 없습니다. 모델 임포트 설정에서 Read/Write Enabled를 켜주세요.", this);
            return false;
        }

        _cacheFailed = false;
        return true;
    }

    // 삼각형 인덱스를 훑어 메시의 모든 간선을 중복 없이 선으로 그린다
    private void HandleDrawEdges(Matrix4x4 localToWorld, int indexLimit)
    {
        Gizmos.color = _edgeColor;
        _drawnEdges.Clear();

        for (int i = 0; i + 2 < indexLimit; i += 3)
        {
            int a = _triangles[i];
            int b = _triangles[i + 1];
            int c = _triangles[i + 2];

            HandleDrawEdgeOnce(localToWorld, a, b);
            HandleDrawEdgeOnce(localToWorld, b, c);
            HandleDrawEdgeOnce(localToWorld, c, a);
        }
    }

    // 정점 인덱스 쌍을 키로 중복을 걸러 간선 하나를 한 번만 그린다
    private void HandleDrawEdgeOnce(Matrix4x4 localToWorld, int from, int to)
    {
        int min = Mathf.Min(from, to);
        int max = Mathf.Max(from, to);
        long key = ((long)min << 32) | (uint)max;
        if (!_drawnEdges.Add(key)) return;

        Gizmos.DrawLine(localToWorld.MultiplyPoint3x4(_vertices[min]), localToWorld.MultiplyPoint3x4(_vertices[max]));
    }

    // 각 정점 위치에 작은 구를 그린다
    private void HandleDrawVertices(Matrix4x4 localToWorld)
    {
        if (_vertexSize <= 0f) return;

        Gizmos.color = _vertexColor;
        int vertexLimit = Mathf.Min(_vertices.Length, _maxVertexCount);
        for (int i = 0; i < vertexLimit; i++)
            Gizmos.DrawSphere(localToWorld.MultiplyPoint3x4(_vertices[i]), _vertexSize);
    }

    // 메시에 저장된 정점 노멀을 정점에서 뻗어 나가는 선으로 그린다
    private void HandleDrawVertexNormals(Matrix4x4 localToWorld)
    {
        if (_normals == null || _normals.Length != _vertices.Length || _normalLength <= 0f) return;

        Gizmos.color = _vertexNormalColor;
        int vertexLimit = Mathf.Min(_vertices.Length, _maxVertexCount);
        for (int i = 0; i < vertexLimit; i++)
        {
            Vector3 origin = localToWorld.MultiplyPoint3x4(_vertices[i]);
            Vector3 direction = localToWorld.MultiplyVector(_normals[i]);
            if (direction.sqrMagnitude <= Mathf.Epsilon) continue;

            Gizmos.DrawLine(origin, origin + direction.normalized * _normalLength);
        }
    }

    // 삼각형마다 세 정점으로 계산한 면 노멀을 무게중심에서 뻗어 나가는 선으로 그린다
    private void HandleDrawTriangleNormals(Matrix4x4 localToWorld, int indexLimit)
    {
        if (_normalLength <= 0f) return;

        Gizmos.color = _triangleNormalColor;
        for (int i = 0; i + 2 < indexLimit; i += 3)
        {
            Vector3 a = localToWorld.MultiplyPoint3x4(_vertices[_triangles[i]]);
            Vector3 b = localToWorld.MultiplyPoint3x4(_vertices[_triangles[i + 1]]);
            Vector3 c = localToWorld.MultiplyPoint3x4(_vertices[_triangles[i + 2]]);

            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude <= Mathf.Epsilon) continue;

            Vector3 center = (a + b + c) / 3f;
            Gizmos.DrawLine(center, center + normal.normalized * _normalLength);
        }
    }
#endif
}
