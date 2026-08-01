using UnityEngine;

// 세그먼트 길이에 맞춰 단일 ParticleSystem의 크기를 조정하는 경계 벽 이펙트. 실제 파티클 비주얼은
// 프리팹에서 할당한 ParticleSystem(예: 구매 에셋의 배리어 이펙트)을 그대로 사용한다.
public class DustWallParticleEffect : BoundaryWallEffect
{
    // 세그먼트 길이에 맞춰 크기를 조정할 대상 ParticleSystem
    [SerializeField] private ParticleSystem _particleSystem;

    // 위치/회전은 기본 동작을 따르고, 세그먼트 길이에 맞춰 파티클 시스템의 가로 크기를 조정한다
    public override void SetSegment(Vector3 start, Vector3 end)
    {
        base.SetSegment(start, end);

        if (_particleSystem == null) return;

        Vector3 direction = end - start;
        direction.y = 0f;

        ParticleSystem.MainModule main = _particleSystem.main;
        main.startSizeX = direction.magnitude;
    }

    // 이펙트 방출을 시작한다
    public override void Play()
    {
        if (_particleSystem != null)
            _particleSystem.Play();
    }

    // 방출만 멈추고 남은 입자는 자연 소멸시킨다 (페이드 아웃)
    public override void Stop()
    {
        if (_particleSystem != null)
            _particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    // 남은 입자까지 모두 사라져 재사용 가능한 상태인지
    public override bool IsFinished => _particleSystem == null || !_particleSystem.IsAlive(false);
}
