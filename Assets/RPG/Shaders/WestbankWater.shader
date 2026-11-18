Shader "Riftbound/Westbank Water"
{
    Properties
    {
        _DeepColor ("Deep water", Color) = (0.07, 0.24, 0.26, 0.94)
        _ShallowColor ("Shallows", Color) = (0.19, 0.42, 0.37, 0.86)
        _FoamColor ("Whitewater", Color) = (0.66, 0.76, 0.65, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor, _ShallowColor, _FoamColor;
            CBUFFER_END

            struct Input { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Output { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; float2 uv : TEXCOORD1; half fog : TEXCOORD2; };

            Output Vert(Input input)
            {
                Output output;
                output.world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.world);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Output input) : SV_Target
            {
                float t = _Time.y;
                float x = input.world.x;
                float z = input.world.z - 0.75 * t;
                float side = min(input.uv.x, 1.0 - input.uv.x);
                float current = sin(x * 1.72 + z * 0.52 + sin(z * 0.37 + x) * 0.54);
                float crossRipple = sin(z * 4.62 + x * 0.86 + cos(x * 2.3 - z * 0.43) * 1.05);
                float breakup = sin(z * 1.7 + x * 2.9) * cos(x * 1.9 - z * 0.81);
                float shore = 1.0 - smoothstep(0.035, 0.30, side);
                half4 water = lerp(_DeepColor, _ShallowColor, shore * 0.83 + current * 0.07);
                float brokenLine = side + crossRipple * 0.012 + current * 0.017;
                float foam = (1.0 - smoothstep(0.008, 0.024, abs(brokenLine - 0.057))) * smoothstep(-0.68, 0.47, breakup);
                float ripples = smoothstep(0.84, 1.0, crossRipple) * smoothstep(0.15, 1.0, breakup);
                half grazing = 1.0 - saturate(normalize(GetWorldSpaceViewDir(input.world)).y);
                half3 result = lerp(water.rgb, _FoamColor.rgb, foam * 0.56 + ripples * (0.23 + grazing * 0.11));
                half ends = smoothstep(0.0, 0.05, input.uv.y) * (1.0 - smoothstep(0.96, 1.0, input.uv.y));
                return half4(MixFog(result, input.fog), water.a * ends);
            }
            ENDHLSL
        }
    }
}
