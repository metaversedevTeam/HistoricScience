using UnityEngine;

// 여러 클래스가 공용으로 쓰는 효과음을 용도별로 모아 두는 스크립터블 오브젝트.
// 씬마다 배치되는 AudioManager에 클립을 일일이 다시 연결하지 않도록, Resources/Audio에 이 에셋 하나만 두고 실행 중에 읽는다.
// 연결하지 않은 칸은 null이고, null 클립은 재생 쪽에서 무시하므로 비워 둬도 안전하다.
[CreateAssetMenu(fileName = "오디오 프리셋", menuName = "스크립터블 오브젝트/오디오/오디오 프리셋", order = int.MinValue)]
public class AudioPreset : ScriptableObject
{
    // Resources 폴더 하위의 이 에셋 경로 (확장자 제외)
    private const string ResourcePath = "Audio/오디오 프리셋";

    // 버튼을 눌렀을 때
    [SerializeField] private AudioClip _buttonClick;

    // 팝업·창이 열릴 때
    [SerializeField] private AudioClip _popupOpen;

    // 팝업·창이 닫힐 때
    [SerializeField] private AudioClip _popupClose;

    // 확인·수락을 눌렀을 때
    [SerializeField] private AudioClip _confirm;

    // 취소·뒤로가기를 눌렀을 때
    [SerializeField] private AudioClip _cancel;

    // 할 수 없는 동작을 시도했을 때 (자원 부족 등)
    [SerializeField] private AudioClip _error;

    // 한 번 읽어 둔 에셋
    private static AudioPreset _instance;

    // Resources에서 읽어 온 프리셋. 에셋이 없으면 모든 칸이 비어 있는 임시 프리셋을 대신 만들어 주므로 null이 아니다.
    public static AudioPreset Instance
    {
        get
        {
            if (_instance != null) return _instance;

            _instance = Resources.Load<AudioPreset>(ResourcePath);
            if (_instance == null)
            {
                Debug.LogWarning($"[{nameof(AudioPreset)}] Resources/{ResourcePath} 에셋을 찾지 못해 빈 프리셋을 사용한다.");
                _instance = CreateInstance<AudioPreset>();
            }

            return _instance;
        }
    }

    public AudioClip ButtonClick => _buttonClick;

    public AudioClip PopupOpen => _popupOpen;

    public AudioClip PopupClose => _popupClose;

    public AudioClip Confirm => _confirm;

    public AudioClip Cancel => _cancel;

    public AudioClip Error => _error;
}
