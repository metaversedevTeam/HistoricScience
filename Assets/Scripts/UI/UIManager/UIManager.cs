using System.Collections.Generic;
using UnityEngine;

// 캔버스 UI의 활성화, 풀링을 담당하는 관리자 클래스.
// 풀은 인스턴스가 외부에서 파괴되지 않는다고 가정한다 (파괴는 반드시 Close·풀링 경로로만).
public class UIManager : MonoBehaviour
{
    // UI 인스턴스를 생성해 붙일 부모 (미지정 시 자기 자신의 트랜스폼 사용)
    [SerializeField] private Transform uiRoot;

    // 프리팹별 대기(Closed) 인스턴스 풀
    private readonly Dictionary<MonoBehaviour, Stack<IManagedUI>> pools = new Dictionary<MonoBehaviour, Stack<IManagedUI>>();

    // 인스턴스가 소속된 프리팹(풀 키)의 역매핑
    private readonly Dictionary<IManagedUI, MonoBehaviour> instanceToPrefab = new Dictionary<IManagedUI, MonoBehaviour>();

    // 현재 활성(Opening·Open·Closing) 인스턴스 목록
    private readonly List<IManagedUI> activeInstances = new List<IManagedUI>();

    // 프리팹에 해당하는 UI를 풀에서 꺼내거나 생성해 연다 (페이로드 없는 UI용).
    // 같은 프리팹으로 여러 번 호출하면 각각 별도의 인스턴스가 열린다 (다중 인스턴스 허용).
    public T OpenUI<T>(T prefab) where T : MonoBehaviour, IOpenableUI
    {
        T instance = GetOrCreateInstance(prefab);
        ((IOpenableUI)instance).Open();
        return instance;
    }

    // 프리팹에 해당하는 UI를 풀에서 꺼내거나 생성해, 데이터를 주입하며 연다.
    // 같은 프리팹으로 여러 번 호출하면 각각 별도의 인스턴스가 열린다 (다중 인스턴스 허용).
    public T OpenUI<T, TData>(T prefab, TData data) where T : MonoBehaviour, IOpenableUI<TData>
    {
        T instance = GetOrCreateInstance(prefab);
        ((IOpenableUI<TData>)instance).Open(data);
        return instance;
    }

    // 열려 있는 UI를 닫는 facade — instance.Close와 동일하며, 닫기 완료 시 풀로 반납된다
    public void CloseUI(IManagedUI instance, bool immediate = false)
    {
        instance.Close(immediate);
    }

    // 열려 있는 모든 UI를 일괄 닫기 — 씬 전환용이므로 기본값은 연출 없는 즉시 닫기
    public void CloseAll(bool immediate = true)
    {
        // 즉시 닫기 시 HandleFinishClose가 순회 중 활성 목록을 수정하므로 역순으로 순회한다
        for (int i = activeInstances.Count - 1; i >= 0; i--)
        {
            activeInstances[i].Close(immediate);
        }
    }

    // 프리팹 풀에서 대기 중인 인스턴스를 꺼내거나 새로 생성해 활성 목록에 등록한다
    private T GetOrCreateInstance<T>(T prefab) where T : MonoBehaviour, IManagedUI
    {
        if (!pools.TryGetValue(prefab, out Stack<IManagedUI> pool))
        {
            pool = new Stack<IManagedUI>();
            pools.Add(prefab, pool);
        }

        T instance = pool.Count > 0 ? (T)pool.Pop() : CreateInstance(prefab);
        activeInstances.Add(instance);
        return instance;
    }

    // 프리팹으로 새 인스턴스를 생성하고 풀 반납에 필요한 역매핑과 닫기 구독을 등록한다
    private T CreateInstance<T>(T prefab) where T : MonoBehaviour, IManagedUI
    {
        T instance = Instantiate(prefab, uiRoot != null ? uiRoot : transform);
        instance.gameObject.SetActive(false);
        instanceToPrefab.Add(instance, prefab);
        instance.OnFinishClose += HandleFinishClose;
        return instance;
    }

    // 닫기 완료된 인스턴스를 활성 목록에서 제거하고 소속 프리팹 풀로 반납한다
    private void HandleFinishClose(IManagedUI instance)
    {
        activeInstances.Remove(instance);
        pools[instanceToPrefab[instance]].Push(instance);
    }
}
