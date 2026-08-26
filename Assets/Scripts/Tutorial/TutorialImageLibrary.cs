using UnityEngine;

// 튜토리얼이 대사창에 띄우는 그림을 모아 두는 스크립터블 오브젝트.
// 튜토리얼 UI는 코드로 만들어져 인스펙터로 그림을 연결할 곳이 없으므로, Resources/Tutorial에 이 에셋 하나만 두고 실행 중에 읽는다.
// 그림 자체는 원래 있던 자리(Art/Sprites/...)를 그대로 참조하므로, 원본을 바꾸면 튜토리얼에도 그대로 반영된다.
[CreateAssetMenu(fileName = "튜토리얼 이미지", menuName = "스크립터블 오브젝트/튜토리얼/튜토리얼 이미지", order = int.MinValue)]
public class TutorialImageLibrary : ScriptableObject
{
    // Resources 폴더 하위의 이 에셋 경로 (확장자 제외)
    private const string ResourcePath = "Tutorial/튜토리얼 이미지";

    // 채집 안내에서 "이렇게 생긴 것을 캐라"고 보여 줄 돌 자원 소스의 모습
    [SerializeField] private Sprite _stoneSource;

    // 대사창 왼쪽에 띄울 안내자 얼굴. 비워 두면 이름 첫 글자로 된 자리표시자를 그린다.
    [SerializeField] private Sprite _guideAvatar;

    // 한 번 읽어 둔 에셋
    private static TutorialImageLibrary _instance;

    // Resources에서 읽어 둔 그림 목록. 에셋이 없으면 null이다.
    public static TutorialImageLibrary Instance =>
        _instance != null ? _instance : _instance = Resources.Load<TutorialImageLibrary>(ResourcePath);

    // 돌 자원 소스의 모습. 등록되지 않았으면 null이다.
    public static Sprite StoneSource => Instance != null ? Instance._stoneSource : null;

    // 안내자 얼굴. 등록되지 않았으면 null이다.
    public static Sprite GuideAvatar => Instance != null ? Instance._guideAvatar : null;
}
