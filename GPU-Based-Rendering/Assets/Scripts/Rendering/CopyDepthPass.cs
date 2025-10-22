using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using static Rendering.GPURendererFeature;

namespace Rendering
{
    public class CopyDepthPass : ScriptableRenderPass
    {
        private CopyDepthPassSettings m_Settings;
        private RTHandle depthCopyTexture;
        private ComputeShader depthCopyComputeShader;
        private ComputeShader depthMipGenComputeShader;

        private static readonly int depthCopyTextureId = Shader.PropertyToID("_PreviousFrameDepthTexture");

        public CopyDepthPass(CopyDepthPassSettings settings)
        {
            m_Settings = settings;
            
            // Load the depth copy compute shader
            depthCopyComputeShader = m_Settings.DepthCopyShader;
            if (depthCopyComputeShader == null)
            {
                Debug.LogError("CopyDepthPass: Could not find DepthCopy compute shader in Resources/Shaders/");
            }
            
            // Load the depth mipmap generation compute shader
            depthMipGenComputeShader = m_Settings.MipMapGenerationShader;
            if (depthMipGenComputeShader == null)
            {
                Debug.LogError("CopyDepthPass: Could not find DepthMipGen compute shader in Resources/Shaders/");
            }
        }

        public void Setup(RTHandle depthCopyTexture)
        {
            this.depthCopyTexture = depthCopyTexture;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (depthCopyTexture == null || depthCopyComputeShader == null || depthMipGenComputeShader == null)
                return;

            // Skip Scene view cameras - only copy depth for Game cameras
            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.camera.cameraType != CameraType.Game)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            using (var builder = renderGraph.AddComputePass<DepthCopyPassData>("Depth Copy Pass", out var passData))
            {
                passData.sourceDepthTexture = resourceData.activeDepthTexture;
                passData.depthCopyTexture = renderGraph.ImportTexture(depthCopyTexture);
                passData.depthCopyComputeShader = depthCopyComputeShader;
                passData.depthMipGenComputeShader = depthMipGenComputeShader;

                builder.UseTexture(passData.sourceDepthTexture, AccessFlags.Read);
                builder.UseTexture(passData.depthCopyTexture, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                
                builder.SetRenderFunc((DepthCopyPassData data, ComputeGraphContext context) =>
                {
                    var cmd = context.cmd;
                    
                    // First, copy the base depth texture (mip 0)
                    int copyKernelIndex = data.depthCopyComputeShader.FindKernel("CSMain");
                    
                    // Set textures for copy
                    cmd.SetComputeTextureParam(data.depthCopyComputeShader, copyKernelIndex, "_SourceDepthTexture", data.sourceDepthTexture);
                    cmd.SetComputeTextureParam(data.depthCopyComputeShader, copyKernelIndex, "_DestDepthTexture", data.depthCopyTexture, 0);
                    
                    // Get texture dimensions
                    var rtDesc = data.depthCopyTexture.GetDescriptor(renderGraph);
                    int width = rtDesc.width;
                    int height = rtDesc.height;
                    
                    // Dispatch copy for mip 0
                    int threadGroupsX = Mathf.CeilToInt(width / 8.0f);
                    int threadGroupsY = Mathf.CeilToInt(height / 8.0f);
                    cmd.DispatchCompute(data.depthCopyComputeShader, copyKernelIndex, threadGroupsX, threadGroupsY, 1);
                    
                    // Generate mipmaps using the mipmap generation shader
                    int mipGenKernelIndex = data.depthMipGenComputeShader.FindKernel("CSMain");
                    
                    // Calculate the actual number of mips to generate (limited by texture size and settings)
                    int maxMips = Mathf.FloorToInt(Mathf.Log(Mathf.Max(width, height), 2)) + 1;
                    
                    // Generate each mip level
                    int currentWidth = width;
                    int currentHeight = height;

                    for (int mip = 1; mip < maxMips; mip++)
                    {
                        int sourceMip = mip - 1;
                        int destMip = mip;
                        
                        // Source dimensions are the previous iteration's destination
                        int sourceMipWidth = currentWidth;
                        int sourceMipHeight = currentHeight;

                        // Destination should be ceil(source/2) to handle odd dimensions
                        int mipWidth = Mathf.Max(1, sourceMipWidth / 2);
                        int mipHeight = Mathf.Max(1, sourceMipHeight / 2);
                        
                        // Update current dimensions for next iteration
                        currentWidth = mipWidth;
                        currentHeight = mipHeight;

                        // Set the source mip level parameter
                        cmd.SetComputeIntParam(data.depthMipGenComputeShader, "_SourceMipLevel", sourceMip);
                        
                        // Set source and destination textures for mip generation
                        cmd.SetComputeTextureParam(data.depthMipGenComputeShader, mipGenKernelIndex, "_SourceDepthTexture", data.depthCopyTexture);
                        cmd.SetComputeTextureParam(data.depthMipGenComputeShader, mipGenKernelIndex, "_DestDepthTexture", data.depthCopyTexture, destMip);
                        
                        // Dispatch for this mip level
                        int mipThreadGroupsX = Mathf.CeilToInt(mipWidth / 8.0f);
                        int mipThreadGroupsY = Mathf.CeilToInt(mipHeight / 8.0f);
                        cmd.DispatchCompute(data.depthMipGenComputeShader, mipGenKernelIndex, mipThreadGroupsX, mipThreadGroupsY, 1);

                       }
                });
            }
        }

        private class DepthCopyPassData
        {
            public TextureHandle sourceDepthTexture;
            public TextureHandle depthCopyTexture;
            public ComputeShader depthCopyComputeShader;
            public ComputeShader depthMipGenComputeShader;
            public int mipCount;
        }
    }
}