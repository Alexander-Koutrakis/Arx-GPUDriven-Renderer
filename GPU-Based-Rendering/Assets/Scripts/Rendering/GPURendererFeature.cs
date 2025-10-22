using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Rendering.Data;

namespace Rendering
{
    

    public class GPURendererFeature : ScriptableRendererFeature
    {
        // Singleton for easy access from debugger
        public static GPURendererFeature Instance { get; private set; }

        public ModelData modelData;
        [System.Serializable]
        public class CustomCullingSettings{

            public ComputeShader cullingComputeShader;
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        }

        [System.Serializable]
        public class CustomRendererSettings
        {
            public Material renderMaterial;
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        [System.Serializable]
        public class CopyDepthPassSettings
        {
            public RenderPassEvent depthPassEvent = RenderPassEvent.AfterRenderingOpaques;
            public ComputeShader DepthCopyShader;
            public ComputeShader MipMapGenerationShader;
            [HideInInspector]public string depthCopyTextureName = "_PreviousFrameDepthTexture";
        }

        // Buffers

        private ComputeBuffer vertexBuffer;
        private ComputeBuffer triangleBuffer;
        private ComputeBuffer materialBuffer;
        private ComputeBuffer triangleIndexBuffer;
        private ComputeBuffer triangleRenderingModeBuffer;
        private ComputeBuffer cellBuffer;

        private ComputeBuffer opaqueVisibleTrianglesBuffer;
        private ComputeBuffer opaqueDrawArgsBuffer;
        private ComputeBuffer transparentVisibleTrianglesBuffer;
        private ComputeBuffer transparentDrawArgsBuffer;

        private int vertexBufferID;
        private int triangleBufferID;
        private int triangleIndexBufferID;
        private int cellBufferID;
        private int materialBufferID;
        private int visibleTrianglesBufferID;

        UnityEngine.Texture2DArray unityTextureArray;
        UnityEngine.Texture2DArray unityTextureArray1;
        UnityEngine.Texture2DArray unityTextureArray2;
        UnityEngine.Texture2DArray unityTextureArray3;
        UnityEngine.Texture2DArray unityTextureArray4;
        UnityEngine.Texture2DArray unityTextureArray5;

        public CustomCullingSettings cullingSettings=new CustomCullingSettings();
        public CustomRendererSettings opaqueDrawingSettings = new CustomRendererSettings();
        public CustomRendererSettings trasnparentDrawingSettings = new CustomRendererSettings();
        public CopyDepthPassSettings copyDepthSettings=new CopyDepthPassSettings();
        private CullingPass cullingPass;
        private RenderingPass opaqueRenderPass;
        private RenderingPass transparentRenderPass;

        private CopyDepthPass copyDepthPass;
        private RTHandle copyDepthTextureHandle;

        /// <summary>
        /// Updates the model data used by this renderer
        /// </summary>
        /// <param name="newModelData">The new model data to use</param>
        public void UpdateModelData(ModelData newModelData)
        {
            if (opaqueRenderPass != null)
            {
                opaqueRenderPass.UpdateModelData(newModelData);
            }

            if (transparentRenderPass != null)
            {
                transparentRenderPass.UpdateModelData(newModelData);
            }
        }


