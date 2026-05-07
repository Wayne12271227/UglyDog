Shader "Custom/ToonLitOutline"
{
    Properties
    {
        _Color ("Base Color", Color) = (1,1,1,1)
        _MainTex ("Base Map", 2D) = "white" {}
        _ShadowColor ("Shadow Color", Color) = (0.72,0.62,0.58,1)
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowSmoothness ("Shadow Smoothness", Range(0.001, 0.25)) = 0.05
        _RimColor ("Rim Color", Color) = (1,0.95,0.9,1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.2
        _OutlineColor ("Outline Color", Color) = (0.18,0.12,0.1,1)
        _OutlineWidth ("Outline Width", Range(0, 0.03)) = 0.006
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 250

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="Always" }
            Cull Front
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            float _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 expanded = v.vertex.xyz + normalize(v.normal) * _OutlineWidth;
                o.pos = UnityObjectToClipPos(float4(expanded, 1.0));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }

        CGPROGRAM
        #pragma surface surf ToonRamp fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _ShadowColor;
        fixed4 _RimColor;
        half _ShadowThreshold;
        half _ShadowSmoothness;
        half _RimPower;
        half _RimStrength;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
        };

        inline fixed4 LightingToonRamp(SurfaceOutput s, fixed3 lightDir, fixed3 viewDir, fixed atten)
        {
            fixed ndl = dot(s.Normal, lightDir) * 0.5h + 0.5h;
            fixed band = smoothstep(_ShadowThreshold - _ShadowSmoothness, _ShadowThreshold + _ShadowSmoothness, ndl);
            fixed3 litColor = lerp(_ShadowColor.rgb, s.Albedo, band);
            fixed rim = pow(1.0h - saturate(dot(normalize(viewDir), s.Normal)), _RimPower) * _RimStrength;
            litColor += _RimColor.rgb * rim * band;

            fixed4 c;
            c.rgb = litColor * _LightColor0.rgb * (atten * 2.0h);
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = tex.rgb;
            o.Alpha = tex.a;
        }
        ENDCG
    }

    Fallback "Diffuse"
}
