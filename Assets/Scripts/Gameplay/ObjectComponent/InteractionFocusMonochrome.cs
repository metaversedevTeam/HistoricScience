using UnityEngine;

// 시민의 상호작용 명령 목록이 떠 있는 동안 MonochromeController로 화면을 흑백으로 바꾸고,
// 명령 목록이 가리키는 대상과 시민 자신에만 KeepColorTarget을 붙여 둘만 색을 유지하게 하는 컴포넌트.
// Citizen의 OnInteractionTargetChanged 콜백을 받아 동작하며, 설정에서 흑백 강조를 끄면 화면을 흑백으로 바꾸지 않는다.
[RequireComponent(typeof(Citizen))]
public class InteractionFocusMonochrome : MonoBehaviour
{
    // 명령 목록이 뜰 때 흑백으로 바뀌는 데 걸리는 시간(초)
    [SerializeField, Min(0f)] private float _fadeInDuration = 0.25f;

    // 명령 목록이 사라질 때 컬러로 돌아오는 데 걸리는 시간(초)
    [SerializeField, Min(0f)] private float _fadeOutDuration = 0.2f;

    // 상호작용 대상을 알려 주는 시민
    private Citizen _citizen;

    // 이 컴포넌트가 대상에 붙여 둔 KeepColorTarget. 대상에 원래 있던 것은 건드리지 않으므로 여기 담지 않는다.
    private KeepColorTarget _addedKeepColor;

    // 이 컴포넌트가 시민 자신에게 붙여 둔 KeepColorTarget. 시민에 원래 있던 것은 건드리지 않으므로 여기 담지 않는다.
    private KeepColorTarget _addedCitizenKeepColor;

    // 컬러로 돌아오는 동안 색을 유지시켜 두다가 다 돌아오면 떼어 낼 대상·시민의 KeepColorTarget
    private KeepColorTarget _pendingRelease;
    private KeepColorTarget _pendingCitizenRelease;

    // _pendingRelease를 떼어 낼 시각(unscaled 시간, 초)
    private float _pendingReleaseTime;

    // 화면을 흑백으로 돌려 둔 상태인지 여부
    private bool _isFocusing;

    private void Awake()
    {
        _citizen = GetComponent<Citizen>();
    }

    private void OnEnable()
    {
        _citizen.OnInteractionTargetChanged += HandleTargetChanged;
        GameSettings.MonochromeFocusEnabledChanged += HandleMonochromeFocusSettingChanged;
        HandleTargetChanged(_citizen.InteractionTarget);
    }

    private void OnDisable()
    {
        _citizen.OnInteractionTargetChanged -= HandleTargetChanged;
        GameSettings.MonochromeFocusEnabledChanged -= HandleMonochromeFocusSettingChanged;

        // 씬을 내리는 중에는 MonochromeController가 스스로 화면 색을 되돌리므로, 새로 만들어지지 않도록 부르지 않는다.
        if (gameObject.scene.isLoaded)
            HandleTargetChanged(null);

        _isFocusing = false;
        ReleaseKeepColor(ref _addedKeepColor);
        ReleaseKeepColor(ref _addedCitizenKeepColor);
        ReleaseKeepColor(ref _pendingRelease);
        ReleaseKeepColor(ref _pendingCitizenRelease);
    }

    private void Update()
    {
        HandlePendingRelease();
    }

    // 대상이 생기면 그 대상과 시민만 색을 남기고 화면을 흑백으로, 대상이 사라지거나 흑백 강조가 꺼져 있으면 화면을 컬러로 되돌린다.
    private void HandleTargetChanged(GameObject target)
    {
        if (target == null || !GameSettings.MonochromeFocusEnabled)
        {
            EndFocus();
            return;
        }

        // 다른 대상으로 바로 옮겨 가면 화면은 흑백 그대로이므로, 이전 대상은 바로 흑백으로 돌린다.
        ReleaseKeepColor(ref _pendingRelease);
        ReleaseKeepColor(ref _addedKeepColor);
        _addedKeepColor = AddKeepColor(target);
        KeepCitizenColor();

        _isFocusing = true;
        MonochromeController.Instance.SetMonochrome(true, _fadeInDuration);
    }

    // 설정에서 흑백 강조를 켜거나 끄면 지금 대상 기준으로 흑백 상태를 다시 맞춘다.
    private void HandleMonochromeFocusSettingChanged(bool isEnabled)
    {
        HandleTargetChanged(_citizen.InteractionTarget);
    }

    // 화면을 컬러로 되돌린다. 돌아오는 동안 대상이 먼저 흑백으로 바뀌어 보이지 않도록, 색 유지는 전환이 끝난 뒤에 뗀다.
    private void EndFocus()
    {
        if (!_isFocusing) return;

        _isFocusing = false;
        MonochromeController.Instance.SetMonochrome(false, _fadeOutDuration);

        ReleaseKeepColor(ref _pendingRelease);
        _pendingRelease = _addedKeepColor;
        _addedKeepColor = null;

        _pendingCitizenRelease = _addedCitizenKeepColor;
        _addedCitizenKeepColor = null;

        _pendingReleaseTime = Time.unscaledTime + _fadeOutDuration;
    }

    // 시민 자신도 색을 유지하게 한다. 컬러로 돌아오는 중에 다시 흑백이 되면 떼려고 남겨 둔 것을 그대로 다시 쓴다.
    private void KeepCitizenColor()
    {
        if (_addedCitizenKeepColor != null) return;

        if (_pendingCitizenRelease != null)
        {
            _addedCitizenKeepColor = _pendingCitizenRelease;
            _pendingCitizenRelease = null;
            return;
        }

        _addedCitizenKeepColor = AddKeepColor(gameObject);
    }

    // 컬러로 돌아오는 전환이 끝났으면 남겨 둔 색 유지를 뗀다.
    private void HandlePendingRelease()
    {
        if (_pendingRelease == null && _pendingCitizenRelease == null) return;
        if (Time.unscaledTime < _pendingReleaseTime) return;

        ReleaseKeepColor(ref _pendingRelease);
        ReleaseKeepColor(ref _pendingCitizenRelease);
    }

    // 대상에 KeepColorTarget이 없으면 붙여서 돌려준다. 이미 있으면 원래 것을 그대로 두고 null을 돌려준다.
    private static KeepColorTarget AddKeepColor(GameObject target)
    {
        if (target.TryGetComponent(out KeepColorTarget _)) return null;

        return target.AddComponent<KeepColorTarget>();
    }

    // 이 컴포넌트가 붙였던 KeepColorTarget을 즉시 끄고(렌더링 레이어 원복) 제거한 뒤 참조를 비운다.
    private static void ReleaseKeepColor(ref KeepColorTarget keepColor)
    {
        if (keepColor != null)
        {
            keepColor.enabled = false;
            Destroy(keepColor);
        }
        keepColor = null;
    }
}
