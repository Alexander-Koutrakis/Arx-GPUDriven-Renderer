using UnityEngine;
using System.Collections.Generic;


namespace Rendering.Data
{
    [System.Serializable]
    public struct Vertex
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector2 uv;
        public Vector4 tangent;
    }

    [System.Serializable]
    public struct Triangle 
    {

        public uint vertexIndex1;
        public uint vertexIndex2;
        public uint vertexIndex3;
        public uint materialIndex;
    }

    [System.Serializable]
    public struct MaterialData
    {
        public TextureReference albedo;
        public TextureReference normal;
        public float metallic;
        public float smoothness;
        public Vector4 color;
        public int alphaClip;
        public float alphaThreshold;
    }
    [System.Serializable]
    public struct TextureSize 
    {
        public int width;
        public int height;
    }
    [System.Serializable]
    public struct Texture2DArray
    {
        public TextureSize size;       
        public Texture2D[] Textures;
    }
    [System.Serializable]
    public struct TextureReference 
    {
        public TextureSize size;
        public int arrayReference;
        public int textureIndex;

    }
    [System.Serializable]
    public struct Cell 
    {
        public Vector3 min;
        public Vector3 max;
        public uint firstTriangleIndex;
        public uint count;
    }

}
