using System;
using System.IO;
using UnityEngine;

namespace ArxConverter
{
    /// <summary>
    /// Main converter utility for Arx Fatalis level data
    /// </summary>
    public static class ArxConverter
    {
        /// <summary>
        /// Convert an Arx Fatalis JSON file to OBJ format
        /// </summary>
        /// <param name="jsonFilePath">Path to the JSON file</param>
        /// <param name="objFilePath">Path to save the OBJ file (if null, will use same name as JSON but with .obj extension)</param>
        /// <param name="includeMaterials">Whether to generate a MTL file</param>
        /// <returns>True if conversion was successful</returns>
        public static bool ConvertJsonToObj(string jsonFilePath, string objFilePath = null, bool includeMaterials = true)
        {
            try
            {
                // Validate input path
                if (!File.Exists(jsonFilePath))
                {
                    Debug.LogError($"Input file not found: {jsonFilePath}");
                    return false;
                }

                // Set default output path if not provided
                if (string.IsNullOrEmpty(objFilePath))
                {
                    objFilePath = Path.ChangeExtension(jsonFilePath, ".obj");
                }

                Debug.Log($"Converting {jsonFilePath} to {objFilePath}");

                // Load the Arx FTS data from JSON
                ArxFTS arxData = ArxJsonLoader.LoadFromJson(jsonFilePath);
                if (arxData == null)
                {
                    Debug.LogError("Failed to load Arx data from JSON file");
                    return false;
                }

                // Export to OBJ
                return ArxObjExporter.ExportToObj(arxData, objFilePath, includeMaterials);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error converting JSON to OBJ: {e.Message}");
                return false;
            }
        }


#if UNITY_EDITOR
        /// <summary>
        /// Utility function to export any Unity Mesh to OBJ format
        /// </summary>
        /// <param name="mesh">The mesh to export</param>
        /// <param name="outputPath">File path for the OBJ file</param>
        /// <returns>True if export was successful</returns>
        public static bool ExportMeshToObj(Mesh mesh, string outputPath)
        {
            if (mesh == null)
            {
                Debug.LogError("No mesh provided for export");
                return false;
            }

            try
            {
                // Get mesh data
                Vector3[] vertices = mesh.vertices;
                Vector2[] uvs = mesh.uv;
                Vector3[] normals = mesh.normals;
                int[] triangles = mesh.triangles;

                // Create OBJ string builder
                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                // Header
                sb.AppendLine("# Mesh export from Unity");
                sb.AppendLine($"# Vertices: {vertices.Length}");
                sb.AppendLine($"# Triangles: {triangles.Length / 3}");
                sb.AppendLine();

                // Vertices
                foreach (Vector3 v in vertices)
                {
                    sb.AppendLine($"v {v.x} {v.y} {v.z}");
                }
                sb.AppendLine();

                // UVs
                if (uvs != null && uvs.Length > 0)
                {
                    foreach (Vector2 uv in uvs)
                    {
                        sb.AppendLine($"vt {uv.x} {uv.y}");
                    }
                    sb.AppendLine();
                }

                // Normals
                if (normals != null && normals.Length > 0)
                {
                    foreach (Vector3 n in normals)
                    {
                        sb.AppendLine($"vn {n.x} {n.y} {n.z}");
                    }
                    sb.AppendLine();
                }

                // Faces
                sb.AppendLine("g MeshExport");
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    // OBJ is 1-indexed
                    int v1 = triangles[i] + 1;
                    int v2 = triangles[i + 1] + 1;
                    int v3 = triangles[i + 2] + 1;

                    // Format: f v1/vt1/vn1 v2/vt2/vn2 v3/vt3/vn3
                    if (uvs != null && uvs.Length > 0 && normals != null && normals.Length > 0)
                    {
                        sb.AppendLine($"f {v1}/{v1}/{v1} {v2}/{v2}/{v2} {v3}/{v3}/{v3}");
                    }
                    // Format: f v1//vn1 v2//vn2 v3//vn3 (no UVs)
                    else if (normals != null && normals.Length > 0)
                    {
                        sb.AppendLine($"f {v1}//{v1} {v2}//{v2} {v3}//{v3}");
                    }
                    // Format: f v1/vt1 v2/vt2 v3/vt3 (no normals)
                    else if (uvs != null && uvs.Length > 0)
                    {
                        sb.AppendLine($"f {v1}/{v1} {v2}/{v2} {v3}/{v3}");
                    }
                    // Format: f v1 v2 v3 (vertices only)
                    else
                    {
                        sb.AppendLine($"f {v1} {v2} {v3}");
                    }
                }

                // Write to file
                File.WriteAllText(outputPath, sb.ToString());
                Debug.Log($"Mesh exported to: {outputPath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error exporting mesh to OBJ: {e.Message}");
                return false;
            }
        }
#endif
    }
}