Shader "Unlit/VideoFeather"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Feather ("Feather XY (UV)", Vector) = (0.05, 0.05, 0, 0)
        _Power ("Falloff Power", Range(0.1,5)) = 1
        _GlobalAlpha ("Global Alpha", Range(0,1)) = 1
        _UseFeather ("Use Feather (1/0)", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Feather; // x,y used
            float _Power;
            float _GlobalAlpha;
            float _UseFeather;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                if (_UseFeather > 0.5)
                {
                    float2 uv = i.uv;
                    float2 edge = float2(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                    float2 fw = max(_Feather.xy, float2(1e-5, 1e-5));
                    float2 axy = saturate(edge / fw);
                    float a = min(axy.x, axy.y);
                    a = pow(a, _Power);
                    col.a *= a;
                }

                col.a *= _GlobalAlpha;
                return col;
            }
            ENDCG
        }
    }
}

