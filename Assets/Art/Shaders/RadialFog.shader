// 불투명 물체를 다 그린 뒤 화면 전체에 반경 거리 안개를 덮는 셰이더
// 깊이 텍스처로 각 픽셀의 월드 위치를 복원해 카메라까지의 실제 거리로 안개 비율을 구하고, 그 비율을 알파로 안개 색을 섞는다
// 렌더러의 Full Screen Pass Renderer Feature(Before Rendering Transparents, Requirements: Depth, Fetch Color Buffer 끔)가 사용한다
Shader "Hidden/HistoricScience/RadialFog"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        ZWrite Off
        ZTest Always
        Cull Off
        // 색 버퍼를 복사하지 않고 알파(안개 비율)로 기존 색 위에 안개 색을 섞는다
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "RadialFog"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragRadialFog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "RadialFog.hlsl"

            half4 FragRadialFog(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float rawDepth = SampleSceneDepth(uv);

                // 아무것도 그려지지 않은 원평면 픽셀(하늘)은 안개를 덮지 않는다. 내장 안개도 스카이박스에는 적용되지 않았다
                #if UNITY_REVERSED_Z
                if (rawDepth <= 0.0)
                    return 0;
                #else
                if (rawDepth >= 1.0)
                    return 0;
                #endif

                #if !UNITY_REVERSED_Z
                // OpenGL 계열은 클립 공간 z가 -1~1이므로 깊이 값을 그 범위로 바꾼다
                rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                return half4(_RadialFogColor.rgb, RadialFogFactor(positionWS));
            }
            ENDHLSL
        }
    }
}
