Shader "Custom/TriangleDrawerForwardOpaque"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TexArray0("Texture Array 0", 2DArray) = "" {}
        _TexArray1("Texture Array 1", 2DArray) = "" {}
        _TexArray2("Texture Array 2", 2DArray) = "" {}
        _TexArray3("Texture Array 3", 2DArray) = "" {}
        _TexArray4("Texture Array 4", 2DArray) = "" {}
        _TexArray5("Texture Array 5", 2DArray) = "" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            Cull Back
            ZWrite On
            ZTest LEqual
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0


            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            // Structures matching C# structs
            struct Vertex {
                float3 position;
                float3 normal;
                float2 uv;
                float4 tangent;
            };

            struct Triangle {
                uint vertexIndex1;
                uint vertexIndex2;
                uint vertexIndex3;
                uint materialIndex;
            };

            struct TextureSize{
                int width;
                int height;
            };

            struct Texture2DReference{
                TextureSize size;
                int arrayIndex;
                int textureIndex;
            };

            struct MaterialData {
                Texture2DReference albedo;
                Texture2DReference normal;
                float metallic;
                float smoothness;
                float4 color;
                uint alphaClip;
                float alphaThreshold;
            };

            // Buffers
            StructuredBuffer<Vertex> _VertexBuffer;
            StructuredBuffer<Triangle> _TriangleBuffer;
            StructuredBuffer<MaterialData> _MaterialBuffer;
            StructuredBuffer<uint> _VisibleTrianglesBuffer;
            
            TEXTURE2D_ARRAY(_TexArray0); SAMPLER(sampler_TexArray0);
            TEXTURE2D_ARRAY(_TexArray1); SAMPLER(sampler_TexArray1);
            TEXTURE2D_ARRAY(_TexArray2); SAMPLER(sampler_TexArray2);
            TEXTURE2D_ARRAY(_TexArray3); SAMPLER(sampler_TexArray3);
            TEXTURE2D_ARRAY(_TexArray4); SAMPLER(sampler_TexArray4);
            TEXTURE2D_ARRAY(_TexArray5); SAMPLER(sampler_TexArray5);
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 tangentWS:TEXCOORD3;
                uint materialIndex : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
                half fogFactor : TEXCOORD6;
            };

            Varyings vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                Varyings output = (Varyings)0;
                
                uint triangleIndex = _VisibleTrianglesBuffer[instanceID];
                Triangle tri = _TriangleBuffer[triangleIndex];
                
                uint vertexIndex;
                switch (vertexID)
                {
                    case 0: vertexIndex = tri.vertexIndex1; break;
                    case 1: vertexIndex = tri.vertexIndex2; break;
                    case 2: vertexIndex = tri.vertexIndex3; break;
                    default: vertexIndex = tri.vertexIndex1; break;
                }
                
                Vertex vertex = _VertexBuffer[vertexIndex];
                
                output.positionWS = vertex.position;
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = vertex.normal;
                output.uv = vertex.uv;
                output.tangentWS.xyz =normalize(vertex.tangent.xyz);
                output.tangentWS.w=vertex.tangent.w;
                output.materialIndex = tri.materialIndex;
                output.shadowCoord = TransformWorldToShadowCoord(output.positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float4 SampleTextureArray(int arrayIndex, float2 uv, int textureIndex)
            {
                switch(arrayIndex)
                {
                    case 0: return SAMPLE_TEXTURE2D_ARRAY(_TexArray0, sampler_TexArray0, uv, textureIndex);
                    case 1: return SAMPLE_TEXTURE2D_ARRAY(_TexArray1, sampler_TexArray1, uv, textureIndex);
                    case 2: return SAMPLE_TEXTURE2D_ARRAY(_TexArray2, sampler_TexArray2, uv, textureIndex);
                    case 3: return SAMPLE_TEXTURE2D_ARRAY(_TexArray3, sampler_TexArray3, uv, textureIndex);
                    case 4: return SAMPLE_TEXTURE2D_ARRAY(_TexArray4, sampler_TexArray4, uv, textureIndex);
                    case 5: return SAMPLE_TEXTURE2D_ARRAY(_TexArray5, sampler_TexArray5, uv, textureIndex);
                    default: return float4(1, 0, 1, 1);
                }
            }

           half4 frag(Varyings input) : SV_Target
        {
            MaterialData material = _MaterialBuffer[input.materialIndex];
    
            // Sample albedo
            float4 albedoAlpha = SampleTextureArray(material.albedo.arrayIndex, input.uv, material.albedo.textureIndex);
            float3 albedo = albedoAlpha.rgb * material.color.rgb;
            float alpha = albedoAlpha.a * material.color.a;
    
            // Alpha clipping
            if(material.alphaClip == 1)
            {
                clip(alpha - material.alphaThreshold);
            }

            // Normal calculation
            float3 normalWS = normalize(input.normalWS);         
            if(material.normal.arrayIndex != -1)
            {
                float4 normalSample = SampleTextureArray(material.normal.arrayIndex, input.uv, material.normal.textureIndex);
                half3 normalTS = UnpackNormal(normalSample);
        
                float3 tangentWS = normalize(input.tangentWS.xyz);
                float3 bitangentWS = normalize(cross(normalWS, tangentWS) * input.tangentWS.w);
                tangentWS = normalize(tangentWS - dot(tangentWS, normalWS) * normalWS);
                bitangentWS = cross(normalWS, tangentWS) * input.tangentWS.w;
        
                float3x3 tangentToWorld = float3x3(tangentWS, bitangentWS, normalWS);
                normalWS = normalize(mul(normalTS, tangentToWorld));
            }

            // Build SurfaceData
            SurfaceData surfaceData = (SurfaceData)0;
            surfaceData.albedo = albedo;
            surfaceData.metallic = saturate(material.metallic);
            surfaceData.smoothness = saturate(material.smoothness);
            surfaceData.normalTS = half3(0, 0, 1);
            surfaceData.emission = half3(0, 0, 0);
            surfaceData.occlusion = 1.0;
            surfaceData.alpha = alpha;
            surfaceData.clearCoatMask = 0.0;
            surfaceData.clearCoatSmoothness = 0.0;
            surfaceData.specular = half3(0.04, 0.04, 0.04);

            InputData inputData = (InputData)0;
            inputData.positionWS = input.positionWS;
            inputData.normalWS = normalWS;
            inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            inputData.shadowCoord = input.shadowCoord;
            inputData.fogCoord = input.fogFactor;
            inputData.vertexLighting = half3(0, 0, 0);
            inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            inputData.shadowMask = half4(1, 1, 1, 1);
            inputData.bakedGI = SampleSH(inputData.normalWS);
     
            half4 color = UniversalFragmentPBR(inputData, surfaceData);
            color.rgb = MixFog(color.rgb, inputData.fogCoord);
                
             return color;         
        }
            ENDHLSL
        }

    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}