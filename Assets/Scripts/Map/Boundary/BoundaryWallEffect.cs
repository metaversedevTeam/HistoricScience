using UnityEngine;

// 내브메시 경계 세그먼트 위에 세워지는 벽 이펙트의 공통 베이스. 구매 에셋 등으로 교체할 때는 이 클래스를
// 상속한 어댑터를 프리팹 루트에 붙여 BoundaryWallEffectController의 프리팹 슬롯에 할당하면 된다.
public abstract class BoundaryWallEffect : MonoBehaviour
{
    // 세그먼트 양 끝점에 맞춰 위치와 회전을 잡는다. 벽 형태의 오브젝트/이펙트로 교체해도 자연스럽도록
    // 회전은 Y축(요)만 조절해 항상 수직으로 세우고, 높이는 두 끝점 중 낮은 쪽에 맞춘다. 하위 클래스에서
    // 오버라이드해 세그먼트 길이에 맞는 크기 조정 등을 추가로 수행한다.
    public virtual void SetSegment(Vector3 start, Vector3 end)
    {
        Vector3 direction = end - start;
        direction.y = 0f;

        Vector3 position = (start + end) * 0.5f;
        position.y = Mathf.Min(start.y, end.y);
        transform.position = position;

        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    // 이펙트 방출을 시작한다 (페이드 인)
    public abstract void Play();

    // 방출을 멈춘다. 남은 입자는 자연 소멸해 페이드 아웃처럼 보여야 한다.
    public abstract void Stop();

    // Stop 이후 남은 입자까지 모두 사라져 재사용(풀 회수)이 가능한 상태인지
    public abstract bool IsFinished { get; }
}
