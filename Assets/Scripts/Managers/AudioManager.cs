using System.Collections.Generic;
using UnityEngine;

// 씬마다 하나씩 배치해 그 씬의 BGM 목록과 UI 효과음 재생을 담당하는 매니저.
// BGM은 목록에서 무작위로 한 곡을 골라 재생하고, 곡이 끝나면 방금 튼 곡을 뺀 나머지 중에서 다시 무작위로 골라 이어 재생한다.
// 효과음 클립은 Resources의 오디오 프리셋 에셋(AudioPreset)에 모아 두고, 재생할 때는 부르는 쪽이 그 클립을 매개변수로 넘긴다.
// 시민의 발소리처럼 어떤 대상이 특정 상태인 동안 계속 이어져야 하는 소리는 반복 효과음(LoopSfx)으로 재생한다. 이때 쓰는 클립은 프리셋이 아니라 부르는 쪽이 들고 있는다.
// 씬에 배치하지 않았더라도 Instance에 처음 접근할 때 빈 매니저를 하나 만들어 주므로, 부르는 쪽에서 null을 확인할 필요가 없다.
public class AudioManager : MonoBehaviour
{
    // 현재 씬의 AudioManager. 씬에 배치돼 있으면 그 인스턴스를 쓰고, 없으면 빈 게임오브젝트를 만들어 붙여 준다.
    // 이렇게 만들어진 인스턴스는 BGM 목록이 비어 있어 효과음 재생 용도로만 쓸 수 있다. (UIManager와 같은 지연 생성 방식)
    public static AudioManager Instance
    {
        get
        {
            if (_instance != null) return _instance;

            _instance = FindFirstObjectByType<AudioManager>();
            if (_instance == null)
                _instance = new GameObject(nameof(AudioManager)).AddComponent<AudioManager>(); // Awake에서 _instance에 자신을 등록한다

            return _instance;
        }
    }

    // Resources에서 읽어 온 공용 오디오 프리셋. 연결하지 않은 칸은 null이며, null 클립은 재생 쪽에서 무시한다.
    public static AudioPreset Preset => AudioPreset.Instance;

    // 지금 재생 중인 BGM 클립. 재생 중이 아니면 null이다.
    public AudioClip CurrentBgm => _bgmSource != null ? _bgmSource.clip : null;

    public float BgmVolume => _bgmVolume;

    public float SfxVolume => _sfxVolume;

    [Header("BGM")]
    [Tooltip("이 씬에서 돌려 가며 재생할 BGM 목록. 비워 두면 BGM을 재생하지 않는다.")]
    [SerializeField] private List<AudioClip> _bgmClips = new List<AudioClip>();

    [Tooltip("씬이 시작될 때 BGM 재생을 자동으로 시작할지 여부")]
    [SerializeField] private bool _playBgmOnStart = true;

    [Tooltip("한 곡이 끝나고 다음 곡을 시작하기까지 쉬는 시간(초)")]
    [SerializeField, Min(0f)] private float _bgmInterval = 1f;

    [SerializeField, Range(0f, 1f)] private float _bgmVolume = 0.5f;

    [Header("SFX")]
    [SerializeField, Range(0f, 1f)] private float _sfxVolume = 1f;

    [Header("반복 효과음")]
    [Tooltip("반복 효과음이 이 거리(m) 안에서는 줄어들지 않고 그대로 들린다.")]
    [SerializeField, Min(0f)] private float _loopSfxMinDistance = 3f;

    [Tooltip("반복 효과음이 이 거리(m)를 넘어가면 들리지 않는다.")]
    [SerializeField, Min(0f)] private float _loopSfxMaxDistance = 25f;

    // AudioSource가 받아 주는 재생 속도(피치)의 범위. 이 밖의 값은 소리가 나지 않으므로 잘라서 쓴다.
    private const float k_MinLoopSfxPitch = 0.1f;
    private const float k_MaxLoopSfxPitch = 3f;

    private static AudioManager _instance;

    private AudioSource _bgmSource;

    private AudioSource _sfxSource;

