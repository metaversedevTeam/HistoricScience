using UnityEngine;
using UnityEngine.InputSystem;

// 스타크래프트식 RTS 카메라 이동을 담당하는 컨트롤러
public class CameraController : MonoBehaviour
{
    [SerializeField] private PlayerManager _playerManager;

    [SerializeField] private float _moveSpeed = 20f;

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

        HandleTerrainHeight();
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

    // 키보드 입력을 오프셋에 누적하고 대상의 XZ 좌표에 오프셋을 더한 위치로 카메라를 이동시킨다
    private void HandleFollowTarget()
    {
        _followOffset += GetKeyboardMoveDelta();

        Vector3 position = transform.position;
        position.x = _followTarget.position.x + _followOffset.x;
        position.z = _followTarget.position.z + _followOffset.z;
        transform.position = position;
    }

    // 키보드 화살표 입력으로 카메라를 XZ 평면에서 이동시킨다
    private void HandleKeyboardMovement()
    {
        transform.position += GetKeyboardMoveDelta();
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

    // 카메라의 Y좌표를 현재 XZ 위치의 터레인 높이에 맞춘다
    private void HandleTerrainHeight()
    {
        Terrain terrain = FindTerrainAt(transform.position);
        if (terrain == null)
        {
            return;
        }

        Vector3 position = transform.position;
        position.y = terrain.transform.position.y + terrain.SampleHeight(position);
        transform.position = position;
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

    // 지정한 XZ 좌표로 카메라를 즉시 이동시킨다
    public void MoveTo(Vector2 xzPosition)
    {
        transform.position = new Vector3(xzPosition.x, transform.position.y, xzPosition.y);
        HandleTerrainHeight();
    }

    // 지정한 X, Z 좌표로 카메라를 즉시 이동시킨다
    public void MoveTo(float x, float z)
    {
        MoveTo(new Vector2(x, z));
    }
}
