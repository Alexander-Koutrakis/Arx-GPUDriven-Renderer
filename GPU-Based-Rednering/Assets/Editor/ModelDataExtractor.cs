using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Threading.Tasks;

namespace Rendering.Data
{
    public enum RenderingType
    {
        Opaque,
        Transparent
    }

    public class ModelDataExtractor : MonoBehaviour
    {
        private List<Vertex> vertexDataList = new List<Vertex>();
        private List<Triangle> triangleDataList = new List<Triangle>();
        private List<uint> triangleRenderingOrderList = new List<uint>(); // Added this
        private List<MaterialData> materialDataList = new List<MaterialData>();
        private Dictionary<TextureDescriptor, List<Texture2D>> textureGroups = new Dictionary<TextureDescriptor, List<Texture2D>>();
        private List<Texture2DArray> textureArrays = new List<Texture2DArray>();
        public GameObject target;
        private int vertexOffset = 0;

        public async Task ExtractData()
        {

            vertexDataList.Clear();
            triangleDataList.Clear();
            triangleRenderingOrderList.Clear(); // Clear the new list
            materialDataList.Clear();
            textureArrays.Clear();
            textureGroups.Clear();
            vertexOffset = 0;
            MeshFilter[] meshFilters = target.GetComponentsInChildren<MeshFilter>();
            MeshRenderer[] meshRenderers = target.GetComponentsInChildren<MeshRenderer>();

            for (int i = 0; i < meshRenderers.Length; i++)
            {
                CollectTextures(meshRenderers[i]);
            }
            BuildTextureArrays();
            for (int i = 0; i < meshFilters.Length; i++)
            {
                ExtractModelData(meshFilters[i]);
                await Task.Yield();
            }


            CreateScriptableObject();
        }

        private void CollectTextures(MeshRenderer meshRenderer)
        {
            Material[] materials = meshRenderer.sharedMaterials;
            foreach (var material in meshRenderer.sharedMaterials)
            {
                TryAddTexture(material, "_MainTex");
                TryAddTexture(material, "_BumpMap");
            }
        }

        private void ExtractModelData(MeshFilter targetModel)
        {

            if (targetModel == null || targetModel.sharedMesh == null)
            {
                Debug.LogError("No mesh assigned to MeshFilter!");
                return;
            }
            int materialResult = CreateMaterialData(targetModel);
            if (materialResult < 0) return;

            uint materialDataIndex = (uint)materialResult;

            // Get the material to determine rendering type
            MeshRenderer renderer = targetModel.GetComponent<MeshRenderer>();
            Material material = renderer.sharedMaterial;
            uint renderingType = IsMatOpaque(material) ? 0u : 1u; // 0 for opaque, 1 for transparent

            Mesh mesh = targetModel.sharedMesh;
            mesh.RecalculateTangents();
            Transform modelTransform = targetModel.transform;
            // Create and populate vertex data list
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            Vector4[] tangents = mesh.tangents;

            Matrix4x4 localToWorld = modelTransform.localToWorldMatrix;
            Matrix4x4 worldToLocal = modelTransform.worldToLocalMatrix;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 worldPos = localToWorld.MultiplyPoint3x4(vertices[i]);

                Matrix4x4 normalMatrix = localToWorld.inverse.transpose;
                Vector3 worldNormal = normalMatrix.MultiplyVector(normals[i]).normalized;

                Vector3 localTangent = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                Vector3 worldTangent = normalMatrix.MultiplyVector(localTangent).normalized;
                worldTangent = new Vector4(worldTangent.x, worldTangent.y, worldTangent.z, tangents[i].w);

                Vertex vertexData = new Vertex
                {
                    position = worldPos,
                    normal = worldNormal,
                    uv = uvs[i],
                    tangent = worldTangent
                };
                vertexDataList.Add(vertexData);
            }
            int[] indices = mesh.GetTriangles(0);

            for (int i = 0; i < indices.Length; i += 3)
            {

                Triangle triangleData = new Triangle
                {
                    materialIndex = materialDataIndex,
                    vertexIndex1 = (uint)(indices[i] + vertexOffset),
                    vertexIndex2 = (uint)(indices[i + 1] + vertexOffset),
                    vertexIndex3 = (uint)(indices[i + 2] + vertexOffset)
                };
                triangleDataList.Add(triangleData);

                // Add the rendering type for this triangle
                triangleRenderingOrderList.Add(renderingType);
            }

            Debug.Log($"Model extraction complete: {vertexDataList.Count} vertices, {triangleDataList.Count} triangles");

            vertexOffset += mesh.vertices.Length;

        }

