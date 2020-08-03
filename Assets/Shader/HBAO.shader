Shader "Hidden/HBAO"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    CGINCLUDE

    UNITY_DECLARE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture);
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
    //UNITY_DECLARE_DEPTH_TEXTURE(_DepthTex);

    ENDCG
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "HBAO.cginc"

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
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;

            float4 frag (v2f i) : SV_Target
            {
                return float4(FetchViewNormal(i.uv), 1);
            }
            ENDCG
        }
    }
}
