using System.Collections.Generic;
using UnityEngine;

// 맵 청크에 소환할 자원 소스의 소환 방식들을 한곳에 모아 두는 스크립터블 오브젝트.
// 소환기가 전체 아이템 목록을 순회하며 소환 대상을 걸러 내는 대신, 이 목록만 읽어 바로 소환하도록 한다.
// 소환할 프리팹은 소환 방식이 직접 들고 있으므로, 여기서는 소환 방식만 참조한다.
[CreateAssetMenu(fileName = "자원 소스 목록", menuName = "스크립터블 오브젝트/자원/자원 소스 목록", order = int.MinValue + 4)]
public class ResourceSourceList : ScriptableObject
{
    public IReadOnlyList<ResourceSpawnRule> Sources => _sources;

    [SerializeField] private List<ResourceSpawnRule> _sources = new();
    // 다음에 부여할 소환 시드 번호 (한번 증가한 값은 되돌아오지 않음)
    [SerializeField, HideInInspector] private int _nextSeedId = 1;

#if UNITY_EDITOR
    private void OnValidate()
    {
        HandleAssignSeedIds();
    }

    // 시드 번호가 없거나 앞 항목과 번호가 겹치는 소환 방식에만 새 번호를 부여한다. 이미 고유한 번호를 받은 소환 방식은 건드리지 않으므로, 목록을 재정렬하거나 다른 항목을 지워도 배치가 유지된다.
    // 에셋을 복제하면 시드 번호도 함께 복사되어 두 소환 방식이 같은 랜덤 스트림을 쓰게 되고, 같은 바이옴이면 자원이 같은 자리에 겹쳐 소환되므로 중복 번호도 다시 부여한다.
    private void HandleAssignSeedIds()
    {
        if (_sources == null)
            return;

        HashSet<int> usedSeedIds = new HashSet<int>();
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_sources[i] == null)
                continue;

            // 같은 에셋이 목록에 두 번 들어 있으면 같은 번호를 쓰는 게 맞으므로 처음 나온 항목만 검사한다.
            if (_sources.IndexOf(_sources[i]) != i)
                continue;

            if (_sources[i].SeedId > 0 && usedSeedIds.Add(_sources[i].SeedId))
                continue;

            // 직접 수정한 번호와 겹치지 않도록, 이미 쓰이는 번호보다 큰 값부터 부여한다.
            _nextSeedId = Mathf.Max(_nextSeedId, HandleGetMaxSeedId() + 1);
            usedSeedIds.Add(_nextSeedId);

            _sources[i].EditorAssignSeedId(_nextSeedId++);
            UnityEditor.EditorUtility.SetDirty(_sources[i]);
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }

    // 목록의 소환 방식들이 쓰고 있는 시드 번호 중 가장 큰 값을 반환한다.
    private int HandleGetMaxSeedId()
    {
        int max = 0;
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_sources[i] != null)
                max = Mathf.Max(max, _sources[i].SeedId);
        }

        return max;
    }
#endif
}
