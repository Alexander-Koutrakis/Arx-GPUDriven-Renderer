using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;


public class MaterialLoaderTool : MonoBehaviour
{
    // This will be used to load materials from the Resources folder
    private const string MATERIALS_PATH = "Materials/";
    public Transform RootTransform;
    /// <summary>
    /// Asynchronously loads materials for all child objects with renderers based on their name format.
    /// Object name format should be: "cell_{cellid}_{material name}"
    /// </summary>
    /// <param name="rootTransform">The root transform to search from</param>
    /// <returns>Task that completes when all materials are loaded</returns>
    public static async Task LoadMaterialsForChildren(Transform rootTransform)
    {
        Debug.Log($"Starting material loading process for {rootTransform.name}");

        // Get all renderers in children
        Renderer[] renderers = rootTransform.GetComponentsInChildren<Renderer>(true);

        List<Task> loadingTasks = new List<Task>();

        foreach (Renderer renderer in renderers)
        {
            loadingTasks.Add(LoadMaterialForRenderer(renderer));
        }

        await Task.WhenAll(loadingTasks);

        Debug.Log($"Finished loading materials for {renderers.Length} objects under {rootTransform.name}");
    }

    private static async Task LoadMaterialForRenderer(Renderer renderer)
    {
        string objectName = renderer.gameObject.name;

        // Check if the name matches our expected format
        if (!objectName.StartsWith("group_"))
        {
            return;
        }

        // Split the name by underscore
        string[] nameParts = objectName.Split('_');

        // Need at least 3 parts: "cell", "cellid", and "material name"
        if (nameParts.Length < 2)
        {
            Debug.LogWarning($"Object {objectName} doesn't follow the expected naming convention: cell_{{cellid}}_{{material name}}");
            return;
        }

        // Get material name (could be multiple parts if material name contains underscores)
        string materialName = string.Join("_", nameParts.Skip(1));

        // Load the material asynchronously using a TaskCompletionSource
        Material loadedMaterial = await LoadMaterialAsync(materialName);

        if (loadedMaterial != null)
        {
            renderer.material = loadedMaterial;
            Debug.Log($"Applied material '{materialName}' to object '{objectName}'");
        }
        else
        {
            Debug.LogError($"Failed to load material '{materialName}' for object '{objectName}'");
        }
    }

    private static Task<Material> LoadMaterialAsync(string materialName)
    {
        TaskCompletionSource<Material> tcs = new TaskCompletionSource<Material>();

        // Start the resource loading process
        ResourceRequest request = Resources.LoadAsync<Material>(MATERIALS_PATH + materialName);

        // Set up a completed callback
        request.completed += operation => {
            Material material = request.asset as Material;
            tcs.SetResult(material);
        };

        return tcs.Task;
    }

#if UNITY_EDITOR
    // Static method to trigger the material loading from the editor
    public static async void LoadMaterialsForChildrenFromEditor(Transform rootTransform)
    {
        try
        {
            await LoadMaterialsForChildren(rootTransform);
            Debug.Log("Editor material loading completed successfully");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error loading materials from editor: {ex.Message}");
        }
    }
#endif
}

