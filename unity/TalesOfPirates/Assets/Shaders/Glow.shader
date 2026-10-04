Shader "Top/Glow"
{
    Properties
    {
        // The shape of the light: white or grey where it is strong, dark where it is not. Its own
        // colour is not used - Glow Colour is - so that one sheet can be any of them.
        [MainTexture] _BaseMap("Glow Map", 2D) = "white" {}
        [HDR] _GlowColour("Glow Colour", Color) = (1, 1, 1, 1)
        _Opacity("Opacity", Range(0, 1)) = 1

        // Everything darker than this on the sheet is left out rather than added, so that a sheet with
        // dark figures in it gives light in the form of them and nothing in the dark.
        _Cutoff("Cutoff", Range(0, 1)) = 0

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
                half _Cutoff;
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
                // How much light there is here at all, from the sheet's own brightness and its alpha
                // together, so that either a white sheet or a masked one gives a shape.
                half4 sheet = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half shape = max(sheet.r, max(sheet.g, sheet.b)) * sheet.a;

                // Left out rather than added where the sheet is too dark: a sheet with dark figures in
                // it would otherwise lift the whole item faintly instead of lighting the figures.
                clip(shape - _Cutoff);

                // Strictly the material's colour, with the sheet deciding only how much of it there is.
                // Multiplying the sheet's own colour in as well would make the glow the colour of a
                // picture - which is how a client's glow sheet works, and why one cannot be recoloured.
                return half4(_GlowColour.rgb * shape * _GlowColour.a * _Opacity, 0);
            }
            ENDHLSL
        }
    }
}
