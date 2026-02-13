Shader "Custom/SignImage_BlockXRay"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        _Cutoff ("Umbral de Recorte", Range(0,1)) = 0.5
        [HDR] _EmissionColor ("Color Emision", Color) = (0,0,0,0)
        
        // Debe coincidir con el valor del shader XRay
        [IntRange] _StencilRef ("Stencil Reference", Range(0, 255)) = 1
    }
    SubShader
    {
        // Renderizar ANTES que el XRay para escribir primero en el Stencil
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }

        Pass
        {
            Name "Main"
            ZTest LEqual
            ZWrite On
            Cull Off
            
            // Escribir en Stencil: bloquea el X-Ray donde se dibuja esta imagen
            Stencil
            {
                Ref [_StencilRef]
                Comp Always
                Pass Replace
            }
            
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
                
                // Iluminación básica
                float light = abs(dot(normalize(i.normal), float3(0,1,0))) * 0.5 + 0.5;
                float3 baseColor = col.rgb * light;
                
                // Añadir emisión (para highlight)
                float3 finalColor = baseColor + _EmissionColor.rgb;
                
                return float4(finalColor, col.a);
            }
            ENDCG
        }
    }
    FallBack "Transparent/Cutout/Diffuse"
}
