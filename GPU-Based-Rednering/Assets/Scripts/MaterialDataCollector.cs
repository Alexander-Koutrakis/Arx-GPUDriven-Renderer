using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MaterialDataCollector : MonoBehaviour
{
    [SerializeField] private Texture[] textures;
    public MeshRenderer meshRenderer;
    private List<int> albedoTextureIndices = new List<int>();
    private List<int> normalTextureIndices = new List<int>();

    private void Start()
    {
        CollectTextureIndices();
    }
    public void CollectTextureIndices()
    {
        albedoTextureIndices.Clear();
        normalTextureIndices.Clear();

        if (meshRenderer == null || textures == null) return;

        foreach (var material in meshRenderer.sharedMaterials)
        {
            // Albedo (_MainTex)
            if (material.HasProperty("_MainTex"))
            {
                Texture albedo = material.GetTexture("_MainTex");
                int index = System.Array.IndexOf(textures, albedo);
                if (index >= 0)
                {
                    albedoTextureIndices.Add(index);
                }
            }

            // Normal Map (_BumpMap)
            if (material.HasProperty("_BumpMap"))
            {
                Texture normal = material.GetTexture("_BumpMap");
                int index = System.Array.IndexOf(textures, normal);
                if (index >= 0)
                {
                    normalTextureIndices.Add(index);
                }
            }
        }
    }
}

