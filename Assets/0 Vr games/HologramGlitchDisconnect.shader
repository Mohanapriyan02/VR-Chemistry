Shader "Custom/HologramGlitchDisconnect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _HologramColor ("Hologram Color", Color) = (0, 1, 1, 1)
        _ScanLines ("Scan Line Density", Range(1, 100)) = 20
        _ScanSpeed ("Scan Speed", Range(0, 5)) = 1
        _ScanOffset ("Scan Line Offset", Range(0, 1)) = 0.5
        _GlitchIntensity ("Glitch Intensity", Range(0, 1)) = 0.4
        _GlitchSpeed ("Glitch Speed", Range(0, 10)) = 3
        _FresnelPower ("Edge Glow", Range(0, 10)) = 3
        _GlowIntensity ("Glow Intensity", Range(0, 10)) = 4
        _Alpha ("Transparency", Range(0, 1)) = 0.85
        _NoiseScale ("Noise Scale", Range(0.1, 10)) = 1
        _SignalWeakness ("Signal Weakness", Range(0, 1)) = 0.3
        _DisconnectChance ("Disconnect Chance", Range(0,1)) = 0.2

        // ✅ Just added this checkbox
        [Toggle] _DoubleSided ("Render Both Sides", Float) = 0
    }
    
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 300
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        // ✅ This line enables front/back toggle
        Cull [_DoubleSided]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _HologramColor;
            float _ScanLines;
            float _ScanSpeed;
            float _ScanOffset;
            float _GlitchIntensity;
            float _GlitchSpeed;
            float _FresnelPower;
            float _GlowIntensity;
            float _Alpha;
            float _NoiseScale;
            float _SignalWeakness;
            float _DisconnectChance;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                return o;
            }
            
            float random(float2 st)
            {
                return frac(sin(dot(st.xy, float2(12.9898, 78.233))) * 43758.5453123);
            }
            
            float noise(float2 st)
            {
                st *= _NoiseScale;
                float2 i = floor(st);
                float2 f = frac(st);
                
                float a = random(i);
                float b = random(i + float2(1.0, 0.0));
                float c = random(i + float2(0.0, 1.0));
                float d = random(i + float2(1.0, 1.0));
                
                float2 u = f * f * (3.0 - 2.0 * f);
                
                return lerp(a, b, u.x) + 
                       (c - a) * u.y * (1.0 - u.x) + 
                       (d - b) * u.x * u.y;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                
                // Scan lines
                float scan = sin((i.worldPos.y + _Time.y * _ScanSpeed) * _ScanLines + _ScanOffset);
                scan = (scan * 0.5 + 0.5) * 0.3 + 0.7;
                
                // Fresnel edge glow
                float3 viewDir = normalize(i.viewDir);
                float3 worldNormal = normalize(i.worldNormal);
                float fresnel = pow(1.0 - saturate(dot(viewDir, worldNormal)), _FresnelPower);
                float3 glowColor = fresnel * _HologramColor.rgb * _GlowIntensity * 3.0;

                // Base hologram color
                fixed4 finalColor = _HologramColor;
                finalColor.rgb *= scan;

                // Flicker dropout bands (disconnect effect)
                float band = step(0.9, frac(i.worldPos.y * 20.0 + _Time.y * 10.0));
                if (random(float2(_Time.y * 2.0, i.worldPos.y)) < _DisconnectChance)
                {
                    finalColor.rgb *= band; 
                    finalColor.a *= band;
                }

                // Horizontal slice offset glitch
                float glitchBand = frac(i.worldPos.y * 40.0 + _Time.y * _GlitchSpeed);
                if (glitchBand < 0.1 * _GlitchIntensity)
                {
                    i.uv.x += (random(float2(i.worldPos.y, _Time.y)) - 0.5) * 0.3;
                }

                // Color channel split glitch (RGB offset)
                float2 offset = float2(0.02 * _GlitchIntensity, 0);
                float r = tex2D(_MainTex, i.uv + offset).r;
                float g = tex2D(_MainTex, i.uv).g;
                float b = tex2D(_MainTex, i.uv - offset).b;
                float3 glitchRGB = float3(r, g, b);

                // Blend glitchy channels
                finalColor.rgb = lerp(finalColor.rgb, glitchRGB, _GlitchIntensity);

                // Signal flicker
                float flicker = 0.7 + 0.3 * sin(_Time.y * 15.0 + i.worldPos.y * 7.0);
                finalColor.rgb *= flicker;

                // Noise overlay
                float signalNoise = random(float2(_Time.y * 0.5, i.worldPos.x + i.worldPos.z));
                finalColor.rgb += signalNoise * _SignalWeakness * 0.3;

                // Add emissive glow
                finalColor.rgb += glowColor;

                // Alpha handling
                finalColor.a = _Alpha * (0.6 + fresnel * 0.4);
                finalColor.a *= tex.a;

                return finalColor;
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}
