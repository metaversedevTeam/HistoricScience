using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;

// 디버그 UI 치트가 켜져 있는 동안 화면 왼쪽 위에 플레이어의 X·Z 좌표, 서 있는 바이옴, 프레임, 배치 수, GC 지연 그래프를 표시하는 오버레이.
// 프리팹 없이 실행 중에 자기 캔버스를 만들며, 측정값을 흐리지 않도록 문자열 버퍼와 그래프 픽셀 버퍼를 재사용해 표시 중에 GC 할당을 만들지 않는다.
// 배치 수와 GC 지연은 프로파일러 기록기(ProfilerRecorder)로 읽으므로 에디터와 Development 빌드에서만 값이 나온다.
public class DebugOverlayUI : MonoBehaviour
{
    // 다른 모든 UI보다 위에 그리도록 쓰는 캔버스 정렬 순서
    private const int k_SortingOrder = 32000;

    // 글자 정보를 다시 쓰는 간격(초). 매 프레임 바꾸면 숫자가 읽히지 않을 만큼 깜박인다.
    private const float k_TextRefreshInterval = 0.25f;

    // 플레이어 시민을 아직 못 찾았을 때 다시 찾아보는 간격(초)
    private const float k_PlayerSearchInterval = 1f;

    // 그래프 가로 칸 수. 한 칸이 그래프 한 픽셀 열이다.
    private const int k_GraphColumns = 240;

    // 그래프 세로 픽셀 수
    private const int k_GraphHeight = 64;

    // 그래프 한 화면이 보여 주는 시간(초). 한 칸은 이 시간을 칸 수로 나눈 만큼을 담는다.
    private const float k_GraphDuration = 12f;

    // 그래프 세로축의 최소 상한(ms). GC가 거의 없어도 작은 값이 꼭대기까지 차오르지 않게 한다.
    private const float k_MinGraphScaleMs = 1f;

    // 오버레이 안쪽 여백과 글자 영역 높이(캔버스 단위)
    private const float k_Padding = 8f;
    private const float k_TextHeight = 118f;
    private const float k_FontSize = 18f;

    // 프로파일러에서 GC 수집 시간을 기록하는 마커 이름
    private const string k_GcMarkerName = "GC.Collect";

    // 프로파일러에서 렌더링 배치 수를 기록하는 카운터 이름
    private const string k_BatchesCounterName = "Batches Count";

    private static readonly Color32 s_GraphBackground = new Color32(0x10, 0x0C, 0x27, 0xFF);
    private static readonly Color32 s_GraphGrid = new Color32(0x33, 0x41, 0x55, 0xFF);
    private static readonly Color32 s_GraphBar = new Color32(0x00, 0xE5, 0xFF, 0xFF);
    private static readonly Color32 s_GraphSpike = new Color32(0xFF, 0x5A, 0x5A, 0xFF);

    private TextMeshProUGUI _text;
    private Texture2D _graphTexture;
    private Color32[] _graphPixels;

    // 칸마다 그 시간 동안 가장 컸던 한 프레임의 GC 지연(ms). _graphHead가 가장 오래된 칸이다.
    private readonly float[] _gcSamples = new float[k_GraphColumns];
    private int _graphHead;

    // 지금 채우는 칸의 경과 시간과 그 안의 최대 GC 지연(ms)
    private float _bucketElapsed;
    private float _bucketMaxMs;

    // 프레임 수를 세는 구간의 경과 시간과 프레임 수, 마지막으로 계산한 FPS
    private float _fpsElapsed;
    private int _fpsFrames;
    private float _fps;

    private float _textRefreshTimer;
    private float _nextPlayerSearchTime;
    private Transform _player;

    private ProfilerRecorder _gcRecorder;
    private ProfilerRecorder _batchesRecorder;

    // 할당 없이 글자를 조립하려고 재사용하는 버퍼
    private readonly StringBuilder _builder = new(256);

    private void Awake()
    {
        BuildCanvas();
        StartRecorders();
    }

    private void OnDestroy()
    {
        _gcRecorder.Dispose();
        _batchesRecorder.Dispose();

        if (_graphTexture != null)
            Destroy(_graphTexture);
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        HandleFrameCounter(deltaTime);
        HandleGcGraph(deltaTime);
        HandleTextRefresh(deltaTime);
    }

    // 화면 왼쪽 위에 반투명 배경, 글자, 그래프 이미지로 이루어진 오버레이 캔버스를 만든다.
    private void BuildCanvas()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = k_SortingOrder;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        float panelWidth = k_GraphColumns + k_Padding * 2f;
        float panelHeight = k_TextHeight + k_GraphHeight + k_Padding * 3f;

