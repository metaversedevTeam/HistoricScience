using UnityEngine;

// 자신의 XZ 위치에서 Ground 레이어로 아래를 향해 레이캐스트해 터레인 표면 높이에 맞춰 배치하는 컴포넌트.
// 청크 터레인이 여러 개라 Terrain.activeTerrain은 위치에 따라 다른 청크를 가리켜 틀릴 수 있으므로 레이캐스트를 쓴다.
public class GroundSnapper : MonoBehaviour
{
    // 레이캐스트로 감지할 지면 레이어. 비워두면 Awake에서 "Ground" 레이어를 자동으로 찾는다.
    [SerializeField] private LayerMask _groundLayer;
    // 지면에서 띄울 높이(m)
    [SerializeField] private float _heightOffset = 0f;
    // 자신의 위치 기준 이 높이(m)만큼 위에서부터 아래로 레이를 쏜다. 터레인 최대 높이보다 충분히 커야 한다.
    [SerializeField, Min(0f)] private float _raycastUpDistance = 500f;
    // 레이캐스트 총 길이(m). 위 시작 높이를 지나 터레인 아래까지 닿을 만큼 충분히 커야 한다.
    [SerializeField, Min(0f)] private float _raycastLength = 1000f;
    // 활성화될 때(오브젝트 배치 시점) 자동으로 지면 높이에 맞출지 여부
    [SerializeField] private bool _snapOnEnable = true;

    // Ground 레이어가 미설정된 경우 자동으로 찾아 할당
    private void Awake()
    {
        if (_groundLayer.value == 0)
        {
            int idx = LayerMask.NameToLayer("Ground");
            if (idx >= 0)
                _groundLayer = 1 << idx;
            else
                Debug.LogWarning("[GroundSnapper] 'Ground' 레이어를 찾을 수 없습니다. Inspector에서 직접 설정해주세요.");
        }
    }

    // 활성화 시(오브젝트가 배치되는 시점) 자동으로 지면 높이에 맞춘다
    private void OnEnable()
    {
        if (_snapOnEnable)
            SnapToGround();
    }

    // 자신의 XZ 위치 아래 지면 높이를 레이캐스트로 찾아 Y 위치를 맞춘다. 원하는 임의의 타이밍에 호출해 갱신할 수 있다.
    // 지면을 찾지 못하면 위치를 바꾸지 않고 false를 반환한다.
    public bool SnapToGround()
    {
        Vector3 origin = transform.position + Vector3.up * _raycastUpDistance;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _raycastLength, _groundLayer))
            return false;

        Vector3 position = transform.position;
        position.y = hit.point.y + _heightOffset;
        transform.position = position;
        return true;
    }
}
