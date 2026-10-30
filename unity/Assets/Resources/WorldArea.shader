Shader "Ecosystem/WorldArea"
{
    Properties { _Color ("Color", Color) = (0.7,0.45,0.85,0.2) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; };
            float4 _Color;
            float4 _View;
            float4 _Facing;
            float _Focused;
            Output vert(Input input)
            {
                Output output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.world = mul(unity_ObjectToWorld, input.vertex).xy;
                output.uv = input.uv * 2 - 1;
                return output;
            }
            fixed4 frag(Output input) : SV_Target
            {
                float radius = length(input.uv);
                clip(1 - radius);
                if (_Focused > 0.5)
                {
                    float2 delta = input.world - _View.xy;
                    float distance = length(delta);
                    clip(_View.z - distance);
                    clip(dot(delta, _Facing.xy) - distance * 0.5 + 0.000001);
                }
                float border = smoothstep(1 - max(fwidth(radius) * 2, 0.025), 1 - fwidth(radius), radius);
                return fixed4(_Color.rgb, lerp(_Color.a, 0.85, border));
            }
            ENDCG
        }
    }
}
