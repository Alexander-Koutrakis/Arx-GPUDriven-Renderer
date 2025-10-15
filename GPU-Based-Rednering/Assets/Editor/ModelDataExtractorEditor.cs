using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


namespace Rendering.Data
{
    [CustomEditor(typeof(ModelDataExtractor))]
    public class ModelDataExtractorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default inspector
            DrawDefaultInspector();

            // Add a button below the default inspector
            ModelDataExtractor modelDataExtractor = (ModelDataExtractor)target;
            if (GUILayout.Button("Extract Data"))
            {
                modelDataExtractor.ExtractData();
            }
        }
    }
}
