// Holy, see-through glow for the pharaoh's stand (URP, unlit, no HDR / bloom needed).
// Pass 1 writes depth only, so the body reads as one solid translucent shape (inner faces / overlapping parts hidden).
// Turn "Solid Look" off for big see-through shells (the barrier) that must not hide what is inside them.
// Pass 2: tinted see-through body + bright Fresnel rim + slow upward shimmer.
// _BaseColor.rgb = tint × brightness (a MaterialPropertyBlock may override it); opacity comes from _Alpha / _RimAlpha.
Shader "Game4026/SpectralGlow"
{
    Properties
    {
        [HDR] _BaseColor ("Tint", Color) = (1, 0.8, 0.4, 1)
        _Alpha ("Body Opacity", Range(0, 1)) = 0.35
        _Inner ("Body Brightness", Range(0, 2)) = 0.6
        _RimColor ("Rim Color", Color) = (1, 0.95, 0.75, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimIntensity ("Rim Brightness", Range(0, 4)) = 1.6
        _RimAlpha ("Rim Opacity", Range(0, 1)) = 0.6
        _Shimmer ("Shimmer Amount", Range(0, 1)) = 0.25
        _ShimmerScale ("Shimmer Bands per Metre", Range(0.1, 10)) = 2
        _ShimmerSpeed ("Shimmer Speed", Range(0, 5)) = 0.8
        [Enum(Off, 0, On, 1)] _PrepassZWrite ("Solid Look (depth prepass)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Alpha;
            half _Inner;
            half4 _RimColor;
            half _RimPower;
            half _RimIntensity;
            half _RimAlpha;
            half _Shimmer;
            half _ShimmerScale;
            half _ShimmerSpeed;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = p.positionCS;
            output.positionWS = p.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            return output;
        }
        ENDHLSL

        // Depth prepass: only the front-most surface of the stand gets coloured.
        Pass
        {
            Name "SpectralDepth"
            ZWrite [_PrepassZWrite]
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepth
            #pragma multi_compile_instancing

            half4 FragDepth(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "SpectralGlow"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1.0h - saturate(abs(dot(n, v))), _RimPower);

                half wave = sin(input.positionWS.y * _ShimmerScale * 6.2832 - _Time.y * _ShimmerSpeed * 6.2832);
                half shimmer = 1.0h + _Shimmer * wave;

                half3 body = _BaseColor.rgb * _Inner * shimmer;
                half3 edge = _RimColor.rgb * _BaseColor.rgb * rim * _RimIntensity;
                half alpha = saturate(_Alpha * shimmer + rim * _RimAlpha);
                // Colour is divided by alpha so the rim stays bright even where the body is thin.
                half3 colour = (body * _Alpha + edge) / max(alpha, 0.05h);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
