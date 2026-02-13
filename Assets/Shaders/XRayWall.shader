Shader "Custom/XRay_Pro_Distance"
{
    Properties
    {
        _MainTex ("Textura de la Señal", 2D) = "white" {}
        [HDR] _XRayColor ("Color X-Ray Base", Color) = (0, 1, 0, 1)
        [HDR] _EmissionColor ("Color Emision (Highlight)", Color) = (0,0,0,0)
        _Cutoff ("Umbral de Recorte", Range(0,1)) = 0.5
        _Opacity ("Opacidad Maxima XRay", Range(0, 1)) = 0.5
        
        // --- CONTROLES DE DISTANCIA ---
        _DistMin ("Distancia Inicio (Invisible)", Float) = 10.0
        _DistMax ("Distancia Fin (Visible)", Float) = 20.0
        
        // --- CONTROL DE HIGHLIGHT ---
        _HighlightEnabled ("Highlight Habilitado", Float) = 1
        
        // NOTA: Se eliminó _IsSignImage para unificar comportamiento
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest+1" "RenderType"="TransparentCutout" }

        // --- PASE 1: NORMAL (VISIBLE) ---
        Pass
        {
            Name "Normal"
            ZTest LEqual
            ZWrite On
            Cull Off
            
            // ELIMINADO STENCIL: Ahora la capa normal no intenta bloquear el XRay del otro objeto
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing 
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Cutoff;
            float4 _EmissionColor;
            float _HighlightEnabled;

            v2f vert (appdata v) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 col = tex2D(_MainTex, i.uv);
                clip(col.a - _Cutoff);
                float light = abs(dot(normalize(i.normal), float3(0,1,0))) * 0.5 + 0.5;
                float3 baseColor = col.rgb * light;
                
                float3 highlightColor = _EmissionColor.rgb * _HighlightEnabled;
                float3 finalColor = baseColor + highlightColor;
                
                return float4(finalColor, col.a);
            }
            ENDCG
        }

        // --- PASE 2: X-RAY (FANTASMA / OVERLAY) ---
        Pass
        {
            Name "XRay"
            ZTest Greater // Dibuja SOLO si está detrás de algo
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            // ELIMINADO STENCIL CHECK: Poste y señal se dibujan libremente en modo XRay
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _XRayColor;
            float4 _EmissionColor;
            float _Cutoff;
            float _Opacity;
            float _DistMin;
            float _DistMax;
            float _HighlightEnabled;

            v2f vert (appdata v) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 col = tex2D(_MainTex, i.uv);
                clip(col.a - _Cutoff); 

                float dist = distance(_WorldSpaceCameraPos, i.worldPos);
                float fadeFactor = smoothstep(_DistMin, _DistMax, dist);

                float highlightPower = max(_EmissionColor.r, max(_EmissionColor.g, _EmissionColor.b));
                float hasHighlight = step(0.1, highlightPower) * _HighlightEnabled;
                
                float3 displayColor = lerp(_XRayColor.rgb, _EmissionColor.rgb, hasHighlight);
                
                float baseOpacity = _Opacity * fadeFactor;
                float highlightOpacity = 0.85; 
                float finalOpacity = lerp(baseOpacity, highlightOpacity, hasHighlight);

                return fixed4(displayColor, finalOpacity);
            }
            ENDCG
        }
    }
    FallBack "Transparent/Cutout/Diffuse"
}