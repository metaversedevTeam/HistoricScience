#ifndef HISTORICSCIENCE_BUILDING_CONSTRUCT_FORWARD_PASS_INCLUDED
#define HISTORICSCIENCE_BUILDING_CONSTRUCT_FORWARD_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv           : TEXCOORD0;
    // 절단 판정에 픽셀의 월드 좌표가 필요하므로 항상 넘긴다.
    float3 positionWS   : TEXCOORD1;
    half3  normalWS     : TEXCOORD2;
    half4  tangentWS    : TEXCOORD3;
    half   fogFactor    : TEXCOORD4;
    half3  vertexSH     : TEXCOORD5;
    float4 positionCS   : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// 조명 계산에 필요한 정보를 보간된 값에서 채운다. 라이트맵은 쓰지 않고 프로브(SH)만 사용한다.
void InitializeConstructInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

#if defined(_NORMALMAP)
    float tangentSign = input.tangentWS.w;
    float3 bitangent = tangentSign * cross(input.normalWS.xyz, input.tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
    inputData.tangentToWorld = tangentToWorld;
    inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
#else
    inputData.normalWS = input.normalWS;
#endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.bakedGI = SampleSHPixel(input.vertexSH, inputData.normalWS);
    inputData.shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);
}

Varyings ConstructForwardVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionWS = positionInputs.positionWS;
    output.positionCS = positionInputs.positionCS;
    output.normalWS = normalInputs.normalWS;
    output.tangentWS = half4(normalInputs.tangentWS.xyz, input.tangentOS.w * GetOddNegativeScale());
    output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
    output.vertexSH = SampleSHVertex(normalInputs.normalWS);

    return output;
}

half4 ConstructForwardFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    ClipConstruction(input.positionWS);

    SurfaceData surfaceData;
    InitializeConstructSurfaceData(input.uv, surfaceData);
    // 지어지고 있는 단면이 눈에 띄도록 절단 경계에 빛나는 띠를 더한다.
    surfaceData.emission += _EdgeColor.rgb * ConstructEdgeFactor(input.positionWS);

    InputData inputData;
    InitializeConstructInputData(input, surfaceData.normalTS, inputData);

#if defined(_SCREEN_SPACE_OCCLUSION)
    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
    surfaceData.occlusion = min(surfaceData.occlusion, aoFactor.indirectAmbientOcclusion);
    inputData.bakedGI *= aoFactor.indirectAmbientOcclusion;
#endif

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = 1.0h;

    return color;
}

#endif