        public override void Create()
        {
            // Set singleton instance
            Instance = this;

            copyDepthPass = new CopyDepthPass(copyDepthSettings);
            copyDepthPass.renderPassEvent = copyDepthSettings.depthPassEvent;

            ReleaseBuffers();
            ReleaseTextureArrays();
            
            SetupResources();

            CreateCullingPass();

            opaqueRenderPass=new RenderingPass();
            CreateOpaqueDrawingPass(opaqueRenderPass,opaqueDrawingSettings);

            transparentRenderPass=new RenderingPass();
            CreateTransparentDrawingPass(transparentRenderPass, trasnparentDrawingSettings);
        }
        private void CreateCullingPass()
        {
            ComputeBuffer[] computeBuffers = new ComputeBuffer[7]
            {
                triangleIndexBuffer,
                cellBuffer,
                triangleRenderingModeBuffer,
                opaqueVisibleTrianglesBuffer,
                opaqueDrawArgsBuffer,
                transparentVisibleTrianglesBuffer,
                transparentDrawArgsBuffer
            };

            cullingPass = new CullingPass();
            cullingPass.SetupRenderPass(modelData, cullingSettings, computeBuffers, copyDepthTextureHandle);
        }
        private void CreateOpaqueDrawingPass(RenderingPass renderPass,CustomRendererSettings settings)
        {
            ComputeBuffer[] computeBuffers = new ComputeBuffer[5]
          {
                vertexBuffer,
                triangleBuffer,
                materialBuffer,
                opaqueVisibleTrianglesBuffer,
                opaqueDrawArgsBuffer
          };

            int[] bufferIDs = new int[4]
            {
                vertexBufferID,
                triangleBufferID,
                materialBufferID,
                visibleTrianglesBufferID
            };

            UnityEngine.Texture2DArray[] texture2DArrays = new UnityEngine.Texture2DArray[6]
            {
                unityTextureArray,
                unityTextureArray1,
                unityTextureArray2,
                unityTextureArray3,
                unityTextureArray4,
                unityTextureArray5
            };

            renderPass.SetupRenderPass(modelData,settings, computeBuffers, bufferIDs, texture2DArrays);
        }
        private void CreateTransparentDrawingPass(RenderingPass renderPass, CustomRendererSettings settings)
        {
            ComputeBuffer[] computeBuffers = new ComputeBuffer[5]
          {
                vertexBuffer,
                triangleBuffer,
                materialBuffer,
                transparentVisibleTrianglesBuffer,
                transparentDrawArgsBuffer
          };

            int[] bufferIDs = new int[4]
            {
                vertexBufferID,
                triangleBufferID,
                materialBufferID,
                visibleTrianglesBufferID
            };

            UnityEngine.Texture2DArray[] texture2DArrays = new UnityEngine.Texture2DArray[6]
            {
                unityTextureArray,
                unityTextureArray1,
                unityTextureArray2,
                unityTextureArray3,
                unityTextureArray4,
                unityTextureArray5
            };

            renderPass.SetupRenderPass(modelData, settings, computeBuffers, bufferIDs, texture2DArrays);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(cullingPass);
            renderer.EnqueuePass(opaqueRenderPass);
            renderer.EnqueuePass(transparentRenderPass);
            renderer.EnqueuePass(copyDepthPass);
        }

        protected override void Dispose(bool disposing)
        {
            // Clear singleton instance
            if (Instance == this)
                Instance = null;

            opaqueRenderPass?.Dispose();
            opaqueRenderPass = null;

            transparentRenderPass?.Dispose();
            transparentRenderPass = null;

            cullingPass?.Dispose();
            cullingPass = null;
            
            ReleaseTextureArrays();
            ReleaseBuffers();
            ReleaseRTHandles();
        }

        public void SetupResources()
        {
            // Initialize buffers if we have model data
            if (modelData != null)
            {
                // Cache shader property IDs
                vertexBufferID = Shader.PropertyToID("_VertexBuffer");
                triangleBufferID = Shader.PropertyToID("_TriangleBuffer");
                materialBufferID = Shader.PropertyToID("_MaterialBuffer");
                visibleTrianglesBufferID = Shader.PropertyToID("_VisibleTrianglesBuffer");
                triangleIndexBufferID = Shader.PropertyToID("_TriangleIndexBufferID");
                cellBufferID = Shader.PropertyToID("_CellBuffer");

                int maxVertexCount = modelData.Vertices.Length;
                int maxTriangleCount = modelData.Triangles.Length;
                int maxMaterialCount = modelData.Materials.Length;
                int maxtriangleIndexCount = modelData.TriangleIndexes.Length;
                int maxCellCount = modelData.Cells.Length;
                InitializeBuffers(maxVertexCount, maxTriangleCount, maxMaterialCount, maxtriangleIndexCount, maxCellCount);
                UpdateBuffers(modelData);
                SetupTextureArrays();
                CreateDepthCopyRTHandle();
            }

           
        }

        private void CreateDepthCopyRTHandle()
        {

            var descriptor = new RenderTextureDescriptor(1920, 1080, RenderTextureFormat.RFloat, 0)
            {
                msaaSamples = 1,
                dimension = TextureDimension.Tex2D,
                useMipMap = true,
                autoGenerateMips = false,
                enableRandomWrite = true,  // Enable UAV usage for compute shader
                mipCount = 8 // Set the mip count from settings
            };

            copyDepthTextureHandle = RTHandles.Alloc(descriptor,
                name: copyDepthSettings.depthCopyTextureName);

            if (copyDepthTextureHandle != null)
            {
                var renderTexture = copyDepthTextureHandle.rt;
                renderTexture.wrapMode = TextureWrapMode.Clamp;
                
                copyDepthPass.Setup(copyDepthTextureHandle);
            }
        }


        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            Debug.Log("SetupRenderPasses called");
            // Ensure we have a depth copy texture with the right dimensions
            var cameraData = renderingData.cameraData;
            var descriptor = cameraData.cameraTargetDescriptor;

