using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// 도감 UI가 쓰는 둥근 사각형·아이콘 스프라이트를 SDF로 그려 PNG 에셋으로 굽는 에디터 유틸리티.
// 외부 아트 의존 없이 프로젝트 안에서 UI 스프라이트를 재현하기 위한 도구다.
public static class UIProceduralSpriteFactory
{
    public const string OutputFolder = "Assets/Art/Sprites/UI/Generated";

    // 생성할 둥근 사각형 반지름 목록 (배지/게이지, 상태바, 아이콘 박스, 카드, 패널, 알약형 순)
    private static readonly int[] Radii = { 6, 8, 10, 12, 16, 20 };

    private const float StrokeWidth = 2f;
    private const int IconSize = 64;

    // 필요한 모든 스프라이트를 한 번에 굽는다. 이미 있으면 덮어쓴다.
    public static void GenerateAll()
    {
        EnsureFolder();

        foreach (int radius in Radii)
        {
            SaveSlicedSprite(FillName(radius), BuildRoundedRect(radius, stroke: false), radius + 2);
            SaveSlicedSprite(LineName(radius), BuildRoundedRect(radius, stroke: true), radius + 2);
        }

        SaveIcon("Icon_Book", BuildIcon(SdBook));
        SaveIcon("Icon_Search", BuildIcon(SdSearch));
        SaveIcon("Icon_Lock", BuildIcon(SdLock));
        SaveIcon("Icon_Check", BuildIcon(SdCheck));
        SaveIcon("Icon_Clock", BuildIcon(SdClock));
        SaveIcon("Icon_Star", BuildIcon(SdStar));
        SaveIcon("Icon_Close", BuildIcon(SdClose));

        AssetDatabase.Refresh();
    }

    // 반지름 r짜리 둥근 사각형 채움 스프라이트를 불러온다.
    public static Sprite LoadFill(int radius) => Load(FillName(radius));

    // 반지름 r짜리 둥근 사각형 테두리 스프라이트를 불러온다.
    public static Sprite LoadLine(int radius) => Load(LineName(radius));

    // 이름으로 아이콘 스프라이트를 불러온다. (예: "Icon_Lock")
    public static Sprite LoadIcon(string iconName) => Load(iconName);

    private static string FillName(int radius) => $"RoundFill_{radius}";

    private static string LineName(int radius) => $"RoundLine_{radius}";

    // 생성된 스프라이트를 경로로 불러온다.
    private static Sprite Load(string assetName) =>
        AssetDatabase.LoadAssetAtPath<Sprite>($"{OutputFolder}/{assetName}.png");

