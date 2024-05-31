Shader "Tarodev/CubeInstanced"
{
    Properties
    {
        _FarColor("Far color", Color) = (.2, .2, .2, 1)
    }
    SubShader
    {
        Pass
        {
            Tags
            {
                "RenderType"="Opaque"
                "RenderPipeline" = "UniversalRenderPipeline"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _FarColor;

            struct PhysObj
            {
                float4x4 mat;
                /*float3 position;
                float3 velocity;
                float radius;
                bool isStatic;
                uint gridIndex;*/
            };

            StructuredBuffer<PhysObj> physObjects;

            struct attributes
            {
                float3 normal : NORMAL;
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct varyings
            {
                float4 vertex : SV_POSITION;
                float3 diffuse : TEXCOORD2;
                float3 color : TEXCOORD3;
            };

            varyings vert(attributes v, const uint instance_id : SV_InstanceID)
            {
                varyings o;

                float4 pos = mul(physObjects[instance_id].mat, v.vertex);;

                
                o.vertex = mul(UNITY_MATRIX_VP, mul(unity_ObjectToWorld, pos));
                o.diffuse = saturate(dot(v.normal, _MainLightPosition.xyz));
                o.color = 1;

                return o;
            }

            half4 frag(const varyings i) : SV_Target
            {
                const float3 lighting = i.diffuse * 1.7;
                return half4(i.color * lighting, 1);;
            }
            ENDHLSL
        }
    }
}