    // BGM 목록에서 빈 칸을 걸러 낸 실제 재생 목록
    private readonly List<AudioClip> _playableBgmClips = new List<AudioClip>();

    // 지금 재생 중인 반복 효과음 목록. 매 프레임 대상 위치를 따라가고, 대상이 사라지면 여기서 정리한다.
    private readonly List<LoopSfxHandle> _activeLoopSfx = new List<LoopSfxHandle>();

    // 멈춘 반복 효과음의 AudioSource를 모아 두고 다시 쓰는 목록 (소리마다 게임오브젝트를 새로 만들지 않기 위함)
    private readonly List<AudioSource> _idleLoopSfxSources = new List<AudioSource>();

    // 지금 재생 중인 곡의 _playableBgmClips 내 인덱스. 아직 한 곡도 틀지 않았으면 -1.
    private int _currentBgmIndex = -1;

    // 다음 곡을 시작하기까지 남은 대기 시간
    private float _bgmIntervalTimer;

    // BGM 자동 재생이 켜져 있는지 여부 (StopBgm으로 끈다)
    private bool _isBgmRunning;

    // PauseBgm으로 멈춰 둔 상태인지 여부. 이때는 곡이 끝난 것으로 착각해 다음 곡으로 넘어가면 안 된다.
    private bool _isBgmPaused;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"[{nameof(AudioManager)}] 씬에 AudioManager가 둘 이상 있어 {name}의 것을 제거한다.", this);
            Destroy(this);
            return;
        }

        _instance = this;

        SetupSources();
        BuildPlayableBgmList();
    }

    private void Start()
    {
        if (_playBgmOnStart)
            PlayBgm();
    }

    private void Update()
    {
        HandleBgmAdvance();
        HandleLoopSfxFollow();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void OnValidate()
    {
        ApplyBgmVolume();
        ApplyLoopSfxVolume();
    }

    // AudioManager가 없는 씬에서 불러도 안전한 UI 효과음 재생 진입점
    public static void PlayUI(AudioClip clip)
    {
        if (clip == null) return; // 재생할 것이 없으면 매니저를 새로 만들지 않는다

        Instance.PlayUISfx(clip);
    }

    // AudioManager가 없는 씬에서 불러도 안전한 반복 효과음 재생 진입점. 멈출 때 쓸 손잡이를 돌려준다.
    public static LoopSfxHandle PlayLoop(AudioClip clip, Transform target, float volumeScale = 1f, float pitch = 1f)
    {
        if (clip == null || target == null) return null; // 재생할 것이 없으면 매니저를 새로 만들지 않는다

        return Instance.PlayLoopSfx(clip, target, volumeScale, pitch);
    }

    // 손잡이가 가리키는 반복 효과음을 멈춘다. 매니저가 이미 사라진 뒤에 불러도 안전하다.
    public static void StopLoop(LoopSfxHandle handle)
    {
        if (handle == null || _instance == null) return;

        _instance.StopLoopSfx(handle);
    }

    // 손잡이가 가리키는 반복 효과음의 재생 속도를 바꾼다. 매니저가 이미 사라진 뒤에 불러도 안전하다.
    public static void SetLoopPitch(LoopSfxHandle handle, float pitch)
    {
        if (handle == null || _instance == null) return;

        _instance.SetLoopSfxPitch(handle, pitch);
    }

    // 버튼을 눌렀을 때의 효과음을 프리셋에서 꺼내 재생한다
    public static void PlayButtonClick() => PlayUI(Preset.ButtonClick);

    // 팝업·창이 열릴 때의 효과음을 프리셋에서 꺼내 재생한다
    public static void PlayPopupOpen() => PlayUI(Preset.PopupOpen);

    // 팝업·창이 닫힐 때의 효과음을 프리셋에서 꺼내 재생한다
    public static void PlayPopupClose() => PlayUI(Preset.PopupClose);

    // 확인·수락을 눌렀을 때의 효과음을 프리셋에서 꺼내 재생한다
    public static void PlayConfirm() => PlayUI(Preset.Confirm);

    // 취소·뒤로가기를 눌렀을 때의 효과음을 프리셋에서 꺼내 재생한다
    public static void PlayCancel() => PlayUI(Preset.Cancel);

    // 할 수 없는 동작을 시도했을 때의 효과음을 프리셋에서 꺼내 재생한다
    public static void PlayError() => PlayUI(Preset.Error);

    // 씬의 BGM 목록에서 무작위로 한 곡을 골라 재생을 시작한다
    public void PlayBgm()
    {
        if (_playableBgmClips.Count == 0)
        {
            _isBgmRunning = false;
            return;
        }

        _isBgmRunning = true;
        PlayNextBgm();
    }

    // 재생 중인 곡을 멈추고 자동 재생을 끈다
    public void StopBgm()
    {
        _isBgmRunning = false;
        _isBgmPaused = false;
        _bgmIntervalTimer = 0f;
        _currentBgmIndex = -1;

        if (_bgmSource != null)
            _bgmSource.Stop();
    }

    // 현재 곡을 즉시 끝내고 다른 곡으로 넘어간다
    public void SkipBgm()
    {
        if (!_isBgmRunning || _playableBgmClips.Count == 0) return;

        PlayNextBgm();
    }

    // 재생 위치를 유지한 채 BGM을 잠시 멈춘다
    public void PauseBgm()
    {
        if (_bgmSource == null || !_bgmSource.isPlaying) return;

        _bgmSource.Pause();
        _isBgmPaused = true;
    }

    // 일시정지한 BGM을 멈춘 지점부터 다시 재생한다
    public void ResumeBgm()
    {
        if (_bgmSource == null || !_isBgmPaused) return;

        _bgmSource.UnPause();
        _isBgmPaused = false;
    }

    // 넘겨받은 클립으로 UI 효과음을 재생한다 (여러 소리가 겹쳐도 서로 끊기지 않는다)
    public void PlayUISfx(AudioClip clip)
    {
        if (clip == null || _sfxSource == null) return;

        _sfxSource.PlayOneShot(clip, _sfxVolume);
    }

    // 넘겨받은 클립을 볼륨 배율을 따로 지정해 재생한다 (효과음별 크기 보정용)
    public void PlayUISfx(AudioClip clip, float volumeScale)
    {
        if (clip == null || _sfxSource == null) return;

        _sfxSource.PlayOneShot(clip, _sfxVolume * Mathf.Clamp01(volumeScale));
    }

    // 대상의 위치에서 반복 재생되는 효과음을 시작하고, 나중에 멈출 때 쓸 손잡이를 돌려준다
    public LoopSfxHandle PlayLoopSfx(AudioClip clip, Transform target, float volumeScale = 1f, float pitch = 1f)
    {
        if (clip == null || target == null) return null;

        AudioSource source = RentLoopSfxSource();
        source.transform.position = target.position;
        source.clip = clip;
        source.pitch = ClampLoopSfxPitch(pitch);
        source.Play();

        var handle = new LoopSfxHandle(source, target, Mathf.Clamp01(volumeScale));
        handle.ApplyVolume(_sfxVolume);
        _activeLoopSfx.Add(handle);
        return handle;
    }

    // 손잡이가 가리키는 반복 효과음을 멈추고 AudioSource를 재사용 목록으로 돌려놓는다 (이미 멈춘 손잡이는 무시한다)
    public void StopLoopSfx(LoopSfxHandle handle)
    {
        if (handle == null || !handle.IsPlaying) return;

        _activeLoopSfx.Remove(handle);
        ReleaseLoopSfx(handle);
    }

    // 재생 중인 반복 효과음의 볼륨 배율만 바꾼다 (소리마다 크기를 따로 보정하는 용도)
    public void SetLoopSfxVolumeScale(LoopSfxHandle handle, float volumeScale)
    {
        if (handle == null || !handle.IsPlaying) return;

        handle.VolumeScale = Mathf.Clamp01(volumeScale);
        handle.ApplyVolume(_sfxVolume);
    }

    // 재생 중인 반복 효과음의 재생 속도(피치)를 바꾼다 (이동 속도가 바뀌는 동안 발소리 빠르기를 맞추는 용도)
    public void SetLoopSfxPitch(LoopSfxHandle handle, float pitch)
    {
        if (handle == null || !handle.IsPlaying) return;

        handle.Source.pitch = ClampLoopSfxPitch(pitch);
    }

    // BGM 볼륨을 0~1 범위로 바꾸고 재생 중인 곡에 바로 반영한다
    public void SetBgmVolume(float volume)
    {
        _bgmVolume = Mathf.Clamp01(volume);
        ApplyBgmVolume();
    }

    // 효과음 볼륨을 0~1 범위로 바꾼다 (단발 효과음은 다음 재생부터, 반복 효과음은 재생 중인 것까지 바로 적용된다)
    public void SetSfxVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        ApplyLoopSfxVolume();
    }

    // BGM용과 효과음용 AudioSource를 코드로 만들어 둔다
    private void SetupSources()
    {
        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.playOnAwake = false;
        _bgmSource.loop = false;
        _bgmSource.spatialBlend = 0f; // 거리와 무관한 2D 재생
        _bgmSource.volume = _bgmVolume;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.loop = false;
        _sfxSource.spatialBlend = 0f;
        _sfxSource.volume = 1f; // 효과음 볼륨은 PlayOneShot에 배율로 넘긴다
    }

    // BGM 목록에서 비어 있는 칸을 걸러 실제 재생 목록을 만든다
    private void BuildPlayableBgmList()
    {
        _playableBgmClips.Clear();

        foreach (AudioClip clip in _bgmClips)
        {
            if (clip != null)
                _playableBgmClips.Add(clip);
        }
    }

    // 곡이 끝났는지 지켜보다가, 쉬는 시간만큼 기다린 뒤 다른 곡으로 넘어간다
    private void HandleBgmAdvance()
    {
        if (!_isBgmRunning || _isBgmPaused || AudioListener.pause) return;
        if (_bgmSource == null || _bgmSource.isPlaying) return;

        _bgmIntervalTimer -= Time.unscaledDeltaTime;
        if (_bgmIntervalTimer > 0f) return;

        PlayNextBgm();
    }

    // 지금 곡이 아닌 다른 곡을 무작위로 골라 재생한다
    private void PlayNextBgm()
    {
        _currentBgmIndex = PickNextBgmIndex();
        _isBgmPaused = false;
        _bgmIntervalTimer = _bgmInterval;

        _bgmSource.clip = _playableBgmClips[_currentBgmIndex];
        _bgmSource.volume = _bgmVolume;
        _bgmSource.Play();
    }

    // 현재 곡을 뺀 나머지 중에서 다음 곡의 인덱스를 무작위로 고른다 (곡이 하나뿐이면 그 곡을 다시 튼다)
    private int PickNextBgmIndex()
    {
        int count = _playableBgmClips.Count;
        if (count <= 1 || _currentBgmIndex < 0)
            return Random.Range(0, count);

        // 현재 곡을 뺀 개수 안에서 뽑은 뒤, 현재 곡 자리부터는 한 칸씩 밀어 현재 곡을 건너뛴다
        int index = Random.Range(0, count - 1);
        if (index >= _currentBgmIndex)
            index++;

        return index;
    }

    // 설정된 BGM 볼륨을 AudioSource에 반영한다
    private void ApplyBgmVolume()
    {
        if (_bgmSource == null) return;

        _bgmSource.volume = _bgmVolume;
    }

    // 재생 중인 반복 효과음을 대상 위치로 따라 옮기고, 대상이 사라졌으면 소리를 정리한다.
    // 소리를 내는 쪽이 파괴되면 멈춰 달라는 요청도 함께 사라지므로, 매니저가 스스로 끊어 줘야 소리가 남지 않는다.
    private void HandleLoopSfxFollow()
    {
        for (int i = _activeLoopSfx.Count - 1; i >= 0; i--)
        {
            LoopSfxHandle handle = _activeLoopSfx[i];

            if (handle.Target == null || handle.Source == null)
            {
                _activeLoopSfx.RemoveAt(i);
                ReleaseLoopSfx(handle);
                continue;
            }

            handle.Source.transform.position = handle.Target.position;
        }
    }

    // 재사용 목록에서 반복 효과음용 AudioSource를 꺼내고, 남은 것이 없으면 새로 만든다
    private AudioSource RentLoopSfxSource()
    {
        while (_idleLoopSfxSources.Count > 0)
        {
            int last = _idleLoopSfxSources.Count - 1;
            AudioSource pooled = _idleLoopSfxSources[last];
            _idleLoopSfxSources.RemoveAt(last);

            if (pooled != null)
                return pooled;
        }

        return CreateLoopSfxSource();
    }

    // 반복 효과음용 AudioSource를 매니저의 자식 오브젝트로 새로 만든다 (대상 위치에서 들리도록 3D로 재생한다)
    private AudioSource CreateLoopSfxSource()
    {
        var go = new GameObject("LoopSfxSource");
        go.transform.SetParent(transform, false);

        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 1f; // 거리에 따라 줄어드는 3D 재생
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = _loopSfxMinDistance;
        source.maxDistance = _loopSfxMaxDistance;
        return source;
    }

    // 반복 효과음을 멈추고 그 AudioSource를 재사용 목록에 넣는다 (손잡이는 이 시점부터 쓸 수 없다)
    private void ReleaseLoopSfx(LoopSfxHandle handle)
    {
        AudioSource source = handle.Source;
        handle.Release();

        if (source == null) return;

        source.Stop();
        source.clip = null;
        source.pitch = 1f; // 다음에 빌려 가는 소리가 앞서 쓴 재생 속도를 물려받지 않게 되돌린다
        _idleLoopSfxSources.Add(source);
    }

    // 넘겨받은 재생 속도를 AudioSource가 받아 주는 범위로 자른다
    private static float ClampLoopSfxPitch(float pitch)
    {
        return Mathf.Clamp(pitch, k_MinLoopSfxPitch, k_MaxLoopSfxPitch);
    }

    // 설정된 효과음 볼륨을 재생 중인 모든 반복 효과음에 반영한다
    private void ApplyLoopSfxVolume()
    {
        foreach (LoopSfxHandle handle in _activeLoopSfx)
            handle.ApplyVolume(_sfxVolume);
    }

    // 재생 중인 반복 효과음 하나를 가리키는 손잡이. AudioManager가 만들어 돌려주며, 멈추거나 볼륨을 바꿀 때 이것을 다시 넘긴다.
    public class LoopSfxHandle
    {
        // 이 소리를 내고 있는 AudioSource. 멈춘 뒤에는 null이 된다.
        internal AudioSource Source { get; private set; }

        // 소리가 따라다닐 대상. 대상이 파괴되면 매니저가 소리를 스스로 정리한다.
        internal Transform Target { get; private set; }

        // 이 소리에만 적용할 볼륨 배율. 매니저의 효과음 볼륨에 곱해 쓴다.
        internal float VolumeScale { get; set; }

        // 아직 재생 중인지 여부. 한 번 멈춘 손잡이는 다시 쓸 수 없다.
        public bool IsPlaying => Source != null;

        // 매니저가 준비한 AudioSource와 따라다닐 대상, 볼륨 배율을 받아 손잡이를 만든다
        internal LoopSfxHandle(AudioSource source, Transform target, float volumeScale)
        {
            Source = source;
            Target = target;
            VolumeScale = volumeScale;
        }

        // 매니저의 효과음 볼륨에 자신의 배율을 곱해 AudioSource에 반영한다
        internal void ApplyVolume(float sfxVolume)
        {
            if (Source == null) return;

            Source.volume = sfxVolume * VolumeScale;
        }

        // 재생이 끝나 AudioSource를 놓아준다 (이후 IsPlaying이 false가 된다)
        internal void Release()
        {
            Source = null;
            Target = null;
        }
    }
}