        private bool IsMatOpaque(Material material)
        {
            // Check for URP/HDRP surface type
            if (material.HasProperty("_Surface"))
            {
                return material.GetFloat("_Surface") == 0.0f; // 0 = Opaque, 1 = Transparent
            }

            // Check for Built-in render pipeline
            if (material.HasProperty("_Mode"))
            {
                float mode = material.GetFloat("_Mode");
                return mode == 0.0f; // 0 = Opaque, 1 = Cutout, 2 = Fade, 3 = Transparent
            }

            // Check rendering queue as fallback
            int renderQueue = material.renderQueue;
            return renderQueue <= 2500; // Geometry queue and below are typically opaque
        }

        private int CreateMaterialData(MeshFilter targetModel)
        {
            MeshRenderer renderer = targetModel.GetComponent<MeshRenderer>();
            Material material = renderer.sharedMaterial;


            Texture2D albedoTex = material.GetTexture("_MainTex") as Texture2D;
            Texture2D normalTex = material.GetTexture("_BumpMap") as Texture2D;

            TextureSize zeroSize = new TextureSize
            {
                width = 0,
                height = 0
            };
            TextureReference defaultReference = new TextureReference
            {
                size = zeroSize,
                arrayReference = -1,
                textureIndex = -1
            };

            MaterialData materialData = new MaterialData
            {
                albedo = albedoTex ? GetTextureReference(albedoTex) : defaultReference,
                normal = normalTex ? GetTextureReference(normalTex) : defaultReference,
                metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f,
                smoothness = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.5f,
                color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white,
                alphaClip = Mathf.RoundToInt(material.GetFloat("_AlphaClip")),
                alphaThreshold = material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") : 0.5f
            };

            if (materialDataList.Contains(materialData))
            {
                return materialDataList.IndexOf(materialData);
            }
            else
            {
                materialDataList.Add(materialData);
                return (materialDataList.Count - 1);
            }
        }

        private void TryAddTexture(Material material, string property)
        {

            if (material.HasProperty(property) && material.GetTexture(property) is Texture2D tex2D)
            {
                TextureDescriptor desc = new TextureDescriptor
                {
                    width = tex2D.width,
                    height = tex2D.height,
                    format = tex2D.format
                };

                if (!textureGroups.TryGetValue(desc, out var list))
                {
                    list = new List<Texture2D>();
                    textureGroups[desc] = list;
                }

                if (!list.Contains(tex2D))
                {
                    list.Add(tex2D);
                }
            }
        }

        private TextureReference GetTextureReference(Texture2D texture)
        {
            for (int i = 0; i < textureArrays.Count; i++)
            {
                Texture2DArray array = textureArrays[i];

                for (int j = 0; j < array.Textures.Length; j++)
                {
                    if (array.Textures[j] == texture)
                    {
                        return new TextureReference
                        {
                            size = array.size,
                            arrayReference = i,
                            textureIndex = j
                        };
                    }
                }
            }

            return new TextureReference { textureIndex = -1 }; // fallback
        }

        private void BuildTextureArrays()
        {
            textureArrays.Clear();
            foreach (var kvp in textureGroups)
            {
                TextureDescriptor textureDescriptor = kvp.Key;
                TextureSize textureSize = new TextureSize
                {
                    width = textureDescriptor.width,
                    height = textureDescriptor.height,
                };
                Texture2DArray array = new Texture2DArray
                {
                    size = textureSize,
                    Textures = kvp.Value.ToArray()
                };
                textureArrays.Add(array);
            }


        }

        private async void CreateScriptableObject()
        {
            Grid spatialGrid = new Grid();
            spatialGrid.CreateGrid(vertexDataList.ToArray(), triangleDataList.ToArray(), new Vector3Int(48, 16, 64));


            ModelData modelDataAsset = ScriptableObject.CreateInstance<ModelData>();
            modelDataAsset.Vertices = vertexDataList.ToArray();
            modelDataAsset.Triangles = triangleDataList.ToArray();
            modelDataAsset.TriangleRenderingOrder = triangleRenderingOrderList.ToArray(); // Add this line
            modelDataAsset.Materials = materialDataList.ToArray();
            modelDataAsset.TextureArrays = textureArrays.ToArray();
            modelDataAsset.Cells = spatialGrid.Cells;
            modelDataAsset.TriangleIndexes = spatialGrid.TriangleIndices;




            string path = "Assets/Resources/ModelData/ModelData.asset";
            AssetDatabase.CreateAsset(modelDataAsset, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("ModelData ScriptableObject created at: " + path);
        }

    }


    public struct TextureDescriptor
    {
        public int width;
        public int height;
        public TextureFormat format;

        public override bool Equals(object obj)
        {
            if (!(obj is TextureDescriptor)) return false;
            TextureDescriptor other = (TextureDescriptor)obj;
            return width == other.width && height == other.height && format == other.format;
        }

        public override int GetHashCode()
        {
            return width ^ height ^ (int)format;
        }
    }
}