using System.Collections.Generic;
using UnityEngine;

// 경계 벽 이펙트 인스턴스의 소환·회수·풀링을 전담하는 컴포넌트. 어떤 세그먼트에 벽을 세울지는
// BoundaryWallEffectController가 정하고, 이 클래스는 키로 식별되는 벽의 생명주기만 관리한다.
public class BoundaryWallSpawner : MonoBehaviour
{
    // 벽 이펙트 프리팹 슬롯. 기본 먼지 벽은 Assets/Prefabs/Effects/DustWall.prefab을 할당한다.
    [SerializeField] private BoundaryWallEffect _effectPrefab;

    // 현재 서 있는 벽 하나: 이펙트 인스턴스와 실제 배치된 끝점 (기즈모 표시용)
    private struct ActiveWall
    {
        public BoundaryWallEffect Effect;
        public Vector3 Start;
        public Vector3 End;
    }

    private bool _missingPrefabLogged;

    private readonly Dictionary<long, ActiveWall> _activeWalls = new Dictionary<long, ActiveWall>();
    private readonly List<BoundaryWallEffect> _stoppingWalls = new List<BoundaryWallEffect>();
    private readonly Stack<BoundaryWallEffect> _idlePool = new Stack<BoundaryWallEffect>();
    private readonly List<long> _removedKeys = new List<long>();

    private void Update()
    {
        HandleRecycleFinished();
    }

    // 키에 해당하는 벽이 이미 서 있는지 확인한다
    public bool IsActive(long key)
    {
        return _activeWalls.ContainsKey(key);
    }

    // 키로 식별되는 벽을 세운다. 유휴 풀에서 꺼내거나 새로 만들어 세그먼트에 맞춰 배치하고 재생한다.
    public void Spawn(long key, Vector3 start, Vector3 end)
    {
        if (_activeWalls.ContainsKey(key)) return;

        BoundaryWallEffect wall = HandleGetWallInstance();
        if (wall == null) return;

        wall.SetSegment(start, end);
        wall.Play();
        _activeWalls[key] = new ActiveWall { Effect = wall, Start = start, End = end };
    }

    // keepKeys에 없는 벽을 모두 페이드 아웃시키고 회수 대기 목록으로 옮긴다
    public void DespawnAllExcept(HashSet<long> keepKeys)
    {
        _removedKeys.Clear();
        foreach (KeyValuePair<long, ActiveWall> pair in _activeWalls)
        {
            if (!keepKeys.Contains(pair.Key))
                _removedKeys.Add(pair.Key);
        }

        foreach (long key in _removedKeys)
            HandleDespawn(key);
    }

    // 서 있는 벽을 모두 페이드 아웃시킨다
    public void DespawnAll()
    {
        _removedKeys.Clear();
        foreach (KeyValuePair<long, ActiveWall> pair in _activeWalls)
            _removedKeys.Add(pair.Key);

        foreach (long key in _removedKeys)
            HandleDespawn(key);
    }

    // 키에 해당하는 벽을 페이드 아웃시키고 활성 목록에서 제거한다
    private void HandleDespawn(long key)
    {
        if (!_activeWalls.TryGetValue(key, out ActiveWall wall)) return;

        wall.Effect.Stop();
        _stoppingWalls.Add(wall.Effect);
        _activeWalls.Remove(key);
    }

    // 방출이 멈추고 잔여 입자까지 사라진 벽을 유휴 풀로 되돌린다
    private void HandleRecycleFinished()
    {
        for (int i = _stoppingWalls.Count - 1; i >= 0; i--)
        {
            BoundaryWallEffect wall = _stoppingWalls[i];
            if (wall == null)
            {
                _stoppingWalls.RemoveAt(i);
                continue;
            }
            if (!wall.IsFinished) continue;

            _stoppingWalls.RemoveAt(i);
            _idlePool.Push(wall);
        }
    }

    // 유휴 풀에서 벽 인스턴스를 꺼내거나 프리팹으로 새로 만든다. 프리팹이 비어 있으면 한 번만 오류를 남긴다.
    private BoundaryWallEffect HandleGetWallInstance()
    {
        while (_idlePool.Count > 0)
        {
            BoundaryWallEffect pooled = _idlePool.Pop();
            if (pooled != null) return pooled; // 파괴된 인스턴스는 버린다
        }

        if (_effectPrefab == null)
        {
            if (!_missingPrefabLogged)
            {
                _missingPrefabLogged = true;
                Debug.LogError("벽 이펙트 프리팹이 할당되지 않았다. Assets/Prefabs/Effects/DustWall.prefab을 _effectPrefab에 할당해야 한다.", this);
            }
            return null;
        }

        return Instantiate(_effectPrefab, transform);
    }

    // 선택 시 현재 서 있는 벽 세그먼트를 빨간 선으로 씬 뷰에 표시한다
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        foreach (KeyValuePair<long, ActiveWall> pair in _activeWalls)
            Gizmos.DrawLine(pair.Value.Start, pair.Value.End);
    }
}
