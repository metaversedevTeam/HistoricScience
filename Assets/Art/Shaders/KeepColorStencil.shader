// 색을 유지할 오브젝트가 화면에 보이는 픽셀에 스텐실 비트(128)를 기록하거나 지우는 셰이더
// 색과 깊이는 쓰지 않으며, KeepColorStencilFeature가 덮어쓰기 셰이더로 사용한다
// 덮어쓰기 셰이더는 원래 머티리얼의 프로퍼티와 키워드를 그대로 받으므로 알파 클립을 따라갈 수 있다
Shader "Hidden/HistoricScience/KeepColorStencil"
{
    Properties
    {
        [HideInInspector] _BaseMap("Base Map", 2D) = "white" {}
        [HideInInspector] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        ColorMask 0
        ZWrite Off
        ZTest LEqual
        // 양면 머티리얼의 뒷면도 표시되도록 컬링하지 않는다
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            half alpha : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.alpha = input.color.a;
            return output;
        }

        // 원래 머티리얼의 베이스 맵과 색으로 알파를 구한다
        half SampleAlpha(Varyings input)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
        }
        ENDHLSL

        // 0: 색을 유지할 픽셀에 스텐실 비트를 기록한다
        Pass
        {
            Name "KeepColorMark"

            Stencil
            {
                Ref 128
                WriteMask 128
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragMark
            #pragma multi_compile_instancing
            // 빌드에서 변형이 빠지지 않도록 shader_feature 대신 multi_compile을 쓴다
            #pragma multi_compile_local_fragment _ _ALPHATEST_ON

            half4 FragMark(Varyings input) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                clip(SampleAlpha(input) - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        // 1: 앞을 가린 투명 오브젝트의 픽셀에서 스텐실 비트를 지운다
        Pass
        {
            Name "KeepColorClear"

            Stencil
            {
                Ref 0
                WriteMask 128
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragClear
            #pragma multi_compile_instancing

            // KeepColorStencilFeature가 넣는 전역 값. 이 알파 이상인 픽셀만 가린 것으로 본다
            half _KeepColorOccluderAlpha;

            half4 FragClear(Varyings input) : SV_Target
            {
                clip(SampleAlpha(input) * input.alpha - _KeepColorOccluderAlpha);
                return 0;
            }
            ENDHLSL
        }
    }
}
