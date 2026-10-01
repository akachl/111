Shader "XingYu/IslandSoftGlow"
{
    Properties { _BaseColor("Color", Color) = (1,1,1,1) _Radial("Radial", Float) = 1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Radial;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _BaseColor;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half radial = saturate(1.0 - lerp(abs(input.uv.y * 2.0 - 1.0), length(input.uv * 2.0 - 1.0), _Radial));
                return half4(input.color.rgb, input.color.a * radial * radial);
            }
            ENDHLSL
        }
    }
}
