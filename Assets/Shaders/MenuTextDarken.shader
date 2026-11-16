Shader "UI/MenuTextDarken"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _InkColor ("Ink", Color) = (0.08,0.05,0.03,1)
        _Amount ("Amount", Range(0,1)) = 0
        _LumMin ("Lum Min", Range(0,1)) = 0.62
        _LumMax ("Lum Max", Range(0,1)) = 0.82
        _SatMax ("Sat Max", Range(0,1)) = 0.30
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _InkColor;
            float _Amount, _LumMin, _LumMax, _SatMax;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float mx = max(c.r, max(c.g, c.b));
                float mn = min(c.r, min(c.g, c.b));
                float lum = dot(c.rgb, float3(0.299, 0.587, 0.114));
                float sat = mx > 0.001 ? (mx - mn) / mx : 0.0;
                // acik renkli + dusuk doygunluklu pikseller = yazi/ikon (ahsap ve gokyuzu etkilenmez)
                float m = smoothstep(_LumMin, _LumMax, lum) * (1.0 - smoothstep(_SatMax, _SatMax + 0.12, sat));
                return fixed4(_InkColor.rgb, m * _Amount * i.color.a);
            }
            ENDCG
        }
    }
}
