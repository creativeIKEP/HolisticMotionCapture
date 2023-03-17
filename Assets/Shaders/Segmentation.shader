Shader "Hidden/SelfieSegmentation/VirtualBackgroundVisuallizer"
{
    Properties
    {
        // Segmentation texture
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _inputImage;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 col = tex2D(_inputImage, i.uv).rgb;
                float mask = tex2D(_MainTex, i.uv).r;
                float3 back = 0;
                
                float3 middle = col < 0.5 ? 2 * back * col : 1 - 2 * (1 - back) * (1 - col);
                float3 rgb = lerp(back, middle, saturate(mask / 0.95));
                rgb = lerp(rgb, col, saturate(mask));
                return float4(rgb, 1);
            }
            ENDCG
        }
    }
}