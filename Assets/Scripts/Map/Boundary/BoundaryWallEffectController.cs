using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 추적 대상 유닛들 주변의 내브메시 경계 중 어떤 세그먼트에 벽을 세울지 결정하는 씬 레벨 컨트롤러.
// 여러 대상을 동시에 추적할 수 있으며(AddTarget/RemoveTarget), PlayerManager에서 선택된 GroundMover
// 유닛도 자동으로 추적 대상에 포함된다. 대상들 주변 경계 세그먼트를 모아 가까운 순으로 배치 위치를
// 계산하고, 실제 벽의 소환·회수·풀링은 BoundaryWallSpawner에 위임한다.
public class BoundaryWallEffectController : MonoBehaviour
{
    [SerializeField] private PlayerManager _playerManager;
    [SerializeField] private NavMeshBoundaryTracker _boundaryTracker;
    [SerializeField] private BoundaryWallSpawner _wallSpawner;
    // 각 대상에서 이 거리(m) 안의 경계에만 벽을 세운다
    [SerializeField, Min(1f)] private float _detectionRadius = 30f;
    // 벽 배치 갱신 주기(초)
    [SerializeField, Min(0.05f)] private float _refreshInterval = 0.25f;
    // 동시에 서 있을 수 있는 벽 최대 개수 (모든 대상을 통틀어 가까운 세그먼트 우선)
    [SerializeField, Min(1)] private int _maxActiveWalls = 24;
    // 벽을 내브메시 높이 대신 실제 지면 높이에 붙일지 여부
    [SerializeField] private bool _snapToGroundHeight = true;
    // 지면에서 벽을 띄울 높이(m)
    [SerializeField] private float _heightOffset = 0.3f;
    // 가짜 경계 판별용으로 경계 바깥쪽을 찔러 볼 거리(m)
    [SerializeField, Min(0.5f)] private float _outwardProbeDistance = 2f;

    // 벽 배치 후보: 세그먼트와 그 키, 가장 가까운 추적 대상까지의 XZ 거리 제곱
    private struct CandidateWall
    {
        public long Key;
        public BoundarySegment Segment;
        public float SqrDistance;
    }

    // 후보 정렬 기준: 가장 가까운 대상까지의 거리 제곱 오름차순 (할당을 피하려고 정적으로 캐싱)
    private static readonly Comparison<CandidateWall> s_compareByDistance =
        (a, b) => a.SqrDistance.CompareTo(b.SqrDistance);

    private Transform _selectionTarget;
    private float _nextRefreshTime;
    private int _groundLayerMask;

    private readonly List<Transform> _targets = new List<Transform>();
    private readonly List<BoundarySegment> _querySegments = new List<BoundarySegment>();
    private readonly Dictionary<long, CandidateWall> _candidateMap = new Dictionary<long, CandidateWall>();
    private readonly List<CandidateWall> _candidates = new List<CandidateWall>();
    private readonly HashSet<long> _desiredKeys = new HashSet<long>();

    private void Awake()
    {
        _groundLayerMask = LayerMask.GetMask("Ground");
    }

    private void OnEnable()
    {
        _playerManager.OnSelected   += HandleSelected;
        _playerManager.OnDeselected += HandleDeselected;
    }

    private void OnDisable()
    {
        _playerManager.OnSelected   -= HandleSelected;
        _playerManager.OnDeselected -= HandleDeselected;

        _targets.Clear();
        _selectionTarget = null;
        HandleStopTracking();
    }

    private void Update()
    {
        HandlePruneDestroyedTargets();
        HandleThrottledRefresh();
    }

    // 추적 대상을 추가하고 경계 추출을 시작한다. 같은 대상을 여러 곳에서 추가/제거해도 짝이 맞도록 중복을 허용한다.
    public void AddTarget(Transform target)
    {
        if (target == null) return;

        _targets.Add(target);
        _nextRefreshTime = 0f;
        if (_boundaryTracker != null)
            _boundaryTracker.StartTracking();
    }

    // 추적 대상을 하나 제거한다. 남은 대상이 없으면 경계 추출을 멈추고 벽을 모두 회수한다.
    public void RemoveTarget(Transform target)
    {
        if (target == null) return;
        if (!_targets.Remove(target)) return;

        if (_targets.Count == 0)
            HandleStopTracking();
    }

    // 선택된 오브젝트가 GroundMover 유닛이면 추적 대상에 추가한다 (이전 선택은 OnDeselected에서 이미 제거됨)
    private void HandleSelected(SelectableObject selected)
    {
        if (selected == null || selected.GetComponent<GroundMover>() == null) return;

        _selectionTarget = selected.transform;
        AddTarget(_selectionTarget);
    }

    // 선택이 해제되면 선택으로 추가했던 대상만 추적에서 제외한다
    private void HandleDeselected()
    {
        if (_selectionTarget != null)
            RemoveTarget(_selectionTarget);

        _selectionTarget = null;
    }

    // 경계 추출을 중지하고 서 있는 벽을 모두 페이드 아웃시킨다
    private void HandleStopTracking()
    {
        if (_boundaryTracker != null)
            _boundaryTracker.StopTracking();
        if (_wallSpawner != null)
            _wallSpawner.DespawnAll();
    }

    // 파괴된 추적 대상을 정리하고, 그 결과 남은 대상이 없으면 추적을 멈춘다
    private void HandlePruneDestroyedTargets()
    {
        bool removedAny = false;
        for (int i = _targets.Count - 1; i >= 0; i--)
        {
            if (_targets[i] != null) continue;

            _targets.RemoveAt(i);
            removedAny = true;
        }

        if (removedAny && _targets.Count == 0)
            HandleStopTracking();
    }

