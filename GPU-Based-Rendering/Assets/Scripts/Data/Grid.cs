using UnityEngine;
using System.Collections.Generic;
using Rendering.Data;

[System.Serializable]
public class Grid
{

    private Vector3Int gridResolution = new Vector3Int(10, 10, 10);
    private Vector3 gridMin;
    private Vector3 gridMax;
    private Vector3 cellSize;

    private Cell[] gridCells;
    private Cell[] populatedCells;        // Only cells that contain triangles
    private uint[] triangleIndices;       // Flat array of triangle indices organized by cells

    public Cell[] Cells { get { return populatedCells; } }
    public uint[] TriangleIndices { get { return triangleIndices; } }
    /// <summary>
    /// Creates a 3D grid of AABBs based on the model's bounds
    /// </summary>
    /// <param name="modelData">The model data containing vertices and triangles</param>
    /// <param name="resolution">Grid resolution (x, y, z)</param>
    public void CreateGrid(Vertex[] vertices, Triangle[] triangles, Vector3Int resolution)
    {
        gridResolution = resolution;

        // Calculate the bounds of the entire model
        CalculateModelBounds(vertices);

        // Calculate cell size
        Vector3 gridSize = gridMax - gridMin;
        cellSize = new Vector3(
            gridSize.x / gridResolution.x,
            gridSize.y / gridResolution.y,
            gridSize.z / gridResolution.z
        );

        // Create the grid cells
        GenerateGridCells();

        // Populate grid cells with triangles
        PopulateGridWithTriangles(vertices,triangles);

        Debug.Log($"Grid created with {gridCells.Length} total cells");
        Debug.Log($"Found {populatedCells.Length} cells with triangles");
        Debug.Log($"Total triangle references: {triangleIndices.Length}");
        Debug.Log($"Grid bounds: Min({gridMin}), Max({gridMax})");
        Debug.Log($"Cell size: {cellSize}");
    }

    /// <summary>
    /// Calculate the bounding box of the entire model from vertices
    /// </summary>
    private void CalculateModelBounds(Vertex[] vertices)
    {
        if (vertices.Length == 0)
        {
            Debug.LogError("No vertices found in model data!");
            return;
        }

        Vector3 min = vertices[0].position;
        Vector3 max = vertices[0].position;

        for (int i = 1; i < vertices.Length; i++)
        {
            Vector3 pos = vertices[i].position;

            if (pos.x < min.x) min.x = pos.x;
            if (pos.y < min.y) min.y = pos.y;
            if (pos.z < min.z) min.z = pos.z;

            if (pos.x > max.x) max.x = pos.x;
            if (pos.y > max.y) max.y = pos.y;
            if (pos.z > max.z) max.z = pos.z;
        }

        // Add small padding to avoid edge cases
        Vector3 padding = Vector3.one * 0.001f;
        gridMin = min - padding;
        gridMax = max + padding;
    }

    /// <summary>
    /// Generate all grid cells as AABBs
    /// </summary>
    private void GenerateGridCells()
    {
        int totalCells = gridResolution.x * gridResolution.y * gridResolution.z;
        gridCells = new Cell[totalCells];

        int index = 0;

        for (int x = 0; x < gridResolution.x; x++)
        {
            for (int y = 0; y < gridResolution.y; y++)
            {
                for (int z = 0; z < gridResolution.z; z++)
                {
                    Vector3 cellMin = gridMin + new Vector3(
                        x * cellSize.x,
                        y * cellSize.y,
                        z * cellSize.z
                    );

                    Vector3 cellMax = cellMin + cellSize;

                    gridCells[index] = new Cell
                    {
                        min = cellMin,
                        max = cellMax,
                        firstTriangleIndex = 0, // Will be set later when we populate triangles
                        count = 0 // Will be set later when we populate triangles
                    };

                    index++;
                }
            }
        }
    }



    /// <summary>
    /// Get the flat array index from 3D grid coordinates
    /// </summary>
    private int GetFlatIndex(Vector3Int gridCoords)
    {
        return gridCoords.x * gridResolution.y * gridResolution.z +
               gridCoords.y * gridResolution.z +
               gridCoords.z;
    }

    /// <summary>
    /// Get the grid cell that contains a specific world position
    /// </summary>
    private Vector3Int GetGridCellFromPosition(Vector3 worldPos)
    {
        Vector3 normalizedPos = worldPos - gridMin;

        int x = Mathf.FloorToInt(normalizedPos.x / cellSize.x);
        int y = Mathf.FloorToInt(normalizedPos.y / cellSize.y);
        int z = Mathf.FloorToInt(normalizedPos.z / cellSize.z);

        // Clamp to grid bounds
        x = Mathf.Clamp(x, 0, gridResolution.x - 1);
        y = Mathf.Clamp(y, 0, gridResolution.y - 1);
        z = Mathf.Clamp(z, 0, gridResolution.z - 1);

        return new Vector3Int(x, y, z);
    }

