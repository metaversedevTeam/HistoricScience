// Full Screen Pass Renderer Feature에서 화면 전체를 흑백으로 바꾸는 셰이더
// 강도는 전역 값 _MonochromeIntensity(0 = 원래 색, 1 = 완전 흑백)로 조절한다
Shader "HistoricScience/Fullscreen/Monochrome"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "Monochrome"

            // KeepColorStencil이 표시한 픽셀(비트 128)은 건너뛰어 원래 색을 유지한다
            Stencil
            {
                Ref 128
                ReadMask 128
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // MonochromeController가 Shader.SetGlobalFloat로 넣는 전역 값
            float _MonochromeIntensity;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                half luminance = Luminance(color.rgb);
                color.rgb = lerp(color.rgb, luminance.xxx, saturate(_MonochromeIntensity));
                return color;
            }
            ENDHLSL
        }
    }
}