    // 대상이 있으면 갱신 주기에 맞춰 벽 배치를 갱신한다
    private void HandleThrottledRefresh()
    {
        if (_targets.Count == 0 || Time.time < _nextRefreshTime) return;

        _nextRefreshTime = Time.time + _refreshInterval;
        HandleRefreshWalls();
    }

    // 모든 대상 주변의 경계 세그먼트를 후보로 모아 가까운 순으로 스포너에 벽을 세우게 하고,
    // 후보에서 빠진 세그먼트의 벽은 회수시킨다
    private void HandleRefreshWalls()
    {
        if (_boundaryTracker == null || !_boundaryTracker.HasData || _wallSpawner == null) return;

        // 여러 대상의 감지 반경이 겹칠 수 있으므로 키 기준으로 합치고, 가장 가까운 대상까지의 거리를 기록한다
        _candidateMap.Clear();
        foreach (Transform target in _targets)
        {
            if (target == null) continue;

            Vector3 targetPosition = target.position;
            _boundaryTracker.GetSegmentsInRadius(targetPosition, _detectionRadius, _querySegments);

            foreach (BoundarySegment segment in _querySegments)
            {
                long key = HandleSegmentKey(segment);
                float sqrDistance = HandleSqrDistanceToMidXZ(segment, targetPosition);
                if (!_candidateMap.TryGetValue(key, out CandidateWall existing) || sqrDistance < existing.SqrDistance)
                    _candidateMap[key] = new CandidateWall { Key = key, Segment = segment, SqrDistance = sqrDistance };
            }
        }

        _candidates.Clear();
        foreach (KeyValuePair<long, CandidateWall> pair in _candidateMap)
            _candidates.Add(pair.Value);
        _candidates.Sort(s_compareByDistance);

        _desiredKeys.Clear();
        foreach (CandidateWall candidate in _candidates)
        {
            if (_desiredKeys.Count >= _maxActiveWalls) break;

            if (_wallSpawner.IsActive(candidate.Key))
            {
                // 이미 서 있는 벽은 건드리지 않아 재추출 후에도 팝 없이 유지된다
                _desiredKeys.Add(candidate.Key);
                continue;
            }

            if (HandleIsFalseBoundary(candidate.Segment)) continue;

            _wallSpawner.Spawn(candidate.Key, HandleSnapHeight(candidate.Segment.Start), HandleSnapHeight(candidate.Segment.End));
            _desiredKeys.Add(candidate.Key);
        }

        _wallSpawner.DespawnAllExcept(_desiredKeys);
    }

    // 경계 바깥쪽 근처에 내브메시가 있으면 진짜 장애물이 아니라 다른 추적 대상 베이크 볼륨의 절단면이다.
    // CalculateTriangulation이 모든 NavMeshData를 병합해 반환하기 때문에 이런 가짜 경계가 섞일 수 있다.
    private bool HandleIsFalseBoundary(in BoundarySegment segment)
    {
        Vector3 mid = (segment.Start + segment.End) * 0.5f;
        Vector3 probe = mid + segment.OutwardNormal * _outwardProbeDistance;

        // 샘플 반경이 프로브 거리 이상이면 경계 안쪽(걷기 가능) 내브메시까지 잡아 오판하므로 더 작게 잡는다
        float sampleRadius = _outwardProbeDistance * 0.75f;
        return NavMesh.SamplePosition(probe, out _, sampleRadius, NavMesh.AllAreas);
    }

    // 내브메시 정점 Y는 복셀화 때문에 지형에서 떠 있으므로 Ground 레이어에 레이캐스트해 실제 지면 높이에 붙인다.
    // 청크 지형이 여러 개라 Terrain.activeTerrain은 다른 청크 위치에서 틀릴 수 있어 레이캐스트를 쓴다.
    private Vector3 HandleSnapHeight(Vector3 position)
    {
        if (_snapToGroundHeight)
        {
            Vector3 origin = position + Vector3.up * 30f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 60f, _groundLayerMask))
            {
                position.y = hit.point.y + _heightOffset;
                return position;
            }
        }

        position.y += _heightOffset;
        return position;
    }

    // 양 끝점의 XZ를 0.5m로 양자화해 순서 무관 키를 만든다. 재추출돼도 같은 세그먼트를 같은 벽으로 식별하기 위한
    // 값이며, Y는 복셀화 오차로 흔들릴 수 있어 제외한다.
    private static long HandleSegmentKey(in BoundarySegment segment)
    {
        int hashA = HandleEndpointHash(segment.Start);
        int hashB = HandleEndpointHash(segment.End);
        if (hashA > hashB)
            (hashA, hashB) = (hashB, hashA);

        return ((long)hashA << 32) | (uint)hashB;
    }

    // XZ를 0.5m 격자로 양자화한 끝점 해시를 계산한다
    private static int HandleEndpointHash(Vector3 position)
    {
        int x = Mathf.RoundToInt(position.x * 2f);
        int z = Mathf.RoundToInt(position.z * 2f);
        unchecked
        {
            return x * 73856093 ^ z * 19349663;
        }
    }

    // origin에서 세그먼트 중점까지의 XZ 거리 제곱을 계산한다
    private static float HandleSqrDistanceToMidXZ(in BoundarySegment segment, Vector3 origin)
    {
        float midX = (segment.Start.x + segment.End.x) * 0.5f - origin.x;
        float midZ = (segment.Start.z + segment.End.z) * 0.5f - origin.z;
        return midX * midX + midZ * midZ;
    }

    // 선택 시 각 추적 대상 주변의 감지 반경을 노란 와이어 구로 씬 뷰에 표시한다
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        foreach (Transform target in _targets)
        {
            if (target != null)
                Gizmos.DrawWireSphere(target.position, _detectionRadius);
        }
    }
}
