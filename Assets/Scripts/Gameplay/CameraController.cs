using UnityEngine;
using UnityEngine.InputSystem;

// 스타크래프트식 RTS 카메라 이동을 담당하는 컨트롤러
public class CameraController : MonoBehaviour
{
    [SerializeField] private PlayerManager _playerManager;

    [SerializeField] private float _moveSpeed = 20f;

    [Header("고도 제한")]
    [Tooltip("카메라가 내려갈 수 있는 최소 고도(월드 Y). 실제 지형 스케일에 맞게 설정한다")]
    [SerializeField] private float _minAltitude = -1000f;

    [Tooltip("카메라가 올라갈 수 있는 최대 고도(월드 Y). 실제 지형 스케일에 맞게 설정한다")]
    [SerializeField] private float _maxAltitude = 1000f;

    [Tooltip("고도 제한에 막힌 영역의 통과 허용 여부. 켜면 고도만 제한 범위로 고정하고, 끄면 해당 영역으로의 이동을 막는다")]
    [SerializeField] private bool _passThroughBlocked = true;

    private Transform _followTarget;

    private Vector3 _followOffset;

    private void OnEnable()
    {
        _playerManager.OnSelected += OnUnitSelected;
        _playerManager.OnDeselected += OnUnitDeselected;
    }

    private void OnDisable()
    {
        _playerManager.OnSelected -= OnUnitSelected;
        _playerManager.OnDeselected -= OnUnitDeselected;
    }

    private void LateUpdate()
    {
        if (_followTarget != null)
        {
            HandleFollowTarget();
        }
        else
        {
            HandleKeyboardMovement();
        }
    }

    // 선택된 유닛을 따라갈 대상으로 설정하고 이동 오프셋을 초기화
    private void OnUnitSelected(SelectableObject selected)
    {
        _followTarget = selected.transform;
        _followOffset = Vector3.zero;
    }

    // 선택 해제 시 따라갈 대상과 이동 오프셋을 초기화
    private void OnUnitDeselected()
    {
        _followTarget = null;
        _followOffset = Vector3.zero;
    }

    // 키보드 오프셋을 더한 위치로 이동을 시도하고, 고도 제한에 막히면 기존 오프셋으로 대상만 따라간다
    private void HandleFollowTarget()
    {
        Vector3 desiredOffset = _followOffset + GetKeyboardMoveDelta();

        if (TryMoveToXZ(_followTarget.position.x + desiredOffset.x, _followTarget.position.z + desiredOffset.z))
        {
            _followOffset = desiredOffset;
        }
        else
        {
            TryMoveToXZ(_followTarget.position.x + _followOffset.x, _followTarget.position.z + _followOffset.z);
        }
    }

    // 키보드 화살표 입력으로 카메라를 XZ 평면에서 이동시킨다
    private void HandleKeyboardMovement()
    {
        Vector3 target = transform.position + GetKeyboardMoveDelta();
        TryMoveToXZ(target.x, target.z);
    }

    // 키보드 화살표 입력으로 계산한 이번 프레임의 XZ 이동량을 반환한다
    private Vector3 GetKeyboardMoveDelta()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector3.zero;
        }

        Vector2 input = Vector2.zero;
        if (keyboard.upArrowKey.isPressed) { input.y += 1f; }
        if (keyboard.downArrowKey.isPressed) { input.y -= 1f; }
        if (keyboard.rightArrowKey.isPressed) { input.x += 1f; }
        if (keyboard.leftArrowKey.isPressed) { input.x -= 1f; }

        if (input == Vector2.zero)
        {
            return Vector3.zero;
        }

        Vector3 direction = new Vector3(input.x, 0f, input.y).normalized;
        return direction * (_moveSpeed * Time.deltaTime);
    }

    // 지정한 XZ로 이동을 시도하며, 고도 제한에 막히고 통과가 불가하면 이동을 취소한다. 이동 성공 여부를 반환한다
    private bool TryMoveToXZ(float x, float z)
    {
        Vector3 candidate = new Vector3(x, transform.position.y, z);
        float altitude = ResolveAltitude(candidate, out bool blocked);

        if (blocked && !_passThroughBlocked)
        {
            return false;
        }

        candidate.y = altitude;
        transform.position = candidate;
        return true;
    }

    // 지정한 위치의 터레인 높이를 고도 제한으로 고정한 값을 반환하고, 제한 범위를 벗어났는지 여부를 blocked로 전달한다
    private float ResolveAltitude(Vector3 position, out bool blocked)
    {
        Terrain terrain = FindTerrainAt(position);
        if (terrain == null)
        {
            blocked = false;
            return position.y;
        }

        float terrainHeight = terrain.transform.position.y + terrain.SampleHeight(position);
        blocked = terrainHeight < _minAltitude || terrainHeight > _maxAltitude;
        return Mathf.Clamp(terrainHeight, _minAltitude, _maxAltitude);
    }

    // 지정한 XZ 좌표를 포함하는 터레인을 찾는다
    private Terrain FindTerrainAt(Vector3 position)
    {
        foreach (Terrain activeTerrain in Terrain.activeTerrains)
        {
            Vector3 origin = activeTerrain.transform.position;
            Vector3 size = activeTerrain.terrainData.size;
            if (position.x >= origin.x && position.x <= origin.x + size.x &&
                position.z >= origin.z && position.z <= origin.z + size.z)
            {
                return activeTerrain;
            }
        }

        return null;
    }

    // 지정한 XZ 좌표로 카메라를 즉시 이동시키고 고도를 제한 범위로 고정한다
    public void MoveTo(Vector2 xzPosition)
    {
        Vector3 candidate = new Vector3(xzPosition.x, transform.position.y, xzPosition.y);
        candidate.y = ResolveAltitude(candidate, out _);
        transform.position = candidate;
    }

    // 지정한 X, Z 좌표로 카메라를 즉시 이동시킨다
    public void MoveTo(float x, float z)
    {
        MoveTo(new Vector2(x, z));
    }
}