            Debug.Log($"Original descriptor: {descriptor.width}x{descriptor.height}, format: {descriptor.colorFormat}");
            // Create descriptor for depth texture copy
            descriptor.colorFormat = RenderTextureFormat.RFloat; // Single channel float for depth
            descriptor.depthBufferBits = 0; // No depth buffer needed for the copy
            descriptor.msaaSamples = 1; // No MSAA for the copy
            descriptor.enableRandomWrite = true; // Enable UAV usage for compute shader
            descriptor.useMipMap = true; // Enable mipmaps for hierarchical Z-buffer
            descriptor.autoGenerateMips = false; // We generate mipmaps manually
           

            Debug.Log($"Modified descriptor: {descriptor.width}x{descriptor.height}, format: {descriptor.colorFormat}");
            Debug.Log($"copyDepthTextureHandle before realloc: {(copyDepthTextureHandle?.rt != null ? "Valid" : "Null")}");


            RenderingUtils.ReAllocateHandleIfNeeded(ref copyDepthTextureHandle, descriptor,
                FilterMode.Point, TextureWrapMode.Clamp, name: copyDepthSettings.depthCopyTextureName);

            Debug.Log($"copyDepthTextureHandle after realloc: {(copyDepthTextureHandle?.rt != null ? "Valid" : "Null")}");

            if (copyDepthTextureHandle != null)
            {
                Debug.Log($"RTHandle created successfully: {copyDepthTextureHandle.name}");
                copyDepthPass.Setup(copyDepthTextureHandle);
            }
            else
            {
                Debug.LogError("Failed to create copyDepthTextureHandle!");
            }
        }

        void InitializeBuffers(int maxVertexCount, int maxTriangleCount, int maxMaterialCount, int maxTriangleIndexCount, int maxCellCount)
        {
            ReleaseBuffers();
            try
            {
                // Ensure we have valid counts
                maxVertexCount = Mathf.Max(1, maxVertexCount);
                maxTriangleCount = Mathf.Max(1, maxTriangleCount);
                maxMaterialCount = Mathf.Max(1, maxMaterialCount);
                maxTriangleIndexCount = Mathf.Max(1, maxTriangleIndexCount);
                maxCellCount = Mathf.Max(1, maxCellCount);

                // Create buffers with proper error handling
                vertexBuffer = new ComputeBuffer(maxVertexCount, System.Runtime.InteropServices.Marshal.SizeOf<Vertex>());
                triangleBuffer = new ComputeBuffer(maxTriangleCount, System.Runtime.InteropServices.Marshal.SizeOf<Triangle>());
                triangleRenderingModeBuffer = new ComputeBuffer(maxTriangleCount, sizeof(uint));
                materialBuffer = new ComputeBuffer(maxMaterialCount, System.Runtime.InteropServices.Marshal.SizeOf<MaterialData>());
                triangleIndexBuffer = new ComputeBuffer(maxTriangleIndexCount, sizeof(uint));
                cellBuffer = new ComputeBuffer(maxCellCount, System.Runtime.InteropServices.Marshal.SizeOf<Cell>());

                // Buffer for the culling results (indices of visible triangles)
                opaqueVisibleTrianglesBuffer = new ComputeBuffer(maxTriangleIndexCount, sizeof(uint), ComputeBufferType.Append);
                transparentVisibleTrianglesBuffer= new ComputeBuffer(maxTriangleIndexCount, sizeof(uint), ComputeBufferType.Append);
                // Draw arguments for indirect drawing
                opaqueDrawArgsBuffer = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);
                transparentDrawArgsBuffer= new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);

                uint[] opaqueArgs = new uint[4] { 3, 0, 0, 0 };
                uint[] transArgs = new uint[4] { 3, 0, 0, 0 };
                opaqueDrawArgsBuffer.SetData(opaqueArgs);
                transparentDrawArgsBuffer.SetData(transArgs);

