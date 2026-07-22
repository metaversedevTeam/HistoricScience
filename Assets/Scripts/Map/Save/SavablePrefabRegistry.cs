using System;
using System.Collections.Generic;
using UnityEngine;

// PrefabId와 프리팹을 짝지어 보관해, 로드 시 어떤 프리팹을 소환할지 찾아주는 레지스트리
[CreateAssetMenu(fileName = "저장 프리팹 목록", menuName = "스크립터블 오브젝트/저장/저장 프리팹 목록")]
public class SavablePrefabRegistry : ScriptableObject
{
    // PrefabId 하나와 소환할 프리팹 하나의 짝
    [Serializable]
    private struct Entry
    {
        public string PrefabId;
        public GameObject Prefab;
    }

    [SerializeField] private List<Entry> _entries = new();

    // 조회 속도를 위해 목록에서 만들어 두는 캐시
    private Dictionary<string, GameObject> _lookup;

    // PrefabId에 해당하는 프리팹을 반환한다. 등록되지 않은 id면 null을 반환한다.
    public GameObject GetPrefab(string prefabId)
    {
        if (_lookup == null)
            HandleBuildLookup();

        return _lookup.TryGetValue(prefabId, out GameObject prefab) ? prefab : null;
    }

    // 목록에서 조회용 딕셔너리를 만든다. 비어 있거나 중복된 항목은 경고를 남기고 무시한다.
    private void HandleBuildLookup()
    {
        _lookup = new Dictionary<string, GameObject>();

        foreach (Entry entry in _entries)
        {
            if (string.IsNullOrEmpty(entry.PrefabId) || entry.Prefab == null)
            {
                Debug.LogWarning($"SavablePrefabRegistry({name}): 비어 있는 항목은 무시됩니다.");
                continue;
            }

            if (!_lookup.TryAdd(entry.PrefabId, entry.Prefab))
                Debug.LogWarning($"SavablePrefabRegistry({name}): 중복된 PrefabId '{entry.PrefabId}'는 무시됩니다.");
        }
    }
}
