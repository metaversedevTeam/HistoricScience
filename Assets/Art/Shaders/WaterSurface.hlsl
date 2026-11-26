// 스타일라이즈드 바다 수면(깊이 색, 해안 거품, 물결, 굴절)을 계산하는 셰이더 그래프 커스텀 함수. Water.shadergraph의 Custom Function 노드가 호출한다.
// 굴절된 바닥은 Emission으로, 물 자체의 색은 Base Color로 내보낸다. 이미 조명이 적용된 바닥 색에 조명이 한 번 더 곱해지지 않게 하기 위해서다.
#ifndef HISTORICSCIENCE_WATER_SURFACE_INCLUDED
#define HISTORICSCIENCE_WATER_SURFACE_INCLUDED

// 물은 반투명이라 불투명 물체에 안개를 덮는 전체 화면 패스보다 나중에 그려지므로, 반경 안개를 직접 적용한다
#include "Assets/Art/Shaders/RadialFog.hlsl"

// 정수 격자 좌표를 0~1 사이 의사 난수로 바꾼다.
float WaterHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

// 격자 꼭짓점 난수를 부드럽게 보간한 2D 값 노이즈(0~1)를 반환한다.
float WaterValueNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);

    float a = WaterHash(cell);
    float b = WaterHash(cell + float2(1.0, 0.0));
    float c = WaterHash(cell + float2(0.0, 1.0));
    float d = WaterHash(cell + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// 서로 다른 방향으로 흐르는 노이즈 두 겹을 겹쳐 월드 XZ 위치의 물결 높이(0~1)를 만든다.
float WaterWaveHeight(float2 worldXZ, float scale, float time)
{
    float2 p = worldXZ * scale;
    float h1 = WaterValueNoise(p + float2(0.6, 0.35) * time);
    float h2 = WaterValueNoise(p * 1.73 + float2(-0.4, 0.55) * time + 17.0);
    return h1 * 0.6 + h2 * 0.4;
}

// 깊이 버퍼 원시값을 카메라 앞 방향 선형 거리(eye depth)로 바꾼다. 직교 카메라도 처리한다.
float WaterRawToEyeDepth(float rawDepth)
{
    if (unity_OrthoParams.w > 0.5)
    {
#if UNITY_REVERSED_Z
        rawDepth = 1.0 - rawDepth;
#endif
        return lerp(_ProjectionParams.y, _ProjectionParams.z, rawDepth);
    }

    return LinearEyeDepth(rawDepth, _ZBufferParams);
}

// 수면 위치와 같은 화면 픽셀에 보이는 바닥까지의 수직 거리(물 깊이)를 구한다. 카메라 각도와 상관없이 같은 깊이는 같은 색이 된다.
float WaterColumnDepth(float3 surfaceWorldPos, float rawSceneDepth)
{
    float3 cameraForward = -UNITY_MATRIX_V[2].xyz;
    float3 toSurface = surfaceWorldPos - _WorldSpaceCameraPos;
    float surfaceEyeDepth = dot(toSurface, cameraForward);
    float sceneEyeDepth = WaterRawToEyeDepth(rawSceneDepth);

    float3 sceneWorldPos;
    if (unity_OrthoParams.w > 0.5)
        sceneWorldPos = surfaceWorldPos + cameraForward * (sceneEyeDepth - surfaceEyeDepth);
    else
        sceneWorldPos = _WorldSpaceCameraPos + toSurface * (sceneEyeDepth / max(surfaceEyeDepth, 1e-4));

    return surfaceWorldPos.y - sceneWorldPos.y;
}

// 화면 UV 위치의 깊이 버퍼 원시값을 읽는다. 그래프 미리보기에서는 노드로 받은 값을 그대로 쓴다.
float WaterSampleRawDepth(float2 uv, float fallbackRawDepth)
{
#ifdef SHADERGRAPH_PREVIEW
    return fallbackRawDepth;
#else
    return SHADERGRAPH_SAMPLE_SCENE_DEPTH(uv);
#endif
}

// 화면 UV 위치의 불투명 화면 색을 읽는다. 그래프 미리보기에서는 노드로 받은 값을 그대로 쓴다.
float3 WaterSampleSceneColor(float2 uv, float3 fallbackColor)
{
#ifdef SHADERGRAPH_PREVIEW
    return fallbackColor;
#else
    return SHADERGRAPH_SAMPLE_SCENE_COLOR(uv);
#endif
}

// 수면 한 픽셀의 최종 표면 값(색, 노멀, 굴절된 바닥 색, 매끄러움)을 계산한다.
void WaterSurface_float(
    float3 WorldPos, float4 ScreenPos, float SceneRawDepth, float3 SceneColor,
    float4 ShallowColor, float4 DeepColor, float DepthMaxDistance,
    float4 FoamColor, float FoamDistance, float FoamNoiseScale, float FoamSpeed, float FoamCutoff,
    float WaveScale, float WaveSpeed, float WaveStrength, float RefractionStrength, float Smoothness,
    out float3 BaseColor, out float3 NormalWS, out float3 Emission, out float SmoothnessOut, out float Alpha)
{
    float time = _TimeParameters.x;
    float2 screenUV = ScreenPos.xy;

    // 물결: 노이즈 높이의 기울기로 월드 노멀을 만든다. 수면이 수평 평면이라 탄젠트 공간을 거칠 필요가 없다.
    float waveScale = max(WaveScale, 1e-4);
    float waveTime = time * WaveSpeed;
    float epsilon = 0.25 / waveScale;
    float height = WaterWaveHeight(WorldPos.xz, waveScale, waveTime);
    float heightX = WaterWaveHeight(WorldPos.xz + float2(epsilon, 0.0), waveScale, waveTime);
    float heightZ = WaterWaveHeight(WorldPos.xz + float2(0.0, epsilon), waveScale, waveTime);
    float3 waveNormal = normalize(float3((height - heightX) * WaveStrength, 1.0, (height - heightZ) * WaveStrength));

    // 굴절: 물결 노멀만큼 화면 UV를 흔든다. 흔든 위치가 수면보다 앞에 있는 물체면 굴절을 취소해 물체가 물속으로 번지지 않게 한다.
    float depth = WaterColumnDepth(WorldPos, SceneRawDepth);
    float2 refractUV = saturate(screenUV + waveNormal.xz * RefractionStrength * saturate(depth));
    float refractDepth = WaterColumnDepth(WorldPos, WaterSampleRawDepth(refractUV, SceneRawDepth));
    if (refractDepth < 0.0)
    {
        refractUV = screenUV;
        refractDepth = depth;
    }
    float3 sceneColor = WaterSampleSceneColor(refractUV, SceneColor);

    // 깊이 색: 얕은 색에서 깊은 색으로 넘어가며, 각 색의 알파를 물의 불투명도로 쓴다.
    float depth01 = saturate(refractDepth / max(DepthMaxDistance, 1e-4));
    float4 waterColor = lerp(ShallowColor, DeepColor, depth01);
    float waterOpacity = waterColor.a;

    // 해안 거품: 얕을수록 노이즈 문턱이 낮아져 거품이 띠처럼 붙고, 깊은 곳에는 문턱을 넘는 일부만 반점으로 남는다.
    float foamDepth01 = saturate(depth / max(FoamDistance, 1e-4));
    float foamThreshold = foamDepth01 * FoamCutoff;
    float2 foamUV = WorldPos.xz * FoamNoiseScale + float2(1.0, 0.7) * FoamSpeed * time + waveNormal.xz * 0.5;
    float foamNoise = WaterValueNoise(foamUV);
    float foam = smoothstep(foamThreshold - 0.02, foamThreshold + 0.02, foamNoise) * FoamColor.a;

    BaseColor = lerp(waterColor.rgb * waterOpacity, FoamColor.rgb, foam);
    Emission = sceneColor * (1.0 - waterOpacity) * (1.0 - foam);
    NormalWS = normalize(lerp(waveNormal, float3(0.0, 1.0, 0.0), foam));
    SmoothnessOut = lerp(Smoothness, 0.0, foam);
    Alpha = 1.0;

    // 반경 안개: 조명은 Base Color에 비례하므로 Base Color와 Emission을 함께 줄이고 줄어든 만큼 안개 색을 Emission으로 더한다.
    // 반사광은 Base Color와 무관하게 더해지므로 매끄러움도 함께 낮춰 먼 수면의 반짝임이 안개를 뚫지 않게 한다.
    float fog = RadialFogFactor(WorldPos);
    BaseColor *= 1.0 - fog;
    Emission = lerp(Emission, _RadialFogColor.rgb, fog);
    SmoothnessOut *= 1.0 - fog;
}

#endif
