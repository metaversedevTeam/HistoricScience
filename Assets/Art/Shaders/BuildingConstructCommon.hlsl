#ifndef HISTORICSCIENCE_BUILDING_CONSTRUCT_COMMON_INCLUDED
#define HISTORICSCIENCE_BUILDING_CONSTRUCT_COMMON_INCLUDED

// 건축 연출 셰이더의 모든 패스가 공유하는 머티리얼 프로퍼티와 절단 계산.
// 프로퍼티 이름은 URP Lit과 동일하게 맞춰 두었다. 원본 머티리얼을 복사해 셰이더만 이 셰이더로 바꿔치면
// 텍스처와 색이 그대로 옮겨 오도록 하기 위함이다.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BumpMap_ST;
    float4 _MetallicGlossMap_ST;
    float4 _OcclusionMap_ST;
    float4 _EmissionMap_ST;
    half4  _BaseColor;
    half4  _EmissionColor;
    half   _Metallic;
    half   _Smoothness;
    half   _BumpScale;
    half   _OcclusionStrength;
    half   _Cull;

    // 이 높이(월드 Y)보다 위쪽은 아직 지어지지 않은 것으로 보고 버린다. 기본값을 크게 두어 값을 넣지 않으면 건물 전체가 보인다.
    float  _CutHeight;
    // 절단 경계에 그릴 빛나는 띠의 두께(월드 단위)
    float  _EdgeWidth;
    half4  _EdgeColor;
    // 절단선을 울퉁불퉁하게 흔드는 노이즈의 촘촘함과 세기
    float  _NoiseScale;
    float  _NoiseStrength;
CBUFFER_END

TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_OcclusionMap);       SAMPLER(sampler_OcclusionMap);

// 2D 좌표 하나를 0~1 난수로 흩뜨린다. 노이즈의 격자 꼭짓점 값을 만드는 데 쓴다.
float ConstructHash(float2 position)
{
    position = frac(position * float2(123.34, 345.45));
    position += dot(position, position + 34.345);
    return frac(position.x * position.y);
}

// 격자 꼭짓점 난수를 부드럽게 보간한 값 노이즈. 절단선이 자로 그은 듯 반듯해 보이지 않게 하는 용도다.
float ConstructNoise(float2 position)
{
    float2 cell = floor(position);
    float2 offset = frac(position);
    offset = offset * offset * (3.0 - 2.0 * offset);

    float cornerA = ConstructHash(cell);
    float cornerB = ConstructHash(cell + float2(1.0, 0.0));
    float cornerC = ConstructHash(cell + float2(0.0, 1.0));
    float cornerD = ConstructHash(cell + float2(1.0, 1.0));

    return lerp(lerp(cornerA, cornerB, offset.x), lerp(cornerC, cornerD, offset.x), offset.y);
}

// 이 픽셀 자리에서의 실제 절단 높이. 기준 높이에 노이즈를 더해 경계를 흐트러뜨린다.
float ConstructCutHeight(float3 positionWS)
{
    float noise = ConstructNoise(positionWS.xz * _NoiseScale) - 0.5;
    return _CutHeight + noise * _NoiseStrength;
}

// 절단면보다 위쪽(아직 지어지지 않은 부분)의 픽셀을 버린다. 모든 패스가 같은 식을 쓰므로 그림자와 깊이도 함께 잘린다.
void ClipConstruction(float3 positionWS)
{
    clip(ConstructCutHeight(positionWS) - positionWS.y);
}

// 절단 경계에 가까울수록 1에 가까워지는 값. 지어지는 단면에 빛나는 띠를 얹는 데 쓴다.
half ConstructEdgeFactor(float3 positionWS)
{
    float depthBelowCut = ConstructCutHeight(positionWS) - positionWS.y;
    return (half)(1.0 - saturate(depthBelowCut / max(_EdgeWidth, 1e-4)));
}

// 원본 머티리얼에서 옮겨 온 텍스처·색을 URP의 표준 SurfaceData로 채운다. 건물은 동적으로 소환되어 라이트맵을 굽지 않으므로 라이트맵 관련 분기는 두지 않는다.
void InitializeConstructSurfaceData(float2 uv, out SurfaceData surfaceData)
{
    surfaceData = (SurfaceData)0;

    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    surfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;
    surfaceData.alpha = 1.0h;

#ifdef _METALLICSPECGLOSSMAP
    half4 metallicGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);
    surfaceData.metallic = metallicGloss.r;
    surfaceData.smoothness = metallicGloss.a * _Smoothness;
#else
    surfaceData.metallic = _Metallic;
    surfaceData.smoothness = _Smoothness;
#endif

    surfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    surfaceData.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));

#ifdef _OCCLUSIONMAP
    half occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
    surfaceData.occlusion = LerpWhiteTo(occlusion, _OcclusionStrength);
#else
    surfaceData.occlusion = 1.0h;
#endif

    surfaceData.specular = half3(0.0h, 0.0h, 0.0h);
}

#endif
