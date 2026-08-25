#ifndef HISTORICSCIENCE_BUILDING_CONSTRUCT_SHADOW_PASS_INCLUDED
#define HISTORICSCIENCE_BUILDING_CONSTRUCT_SHADOW_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

// 그림자 맵을 그리는 광원의 방향과 위치. URP가 패스마다 채워 준다.
float3 _LightDirection;
float3 _LightPosition;

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    // 그림자도 본체와 같은 자리에서 잘려야 하므로 바이어스를 적용하지 않은 원래 월드 좌표를 넘긴다.
    float3 positionWS   : TEXCOORD0;
    float4 positionCS   : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// 그림자 여드름을 막는 바이어스를 적용한 클립 좌표를 구한다.
float4 GetConstructShadowPositionHClip(float3 positionWS, float3 normalWS)
{
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    return ApplyShadowClamping(positionCS);
}

Varyings ConstructShadowVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

    output.positionWS = positionWS;
    output.positionCS = GetConstructShadowPositionHClip(positionWS, normalWS);

    return output;
}

half4 ConstructShadowFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);

    ClipConstruction(input.positionWS);
    return 0;
}

#endif
