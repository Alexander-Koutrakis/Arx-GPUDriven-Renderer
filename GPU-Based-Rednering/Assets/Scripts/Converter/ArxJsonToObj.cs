using System;
using System.IO;
using UnityEngine;

namespace ArxConverter
{
    /// <summary>
    /// Command-line application to convert Arx FTS JSON files to OBJ format
    /// </summary>
    public class ArxJsonToObj
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Arx Fatalis JSON to OBJ Converter");
            Console.WriteLine("==================================");

            if (args.Length < 1)
            {
                ShowUsage();
                return;
            }

            string inputPath = args[0];
            string outputPath = args.Length > 1 ? args[1] : null;
            bool includeMaterials = true;

            // Parse optional parameters
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].Equals("--no-materials", StringComparison.OrdinalIgnoreCase))
                {
                    includeMaterials = false;
                }
            }

            // Validate input path
            if (!File.Exists(inputPath))
            {
                Console.WriteLine($"Error: Input file not found: {inputPath}");
                return;
            }

            // Set default output path if not provided
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.ChangeExtension(inputPath, ".obj");
            }

            Console.WriteLine($"Input:  {inputPath}");
            Console.WriteLine($"Output: {outputPath}");
            Console.WriteLine($"Include materials: {includeMaterials}");
            Console.WriteLine();

            // Load the JSON data
            Console.WriteLine("Loading Arx JSON data...");
            ArxFTS arxData = LoadJsonData(inputPath);

            if (arxData == null)
            {
                Console.WriteLine("Failed to load Arx data from JSON file.");
                return;
            }

            Console.WriteLine($"Loaded level {arxData.header.levelIdx} with {arxData.polygons.Length} polygons.");

            // Export to OBJ format
            Console.WriteLine("Exporting to OBJ format...");
            bool success = ArxObjExporter.ExportToObj(arxData, outputPath, includeMaterials);

            if (success)
            {
                Console.WriteLine("Export completed successfully!");
                Console.WriteLine($"OBJ file saved to: {outputPath}");

                if (includeMaterials)
                {
                    string mtlPath = Path.ChangeExtension(outputPath, ".mtl");
                    Console.WriteLine($"MTL file saved to: {mtlPath}");
                }
            }
            else
            {
                Console.WriteLine("Export failed.");
            }
        }

        private static void ShowUsage()
        {
            Console.WriteLine("Usage: ArxJsonToObj <input.json> [output.obj] [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --no-materials    Don't generate MTL file for materials");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  ArxJsonToObj level1.json");
            Console.WriteLine("  ArxJsonToObj level1.json level1_export.obj");
            Console.WriteLine("  ArxJsonToObj level1.json --no-materials");
        }

        private static ArxFTS LoadJsonData(string jsonPath)
        {
            try
            {
                string jsonContent = File.ReadAllText(jsonPath);
                return JsonUtility.FromJson<ArxFTS>(jsonContent);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error loading JSON: {e.Message}");
                return null;
            }
        }
    }
}