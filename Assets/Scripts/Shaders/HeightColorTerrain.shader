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
            // Manually adjust color to ensure it reaches true purple
            // Remap the color if needed - this allows vertex colors to stay as they are
            // but we can enhance the purple end if needed
            fixed4 c = IN.color;
            
            // Option to enhance purples if needed
            // float h, s, v;
            // Color.RGBToHSV(c, out h, out s, out v);
            // if (h > 0.7f) h = lerp(0.7f, 0.83f, (h - 0.7f) / 0.05f);
            // c = Color.HSVToRGB(h, s, v);
            
            o.Albedo = c.rgb;
            o.Specular = _Shininess / 128.0;
            o.Gloss = _SpecularStrength;
            o.Alpha = c.a;
        }

        inline half4 LightingPhongADS (SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
        {
            // Diffuse (Lambert)
            half NdotL = saturate(dot(s.Normal, lightDir));
            half3 diffuse = _LightColor0.rgb * s.Albedo * NdotL * _DiffuseStrength;
            
            // Specular (Phong)
            half3 reflectionDir = reflect(-lightDir, s.Normal);
            half spec = pow(saturate(dot(viewDir, reflectionDir)), s.Specular * 128.0);
            half3 specular = _LightColor0.rgb * spec * s.Gloss;
            
            // Ambient
            half3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * _AmbientTint.rgb * s.Albedo * _AmbientStrength;

            half3 color = ambient + (diffuse + specular) * atten;
            return half4(color, 1);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