                Debug.Log($"Buffers initialized successfully: {maxVertexCount} vertices, {maxTriangleCount} triangles");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to initialize buffers: {e.Message}");
                ReleaseBuffers(); // Clean up partial initialization
                throw;
            }
        }

        void SetupTextureArrays()
        {

            Rendering.Data.Texture2DArray[] TextureArrays = modelData.TextureArrays;
            int size = TextureArrays.Length;

            unityTextureArray = CreateUnityTextureArray(modelData.TextureArrays[0]);
            if (size <= 1) return;
            unityTextureArray1 = CreateUnityTextureArray(modelData.TextureArrays[1]);
            if (size <= 2) return;
            unityTextureArray2 = CreateUnityTextureArray(modelData.TextureArrays[2]);
            if (size <= 3) return;
            unityTextureArray3 = CreateUnityTextureArray(modelData.TextureArrays[3]);
            if (size <= 4) return;
            unityTextureArray4 = CreateUnityTextureArray(modelData.TextureArrays[4]);
            if (size <= 5) return;
            unityTextureArray5 = CreateUnityTextureArray(modelData.TextureArrays[5]);
        }

        void ReleaseTextureArrays()
        {
            if (unityTextureArray != null)
            {
                DestroyImmediate(unityTextureArray);
                unityTextureArray = null;
            }
            if (unityTextureArray1 != null)
            {
                DestroyImmediate(unityTextureArray1);
                unityTextureArray1 = null;
            }
            if (unityTextureArray2 != null)
            {
                DestroyImmediate(unityTextureArray2);
                unityTextureArray2 = null;
            }
            if (unityTextureArray3 != null)
            {
                DestroyImmediate(unityTextureArray3);
                unityTextureArray3 = null;
            }
            if (unityTextureArray4 != null)
            {
                DestroyImmediate(unityTextureArray4);
                unityTextureArray4 = null;
            }
            if (unityTextureArray5 != null)
            {
                DestroyImmediate(unityTextureArray5);
                unityTextureArray5 = null;
            }
        }

        UnityEngine.Texture2DArray CreateUnityTextureArray(Rendering.Data.Texture2DArray textureArrayData)
        {
            if (textureArrayData.Textures == null || textureArrayData.Textures.Length == 0)
                return null;

            Texture2D firstTexture = textureArrayData.Textures[0];
            int width = textureArrayData.size.width;
            int height = textureArrayData.size.height;
            TextureFormat format = firstTexture.format;
            bool mipChain = firstTexture.mipmapCount > 1;
            int arrayLength = textureArrayData.Textures.Length;

            UnityEngine.Texture2DArray unityArray = new UnityEngine.Texture2DArray(width, height, arrayLength, format, mipChain);

            for (int i = 0; i < arrayLength; i++)
            {
                if (mipChain)
                {
                    for (int mip = 0; mip < textureArrayData.Textures[i].mipmapCount; mip++)
                    {
                        Graphics.CopyTexture(textureArrayData.Textures[i], 0, mip, unityArray, i, mip);
                    }
                }
                else
                {
                    Graphics.CopyTexture(textureArrayData.Textures[i], 0, 0, unityArray, i, 0);
                }
            }

            unityArray.Apply(false);
            return unityArray;
        }

        void UpdateBuffers(ModelData modelData)
        {
            if (modelData == null || modelData.Vertices == null || modelData.Triangles == null || modelData.Materials == null)
            {
                Debug.LogWarning("Model data is null or incomplete, cannot update buffers");
                return;
            }

            // Get data from model
            Vertex[] vertices = modelData.Vertices;
            Triangle[] triangles = modelData.Triangles;
            uint[] triangleRendering = modelData.TriangleRenderingOrder;
            MaterialData[] materials = modelData.Materials;
            uint[] triangleIndexes = modelData.TriangleIndexes;
            Cell[] cells = modelData.Cells;

            // Update buffers with new data
            vertexBuffer.SetData(vertices);
            triangleBuffer.SetData(triangles);
            triangleRenderingModeBuffer.SetData(triangleRendering);
            materialBuffer.SetData(materials);
            triangleIndexBuffer.SetData(triangleIndexes);
            cellBuffer.SetData(cells);        
        }
        void ReleaseBuffers()
        {
            if (vertexBuffer != null) { 
                vertexBuffer?.Release();
                vertexBuffer = null;
            }


            if (triangleBuffer != null)
            {
                triangleBuffer?.Release();
                triangleBuffer = null;
            }

            if(triangleRenderingModeBuffer != null)
            {
                triangleRenderingModeBuffer?.Release();
                triangleRenderingModeBuffer = null;
            }

            if (materialBuffer != null)
            {
                materialBuffer?.Release();
                materialBuffer = null;
            }

            if (opaqueVisibleTrianglesBuffer != null)
            {
                opaqueVisibleTrianglesBuffer?.Release();
                opaqueVisibleTrianglesBuffer = null;
            }

            if (opaqueDrawArgsBuffer != null)
            {
                opaqueDrawArgsBuffer?.Release();
                opaqueDrawArgsBuffer = null;
            }

            if(transparentVisibleTrianglesBuffer != null)
            {
                transparentVisibleTrianglesBuffer?.Release();
                transparentVisibleTrianglesBuffer = null;
            }

            if(transparentDrawArgsBuffer != null)
            {
                transparentDrawArgsBuffer?.Release();
                transparentDrawArgsBuffer = null;
            }

            if (cellBuffer != null)
            {
                cellBuffer?.Release();
                cellBuffer = null;
            }

            if (triangleIndexBuffer != null)
            {
                triangleIndexBuffer?.Release();
                triangleIndexBuffer = null;
            }
        }

        private void ReleaseRTHandles()
        {
            copyDepthTextureHandle?.Release();
            copyDepthTextureHandle = null;
        }

        public void Dispose()
        {
            // Nothing to dispose here as buffers are managed by the renderer feature
        }
    }
}