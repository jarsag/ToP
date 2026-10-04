Shader "Top/Glow"
{
    Properties
    {
        // The shape of the light: white or grey where it is strong, dark where it is not. Its own
        // colour is not used - Glow Colour is - so that one sheet can be any of them.
        [MainTexture] _BaseMap("Glow Map", 2D) = "white" {}
        [HDR] _GlowColour("Glow Colour", Color) = (1, 1, 1, 1)

        // How far from grey the colour is: nought leaves it grey, one is the colour as it stands, and
        // past one it is pushed further. A glow that is too vivid for the item under it, or too pale to
        // be seen on it, is a matter of this rather than of which colour was picked.
        _Saturation("Saturation", Range(0, 3)) = 1
        _Opacity("Opacity", Range(0, 1)) = 1

        // Everything darker than this on the sheet is left out rather than added, so that a sheet with
        // dark figures in it gives light in the form of them and nothing in the dark.
        _Cutoff("Cutoff", Range(0, 1)) = 0

        // How the sheet is laid over the item: how big it is, and where it starts. In the item's own
        // space rather than the item's texture coordinates, so that the sheet lies the same way over
        // every part of a model - an item built from parts whose texture coordinates are turned
        // against each other would otherwise wear the sheet one way on one part and across it on the
        // next.
        _GlowScale("Glow Scale", Vector) = (1, 1, 1, 1)
        _GlowOffset("Glow Offset", Vector) = (0, 0, 0, 0)

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
                half _Saturation;
                half _Opacity;
                half _Cutoff;
                float4 _GlowScale;
                float4 _GlowOffset;
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
                float3 positionOS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                output.positionOS = input.positionOS.xyz;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // The item's own space, not its texture coordinates. An item built from parts whose
                // texture coordinates are turned against each other - which is how the client's own
                // shapes are built - would wear a sheet one way on one part and across it on the next,
                // and a sheet is a picture of light rather than a skin: it should lie the same way over
                // the whole of what it lights.
                float2 at = (input.positionOS.xz * _GlowScale.xy) + _GlowOffset.xy;

                half4 sheet = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, at);
                half shape = max(sheet.r, max(sheet.g, sheet.b)) * sheet.a;

                // Left out rather than added where the sheet is too dark: a sheet with dark figures in
                // it would otherwise lift the whole item faintly instead of lighting the figures.
                clip(shape - _Cutoff);

                // Strictly the material's colour, with the sheet deciding only how much of it there is.
                // Multiplying the sheet's own colour in as well would make the glow the colour of a
                // picture - which is how a client's glow sheet works, and why one cannot be recoloured.
                half3 lit = _GlowColour.rgb;

                // Pulled towards grey or pushed away from it. The weights are the eye's own: green
                // carries most of what is seen as brightness and blue least, so a colour turned grey
                // by them keeps the brightness it looked like it had.
                half grey = dot(lit, half3(0.299h, 0.587h, 0.114h));
                lit = lerp(grey.xxx, lit, _Saturation);

                return half4(lit * shape * _GlowColour.a * _Opacity, 0);
            }
            ENDHLSL
        }
    }
}
