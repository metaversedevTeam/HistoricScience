using System;
using System.Collections.Generic;
using UnityEngine;

// 튜토리얼 진행 판정에 필요한 게임 쪽 변화를 한곳에서 지켜보는 감시자.
// 마우스로 돌린 시점 각도, WASD로 걸은 거리와 Shift로 달린 거리, 휠 확대 변화, 아이템 획득량, 조합법 힌트 공개를 모아 두며,
// 단계가 바뀔 때 기록을 초기화한다.
public class TutorialProgressWatcher : IDisposable
{
    // 씬 참조를 제공하는 문맥
    private readonly TutorialContext _context;

    // 아이템 ID별로 이번 단계에서 새로 얻은 개수
    private readonly Dictionary<int, int> _gained = new Dictionary<int, int>();

    // 직전 프레임에 카메라가 바라보던 수평 방위각(도). 시점 회전량을 누적하는 기준이다.
    private float _lastYaw;

    // 직전 프레임에 카메라가 내려다보던 각도(도). 위아래 시점 회전량을 누적하는 기준이다.
    private float _lastPitch;

    // 시점 각도를 아직 한 번도 재지 않았는지 여부
    private bool _hasLookSample;

    // 직전 프레임의 플레이어 시민 위치. 걸은 거리를 누적하는 기준이다.
    private Vector3 _lastCitizenPosition;

    // 시민 위치를 아직 한 번도 재지 않았는지 여부
    private bool _hasCitizenSample;

    // 직전 프레임의 확대 배율. 배율 변화를 누적하는 기준이다.
    private float _lastZoomScale;

    // 확대 배율을 아직 한 번도 재지 않았는지 여부
    private bool _hasZoomSample;

    // 이번 단계에서 마우스로 돌린 시점 각도의 누적량(도). 좌우와 위아래를 모두 센다.
    public float LookAngle { get; private set; }

    // 이번 단계에서 WASD로 직접 걸은 XZ 거리의 누적량(m). 달린 거리도 포함한다.
    public float WalkDistance { get; private set; }

    // 이번 단계에서 Shift를 누른 채 달린 XZ 거리의 누적량(m)
    public float RunDistance { get; private set; }

    // 이번 단계에서 휠로 바뀐 확대 배율의 누적 변화량
    public float ZoomChange { get; private set; }

    // 이번 단계에서 조합법 힌트가 공개되었는지 여부
    public bool HintRevealed { get; private set; }

    // 지켜볼 문맥을 받아 인벤토리·도감 이벤트를 구독한다.
    public TutorialProgressWatcher(TutorialContext context)
    {
        _context = context;

        if (_context.Inventory != null)
            _context.Inventory.OnAddItemAt += HandleItemGained;

        if (_context.Codex != null)
            _context.Codex.OnHintRevealed += HandleHintRevealed;
    }

    // 시점 회전, 걸은 거리, 확대 배율 변화를 누적한다.
    public void Tick()
    {
        HandleTrackLook();
        HandleTrackWalk();
        HandleTrackZoom();
    }

    // 이번 단계에서 지정한 아이템을 새로 몇 개 얻었는지 돌려준다.
    public int GetGained(ItemData item)
    {
        if (item == null) return 0;
        return _gained.TryGetValue(item.Id, out int count) ? count : 0;
    }

    // 단계별 기록을 모두 비운다. 단계가 바뀔 때 호출한다.
    public void Reset()
    {
        _gained.Clear();
        LookAngle = 0f;
        WalkDistance = 0f;
        RunDistance = 0f;
        ZoomChange = 0f;
        HintRevealed = false;
        _hasLookSample = false;
        _hasCitizenSample = false;
        _hasZoomSample = false;
    }

    // 구독을 모두 끊는다. 튜토리얼이 끝날 때 호출한다.
    public void Dispose()
    {
        if (_context.Inventory != null)
            _context.Inventory.OnAddItemAt -= HandleItemGained;

        if (_context.Codex != null)
            _context.Codex.OnHintRevealed -= HandleHintRevealed;
    }

    // 카메라가 이번 프레임에 좌우·위아래로 돈 각도를 누적한다. 작업대처럼 고정 시점이 걸린 동안에도 세지만,
    // 이 값을 쓰는 단계는 튜토리얼 맨 앞이라 다른 UI가 열려 있을 일이 없다.
    private void HandleTrackLook()
    {
        if (_context.WorldCamera == null) return;

        Vector3 euler = _context.WorldCamera.transform.eulerAngles;

        if (!_hasLookSample)
        {
            _lastYaw = euler.y;
            _lastPitch = euler.x;
            _hasLookSample = true;
            return;
        }

        LookAngle += Mathf.Abs(Mathf.DeltaAngle(_lastYaw, euler.y)) + Mathf.Abs(Mathf.DeltaAngle(_lastPitch, euler.x));
        _lastYaw = euler.y;
        _lastPitch = euler.x;
    }

    // 플레이어가 WASD를 누르고 있는 동안 시민이 움직인 XZ 거리를 누적하고, Shift도 누르고 있었다면 달린 거리에도 더한다.
    // 채집하러 대상에게 저절로 걸어가는 이동은 세지 않는다.
    private void HandleTrackWalk()
    {
        Citizen citizen = _context.FollowedCitizen;
        if (citizen == null) return;

        Vector3 position = citizen.transform.position;

        if (!_hasCitizenSample)
        {
            _lastCitizenPosition = position;
            _hasCitizenSample = true;
            return;
        }

        float dx = position.x - _lastCitizenPosition.x;
        float dz = position.z - _lastCitizenPosition.z;
        _lastCitizenPosition = position;

        PlayerManager player = _context.PlayerManager;
        if (player == null || player.MoveDirection == Vector2.zero) return;

        float distance = Mathf.Sqrt(dx * dx + dz * dz);
        WalkDistance += distance;

        if (player.IsRunning)
            RunDistance += distance;
    }

    // 휠로 바뀐 확대 배율의 변화량을 누적한다. 확대와 축소 어느 쪽이든 움직인 만큼 세므로,
    // 이미 최대나 최소까지 당겨 둔 상태에서도 반대쪽으로 굴리면 진행된다.
    private void HandleTrackZoom()
    {
        Transform cameraRoot = _context.CameraRoot;
        if (cameraRoot == null) return;

        float scale = cameraRoot.localScale.x;

        if (!_hasZoomSample)
        {
            _lastZoomScale = scale;
            _hasZoomSample = true;
            return;
        }

        ZoomChange += Mathf.Abs(scale - _lastZoomScale);
        _lastZoomScale = scale;
    }

    // 새로 얻은 아이템 개수를 종류별로 누적한다.
    private void HandleItemGained(ItemData item, int amount, Vector3 worldPosition)
    {
        if (item == null || amount <= 0) return;

        _gained.TryGetValue(item.Id, out int current);
        _gained[item.Id] = current + amount;
    }

    // 조합법 힌트가 공개되었음을 기록한다.
    private void HandleHintRevealed(ItemData item)
    {
        HintRevealed = true;
    }
}
