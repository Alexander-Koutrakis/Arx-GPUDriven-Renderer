using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Rendering {

    public class TextureManager
    {
        // Structure to hold texture handle data
        [System.Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct TextureHandle
        {
            public ulong handle;        // Native texture pointer as handle
            public int width;           // Texture width
            public int height;          // Texture height
            public int format;          // Texture format as int
        }

        private List<Texture2D> textures;
        private ComputeBuffer textureHandleBuffer;
        private TextureHandle[] textureHandles;

        public TextureManager()
        {
            textures = new List<Texture2D>();
        }

        /// <summary>
        /// Add a texture and return its index
        /// </summary>
        public int AddTexture(Texture2D texture)
        {
            if (texture == null) return -1;

            textures.Add(texture);
            return textures.Count - 1;
        }

        /// <summary>
        /// Build the texture handle buffer for GPU access
        /// </summary>
        public void BuildTextureHandleBuffer()
        {
            if (textures.Count == 0) return;

            // Create texture handles array
            textureHandles = new TextureHandle[textures.Count];

            for (int i = 0; i < textures.Count; i++)
            {
                var texture = textures[i];
                textureHandles[i] = new TextureHandle
                {
                    handle = (ulong)texture.GetNativeTexturePtr().ToInt64(),
                    width = texture.width,
                    height = texture.height,
                    format = (int)texture.format
                };
            }

            // Create compute buffer for texture handles
            if (textureHandleBuffer != null)
                textureHandleBuffer.Release();

            textureHandleBuffer = new ComputeBuffer(textureHandles.Length, Marshal.SizeOf<TextureHandle>());
            textureHandleBuffer.SetData(textureHandles);

            Debug.Log($"Built texture handle buffer with {textureHandles.Length} textures");
        }

        /// <summary>
        /// Set the texture handle buffer to a material
        /// </summary>
        public void SetTextureHandleBuffer(Material material, string propertyName = "_TextureHandles")
        {
            if (textureHandleBuffer != null)
            {
                material.SetBuffer(propertyName, textureHandleBuffer);
            }
        }

        /// <summary>
        /// Set the texture handle buffer to a compute shader
        /// </summary>
        public void SetTextureHandleBuffer(ComputeShader computeShader, int kernelIndex, string propertyName = "_TextureHandles")
        {
            if (textureHandleBuffer != null)
            {
                computeShader.SetBuffer(kernelIndex, propertyName, textureHandleBuffer);
            }
        }

      

        public void Dispose()
        {
            textureHandleBuffer?.Release();
            textureHandleBuffer = null;
        }
    }

    // Enhanced material data structure to work with texture manager
    [System.Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct EnhancedMaterialData
    {
        public int albedoIndex;
        public int normalIndex;
        public float metallic;
        public float smoothness;
        public Vector4 color;
        public Vector4 textureScaleOffset; // xy = scale, zw = offset for UV manipulation
    }
}

