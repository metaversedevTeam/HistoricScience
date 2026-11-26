using UnityEngine;

// 참조한 프리팹(외부 에셋 등)이 프로젝트에 없을 때, 대신 지정한 대체 프리팹을 원하는 부모 아래에 소환하는 컴포넌트
public class PrefabFallbackSpawner : MonoBehaviour
{
    [Tooltip("존재 여부를 확인할 프리팹. 에셋이 없으면 참조가 비어 대체 프리팹을 소환한다.")]
    [SerializeField] private GameObject _referencePrefab;

    [Tooltip("참조 프리팹이 없을 때 대신 소환할 프리팹")]
    [SerializeField] private GameObject _fallbackPrefab;

    [Tooltip("대체 프리팹을 소환할 부모. 비워 두면 이 오브젝트 아래에 소환한다.")]
    [SerializeField] private Transform _spawnParent;

    private void Start()
    {
        HandleFallbackSpawn();
    }

    // 참조 프리팹이 비어 있으면 대체 프리팹을 부모 아래에 소환한다.
    private void HandleFallbackSpawn()
    {
        if (_referencePrefab != null) return;

        if (_fallbackPrefab == null)
        {
            Debug.LogWarning($"[{nameof(PrefabFallbackSpawner)}] {name}: 참조 프리팹과 대체 프리팹이 모두 비어 있습니다.", this);
            return;
        }

        Transform parent = _spawnParent != null ? _spawnParent : transform;
        Instantiate(_fallbackPrefab, parent, false);
    }
}
