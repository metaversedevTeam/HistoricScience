using System;
using System.Collections.Generic;
using UnityEngine;

// 튜토리얼 UI가 쓰는 스프라이트를 실행 중에 그려 내고 캐싱하는 정적 공장.
// 튜토리얼을 코드 폴더 하나만 지워서 걷어낼 수 있게 하려고, 피그마 UI 키트의 도형을 이미지 에셋 없이 직접 그린다.
public static class TutorialSpriteLibrary
{
    // 이름으로 찾아 쓰는 스프라이트 캐시
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    // 정리할 때 파괴해야 하는, 이 공장이 만든 텍스처 목록
    private static readonly List<Texture2D> Textures = new List<Texture2D>();

    // 아무 색으로나 칠해 쓰는 단색 사각형
    public static Sprite Solid => GetOrCreate("solid", () => Bake(4, (x, y, size) => 1f, Vector4.zero));

    // 대화창 본체용 둥근 사각형 채움
    public static Sprite PanelFill => GetOrCreate("panelFill", () => BakeRoundedFill(TutorialTheme.PanelRadius));

    // 대화창 본체용 둥근 사각형 테두리
    public static Sprite PanelOutline => GetOrCreate("panelOutline", () => BakeRoundedOutline(TutorialTheme.PanelRadius, 2f));

    // 배지·버튼용 둥근 사각형 채움
    public static Sprite ChipFill => GetOrCreate("chipFill", () => BakeRoundedFill(TutorialTheme.ChipRadius));

    // 배지·버튼용 둥근 사각형 테두리
    public static Sprite ChipOutline => GetOrCreate("chipOutline", () => BakeRoundedOutline(TutorialTheme.ChipRadius, 2f));

    // 아바타 자리에 쓰는 꽉 찬 원
    public static Sprite Circle => GetOrCreate("circle", () => Bake(128, CircleCoverage, Vector4.zero));

    // 아바타 테두리에 쓰는 원 외곽선
    public static Sprite CircleOutline => GetOrCreate("circleOutline", () => Bake(128, (x, y, size) => RingCoverage(x, y, size, 0.46f, 0.045f), Vector4.zero));

    // 강조 대상을 감싸는 청록 링 (피그마 highlight-ring-outer). 바깥으로 은은한 발광이 번진다.
    public static Sprite FocusRing => GetOrCreate("focusRing", BakeFocusRing);

    // 강조 링 안쪽의 호박색 점선 링 (피그마 highlight-ring-inner)
    public static Sprite FocusRingDashed => GetOrCreate("focusRingDashed", () => Bake(256, (x, y, size) => DashedRingCoverage(x, y, size, 0.34f, 0.014f, 16), Vector4.zero));

    // 강조 대상만 남기고 화면을 덮는 스포트라이트 마스크 (피그마 spotlight-dim-mask + spotlight-cutout).
    // 가운데는 완전히 뚫려 있고 가장자리로 갈수록 불투명해져, 마스크 바깥의 사각형 가림막과 자연스럽게 이어진다.
    public static Sprite SpotlightHole => GetOrCreate("spotlightHole", () => Bake(256, SpotlightHoleCoverage, Vector4.zero));

    // 강조 대상을 가리키는 아래쪽 화살표 (피그마 glowing-arrow-down)
    public static Sprite ArrowDown => GetOrCreate("arrowDown", BakeArrowDown);

    // 대화가 더 남아 있음을 알리는 위쪽 삼각형 (피그마 next-indicator)
    public static Sprite TriangleUp => GetOrCreate("triangleUp", BakeTriangleUp);

    // 만들어 둔 스프라이트와 텍스처를 모두 파괴한다. 튜토리얼이 끝날 때 호출한다.
    public static void Release()
    {
        foreach (Sprite sprite in Cache.Values)
        {
            if (sprite != null)
                UnityEngine.Object.Destroy(sprite);
        }

        foreach (Texture2D texture in Textures)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }

