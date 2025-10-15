using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rendering.Data
{
    [CreateAssetMenu]
    public class ModelData : ScriptableObject
    {

        [HideInInspector] public Vertex[] Vertices;
        [HideInInspector] public Triangle[] Triangles;
        public uint[] TriangleRenderingOrder;//added this 0 for opaques 1 for transparent
        [HideInInspector] public MaterialData[] Materials;
        [HideInInspector] public Texture2DArray[] TextureArrays;
        [HideInInspector] public Cell[] Cells;
        [HideInInspector] public uint[] TriangleIndexes;

    }




}
