using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using Rendering.Data;

namespace Rendering
{

    public class CustomCullingPass : ScriptableRenderPass
    {
        private ModelData modelData;
        private ComputeShader cullingComputeShader;
        private RTHandle depthTexture;

        // Buffers    
        private ComputeBuffer triangleIndexBuffer;
        private ComputeBuffer cellBuffer;
        private ComputeBuffer triangleRenderingModeBuffer;

        private ComputeBuffer opaqueVisibleTrianglesBuffer;
        private ComputeBuffer opaqueDrawArgsBuffer;
        private ComputeBuffer transparentVisibleTrianglesBuffer;
        private ComputeBuffer transparentDrawArgsBuffer;


        private string profilerTag = "Custom Culling Pass";

        private bool isFirstFrame = true;

        public void SetupRenderPass(ModelData modelData,
            CustomRendererFeature.CustomCullingSettings settings,
            ComputeBuffer[] computeBuffers,
            RTHandle depthTextureHandle)
        {
            cullingComputeShader = settings.cullingComputeShader;
            this.modelData = modelData;
            depthTexture = depthTextureHandle;

            triangleIndexBuffer = computeBuffers[0];
            cellBuffer = computeBuffers[1];
            triangleRenderingModeBuffer = computeBuffers[2];
            opaqueVisibleTrianglesBuffer = computeBuffers[3];
            opaqueDrawArgsBuffer = computeBuffers[4];
            transparentVisibleTrianglesBuffer = computeBuffers[5];
            transparentDrawArgsBuffer = computeBuffers[6];
            isFirstFrame = true;
        }

        public void UpdateModelData(ModelData newModelData)
        {
            modelData = newModelData;
            
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Skip first frame since we don't have previous depth data yet
            if (isFirstFrame)
            {
                isFirstFrame = false;
                return;
            }
            
            // Get camera data from the frame data
            var cameraData = frameData.Get<UniversalCameraData>();
            
            // Skip Scene view cameras - only perform culling for Game cameras
            if (cameraData.camera.cameraType != CameraType.Game)
                return;
            
            // Enhanced validation
            if (modelData == null|| modelData.Cells == null || modelData.Cells.Length == 0||
                cullingComputeShader == null|| opaqueVisibleTrianglesBuffer == null || opaqueDrawArgsBuffer == null)
            {
                Debug.LogWarning("CustomCullingPass: ModelData is null");
                return;
            }
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            // Add compute pass for culling
            using (var builder = renderGraph.AddComputePass<CullingPassData>(profilerTag, out var cullingPassData))
            {
                cullingPassData.modelData = modelData;
                cullingPassData.cullingComputeShader = cullingComputeShader;
                cullingPassData.cellBuffer = cellBuffer;
                cullingPassData.triangleIndexBuffer = triangleIndexBuffer;
                cullingPassData.triangleRenderingModeBuffer = triangleRenderingModeBuffer;
                cullingPassData.opaqueVisibleTrianglesBuffer = opaqueVisibleTrianglesBuffer;
                cullingPassData.opaqueDrawArgsBuffer = opaqueDrawArgsBuffer;
                cullingPassData.transparentVisibleTrianglesBuffer = transparentVisibleTrianglesBuffer;
                cullingPassData.transparentDrawArgsBuffer = transparentDrawArgsBuffer;
                cullingPassData.camera = cameraData.camera;

                // Import the depth texture - it should contain previous frame's depth
                if (depthTexture != null && depthTexture.rt != null)
                {
                    cullingPassData.depthTexture = renderGraph.ImportTexture(depthTexture);
                    builder.UseTexture(cullingPassData.depthTexture, AccessFlags.Read);
                }
                else
                {
                    Debug.LogWarning("CustomCullingPass: Previous frame depth texture is null - occlusion culling will be disabled");
                    // Don't use current frame depth as fallback - that would be incorrect
                    return;
                }

                builder.AllowPassCulling(false);

                builder.SetRenderFunc((CullingPassData data, ComputeGraphContext context) =>
                {
                    ExecuteCullingPass(data, context);
                });
            }
        }

        private class CullingPassData
        {
            public ModelData modelData;
            public ComputeShader cullingComputeShader;
            public ComputeBuffer cellBuffer;
            public ComputeBuffer triangleIndexBuffer;
            public ComputeBuffer triangleRenderingModeBuffer;
            public ComputeBuffer opaqueVisibleTrianglesBuffer;
            public ComputeBuffer opaqueDrawArgsBuffer;
            public ComputeBuffer transparentVisibleTrianglesBuffer;
            public ComputeBuffer transparentDrawArgsBuffer;
            public Camera camera;
            public TextureHandle depthTexture;
        }

