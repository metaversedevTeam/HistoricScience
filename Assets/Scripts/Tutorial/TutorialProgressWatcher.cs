using System;
using System.Collections.Generic;
using UnityEngine;

// 튜토리얼 진행 판정에 필요한 게임 쪽 변화를 한곳에서 지켜보는 감시자.
// 카메라 이동 거리, 시민 이동 명령, 아이템 획득량, 조합법 힌트 공개를 모아 두며, 단계가 바뀔 때 기록을 초기화한다.
public class TutorialProgressWatcher : IDisposable
{
    // 씬 참조를 제공하는 문맥
    private readonly TutorialContext _context;

    // 아이템 ID별로 이번 단계에서 새로 얻은 개수
    private readonly Dictionary<int, int> _gained = new Dictionary<int, int>();

    // 이동 명령을 구독 중인 시민의 이동 컴포넌트
    private IMover _subscribedMover;

    // 직전 프레임의 카메라 위치. 이동 거리를 누적하는 기준이다.
    private Vector3 _lastCameraPosition;

    // 카메라 위치를 아직 한 번도 재지 않았는지 여부
    private bool _hasCameraSample;

    // 이번 단계에서 카메라가 XZ 평면에서 움직인 누적 거리
    public float CameraTravel { get; private set; }

    // 이번 단계에서 선택된 시민에게 이동 명령이 내려졌는지 여부
    public bool MoveOrdered { get; private set; }

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

    // 카메라 이동 거리를 누적하고, 선택이 바뀌었으면 이동 명령 구독을 새 시민으로 옮긴다.
    public void Tick()
    {
        HandleTrackCamera();
        HandleTrackSelectedMover();
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
        CameraTravel = 0f;
        MoveOrdered = false;
        HintRevealed = false;
        _hasCameraSample = false;
    }

    // 구독을 모두 끊는다. 튜토리얼이 끝날 때 호출한다.
    public void Dispose()
    {
        if (_context.Inventory != null)
            _context.Inventory.OnAddItemAt -= HandleItemGained;

        if (_context.Codex != null)
            _context.Codex.OnHintRevealed -= HandleHintRevealed;

        UnsubscribeMover();
    }

    // 카메라가 이번 프레임에 움직인 XZ 거리를 누적한다.
    private void HandleTrackCamera()
    {
        if (_context.WorldCamera == null) return;

        Vector3 position = _context.WorldCamera.transform.position;

        if (!_hasCameraSample)
        {
            _lastCameraPosition = position;
            _hasCameraSample = true;
            return;
        }

        float dx = position.x - _lastCameraPosition.x;
        float dz = position.z - _lastCameraPosition.z;
        CameraTravel += Mathf.Sqrt(dx * dx + dz * dz);
        _lastCameraPosition = position;
    }

    // 현재 선택된 시민의 이동 컴포넌트로 구독을 옮긴다. 선택이 풀렸으면 구독을 끊는다.
    private void HandleTrackSelectedMover()
    {
        Citizen citizen = _context.SelectedCitizen;
        IMover mover = citizen != null ? citizen.GetComponent<IMover>() : null;

        if (ReferenceEquals(mover, _subscribedMover) && !IsSubscribedMoverDestroyed())
            return;

        UnsubscribeMover();

        _subscribedMover = mover;
        if (_subscribedMover != null)
            _subscribedMover.OnMoveOrdered += HandleMoveOrdered;
    }

    // 구독해 둔 이동 컴포넌트가 이미 파괴되었는지 확인한다.
    private bool IsSubscribedMoverDestroyed()
    {
        return _subscribedMover is Component component && component == null;
    }

    // 구독 중인 이동 명령 이벤트를 해제하고 참조를 비운다.
    private void UnsubscribeMover()
    {
        if (_subscribedMover == null) return;

        if (!IsSubscribedMoverDestroyed())
            _subscribedMover.OnMoveOrdered -= HandleMoveOrdered;

        _subscribedMover = null;
    }

    // 선택된 시민에게 이동 명령이 내려졌음을 기록한다.
    private void HandleMoveOrdered()
    {
        MoveOrdered = true;
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
