using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 스타크래프트식 RTS 카메라 이동을 담당하는 컨트롤러
public class CameraController : MonoBehaviour, ISavable
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

    [Header("휠 확대/축소")]
    [Tooltip("휠로 줄일 수 있는 최소 크기. 가장 확대된 상태이다")]
    [SerializeField] private float _minZoomScale = 0.5f;

    [Tooltip("휠로 키울 수 있는 최대 크기. 가장 축소된 상태이며, 런타임에 MaxZoomScale로 바꿀 수 있다")]
    [SerializeField] private float _maxZoomScale = 2f;

    [Tooltip("휠을 한 번 굴릴 때 변하는 크기")]
    [SerializeField] private float _zoomStep = 0.1f;

    private Transform _followTarget;

    // 따라갈 대상의 이동 컴포넌트. 대상에 이동 기능이 없으면 null이다.
    private IMover _followTargetMover;

    private Vector3 _followOffset;

    private Camera _camera;

    // 스페이스바를 누른 순간 찾아낸 가장 가까운 시민. 누르고 있는 동안 이 대상을 계속 따라간다.
    private Transform _focusedCitizen;

    // 화면 밖 판정에 쓸 카메라를 캐싱한다. 이 컴포넌트는 카메라에 붙는 것이 기본이고, 아니면 메인 카메라를 쓴다.
    private void Awake()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null)
            _camera = Camera.main;

        ApplyZoomScale(transform.localScale.x);
    }

    private void OnEnable()
    {
        _playerManager.OnSelected += OnUnitSelected;
        _playerManager.OnDeselected += OnUnitDeselected;
    }

    private void OnDisable()
    {
        _playerManager.OnSelected -= OnUnitSelected;
        _playerManager.OnDeselected -= OnUnitDeselected;
        UnsubscribeFromFollowTargetMover();
    }

    private void LateUpdate()
    {
        HandleZoom();

        if (HandleFocusNearestCitizen()) return;

        if (_followTarget != null)
        {
            HandleFollowTarget();
        }
        else
        {
            HandleKeyboardMovement();
        }
    }

    // 휠로 키울 수 있는 최대 크기. 런타임에 바꿀 수 있으며, 값을 바꾸면 현재 크기도 즉시 범위 안으로 맞춘다
    public float MaxZoomScale
    {
        get => _maxZoomScale;
        set
        {
            _maxZoomScale = Mathf.Max(value, _minZoomScale);
            ApplyZoomScale(transform.localScale.x);
        }
    }

    // 마우스 휠 입력으로 이 오브젝트의 크기를 조절해 확대/축소한다
    private void HandleZoom()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f)) return;

        // 휠을 위로 굴리면 크기를 줄여 확대하고, 아래로 굴리면 크기를 키워 축소한다
        ApplyZoomScale(transform.localScale.x - Mathf.Sign(scroll) * _zoomStep);
    }

    // 지정한 크기를 최소/최대 범위로 고정해 균등 스케일로 적용한다
    private void ApplyZoomScale(float scale)
    {
        transform.localScale = Vector3.one * Mathf.Clamp(scale, _minZoomScale, _maxZoomScale);
    }

    // 선택된 유닛을 따라갈 대상으로 설정하고 이동 오프셋을 초기화하며, 대상의 이동 명령을 구독
    private void OnUnitSelected(SelectableObject selected)
    {
        UnsubscribeFromFollowTargetMover();

        _followTarget = selected.transform;
        _followOffset = Vector3.zero;

        SubscribeToFollowTargetMover(selected);
    }

    // 선택 해제 시 따라갈 대상과 이동 오프셋을 초기화하고 이동 명령 구독을 해제
    private void OnUnitDeselected()
    {
        UnsubscribeFromFollowTargetMover();

        _followTarget = null;
        _followOffset = Vector3.zero;
    }

    // 따라갈 대상에 이동 기능이 있으면 이동 명령 이벤트를 구독한다.
    private void SubscribeToFollowTargetMover(SelectableObject selected)
    {
        _followTargetMover = selected.GetComponent<IMover>();
        if (_followTargetMover == null) return;

        _followTargetMover.OnMoveOrdered += HandleFollowTargetMoveOrdered;
    }

    // 구독 중인 이동 명령 이벤트를 해제하고 참조를 비운다.
    private void UnsubscribeFromFollowTargetMover()
    {
        if (_followTargetMover == null) return;

        _followTargetMover.OnMoveOrdered -= HandleFollowTargetMoveOrdered;
        _followTargetMover = null;
    }

    // 이동 명령을 내린 시점에 대상이 화면 밖이면 따라가기를 중단한다. 화면 밖 대상을 오프셋만큼 떨어져 쫓아가면
    // 카메라가 보고 있던 곳에서 멀리 튀므로, 그 경우에는 대상을 놓아주고 현재 위치에 머문다.
    private void HandleFollowTargetMoveOrdered()
    {
        if (_followTarget == null) return;
        if (IsInsideView(_followTarget.position)) return;

        UnsubscribeFromFollowTargetMover();
        _followTarget = null;
    }

    // 지정한 월드 위치가 카메라 화면 안에 들어오는지 판정한다. 카메라를 찾지 못했으면 오프셋을 건드리지 않도록 화면 안으로 본다.
    private bool IsInsideView(Vector3 worldPosition)
    {
        if (_camera == null) return true;

        Vector3 viewportPoint = _camera.WorldToViewportPoint(worldPosition);
        return viewportPoint.z > 0f &&
               viewportPoint.x >= 0f && viewportPoint.x <= 1f &&
               viewportPoint.y >= 0f && viewportPoint.y <= 1f;
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

    // 스페이스바를 누른 순간 가장 가까운 시민을 찾아 두고, 누르고 있는 동안 그 시민 위치로 카메라를 계속 옮긴다.
    // 시민을 따라가는 중이면 true를 반환해 이번 프레임의 다른 이동 처리를 건너뛰게 한다.
    private bool HandleFocusNearestCitizen()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.spaceKey.isPressed)
        {
            _focusedCitizen = null;
            return false;
        }

        // 누른 순간 화살표로 쌓아 둔 오프셋을 지운다. 오프셋을 남겨 두면 스페이스바를 뗀 뒤 선택 유닛을
        // 다시 따라갈 때 카메라가 방금 보고 있던 자리에서 그 오프셋만큼 튀기 때문이다.
        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            _focusedCitizen = FindNearestCitizen();
            _followOffset = Vector3.zero;
        }

        // 누른 순간 시민이 없었거나 따라가던 시민이 파괴되었으면 평소 이동으로 되돌린다
        if (_focusedCitizen == null) return false;

        MoveTo(_focusedCitizen.position.x, _focusedCitizen.position.z);
        return true;
    }

    // 카메라의 XZ 위치에서 가장 가까운 시민의 Transform을 찾는다. 시민이 하나도 없으면 null을 반환한다
    private Transform FindNearestCitizen()
    {
        Citizen[] citizens = FindObjectsByType<Citizen>(FindObjectsSortMode.None);

        Transform nearest = null;
        float nearestSqrDistance = float.MaxValue;

        foreach (Citizen citizen in citizens)
        {
            Vector3 delta = citizen.transform.position - transform.position;
            float sqrDistance = delta.x * delta.x + delta.z * delta.z;
            if (sqrDistance >= nearestSqrDistance) continue;

            nearestSqrDistance = sqrDistance;
            nearest = citizen.transform;
        }

        return nearest;
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

    // 씬에 상주하는 객체라 프리팹 소환에 쓰이지 않는 고정 식별자
    public string PrefabId => "CameraController";

    // 현재 카메라의 XZ 위치를 JSON 문자열로 캡처한다. Y(고도)는 지형 높이로 다시 계산되므로 저장하지 않는다.
    public string CaptureJson()
    {
        SaveState state = new SaveState { Position = new Vector2(transform.position.x, transform.position.z) };
        return JsonUtility.ToJson(state);
    }

    // JSON 문자열에서 카메라의 XZ 위치를 복원한다. MoveTo를 통해 적용되므로 고도는 현재 지형 높이로 다시 계산된다.
    public void ApplyJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        SaveState state = JsonUtility.FromJson<SaveState>(json);
        if (state == null) return;

        MoveTo(state.Position);
    }

    // 카메라 위치 저장 상태의 직렬화 래퍼
    [Serializable]
    private class SaveState
    {
        public Vector2 Position;
    }
}