        RectTransform panel = CreateChild("Panel", transform, new Vector2(k_Padding, -k_Padding), new Vector2(panelWidth, panelHeight));
        var panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.6f);
        panelImage.raycastTarget = false;

        RectTransform textRect = CreateChild("Text", panel, new Vector2(k_Padding, -k_Padding), new Vector2(k_GraphColumns, k_TextHeight));
        _text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        _text.fontSize = k_FontSize;
        _text.color = Color.white;
        _text.textWrappingMode = TextWrappingModes.NoWrap;
        _text.raycastTarget = false;

        RectTransform graphRect = CreateChild("GcGraph", panel, new Vector2(k_Padding, -(k_Padding * 2f + k_TextHeight)), new Vector2(k_GraphColumns, k_GraphHeight));
        var graphImage = graphRect.gameObject.AddComponent<RawImage>();
        graphImage.raycastTarget = false;

        _graphTexture = new Texture2D(k_GraphColumns, k_GraphHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        _graphPixels = new Color32[k_GraphColumns * k_GraphHeight];
        graphImage.texture = _graphTexture;

        RedrawGraph();
    }

    // 부모의 왼쪽 위를 기준으로 지정한 위치와 크기에 놓이는 자식 RectTransform을 만든다.
    private static RectTransform CreateChild(string childName, Transform parent, Vector2 anchoredPosition, Vector2 size)
    {
        var child = new GameObject(childName, typeof(RectTransform));
        var rect = (RectTransform)child.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return rect;
    }

    // GC 수집 시간 마커와 배치 수 카운터를 기록하기 시작한다. 찾지 못한 기록기는 유효하지 않은 채로 남는다.
    private void StartRecorders()
    {
        _batchesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, k_BatchesCounterName);

        // GC 마커는 범주가 버전마다 달라질 수 있어, 범주를 지정하지 않고 이름으로 찾는다.
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        foreach (ProfilerRecorderHandle handle in handles)
        {
            if (ProfilerRecorderHandle.GetDescription(handle).Name != k_GcMarkerName) continue;

            _gcRecorder = new ProfilerRecorder(handle);
            _gcRecorder.Start();
            break;
        }
    }

    // 일정 구간마다 그동안 지난 프레임 수로 FPS를 계산한다.
    private void HandleFrameCounter(float deltaTime)
    {
        _fpsElapsed += deltaTime;
        _fpsFrames++;

        if (_fpsElapsed < k_TextRefreshInterval) return;

        _fps = _fpsFrames / _fpsElapsed;
        _fpsElapsed = 0f;
        _fpsFrames = 0;
    }

    // 지난 프레임의 GC 지연을 지금 칸에 모으고, 칸 하나의 시간이 지나면 그래프를 한 칸 밀어 다시 그린다.
    private void HandleGcGraph(float deltaTime)
    {
        if (_gcRecorder.Valid)
            _bucketMaxMs = Mathf.Max(_bucketMaxMs, _gcRecorder.LastValue * 1e-6f);

        _bucketElapsed += deltaTime;

        float bucketDuration = k_GraphDuration / k_GraphColumns;
        if (_bucketElapsed < bucketDuration) return;

        // 프레임이 길어 여러 칸이 한꺼번에 지났으면 그 칸들도 채워 시간축을 맞춘다.
        int columnsPassed = Mathf.Min(Mathf.FloorToInt(_bucketElapsed / bucketDuration), k_GraphColumns);
        for (int i = 0; i < columnsPassed; i++)
        {
            _gcSamples[_graphHead] = i == 0 ? _bucketMaxMs : 0f;
            _graphHead = (_graphHead + 1) % k_GraphColumns;
        }

        _bucketElapsed -= columnsPassed * bucketDuration;
        _bucketMaxMs = 0f;

        RedrawGraph();
    }

    // 지금 그래프에 담긴 칸들 중 가장 큰 GC 지연에 맞춰 세로축을 잡는다. (1ms 단위로 올림)
    private float GetGraphScaleMs()
    {
        float max = k_MinGraphScaleMs;
        foreach (float sample in _gcSamples)
            max = Mathf.Max(max, sample);

        return Mathf.Ceil(max);
    }

    // 오래된 칸을 왼쪽, 최근 칸을 오른쪽에 두고 칸마다 GC 지연만큼 막대를 그려 텍스처에 올린다.
    private void RedrawGraph()
    {
        float scaleMs = GetGraphScaleMs();
        int halfLine = k_GraphHeight / 2;

        for (int x = 0; x < k_GraphColumns; x++)
        {
            float sample = _gcSamples[(_graphHead + x) % k_GraphColumns];
            int barHeight = sample > 0f ? Mathf.Clamp(Mathf.CeilToInt(sample / scaleMs * k_GraphHeight), 1, k_GraphHeight) : 0;

            // 세로축 상한의 절반을 넘는 지연은 눈에 띄도록 다른 색으로 칠한다.
            Color32 barColor = barHeight > halfLine ? s_GraphSpike : s_GraphBar;

            for (int y = 0; y < k_GraphHeight; y++)
            {
                Color32 color = y < barHeight ? barColor : (y == halfLine ? s_GraphGrid : s_GraphBackground);
                _graphPixels[y * k_GraphColumns + x] = color;
            }
        }

        _graphTexture.SetPixels32(_graphPixels);
        _graphTexture.Apply(false);
    }

    // 일정 간격마다 좌표·바이옴·프레임·배치 수·그래프 축 정보를 글자로 다시 쓴다.
    private void HandleTextRefresh(float deltaTime)
    {
        _textRefreshTimer -= deltaTime;
        if (_textRefreshTimer > 0f) return;
        _textRefreshTimer = k_TextRefreshInterval;

        Transform player = FindPlayer();
        _builder.Clear();

        if (player != null)
        {
            Vector3 position = player.position;
            _builder.Append("X ");
            AppendFixed(_builder, position.x, 1);
            _builder.Append("   Z ");
            AppendFixed(_builder, position.z, 1);
        }
        else
        {
            _builder.Append("X -   Z -");
        }

        _builder.Append("\nBiome ");
        MapBiome biome = player != null ? FindBiome(player.position) : null;
        _builder.Append(biome != null ? biome.Name : "-");

        _builder.Append("\nFPS ");
        AppendFixed(_builder, _fps, 1);
        _builder.Append(" (");
        AppendFixed(_builder, _fps > 0f ? 1000f / _fps : 0f, 1);
        _builder.Append(" ms)");

        _builder.Append("\nBatches ");
        if (_batchesRecorder.Valid)
            AppendFixed(_builder, _batchesRecorder.LastValue, 0);
        else
            _builder.Append("N/A");

        _builder.Append("\nGC delay  max ");
        if (_gcRecorder.Valid)
        {
            AppendFixed(_builder, GetGraphScaleMs(), 0);
            _builder.Append(" ms / ");
            AppendFixed(_builder, k_GraphDuration, 0);
            _builder.Append(" s");
        }
        else
        {
            _builder.Append("N/A");
        }

        _text.SetText(_builder);
    }

    // 카메라가 따라가는 플레이어 시민을 찾는다. 아직 없으면 일정 간격으로만 다시 찾아본다.
    private Transform FindPlayer()
    {
        if (_player != null) return _player;
        if (Time.unscaledTime < _nextPlayerSearchTime) return null;
        _nextPlayerSearchTime = Time.unscaledTime + k_PlayerSearchInterval;

        CameraController cameraController = FindFirstObjectByType<CameraController>();
        if (cameraController != null && cameraController.FollowTarget != null)
        {
            _player = cameraController.FollowTarget;
            return _player;
        }

        Citizen citizen = FindFirstObjectByType<Citizen>();
        _player = citizen != null ? citizen.transform : null;
        return _player;
    }

    // 월드 위치에 해당하는 바이옴을 맵 데이터에서 찾는다. 맵이 없는 씬이면 null을 반환한다.
    private static MapBiome FindBiome(Vector3 worldPosition)
    {
        MapSaveUtil mapSaveUtil = MapSaveUtil.Instance;
        if (mapSaveUtil == null) return null;

        MapData mapData = mapSaveUtil.CurrentMapData;
        if (mapData == null) return null;

        return mapData.GetBiome(mapSaveUtil.WorldToMapPosition(worldPosition));
    }

    // 실수를 소수점 아래 지정한 자릿수까지 문자열 할당 없이 버퍼에 이어 붙인다.
    private static void AppendFixed(StringBuilder builder, double value, int decimals)
    {
        if (value < 0)
        {
            builder.Append('-');
            value = -value;
        }

        long scale = 1;
        for (int i = 0; i < decimals; i++)
            scale *= 10;

        long scaled = (long)System.Math.Round(value * scale);
        long integerPart = scaled / scale;
        long fractionPart = scaled % scale;

        AppendDigits(builder, integerPart);

        if (decimals <= 0) return;

        builder.Append('.');
        for (long digit = scale / 10; digit > 0; digit /= 10)
            builder.Append((char)('0' + fractionPart / digit % 10));
    }

    // 0 이상의 정수를 자릿수마다 문자로 이어 붙인다. (StringBuilder.Append(long)은 Mono에서 문자열을 할당한다)
    private static void AppendDigits(StringBuilder builder, long value)
    {
        long digit = 1;
        while (digit <= value / 10)
            digit *= 10;

        for (; digit > 0; digit /= 10)
            builder.Append((char)('0' + value / digit % 10));
    }
}
