using System;
using System.Collections.Generic;
using UnityEngine;

//다른 컴포넌트에 일꾼 할당 관련 정보를 제공해주는 컴포넌트
public class WorkPlace : MonoBehaviour, IStatePart
{
    public event Action OnAddWorker;
    public event Action<IWorker> OnRemoveWorker;

    // 등록된 일꾼 목록의 직렬화 래퍼
    [Serializable]
    private class SaveState
    {
        public List<SavableEntry> Workers = new();
    }

    // 해제된 일꾼을 다시 소환할 때 프리팹을 찾는 레지스트리
    [SerializeField] private SavablePrefabRegistry _registry;
    // 해제된 일꾼이 나올 위치. 비어 있으면 자신의 위치를 사용한다.
    [SerializeField] private Transform _releasePoint;
    // 동시에 등록할 수 있는 일꾼 수
    [SerializeField, Min(0)] private int _maxWorkerCount = 4;

    // 등록된 일꾼들의 프리팹 식별자와 상태 JSON. 등록된 동안에는 게임오브젝트가 없으므로 이 목록이 유일한 실체다.
    private List<SavableEntry> _workers = new();

    //현재 할당된 일꾼 개수
    public int WorkerCount => _workers.Count;

    // 등록 가능한 최대 일꾼 개수
    public int MaxWorkerCount => _maxWorkerCount;

    //현재 이 일꾼을 받아들일 수 있는지 판정한다.
    public bool CanAddWorker(IWorker worker)
    {
        if (worker == null || _workers.Count >= _maxWorkerCount)
            return false;

        // 이미 다른 일터에 등록된 일꾼은 받지 않는다. 파괴가 프레임 끝에 처리되어 등록 직후에도 잠시 살아 있기 때문이다.
        if (worker.CurrentWorkPlace != null)
            return false;

        // 복원할 프리팹을 찾을 수 없는 일꾼을 받으면 파괴만 되고 되살릴 수 없다.
        if (string.IsNullOrEmpty(worker.PrefabId))
            return false;

        return worker is Component component && component != null;
    }

    //새로운 worker를 등록해 JSON으로 저장하고 등록된 worker의 게임오브젝트 제거
    public bool AddWorker(IWorker worker)
    {
        if (!CanAddWorker(worker))
            return false;

        worker.OnEnterWorkPlace(this);

        _workers.Add(new SavableEntry
        {
            PrefabId = worker.PrefabId,
            StateJson = worker.CaptureJson(),
        });

        Destroy(((Component)worker).gameObject);
        OnAddWorker?.Invoke();
        return true;
    }

    //등록된 worker를 등록해제하고 해당 worker의 인스턴스 생성
    public IWorker RemoveWorker()
    {
        if (_workers.Count == 0)
            return null;

        if (_registry == null)
        {
            Debug.LogWarning($"WorkPlace({name}): 저장 프리팹 목록이 연결되지 않아 일꾼을 해제할 수 없습니다.");
            return null;
        }

        // 소환에 실패하면 목록에 그대로 남겨 일꾼이 사라지지 않게 한다.
        int lastIndex = _workers.Count - 1;
        GameObject instance = _registry.SpawnSavable(_workers[lastIndex]);
        if (instance == null)
            return null;

        _workers.RemoveAt(lastIndex);
        HandlePlaceReleasedWorker(instance.transform);

        if (!instance.TryGetComponent(out IWorker worker))
        {
            Debug.LogError($"WorkPlace({name}): 소환된 '{instance.name}'에 IWorker 컴포넌트가 없습니다.");
            return null;
        }

        worker.OnExitWorkPlace();
        OnRemoveWorker?.Invoke(worker);
        return worker;
    }

    // 해제된 일꾼을 해제 위치로 옮긴다. 저장된 위치는 등록 직전의 자리라서 일터에서 멀 수 있으므로 덮어쓴다.
    private void HandlePlaceReleasedWorker(Transform workerTransform)
    {
        Transform releaseAt = _releasePoint != null ? _releasePoint : transform;
        SavableHandler.PlaceAt(workerTransform, releaseAt.position);
    }

    // 등록된 일꾼 목록을 JSON 문자열로 캡처한다.
    public string CaptureJson()
    {
        return JsonUtility.ToJson(new SaveState { Workers = _workers });
    }

    // JSON 문자열로 등록된 일꾼 목록을 복원한다. 복원은 등록이 아니므로 OnAddWorker를 발행하지 않는다. 구독자는 초기화 시 WorkerCount를 직접 읽어야 한다.
    public void ApplyJson(string json)
    {
        SaveState state = JsonUtility.FromJson<SaveState>(json);
        if (state == null) return;

        _workers = state.Workers ?? new List<SavableEntry>();
    }
}