        private void ExecuteCullingPass(CullingPassData data, ComputeGraphContext context)
        {
            var cmd = context.cmd;

            // Add profiler sample
            using (new ProfilingScope(cmd, new ProfilingSampler("Custom Culling Compute")))
            {
                try
                {
                    // Validate compute shader kernel
                    string kernelName = "CSMain";
                    if (!data.cullingComputeShader.HasKernel(kernelName))
                    {
                        Debug.LogError($"CustomCullingPass: Compute shader doesn't have kernel '{kernelName}'");
                        return;
                    }

                    int kernelIndex = data.cullingComputeShader.FindKernel(kernelName);

                    // Set compute shader buffers
                    cmd.SetComputeBufferParam(data.cullingComputeShader, kernelIndex, "_CellBuffer", data.cellBuffer);
                    cmd.SetComputeBufferParam(data.cullingComputeShader, kernelIndex, "_TriangleIndexBuffer", data.triangleIndexBuffer);
                    cmd.SetComputeBufferParam(data.cullingComputeShader, kernelIndex, "_OpaqueVisibleTrianglesBuffer", data.opaqueVisibleTrianglesBuffer);
                    cmd.SetComputeBufferParam(data.cullingComputeShader, kernelIndex, "_TransparentVisibleTrianglesBuffer", data.transparentVisibleTrianglesBuffer);
                    cmd.SetComputeBufferParam(data.cullingComputeShader, kernelIndex, "_TriangleRenderingMode", data.triangleRenderingModeBuffer);
                    cmd.SetComputeTextureParam(data.cullingComputeShader, kernelIndex, "_OcclusionDepthTexture", data.depthTexture);
                    cmd.SetBufferCounterValue(data.opaqueVisibleTrianglesBuffer, 0);
                    cmd.SetBufferCounterValue(data.transparentVisibleTrianglesBuffer, 0);

                    float width = data.camera.pixelWidth;
                    float height = data.camera.pixelHeight;
                    int maxMipLevel=Mathf.FloorToInt(Mathf.Log(Mathf.Max(width, height), 2)) + 1;
                    // Set camera parameters
                    if (data.camera != null)
                    {
                        // Frustum planes
                        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(data.camera);
                        Vector4[] planeData = new Vector4[6];
                        for (int i = 0; i < 6; i++)
                        {
                            Plane p = planes[i];
                            planeData[i] = new Vector4(p.normal.x, p.normal.y, p.normal.z, p.distance);
                        }
                        cmd.SetComputeVectorArrayParam(data.cullingComputeShader, "_FrustumPlanes", planeData);

                        // Camera matrices for occlusion culling
                        Matrix4x4 viewMatrix = data.camera.worldToCameraMatrix;
                        Matrix4x4 projMatrix = data.camera.projectionMatrix;
                        Matrix4x4 viewProjMatrix = projMatrix * viewMatrix;
                        
                        cmd.SetComputeMatrixParam(data.cullingComputeShader, "_ViewProjMatrix", viewProjMatrix);
                        cmd.SetComputeVectorParam(data.cullingComputeShader, "_CameraPosition", data.camera.transform.position);
                        

                                                
                        
                        cmd.SetComputeVectorParam(data.cullingComputeShader, "_ScreenParams", 
                            new Vector4(width, height, maxMipLevel, 0));
                    }
                    else
                    {
                        Debug.LogWarning("CustomCullingPass: Camera is null, culling may not work correctly");
                    }

                    // Dispatch compute shader
                    int actualCellCount = data.modelData.Cells.Length;
                    int threadGroupSize = 64;
                    int threadGroups = Mathf.CeilToInt((float)actualCellCount / threadGroupSize);
                    threadGroups = Mathf.Max(1, threadGroups);

                    cmd.DispatchCompute(data.cullingComputeShader, kernelIndex, threadGroups, 1, 1);

                    // Copy the counter values to the draw args buffers
                    cmd.CopyCounterValue(data.opaqueVisibleTrianglesBuffer, data.opaqueDrawArgsBuffer, sizeof(uint));
                    cmd.CopyCounterValue(data.transparentVisibleTrianglesBuffer, data.transparentDrawArgsBuffer, sizeof(uint));
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"CustomCullingPass: Error during execution: {e.Message}");
                }
            }
        }


        public void Dispose()
        {
        }
    }
}