    /// <summary>
    /// Populate grid cells with triangles that intersect or are inside them
    /// </summary>
    private void PopulateGridWithTriangles(Vertex[] vertices, Triangle[] triangles)
    {


        // Temporary list to store triangle indices for each cell
        List<uint>[] cellTriangles = new List<uint>[gridCells.Length];
        for (int i = 0; i < cellTriangles.Length; i++)
        {
            cellTriangles[i] = new List<uint>();
        }

        // Test each triangle against all grid cells
        for (uint triangleIndex = 0; triangleIndex < triangles.Length; triangleIndex++)
        {
            Triangle triangle = triangles[triangleIndex];

            // Get triangle vertices
            Vector3 v0 = vertices[triangle.vertexIndex1].position;
            Vector3 v1 = vertices[triangle.vertexIndex2].position;
            Vector3 v2 = vertices[triangle.vertexIndex3].position;

            // Calculate triangle AABB for broad phase
            Vector3 triMin = Vector3.Min(Vector3.Min(v0, v1), v2);
            Vector3 triMax = Vector3.Max(Vector3.Max(v0, v1), v2);

            // Find grid cells that might intersect with triangle
            Vector3Int minCell = GetGridCellFromPosition(triMin);
            Vector3Int maxCell = GetGridCellFromPosition(triMax);

            // Test triangle against potentially intersecting cells
            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                for (int y = minCell.y; y <= maxCell.y; y++)
                {
                    for (int z = minCell.z; z <= maxCell.z; z++)
                    {
                        int cellIndex = GetFlatIndex(new Vector3Int(x, y, z));
                        Cell cellAABB = gridCells[cellIndex];

                        // Test if triangle intersects with this cell
                        if (TriangleAABBIntersection(v0, v1, v2, cellAABB))
                        {
                            cellTriangles[cellIndex].Add(triangleIndex);
                        }
                    }
                }
            }
        }

        // Build final arrays
        BuildFinalArrays(cellTriangles);
    }

    /// <summary>
    /// Build the final populated cells and triangle indices arrays
    /// </summary>
    private void BuildFinalArrays(List<uint>[] cellTriangles)
    {
        List<Cell> populatedCellsList = new List<Cell>();
        List<uint> triangleIndicesList = new List<uint>();

        uint currentTriangleIndex = 0;

        for (int cellIndex = 0; cellIndex < gridCells.Length; cellIndex++)
        {
            if (cellTriangles[cellIndex].Count > 0)
            {
                // Create a copy of the AABB with triangle data
                Cell populatedCell = new Cell
                {
                    min = gridCells[cellIndex].min,
                    max = gridCells[cellIndex].max,
                    firstTriangleIndex = currentTriangleIndex,
                    count = (uint)cellTriangles[cellIndex].Count
                };

                populatedCellsList.Add(populatedCell);

                // Add triangle indices for this cell
                triangleIndicesList.AddRange(cellTriangles[cellIndex]);
                currentTriangleIndex += (uint)cellTriangles[cellIndex].Count;
            }
        }

        populatedCells = populatedCellsList.ToArray();
        triangleIndices = triangleIndicesList.ToArray();
    }

    /// <summary>
    /// Test if a triangle intersects with an AABB
    /// Uses separating axis theorem
    /// </summary>
    private bool TriangleAABBIntersection(Vector3 v0, Vector3 v1, Vector3 v2, Cell aabb)
    {
        Vector3 boxCenter = (aabb.min + aabb.max) * 0.5f;
        Vector3 boxExtents = (aabb.max - aabb.min) * 0.5f;

        // Translate triangle to origin
        v0 -= boxCenter;
        v1 -= boxCenter;
        v2 -= boxCenter;

        // Triangle edges
        Vector3 f0 = v1 - v0;
        Vector3 f1 = v2 - v1;
        Vector3 f2 = v0 - v2;

        // AABB normals (x, y, z axes)
        Vector3[] axes = new Vector3[]
        {
            Vector3.right, Vector3.up, Vector3.forward,
            Vector3.Cross(f0, Vector3.right), Vector3.Cross(f0, Vector3.up), Vector3.Cross(f0, Vector3.forward),
            Vector3.Cross(f1, Vector3.right), Vector3.Cross(f1, Vector3.up), Vector3.Cross(f1, Vector3.forward),
            Vector3.Cross(f2, Vector3.right), Vector3.Cross(f2, Vector3.up), Vector3.Cross(f2, Vector3.forward),
            Vector3.Cross(f0, f1) // Triangle normal
        };

        foreach (Vector3 axis in axes)
        {
            if (axis.sqrMagnitude < 0.0001f) continue; // Skip degenerate axes

            Vector3 normalizedAxis = axis.normalized;

            // Project triangle vertices onto axis
            float triMin = Mathf.Min(Mathf.Min(Vector3.Dot(v0, normalizedAxis),
                                              Vector3.Dot(v1, normalizedAxis)),
                                    Vector3.Dot(v2, normalizedAxis));
            float triMax = Mathf.Max(Mathf.Max(Vector3.Dot(v0, normalizedAxis),
                                              Vector3.Dot(v1, normalizedAxis)),
                                    Vector3.Dot(v2, normalizedAxis));

            // Project box onto axis
            float boxProjection = Mathf.Abs(Vector3.Dot(boxExtents, new Vector3(
                Mathf.Abs(normalizedAxis.x),
                Mathf.Abs(normalizedAxis.y),
                Mathf.Abs(normalizedAxis.z)
            )));

            // Check for separation
            if (triMax < -boxProjection || triMin > boxProjection)
            {
                return false; // Separating axis found
            }
        }

        return true; // No separating axis found, intersection exists
    }
    public void DrawGizmos()
    {
        if (gridCells == null) return;

        Gizmos.color = Color.green;

        // Draw grid bounds
        Vector3 center = (gridMin + gridMax) * 0.5f;
        Vector3 size = gridMax - gridMin;
        Gizmos.DrawWireCube(center, size);

        // Draw individual cells (only draw a subset to avoid performance issues)
        Gizmos.color = Color.yellow;
        int step = Mathf.Max(1, gridCells.Length / 100); // Draw max 100 cells

        for (int i = 0; i < gridCells.Length; i += step)
        {
            Cell cell = gridCells[i];
            Vector3 cellCenter = (cell.min + cell.max) * 0.5f;
            Vector3 cellSize = cell.max - cell.min;

            Gizmos.DrawWireCube(cellCenter, cellSize);
        }
    }
}