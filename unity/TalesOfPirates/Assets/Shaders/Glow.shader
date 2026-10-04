Shader "Top/Glow"
{
    Properties
    {
        // The shape of the light: white or grey where it is strong, dark where it is not. Its own
        // colour is not used - Glow Colour is - so that one sheet can be any of them.
        [MainTexture] _BaseMap("Glow Map", 2D) = "white" {}
        [HDR] _GlowColour("Glow Colour", Color) = (1, 1, 1, 1)
        _Opacity("Opacity", Range(0, 1)) = 1
        _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent"
        }

        Pass
        {
            Name "Glow"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            // Added to what is under it rather than laid over it: a glow is light, and light adds. This
            // is what makes a glow read as something alight rather than as a painted sheet.
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _GlowColour;
                half _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // How much light there is here at all, from the sheet's brightness and its alpha
                // together, so that either a white sheet or a masked one gives a shape.
                half4 sheet = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half light = max(sheet.r, max(sheet.g, sheet.b)) * sheet.a;

                return half4(_GlowColour.rgb * light * _GlowColour.a * _Opacity, 0);
            }
            ENDHLSL
        }
    }
}
