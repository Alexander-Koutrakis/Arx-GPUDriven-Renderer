using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ArxConverter
{
    public static class ArxMeshCreator
    {
        /// <summary>
        /// Creates Unity mesh objects from ArxFTS data, organizing by cell and material
        /// </summary>
        /// <param name="arxData">The Arx level data</param>
        /// <returns>Root GameObject containing cell hierarchy</returns>
        public static GameObject CreateMeshes(ArxFTS arxData)
        {
            if (arxData == null || arxData.polygons == null || arxData.cells == null)
            {
                Debug.LogError("No valid Arx data to process");
                return null;
            }

            // Create root game object for the level
            GameObject rootObject = new GameObject("ArxLevel_" + arxData.header.levelIdx);
            int processedCells = 0;
            int totalPolygons = 0;

            // Create material dictionary
            Dictionary<int, Material> materials = new Dictionary<int, Material>();
            foreach (ArxTextureContainer texContainer in arxData.textureContainers)
            {
                // Create a standard material (you'll need to load actual textures)
                Material mat = new Material(Shader.Find("Standard"));
                mat.name = Path.GetFileNameWithoutExtension(texContainer.filename);
                materials[texContainer.id] = mat;
            }

            // Process each cell
            for (int cellIdx = 0; cellIdx < arxData.cells.Length; cellIdx++)
            {
                ArxCell cell = arxData.cells[cellIdx];

                // Skip empty cells
                if (cell.polygonIndices == null || cell.polygonIndices.Length == 0)
                    continue;

                // Create cell object
                GameObject cellObject = new GameObject($"Cell_{cellIdx}");
                cellObject.transform.SetParent(rootObject.transform, false);

                // Group polygons by material
                Dictionary<int, List<int>> materialToPolygons = new Dictionary<int, List<int>>();

                // First pass: group polygons by material
                foreach (int polyIdx in cell.polygonIndices)
                {
                    if (polyIdx < 0 || polyIdx >= arxData.polygons.Length)
                        continue;

                    ArxPolygon poly = arxData.polygons[polyIdx];

                    // Skip hidden or nodraw polygons
                    if ((poly.flags & (ArxPolygonFlags.Hide | ArxPolygonFlags.NoDraw)) != 0)
                        continue;

                    int materialId = poly.textureContainerId;

                    if (!materialToPolygons.ContainsKey(materialId))
                        materialToPolygons[materialId] = new List<int>();

                    materialToPolygons[materialId].Add(polyIdx);
                }

                // No valid polygons in this cell after filtering
                if (materialToPolygons.Count == 0)
                    continue;

                processedCells++;

                // Second pass: create mesh for each material
                foreach (var kvp in materialToPolygons)
                {
                    int materialId = kvp.Key;
                    List<int> polyIndices = kvp.Value;

                    // Get material
                    Material material = null;
                    if (materials.ContainsKey(materialId))
                    {
                        material = materials[materialId];
                    }
                    else
                    {
                        // Create default material if no matching texture container
                        material = new Material(Shader.Find("Standard"));
                        material.name = $"Material_{materialId}";
                    }

                    // Create gameobject for this submesh
                    string meshName = $"Mesh_{materialId}_{material.name}";
                    GameObject meshObject = new GameObject(meshName);
                    meshObject.transform.SetParent(cellObject.transform, false);

                    // Add mesh components
                    MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
                    MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();

                    // Set material
                    meshRenderer.sharedMaterial = material;

                    // Create mesh
                    Mesh mesh = new Mesh();
                    mesh.name = meshName;

                    List<Vector3> vertices = new List<Vector3>();
                    List<Vector2> uvs = new List<Vector2>();
                    List<Vector3> normals = new List<Vector3>();
                    List<int> triangles = new List<int>();

                    // Process each polygon for this material
                    foreach (int polyIdx in polyIndices)
                    {
                        ArxPolygon poly = arxData.polygons[polyIdx];
                        totalPolygons++;

                        // Get base vertex index
                        int baseVertexIndex = vertices.Count;

                        // Add vertices
                        for (int i = 0; i < poly.vertices.Length; i++)
                        {
                            // Skip 4th vertex if not a quad
                            if (i == 3 && !poly.IsQuad())
                                continue;

                            ArxVertex vertex = poly.vertices[i];
                            vertices.Add(vertex.ToVector3());
                            uvs.Add(vertex.ToUV());

                            // Use vertex normals if available, otherwise use polygon normal
                            if (poly.normals != null && i < poly.normals.Length)
                            {
                                normals.Add(poly.normals[i].ToVector3());
                            }
                            else
                            {
                                normals.Add(poly.norm.ToVector3());
                            }
                        }

                        // Add triangles
                        if (!poly.IsQuad())
                        {
                            // Triangle
                            triangles.Add(baseVertexIndex);
                            triangles.Add(baseVertexIndex + 1);
                            triangles.Add(baseVertexIndex + 2);
                        }
                        else
                        {
                            // Quad - split into two triangles
                            triangles.Add(baseVertexIndex);
                            triangles.Add(baseVertexIndex + 1);
                            triangles.Add(baseVertexIndex + 3);

                            triangles.Add(baseVertexIndex);
                            triangles.Add(baseVertexIndex + 3);
                            triangles.Add(baseVertexIndex + 2);
                        }
                    }

                    // Set mesh data
                    mesh.SetVertices(vertices);
                    mesh.SetUVs(0, uvs);
                    mesh.SetNormals(normals);
                    mesh.SetTriangles(triangles, 0);

                    // Finalize mesh
                    mesh.RecalculateBounds();

                    // Add collider based on polygon flags
                    bool hasCollision = polyIndices.Any(idx =>
                        (arxData.polygons[idx].flags & ArxPolygonFlags.NoCollision) == 0);

                    if (hasCollision)
                    {
                        MeshCollider collider = meshObject.AddComponent<MeshCollider>();
                        collider.sharedMesh = mesh;
                    }

                    // Assign mesh to filter
                    meshFilter.sharedMesh = mesh;
                }
            }

            Debug.Log($"Created mesh hierarchy with {processedCells} cells and {totalPolygons} polygons");
            return rootObject;
        }
    }
}