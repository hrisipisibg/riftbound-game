Shader "Riftbound/Flowing River"
{
    Properties
    {
        _RippleTex ("Water detail", 2D) = "gray" {}
        _Shallows ("Shallow water", Color) = (0.24,0.48,0.43,1)
        _Channel ("Deep water", Color) = (0.105,0.29,0.33,1)
        _Foam ("Foam", Color) = (0.73,0.85,0.78,1)
        _Current ("Current speed", Range(0,2)) = 0.65
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "Flow"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_RippleTex);
            SAMPLER(sampler_RippleTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Shallows;
                half4 _Channel;
                half4 _Foam;
                half _Current;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 flow : TEXCOORD2;
                half4 profile : TEXCOORD3;
                half fog : TEXCOORD4;
            };

            Varyings Vert(Attributes source)
            {
                Varyings result;
                VertexPositionInputs position = GetVertexPositionInputs(source.positionOS.xyz);
                result.positionCS = position.positionCS;
                result.positionWS = position.positionWS;
                result.normalWS = TransformObjectToWorldNormal(source.normalOS);
                result.flow = source.uv;
                result.profile = source.color;
                result.fog = ComputeFogFactor(position.positionCS.z);
                return result;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float u = input.flow.x;
                float v = input.flow.y;
                half rapid = input.profile.r;
                float time = _Time.y * _Current;
                float detail1 = SAMPLE_TEXTURE2D(_RippleTex, sampler_RippleTex,
                    float2(u * (1.2 + input.profile.g * 1.4), v * .084 - time * .20)).r;
                float detail2 = SAMPLE_TEXTURE2D(_RippleTex, sampler_RippleTex,
                    float2(u * 2.1 + v * .012 + time * .014, v * .19 - time * .32)).g;
                float variation = sin(u * 28 + v * 1.53 - time * 3.1 + detail1 * 8.0);
                half edge = saturate(1 - abs(2 * u - 1));
                half depth = smoothstep(.04, .67, edge);
                half3 normalWS = normalize(input.normalWS);
                half facing = abs(dot(normalWS, normalize(GetWorldSpaceViewDir(input.positionWS))));
                half viewGlint = pow(1.0h - saturate(facing), 3.0h);
                half fineFoam = smoothstep(.80, .97, .58 + .28 * variation + .25 * (detail2 - .5));
                half eddy = (1 - smoothstep(.023, .22, edge)) * fineFoam * .17;
                half whitewater = rapid * fineFoam * smoothstep(.015, .18, edge) * .77;
                half3 water = lerp(_Shallows.rgb, _Channel.rgb, depth * .79);
                water *= .86 + .19 * detail1 + .09 * variation;
                water = lerp(water, _Foam.rgb, saturate(eddy + whitewater + .13 * viewGlint));
                half alpha = input.profile.a * smoothstep(0, .064, edge) * (.84 + .14 * depth);
                return half4(MixFog(water, input.fog), alpha);
            }
            ENDHLSL
        }
    }
}
