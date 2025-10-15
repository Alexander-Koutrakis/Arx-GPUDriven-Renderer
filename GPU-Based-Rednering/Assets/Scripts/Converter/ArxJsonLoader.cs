using System.IO;
using UnityEngine;

namespace ArxConverter
{
    public static class ArxJsonLoader
    {
        /// <summary>
        /// Loads an Arx FTS JSON file into our data structures
        /// </summary>
        /// <param name="jsonFilePath">Path to the JSON file</param>
        /// <returns>The parsed ArxFTS data or null if loading failed</returns>
        public static ArxFTS LoadFromJson(string jsonFilePath)
        {
            if (!File.Exists(jsonFilePath))
            {
                Debug.LogError($"File not found: {jsonFilePath}");
                return null;
            }

            try
            {
                string jsonContent = File.ReadAllText(jsonFilePath);
                ArxFTS arxData = JsonUtility.FromJson<ArxFTS>(jsonContent);

                Debug.Log($"Loaded Arx FTS data with {arxData.polygons.Length} polygons");
                return arxData;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error loading Arx FTS JSON: {e.Message}");
                return null;
            }
        }
    }
}