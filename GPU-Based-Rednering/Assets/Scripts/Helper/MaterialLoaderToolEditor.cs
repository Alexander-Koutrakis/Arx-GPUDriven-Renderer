
using UnityEditor;
using UnityEngine;



#if UNITY_EDITOR
[CustomEditor(typeof(MaterialLoaderTool))]
public class MaterialLoaderToolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MaterialLoaderTool tool = (MaterialLoaderTool)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Material Loading Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Load Materials for Children"))
        {

            // Use the static method to start the async process
            MaterialLoaderTool.LoadMaterialsForChildrenFromEditor(tool.RootTransform);
        }
    }
}
#endif