    // 출력 폴더가 없으면 만들고 에셋 데이터베이스에 등록한다.
    private static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(OutputFolder)) return;

        string absolute = Path.Combine(Directory.GetCurrentDirectory(), OutputFolder);
        if (!Directory.Exists(absolute))
            Directory.CreateDirectory(absolute);

        AssetDatabase.Refresh();
    }

    // 9-슬라이스 테두리를 지정해 스프라이트 PNG를 저장한다.
    private static void SaveSlicedSprite(string assetName, Texture2D texture, int border)
    {
        Save(assetName, texture, new Vector4(border, border, border, border));
    }

    // 9-슬라이스 없이 아이콘 스프라이트 PNG를 저장한다.
    private static void SaveIcon(string assetName, Texture2D texture)
    {
        Save(assetName, texture, Vector4.zero);
    }

    // 텍스처를 PNG로 쓰고 스프라이트 임포트 설정을 적용한다.
    private static void Save(string assetName, Texture2D texture, Vector4 border)
    {
        string path = $"{OutputFolder}/{assetName}.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteBorder = border;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spritePixelsPerUnit = 100f;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }

    // 반지름 r짜리 둥근 사각형을 채움 또는 2px 테두리로 그린 텍스처를 만든다.
    private static Texture2D BuildRoundedRect(int radius, bool stroke)
    {
        int size = radius * 2 + 8;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        Vector2 half = new Vector2(size * 0.5f - 2f, size * 0.5f - 2f);

        return Rasterize(size, p =>
        {
            float d = SdRoundedBox(p - center, half, radius);
            return stroke ? Mathf.Abs(d) - StrokeWidth * 0.5f : d;
        });
    }

    // 64x64 아이콘 캔버스에 SDF 함수를 그린 텍스처를 만든다. 좌표는 캔버스 중심 기준이다.
    private static Texture2D BuildIcon(Func<Vector2, float> sdf)
    {
        Vector2 center = new Vector2(IconSize * 0.5f, IconSize * 0.5f);
        return Rasterize(IconSize, p => sdf(p - center));
    }

    // SDF 값을 알파로 변환해(경계에서 부드럽게) 흰색 텍스처로 굽는다.
    private static Texture2D Rasterize(int size, Func<Vector2, float> sdf)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = sdf(new Vector2(x + 0.5f, y + 0.5f));
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - d) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    // 중심 기준 둥근 사각형의 부호 거리 함수.
    private static float SdRoundedBox(Vector2 p, Vector2 half, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x) - half.x + radius, Mathf.Abs(p.y) - half.y + radius);
        return Mathf.Min(Mathf.Max(q.x, q.y), 0f)
             + new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude
             - radius;
    }

    // 선분까지의 거리에서 두께를 뺀 캡슐 부호 거리 함수.
    private static float SdCapsule(Vector2 p, Vector2 a, Vector2 b, float width)
    {
        Vector2 pa = p - a;
        Vector2 ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - width * 0.5f;
    }

    // 중심 c, 반지름 r인 원 테두리의 부호 거리 함수.
    private static float SdRing(Vector2 p, Vector2 c, float radius, float width) =>
        Mathf.Abs((p - c).magnitude - radius) - width * 0.5f;

    // 펼쳐진 책 아이콘 — 둥근 사각형 테두리와 가운데 책등 선.
    private static float SdBook(Vector2 p)
    {
        float frame = Mathf.Abs(SdRoundedBox(p, new Vector2(21f, 16f), 3f)) - 1.6f;
        float spine = SdCapsule(p, new Vector2(0f, -16f), new Vector2(0f, 16f), 3.2f);
        return Mathf.Min(frame, spine);
    }

    // 돋보기 아이콘 — 원 테두리와 손잡이 캡슐.
    private static float SdSearch(Vector2 p)
    {
        float lens = SdRing(p, new Vector2(-3f, 4f), 13f, 3.4f);
        float handle = SdCapsule(p, new Vector2(6.5f, -5.5f), new Vector2(17f, -16f), 4f);
        return Mathf.Min(lens, handle);
    }

    // 자물쇠 아이콘 — 몸통 테두리, 위쪽 고리, 열쇠구멍.
    private static float SdLock(Vector2 p)
    {
        float body = Mathf.Abs(SdRoundedBox(p - new Vector2(0f, -8f), new Vector2(15f, 11f), 3.5f)) - 1.8f;

        float shackleRing = SdRing(p, new Vector2(0f, 3f), 9f, 3.4f);
        float shackle = Mathf.Max(shackleRing, 2f - p.y); // 몸통 위쪽 반원만 남긴다

        float keyhole = (p - new Vector2(0f, -6f)).magnitude - 2.6f;
        float keyStem = SdCapsule(p, new Vector2(0f, -6f), new Vector2(0f, -13f), 2.6f);

        return Mathf.Min(Mathf.Min(body, shackle), Mathf.Min(keyhole, keyStem));
    }

    // 원 안의 체크 아이콘 — 수집 완료 표시용.
    private static float SdCheck(Vector2 p)
    {
        float ring = SdRing(p, Vector2.zero, 13f, 3f);
        float shortStroke = SdCapsule(p, new Vector2(-6.5f, 0.5f), new Vector2(-2f, -5f), 3.2f);
        float longStroke = SdCapsule(p, new Vector2(-2f, -5f), new Vector2(6.8f, 6.5f), 3.2f);
        return Mathf.Min(ring, Mathf.Min(shortStroke, longStroke));
    }

    // 시계 아이콘 — 미획득 표시용.
    private static float SdClock(Vector2 p)
    {
        float ring = SdRing(p, Vector2.zero, 13f, 3f);
        float hourHand = SdCapsule(p, Vector2.zero, new Vector2(0f, 7.5f), 3f);
        float minuteHand = SdCapsule(p, Vector2.zero, new Vector2(5.5f, -1.5f), 3f);
        return Mathf.Min(ring, Mathf.Min(hourHand, minuteHand));
    }

    // 닫기(X) 아이콘 — 교차하는 두 획.
    private static float SdClose(Vector2 p)
    {
        float stroke1 = SdCapsule(p, new Vector2(-11f, -11f), new Vector2(11f, 11f), 4f);
        float stroke2 = SdCapsule(p, new Vector2(-11f, 11f), new Vector2(11f, -11f), 4f);
        return Mathf.Min(stroke1, stroke2);
    }

    // 별 테두리 아이콘 — 바깥 별에서 안쪽 별을 뺀 링 형태로 만든다.
    private static float SdStar(Vector2 p)
    {
        float outer = SdStarPolygon(p, 16f);
        float inner = SdStarPolygon(p, 12f);
        return Mathf.Max(outer, -inner);
    }

    // 꼭짓점 5개짜리 별 다각형의 근사 부호 거리 — 각 변까지의 거리 중 최솟값에 내부 여부로 부호를 준다.
    private static float SdStarPolygon(Vector2 p, float outerRadius)
    {
        const int PointCount = 5;
        float innerRadius = outerRadius * 0.46f;
        var vertices = new Vector2[PointCount * 2];

        for (int i = 0; i < vertices.Length; i++)
        {
            float radius = (i % 2 == 0) ? outerRadius : innerRadius;
            float angle = Mathf.PI * 0.5f + i * Mathf.PI / PointCount;
            vertices[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        float distance = float.MaxValue;
        bool inside = false;

        for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
        {
            distance = Mathf.Min(distance, SdCapsule(p, vertices[j], vertices[i], 0f));

            if ((vertices[i].y > p.y) != (vertices[j].y > p.y) &&
                p.x < (vertices[j].x - vertices[i].x) * (p.y - vertices[i].y) / (vertices[j].y - vertices[i].y) + vertices[i].x)
            {
                inside = !inside;
            }
        }

        return inside ? -distance : distance;
    }
}