        Cache.Clear();
        Textures.Clear();
    }

    // 캐시에 있으면 그대로 주고, 없으면 만들어 등록한 뒤 준다.
    private static Sprite GetOrCreate(string key, Func<Sprite> factory)
    {
        if (Cache.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        Sprite created = factory();
        Cache[key] = created;
        return created;
    }

    // 지정한 반지름의 둥근 사각형 채움 스프라이트를 9슬라이스로 만든다.
    private static Sprite BakeRoundedFill(int radius)
    {
        int inset = radius + 2;
        int size = inset * 2 + 4;
        return Bake(size, (x, y, s) => Mathf.Clamp01(0.5f - RoundedBoxDistance(x, y, s, radius)), new Vector4(inset, inset, inset, inset));
    }

    // 지정한 반지름·두께의 둥근 사각형 테두리 스프라이트를 9슬라이스로 만든다.
    private static Sprite BakeRoundedOutline(int radius, float thickness)
    {
        int inset = radius + 2;
        int size = inset * 2 + 4;
        return Bake(size, (x, y, s) =>
        {
            float distance = RoundedBoxDistance(x, y, s, radius);
            float band = Mathf.Abs(distance + thickness * 0.5f) - thickness * 0.5f;
            return Mathf.Clamp01(0.5f - band);
        }, new Vector4(inset, inset, inset, inset));
    }

    // 픽셀 중심에서 둥근 사각형 경계까지의 부호 있는 거리를 구한다. 안쪽이 음수다.
    private static float RoundedBoxDistance(int x, int y, int size, float radius)
    {
        float half = size * 0.5f;
        float px = x + 0.5f - half;
        float py = y + 0.5f - half;
        float bound = half - radius;

        float qx = Mathf.Abs(px) - bound;
        float qy = Mathf.Abs(py) - bound;
        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;

        return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
    }

    // 텍스처 전체에 내접하는 꽉 찬 원의 픽셀 덮임 정도를 구한다.
    private static float CircleCoverage(int x, int y, int size)
    {
        float half = size * 0.5f;
        float distance = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude;
        return Mathf.Clamp01(0.5f - (distance - (half - 1f)));
    }

    // 정규화 반지름과 두께로 정해지는 원 띠의 픽셀 덮임 정도를 구한다.
    private static float RingCoverage(int x, int y, int size, float normalizedRadius, float normalizedThickness)
    {
        float half = size * 0.5f;
        float distance = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude;
        float band = Mathf.Abs(distance - normalizedRadius * size) - normalizedThickness * size * 0.5f;
        return Mathf.Clamp01(0.5f - band);
    }

    // 원 띠를 각도로 잘라 점선으로 만든 덮임 정도를 구한다.
    private static float DashedRingCoverage(int x, int y, int size, float normalizedRadius, float normalizedThickness, int dashCount)
    {
        float coverage = RingCoverage(x, y, size, normalizedRadius, normalizedThickness);
        if (coverage <= 0f) return 0f;

        float half = size * 0.5f;
        float angle = Mathf.Atan2(y + 0.5f - half, x + 0.5f - half) + Mathf.PI;
        int segment = Mathf.FloorToInt(angle / (Mathf.PI * 2f) * dashCount * 2f);

        return segment % 2 == 0 ? coverage : 0f;
    }

    // 가운데가 완전히 뚫리고 가장자리로 갈수록 불투명해지는 스포트라이트 마스크의 덮임 정도를 구한다.
    private static float SpotlightHoleCoverage(int x, int y, int size)
    {
        float half = size * 0.5f;
        float distance = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude;
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half * 0.86f, half, distance));
    }

    // 청록 링 본체에 바깥 발광을 더한 강조 링 스프라이트를 만든다.
    private static Sprite BakeFocusRing()
    {
        const int Size = 256;

        float[] core = BakeCoverage(Size, (x, y, s) => RingCoverage(x, y, s, 0.42f, 0.028f));
        float[] glow = Blur(core, Size, 4);

        Color[] pixels = new Color[Size * Size];
        for (int i = 0; i < pixels.Length; i++)
        {
            float alpha = Mathf.Clamp01(core[i] + glow[i] * 0.85f);
            pixels[i] = new Color(1f, 1f, 1f, alpha);
        }

        return CreateSprite(Size, Size, pixels, Vector4.zero);
    }

    // 축을 따라 뻗은 대와 삼각 머리로 이루어진 아래쪽 화살표에 발광을 더해 만든다.
    private static Sprite BakeArrowDown()
    {
        const int Size = 128;

        float[] core = BakeCoverage(Size, ArrowDownCoverage);
        float[] glow = Blur(core, Size, 3);

        Color[] pixels = new Color[Size * Size];
        for (int i = 0; i < pixels.Length; i++)
        {
            float alpha = Mathf.Clamp01(core[i] + glow[i] * 0.7f);
            pixels[i] = new Color(1f, 1f, 1f, alpha);
        }

        return CreateSprite(Size, Size, pixels, Vector4.zero);
    }

    // 아래를 가리키는 화살표 모양의 픽셀 덮임 정도를 구한다. (텍스처 좌표는 아래가 y=0이다)
    private static float ArrowDownCoverage(int x, int y, int size)
    {
        return Supersample(x, y, (px, py) =>
        {
            float u = px / size;
            float v = py / size;

            // 머리: 아래 꼭짓점 하나와 위쪽 두 꼭짓점으로 이루어진 삼각형
            if (InsideTriangle(u, v, 0.5f, 0.06f, 0.14f, 0.48f, 0.86f, 0.48f))
                return true;

            // 대: 머리 위로 이어지는 세로 막대
            return u >= 0.38f && u <= 0.62f && v >= 0.46f && v <= 0.92f;
        });
    }

    // 위를 가리키는 작은 삼각형 스프라이트를 만든다.
    private static Sprite BakeTriangleUp()
    {
        const int Width = 32;
        const int Height = 22;

        Color[] pixels = new Color[Width * Height];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                float coverage = Supersample(x, y, (px, py) =>
                {
                    float u = px / Width;
                    float v = py / Height;
                    return InsideTriangle(u, v, 0.5f, 0.95f, 0.04f, 0.08f, 0.96f, 0.08f);
                });

                pixels[y * Width + x] = new Color(1f, 1f, 1f, coverage);
            }
        }

        return CreateSprite(Width, Height, pixels, Vector4.zero);
    }

    // 한 픽셀을 3x3으로 나눠 도형 안에 든 비율을 세, 계단 현상을 줄인 덮임 정도를 구한다.
    private static float Supersample(int x, int y, Func<float, float, bool> isInside)
    {
        const int Steps = 3;
        int hits = 0;

        for (int sy = 0; sy < Steps; sy++)
        {
            for (int sx = 0; sx < Steps; sx++)
            {
                float px = x + (sx + 0.5f) / Steps;
                float py = y + (sy + 0.5f) / Steps;
                if (isInside(px, py)) hits++;
            }
        }

        return hits / (float)(Steps * Steps);
    }

    // 점이 세 꼭짓점으로 이루어진 삼각형 안에 있는지 판정한다.
    private static bool InsideTriangle(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
    {
        float d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
        float d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
        float d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);

        bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;

        return !(hasNegative && hasPositive);
    }

    // 정사각 텍스처의 각 픽셀 덮임 정도를 계산해 배열로 만든다.
    private static float[] BakeCoverage(int size, Func<int, int, int, float> coverage)
    {
        float[] values = new float[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
                values[y * size + x] = Mathf.Clamp01(coverage(x, y, size));
        }

        return values;
    }

    // 덮임 정도 배열을 상자 흐림으로 여러 번 뭉개, 발광에 쓸 부드러운 값을 만든다.
    private static float[] Blur(float[] source, int size, int iterations)
    {
        float[] current = (float[])source.Clone();
        float[] next = new float[source.Length];

        for (int pass = 0; pass < iterations; pass++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float sum = 0f;
                    int count = 0;

                    for (int oy = -2; oy <= 2; oy++)
                    {
                        int sy = y + oy;
                        if (sy < 0 || sy >= size) continue;

                        for (int ox = -2; ox <= 2; ox++)
                        {
                            int sx = x + ox;
                            if (sx < 0 || sx >= size) continue;

                            sum += current[sy * size + sx];
                            count++;
                        }
                    }

                    next[y * size + x] = count > 0 ? sum / count : 0f;
                }
            }

            (current, next) = (next, current);
        }

        return current;
    }

    // 덮임 정도만 계산해 흰색 스프라이트 한 장을 만든다. border를 주면 9슬라이스로 늘어난다.
    private static Sprite Bake(int size, Func<int, int, int, float> coverage, Vector4 border)
    {
        float[] values = BakeCoverage(size, coverage);

        Color[] pixels = new Color[values.Length];
        for (int i = 0; i < values.Length; i++)
            pixels[i] = new Color(1f, 1f, 1f, values[i]);

        return CreateSprite(size, size, pixels, border);
    }

    // 픽셀 배열로 텍스처와 스프라이트를 만들고, 나중에 정리할 수 있도록 텍스처를 등록한다.
    private static Sprite CreateSprite(int width, int height, Color[] pixels, Vector4 border)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "TutorialGeneratedTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };

        texture.SetPixels(pixels);
        texture.Apply();
        Textures.Add(texture);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            border);

        sprite.name = "TutorialGeneratedSprite";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
