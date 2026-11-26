using System.Collections.Generic;
using UnityEngine;

// 외부 에셋(git에 올라가지 않는 ExternalAssets)의 텍스처를 쓰는 터레인 레이어가 텍스처를 잃었을 때,
// 흰색 단색 텍스처에 지정한 색을 곱한 대체 레이어를 만들어 주는 도우미. 메인 스레드에서만 호출해야 한다.
public static class TerrainLayerFallback
{
    // 원래 레이어와 색 조합마다 한 번만 만든 대체 레이어. 청크마다 같은 레이어를 공유하게 한다.
    private static readonly Dictionary<(TerrainLayer Primary, Color Color), TerrainLayer> s_placeholders = new Dictionary<(TerrainLayer, Color), TerrainLayer>();

    // 원래 레이어의 색상 텍스처가 있으면 원래 레이어를, 없으면 흰색 텍스처에 placeholderColor를 입힌 대체 레이어를 돌려준다.
    public static TerrainLayer Resolve(TerrainLayer primary, Color placeholderColor)
    {
        if (primary == null || primary.diffuseTexture != null) return primary;

        var key = (primary, placeholderColor);
        if (!s_placeholders.TryGetValue(key, out TerrainLayer placeholder) || placeholder == null)
        {
            placeholder = CreatePlaceholder(primary, placeholderColor);
            s_placeholders[key] = placeholder;
        }
        return placeholder;
    }

    // 원래 레이어의 타일 설정을 따르면서 흰색 텍스처에 색을 곱하는 대체 레이어를 만든다.
    private static TerrainLayer CreatePlaceholder(TerrainLayer primary, Color color)
    {
        var layer = new TerrainLayer
        {
            name = $"{primary.name} (Placeholder)",
            hideFlags = HideFlags.DontSave,
            diffuseTexture = Texture2D.whiteTexture,
            tileSize = primary.tileSize,
            tileOffset = primary.tileOffset,
            metallic = primary.metallic,
            smoothness = primary.smoothness,
            // 터레인 셰이더는 색상 텍스처에 (remapMax - remapMin)을 곱하므로, 흰색 텍스처가 이 색이 된다.
            diffuseRemapMin = Vector4.zero,
            diffuseRemapMax = new Vector4(color.r, color.g, color.b, 1f),
        };
        return layer;
    }
}
