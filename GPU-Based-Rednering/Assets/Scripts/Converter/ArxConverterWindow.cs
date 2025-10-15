#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace ArxConverter
{
    public class ArxConverterWindow : EditorWindow
    {
        private string jsonFilePath = "";
        private string objFilePath = "";
        private bool includeMaterials = true;
        private bool importToScene = false;
        private Material defaultMaterial;
        private Vector3 scale = Vector3.one;

        [MenuItem("Tools/Arx Fatalis/Converter")]
        public static void ShowWindow()
        {
            ArxConverterWindow window = GetWindow<ArxConverterWindow>("Arx Converter");
            window.minSize = new Vector2(400, 350);
        }

        private void OnGUI()
        {
            GUILayout.Label("Arx Fatalis Converter", EditorStyles.boldLabel);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Convert Arx FTS JSON to OBJ or Unity GameObject", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            // JSON file selection
            EditorGUILayout.BeginHorizontal();
            jsonFilePath = EditorGUILayout.TextField("JSON File", jsonFilePath);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string path = EditorUtility.OpenFilePanel("Select Arx JSON File", "", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    jsonFilePath = path;

                    // Auto-generate OBJ path
                    if (string.IsNullOrEmpty(objFilePath))
                    {
                        objFilePath = Path.ChangeExtension(path, ".obj");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            // OBJ file selection (only shown if not importing to scene)
            if (!importToScene)
            {
                EditorGUILayout.BeginHorizontal();
                objFilePath = EditorGUILayout.TextField("OBJ File", objFilePath);
                if (GUILayout.Button("Browse", GUILayout.Width(80)))
                {
                    string initialDir = string.IsNullOrEmpty(objFilePath)
                        ? Path.GetDirectoryName(jsonFilePath)
                        : Path.GetDirectoryName(objFilePath);

                    string path = EditorUtility.SaveFilePanel("Save OBJ File", initialDir,
                        Path.GetFileNameWithoutExtension(jsonFilePath) + ".obj", "obj");

                    if (!string.IsNullOrEmpty(path))
                    {
                        objFilePath = path;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();

            // Conversion options
            EditorGUILayout.LabelField("Conversion Options", EditorStyles.boldLabel);


            includeMaterials = EditorGUILayout.Toggle("Generate Materials", includeMaterials);
    

            EditorGUILayout.Space();

            // Convert button
            GUI.enabled = !string.IsNullOrEmpty(jsonFilePath) &&
                         (importToScene || !string.IsNullOrEmpty(objFilePath));

            if (GUILayout.Button("Convert"))
            {
                ConvertToObj();
            }

            GUI.enabled = true;

            // Help box
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "This tool converts Arx Fatalis level data from JSON format to either:\n\n" +
                "1. OBJ file format that can be imported into Blender or other 3D tools\n" +
                "2. Unity GameObject directly in the scene\n\n" +
                "The JSON files should be exported from the arx-convert tool.",
                MessageType.Info);
        }

        private void ConvertToObj()
        {
            if (string.IsNullOrEmpty(jsonFilePath) || string.IsNullOrEmpty(objFilePath))
            {
                EditorUtility.DisplayDialog("Error", "Both input and output paths must be specified.", "OK");
                return;
            }

            EditorUtility.DisplayProgressBar("Converting", "Converting Arx JSON to OBJ...", 0.5f);

            try
            {
                bool success = ArxConverter.ConvertJsonToObj(jsonFilePath, objFilePath, includeMaterials);

                EditorUtility.ClearProgressBar();

                if (success)
                {
                    bool openFolder = EditorUtility.DisplayDialog("Success",
                        $"OBJ file saved to:\n{objFilePath}", "Open Folder", "Close");

                    if (openFolder)
                    {
                        EditorUtility.RevealInFinder(objFilePath);
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Failed to convert to OBJ. Check console for details.", "OK");
                }
            }
            catch (System.Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Error", $"Conversion failed: {ex.Message}", "OK");
                Debug.LogException(ex);
            }
        }

       
    }
}
#endif