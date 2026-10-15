Shader "Museum/World Text"
{
    Properties
    {
        _MainTex ("Font atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Lighting Off Cull Off ZWrite Off ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 _Color;
            struct Input {
                float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Output {
                float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Output vert(Input v)
            {
                Output o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(Output, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(Output i):SV_Target
            {
                fixed4 color = i.color;
                color.a *= tex2D(_MainTex, i.uv).a;
                return color;
            }
            ENDCG
        }
    }
}
