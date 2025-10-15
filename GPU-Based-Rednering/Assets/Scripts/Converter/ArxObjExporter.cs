using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ArxConverter
{
    public static class ArxObjExporter
    {
        /// <summary>
        /// Exports ArxFTS data to OBJ format
        /// </summary>
        /// <param name="arxData">The Arx level data</param>
        /// <param name="outputPath">Path where to save the OBJ file</param>
        /// <param name="includeMaterials">Whether to generate an MTL file</param>
        /// <returns>True if export was successful</returns>
        public static bool ExportToObj(ArxFTS arxData, string outputPath, bool includeMaterials = true)
        {
            if (arxData == null || arxData.polygons == null || arxData.polygons.Length == 0)
            {
                Debug.LogError("No valid Arx data to export");
                return false;
            }

            try
            {
                string objFilePath = outputPath;
                string mtlFilePath = Path.ChangeExtension(outputPath, ".mtl");
                string mtlFileName = Path.GetFileName(mtlFilePath);

                // Create dictionary for materials
                Dictionary<int, string> materialNames = new Dictionary<int, string>();
                foreach (ArxTextureContainer tex in arxData.textureContainers)
                {
                    // Sanitize material name
                    string matName = SanitizeName(Path.GetFileNameWithoutExtension(tex.filename));
                    materialNames[tex.id] = matName;
                }

                // Use StringBuilder for performance
                StringBuilder objBuilder = new StringBuilder();

                // OBJ header
                objBuilder.AppendLine("# Arx Fatalis level geometry");
                objBuilder.AppendLine($"# Level: {arxData.header.levelIdx}");
                objBuilder.AppendLine($"# Exported: {DateTime.Now}");
                objBuilder.AppendLine($"# Total polygons: {arxData.polygons.Length}");
                objBuilder.AppendLine();

                // MTL reference
                if (includeMaterials)
                {
                    objBuilder.AppendLine($"mtllib {mtlFileName}");
                    objBuilder.AppendLine();
                }

                // Collect vertices, UVs, and normals
                List<Vector3> allVertices = new List<Vector3>();
                List<Vector2> allUVs = new List<Vector2>();
                List<Vector3> allNormals = new List<Vector3>();

                // Face definitions grouped by material
                Dictionary<int, List<string>> materialToFaces = new Dictionary<int, List<string>>();


                // Process all polygons
                foreach (ArxPolygon poly in arxData.polygons)
                {
                    // Skip hidden or nodraw polygons
                    if ((poly.flags & (ArxPolygonFlags.Hide | ArxPolygonFlags.NoDraw)) != 0)
                        continue;

                    int materialId = poly.textureContainerId;

                    // Initialize face list for this material if needed
                    if (!materialToFaces.ContainsKey(materialId))
                    {
                        materialToFaces[materialId] = new List<string>();
                    }

                    // Base indices for this polygon's vertices in global lists
                    int vertexStartIndex = allVertices.Count + 1; // OBJ is 1-indexed
                    int texCoordStartIndex = allUVs.Count + 1;
                    int normalStartIndex = allNormals.Count + 1;


                    if (poly.IsTransparent())
                    {
                        Vector3 normalDir = new Vector3(poly.norm.x, poly.norm.y, poly.norm.z).normalized;
                        float offset = 0.005f; // Very small offset
                        for (int j = 0; j < poly.vertices.Length; j++)
                        {
                            poly.vertices[j].x += normalDir.x * offset;
                            poly.vertices[j].y += normalDir.y * offset;
                            poly.vertices[j].z += normalDir.z * offset;
                        }
                    }

                    if (poly.IsQuad())
                    {
                        ArxVertex temp = poly.vertices[2];
                        poly.vertices[2]= poly.vertices[3];
                        poly.vertices[3]= temp;
                    }

                    // Add vertices, UVs, and normals for this polygon
                    for (int i = 0; i < poly.vertices.Length; i++)
                    {
                        // Skip 4th vertex if not a quad
                        if (i == 3 && !poly.IsQuad())
                            continue;

                        ArxVertex vertex = poly.vertices[i];
                        allVertices.Add(new Vector3(vertex.x, vertex.y, vertex.z));
                        allUVs.Add(new Vector2(vertex.u,1- vertex.v));

                        // Use vertex normals if available, otherwise use polygon normal
  
                        Vector3 normal = new Vector3(poly.norm.x, poly.norm.y, poly.norm.z);                      
                        allNormals.Add(normal);
                    }

                    // Build face definition
                    StringBuilder faceBuilder = new StringBuilder("f ");

                    if (!poly.IsQuad())
                    {

                        // Triangle face - just one face with 3 vertices
                        // Format: f v1/vt1/vn1 v2/vt2/vn2 v3/vt3/vn3
                        faceBuilder.Append($"{vertexStartIndex}/{texCoordStartIndex}/{normalStartIndex} ");
                        faceBuilder.Append($"{vertexStartIndex + 1}/{texCoordStartIndex + 1}/{normalStartIndex + 1} ");
                        faceBuilder.Append($"{vertexStartIndex + 2}/{texCoordStartIndex + 2}/{normalStartIndex + 2}");

                        // Add face to material group
                        materialToFaces[materialId].Add(faceBuilder.ToString());
                    }
                    else
                    {
                        // Quad face - split into two triangles

                        // First triangle (0-1-2)
                        StringBuilder firstTriangle = new StringBuilder("f ");
                        firstTriangle.Append($"{vertexStartIndex}/{texCoordStartIndex}/{normalStartIndex} ");
                        firstTriangle.Append($"{vertexStartIndex + 1}/{texCoordStartIndex + 1}/{normalStartIndex + 1} ");
                        firstTriangle.Append($"{vertexStartIndex + 2}/{texCoordStartIndex + 2}/{normalStartIndex + 2}");

                        // Add first triangle face to material group
                        materialToFaces[materialId].Add(firstTriangle.ToString());

                        // Second triangle (0-3-2)
                        StringBuilder secondTriangle = new StringBuilder("f ");
                        secondTriangle.Append($"{vertexStartIndex}/{texCoordStartIndex}/{normalStartIndex} ");
                        secondTriangle.Append($"{vertexStartIndex + 2}/{texCoordStartIndex + 2}/{normalStartIndex + 2} ");
                        secondTriangle.Append($"{vertexStartIndex + 3}/{texCoordStartIndex + 3}/{normalStartIndex + 3}");
                        // Add second triangle face to material group
                        materialToFaces[materialId].Add(secondTriangle.ToString());
                    }

                    // Add face to material group
                    materialToFaces[materialId].Add(faceBuilder.ToString());
                }

                // Write all vertices
                foreach (Vector3 v in allVertices)
                {
                    objBuilder.AppendLine($"v {v.x} {v.y} {v.z}");
                }
                objBuilder.AppendLine();

                // Write all texture coordinates
                foreach (Vector2 vt in allUVs)
                {
                    objBuilder.AppendLine($"vt {vt.x} {vt.y}");
                }
                objBuilder.AppendLine();

                // Write all normals
                foreach (Vector3 vn in allNormals)
                {
                    objBuilder.AppendLine($"vn {vn.x} {vn.y} {vn.z}");
                }
                objBuilder.AppendLine();

                // Write faces grouped by material
                foreach (var kvp in materialToFaces)
                {
                    int materialId = kvp.Key;
                    List<string> faces = kvp.Value;

                    if (materialNames.ContainsKey(materialId))
                    {
                        objBuilder.AppendLine($"usemtl {materialNames[materialId]}");
                        objBuilder.AppendLine($"g group_{materialNames[materialId]}");
                    }
                    else
                    {
                        objBuilder.AppendLine($"usemtl {materialId}");
                        objBuilder.AppendLine($"g group_{materialId}");
                    }

                    // Write all faces for this material
                    foreach (string face in faces)
                    {
                        objBuilder.AppendLine(face);
                    }
                    objBuilder.AppendLine();
                }

                // Write to file
                File.WriteAllText(objFilePath, objBuilder.ToString());

                Debug.Log($"OBJ export successful: {objFilePath}");
                if (includeMaterials)
                {
                    Debug.Log($"MTL export successful: {mtlFilePath}");
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error exporting OBJ: {e.Message}");
                return false;
            }
        }


        /// <summary>
        /// Sanitize name for OBJ/MTL format
        /// </summary>
        private static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "material";

            // Replace invalid characters
            return name.Replace(" ", "_")
                .Replace("[", "")
                .Replace("]", "")
                .Replace("(", "")
                .Replace(")", "")
                .Replace("/", "_")
                .Replace("\\", "_")
                .Replace(".", "_");
        }
    }
}