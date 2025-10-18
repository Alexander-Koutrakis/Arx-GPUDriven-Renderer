using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using Rendering.Data;

namespace Rendering
{
    public class RenderingPass : ScriptableRenderPass
    {
        private ModelData modelData;
        private Material renderMaterial;

        // Buffers    
        private ComputeBuffer vertexBuffer;
        private ComputeBuffer triangleBuffer;
        private ComputeBuffer materialBuffer;
        private ComputeBuffer visibleTrianglesBuffer;
        private ComputeBuffer drawArgsBuffer;

        // Shader property IDs (for efficiency)
        private int vertexBufferID;
        private int triangleBufferID;
        private int materialBufferID;
        private int visibleTrianglesBufferID;

        // Debug info
        private string profilerTag = "Custom Renderer Pass";

        public void SetupRenderPass(ModelData modelData,
            GPURendererFeature.CustomRendererSettings settings,
            ComputeBuffer[] computeBuffers,
            int[] bufferIDs,
            UnityEngine.Texture2DArray[] texture2DArrays)
        {
            renderMaterial = settings.renderMaterial;
            this.modelData = modelData;

            vertexBuffer = computeBuffers[0];
            triangleBuffer = computeBuffers[1];
            materialBuffer = computeBuffers[2];
            visibleTrianglesBuffer = computeBuffers[3];
            drawArgsBuffer = computeBuffers[4];

            vertexBufferID = bufferIDs[0];
            triangleBufferID = bufferIDs[1];
            materialBufferID = bufferIDs[2];
            visibleTrianglesBufferID = bufferIDs[3];

            if (texture2DArrays[0].depth > 0)
            {
                renderMaterial.SetTexture("_TexArray0", texture2DArrays[0]);
            }

            if (texture2DArrays[1].depth > 0)
            {
                renderMaterial.SetTexture("_TexArray1", texture2DArrays[1]);
            }

            if (texture2DArrays[2].depth > 0)
            {
                renderMaterial.SetTexture("_TexArray2", texture2DArrays[2]);
            }
            if (texture2DArrays[3].depth > 0)
            {
                renderMaterial.SetTexture("_TexArray3", texture2DArrays[3]);
            }
            if (texture2DArrays[4].depth > 0)
            {
                renderMaterial.SetTexture("_TexArray4", texture2DArrays[4]);
            }
            if (texture2DArrays[5].depth > 0)
            {
                renderMaterial.SetTexture("_TexArray5", texture2DArrays[5]);
            }
        }

        public void UpdateModelData(ModelData newModelData)
        {
            modelData = newModelData;
        }

      

        // Alternative: If you want to use RenderGraph (recommended for Unity 6)
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
           
            if (modelData == null || modelData.Triangles == null || modelData.Triangles.Length == 0|| renderMaterial == null)
            {
                return;
            }

            var resourceData = frameData.Get<UniversalResourceData>();

           // Then, add a raster pass for rendering
            using (var builder = renderGraph.AddRasterRenderPass<RenderPassData>(profilerTag, out var renderPassData))
            {
                
                renderPassData.renderMaterial = renderMaterial;
                renderPassData.vertexBuffer = vertexBuffer;
                renderPassData.triangleBuffer = triangleBuffer;
                renderPassData.materialBuffer = materialBuffer;
                renderPassData.visibleTrianglesBuffer = visibleTrianglesBuffer;
                renderPassData.drawArgsBuffer = drawArgsBuffer;

                // Set render targets
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);

                // Set execution function
                builder.SetRenderFunc((RenderPassData data, RasterGraphContext context) =>
                {
                    ExecuteRenderPass(data, context);
                });
            }
        }


        private class RenderPassData
        {
            public Material renderMaterial;
            public ComputeBuffer vertexBuffer;
            public ComputeBuffer triangleBuffer;
            public ComputeBuffer materialBuffer;
            public ComputeBuffer visibleTrianglesBuffer;
            public ComputeBuffer drawArgsBuffer;
        }



        private void ExecuteRenderPass(RenderPassData data, RasterGraphContext context)
        {
            var cmd = context.cmd;

            // Set buffers for render shader
            data.renderMaterial.SetBuffer(vertexBufferID, data.vertexBuffer);
            data.renderMaterial.SetBuffer(triangleBufferID, data.triangleBuffer);
            data.renderMaterial.SetBuffer(materialBufferID, data.materialBuffer);
            data.renderMaterial.SetBuffer(visibleTrianglesBufferID, data.visibleTrianglesBuffer);

            cmd.DrawProceduralIndirect(Matrix4x4.identity, data.renderMaterial, 0, MeshTopology.Triangles, data.drawArgsBuffer, 0);
        }



        public void Dispose()
        {
            
        }
    }
}