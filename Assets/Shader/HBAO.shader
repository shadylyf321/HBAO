Shader "Hidden/HBAO"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "" {}
        _HbaoTex("Texture", 2D) = "" {}
        _HbaoTexBlur("Texture", 2D) = "" {}
    }

    CGINCLUDE
    UNITY_DECLARE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture)
    sampler2D _MainTex;
    sampler2D _HbaoTex;
    sampler2D _HbaoBlurTex;
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);//depth should use high precise

    float4 _UV2View;
    float4 _TexelSize;
    float _AOStrengh;
    float _MaxRadiusPixel;
    float _RadiusPixel;
    float _Radius;
    float _AngleBias;
    float _BlurRadiusPixel;
    int _BlurSamples;

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

    float2 TransformTriangleVertexToUV(float2 vertex)
    {
        return (vertex + 1.0) * 0.5;
    }

    v2f vert (appdata v)
    {
        v2f o;
        o.vertex = float4(v.vertex.xy, 0.0, 1.0);
        o.uv = TransformTriangleVertexToUV(o.vertex.xy);
#if UNITY_UV_STARTS_AT_TOP
        o.uv = float2(o.uv.x, 1 - o.uv.y);
#endif
        return o;
    }
    ENDCG

    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always
        //0
        Pass
        {
            CGPROGRAM
            #pragma multi_compile DIRECTION_4 DIRECTION_6 DIRECTION_8
            #pragma multi_compile STEPS_4 STEPS_6 STEPS_8

            #if DIRECTION_4
                #define DIRECTION 4
            #elif DIRECTION_6   
                #define DIRECTION 6
            #elif DIRECTION_8
                #define DIRECTION 8
            #endif

            #if STEPS_4
                #define STEPS       4
            #elif STEPS_6
                #define STEPS       6
            #elif STEPS_8
                #define STEPS       8
            #endif

            #pragma vertex vert
            #pragma fragment hbao

            #include "HBAO.cginc"

       
            float4 frag (v2f i) : SV_Target
            {
                return float4(FetchViewPos(i.uv), 1);
            }
            ENDCG
        }
        //1
        Pass
        {
            Name "Composite"
            CGPROGRAM
            #pragma multi_compile __ ENABLEBLUR
            #pragma vertex vert
            #pragma fragment fragComposite

            half4 fragComposite (v2f i) : SV_Target
            {
                #if ENABLEBLUR
                    half4 ao = tex2D(_HbaoBlurTex, i.uv);
                #else
                    half4 ao = tex2D(_HbaoTex, i.uv);
                #endif
                half4 col = tex2D(_MainTex, i.uv);
                col.rgb *= ao.a;
                return col;
            }
            ENDCG
        }
         //2
        Pass
        {
            Name "Blur"
            CGPROGRAM
            #pragma multi_compile __ GUASSBLUR
            #pragma vertex vert
            #pragma fragment fragBlur

            #include "blur.cginc"
            ENDCG
        }

    }
}
