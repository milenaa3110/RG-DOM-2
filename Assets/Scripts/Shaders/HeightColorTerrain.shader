Shader "Custom/HeightColorTerrain" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _AmbientTint ("Ambient Tint", Color) = (0.4,0.4,0.4,1)
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.4
        _DiffuseStrength ("Diffuse Strength", Range(0, 1)) = 0.6
        _Shininess ("Shininess", Range(1, 128)) = 32
        _SpecularStrength ("Specular Strength", Range(0, 1)) = 0.5
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf PhongADS fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _AmbientTint;
        float _AmbientStrength;
        float _DiffuseStrength;
        float _Shininess;
        float _SpecularStrength;

        struct Input {
            float2 uv_MainTex;
            float3 worldPos;
            float4 color : COLOR;
        };

        void surf (Input IN, inout SurfaceOutput o) {
            fixed4 c = IN.color;
            o.Albedo = c.rgb;
            o.Specular = _Shininess / 128.0;
            o.Gloss = _SpecularStrength;
            o.Alpha = c.a;
        }

        inline half4 LightingPhongADS (SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
        {
            half NdotL = saturate(dot(s.Normal, lightDir));
            half3 diffuse = _LightColor0.rgb * s.Albedo * NdotL * _DiffuseStrength;
            half3 reflectionDir = reflect(-lightDir, s.Normal);
            half spec = pow(saturate(dot(viewDir, reflectionDir)), s.Specular * 128.0);
            half3 specular = _LightColor0.rgb * spec * s.Gloss;
            half3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * _AmbientTint.rgb * s.Albedo * _AmbientStrength;
            half3 color = ambient + (diffuse + specular) * atten;
            return half4(color, 1);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
