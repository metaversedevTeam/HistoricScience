using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 메인 메뉴 배경 격자의 각 칸에 아이템 아이콘을 숨겨 두고, 일정 주기로 대각선 물결이 지나가며 아이콘을 옅게 드러내는 연출.
// 게임 일시정지와 무관하게 돌도록 실제 경과 시간을 쓴다.
public class MainMenuGridIconField : MonoBehaviour
{
    // 아이콘을 뽑아 올 아이템 목록
    [SerializeField] private ItemDataList _itemDataList;

    // 격자 한 칸의 크기(캔버스 단위). 배경 격자 선 간격과 같아야 한다.
    [SerializeField] private float _cellSize = 120f;

    // 가로·세로 칸 수
    [SerializeField] private Vector2Int _gridCount = new Vector2Int(16, 9);

    // 칸 안에 그릴 아이콘의 크기(캔버스 단위)
    [SerializeField] private float _iconSize = 64f;

    // 아이콘의 기본 색(투명도는 _baseAlpha가 덮어쓴다)
    [SerializeField] private Color _iconColor = Color.white;

    // 물결 중심에서 아이콘이 가장 진하게 보일 때의 투명도(평소에는 0으로 숨는다)
    [FormerlySerializedAs("_baseAlpha")]
    [SerializeField, Range(0f, 1f)] private float _peakAlpha = 0.06f;

    // 물결이 왼쪽 위에서 오른쪽 아래까지 한 번 지나가는 데 걸리는 시간(초)
    [SerializeField, Min(0.01f)] private float _waveDuration = 2.5f;

    // 물결이 지나간 뒤 다음 물결까지 쉬는 시간(초)
    [SerializeField, Min(0f)] private float _waveInterval = 4f;

    // 물결 띠의 두께(대각선 전체 길이를 1로 본 비율)
    [SerializeField, Range(0.01f, 1f)] private float _waveWidth = 0.18f;

    // 화면이 켜진 뒤 첫 물결을 시작하기까지 기다리는 시간(초)
    [SerializeField, Min(0f)] private float _startDelay = 1.5f;

    // 생성한 아이콘 이미지들
    private readonly List<Image> _icons = new();

    // 각 아이콘의 대각선 위치(왼쪽 위 0 ~ 오른쪽 아래 1)
    private readonly List<float> _diagonals = new();

    // 화면이 켜진 뒤 지난 시간(초)
    private float _elapsed;

    private void Awake()
    {
        BuildIcons();
    }

    private void OnEnable()
    {
        _elapsed = 0f;
        ApplyAlpha(float.NegativeInfinity);
    }

    private void Update()
    {
        HandleWave();
    }

    // 격자 칸마다 아이콘 이미지를 만들고, 아이템 아이콘을 섞어서 겹치지 않게 배치한다.
    private void BuildIcons()
    {
        List<Sprite> sprites = CollectSprites();
        if (sprites.Count == 0)
            return;

        Shuffle(sprites);
        int columns = Mathf.Max(1, _gridCount.x);
        int rows = Mathf.Max(1, _gridCount.y);
        int diagonalMax = Mathf.Max(1, columns + rows - 2);
        Vector2 origin = new Vector2(-(columns - 1) * 0.5f * _cellSize, (rows - 1) * 0.5f * _cellSize);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int index = row * columns + column;
                Vector2 position = origin + new Vector2(column * _cellSize, -row * _cellSize);
                _icons.Add(CreateIcon(sprites[index % sprites.Count], position));
                _diagonals.Add((float)(column + row) / diagonalMax);
            }
        }
    }

    // 아이템 목록에서 아이콘이 있는 스프라이트만 모은다.
    private List<Sprite> CollectSprites()
    {
        var sprites = new List<Sprite>();
        if (_itemDataList == null)
            return sprites;

        foreach (var item in _itemDataList.Items)
        {
            if (item != null && item.IconSprite != null)
                sprites.Add(item.IconSprite);
        }
        return sprites;
    }

    // 한 칸에 놓일 아이콘 이미지 오브젝트를 만든다.
    private Image CreateIcon(Sprite sprite, Vector2 position)
    {
        var go = new GameObject(sprite.name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(_iconSize, _iconSize);
        rect.anchoredPosition = position;

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    // 목록 순서를 무작위로 섞는다.
    private static void Shuffle(List<Sprite> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // 지난 시간을 한 주기(물결+쉬기) 안의 물결 위치로 바꿔 아이콘 투명도에 반영한다.
    private void HandleWave()
    {
        _elapsed += Time.unscaledDeltaTime;

        float local = _elapsed - _startDelay;
        if (local < 0f)
            return;

        float cycle = local % (_waveDuration + _waveInterval);
        if (cycle > _waveDuration)
        {
            ApplyAlpha(float.NegativeInfinity);
            return;
        }

        float t = cycle / _waveDuration;
        float front = Mathf.Lerp(-_waveWidth, 1f + _waveWidth, t);
        ApplyAlpha(front);
    }

    // 물결 중심 위치를 기준으로 가까운 아이콘일수록 진하게 드러낸다.
    private void ApplyAlpha(float waveCenter)
    {
        for (int i = 0; i < _icons.Count; i++)
        {
            float distance = Mathf.Abs(_diagonals[i] - waveCenter) / _waveWidth;
            float reveal = Mathf.Clamp01(1f - distance);
            reveal = reveal * reveal * (3f - 2f * reveal);

            Color color = _iconColor;
            color.a = _peakAlpha * reveal;
            _icons[i].color = color;
        }
    }
}
