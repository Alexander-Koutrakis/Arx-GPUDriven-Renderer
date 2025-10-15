using Rendering.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rendering
{

    public class CustomRenderer:MonoBehaviour
    {
        public ModelData testData;
        // References to shaders
        private ComputeShader cullingComputeShader;
        private Shader renderShader;

        // Buffers for GPU data
        private ComputeBuffer vertexBuffer;
        private ComputeBuffer triangleBuffer;
        private ComputeBuffer materialBuffer;
        private ComputeBuffer visibleTrianglesBuffer;
        private ComputeBuffer triangleCountBuffer;

        // Material for rendering
        private Material renderMaterial;

        // Buffer sizes - starting with smaller defaults
        private int maxVertexCount = 10000;
        private int maxTriangleCount = 10000;
        private int maxMaterialCount = 100;

        // Shader property IDs (for efficiency)
        private int vertexBufferID;
        private int triangleBufferID;
        private int materialBufferID;
        private int visibleTrianglesBufferID;

        [Header("Debug Information")]
        public int visibleTrianglesCount;
        ModelData modelData;

        private void Awake()
        {
            Initialize(testData);
        }
        public void Initialize(ModelData modelData)
        {
            this.modelData = modelData;
            // Initialize everything
            InitializeShaders();
            InitializeBuffers();
            
            // Populate buffers with model data
            UpdateBuffers(modelData);
        }

        void InitializeShaders()
        {

            cullingComputeShader= Resources.Load<ComputeShader>("Shaders/TriangleCulling");
            renderShader = Shader.Find("Custom/TriangleDrawer");
            renderMaterial = new Material(renderShader);

            // Cache shader property IDs
            vertexBufferID = Shader.PropertyToID("_VertexBuffer");
            triangleBufferID = Shader.PropertyToID("_TriangleBuffer");
            materialBufferID = Shader.PropertyToID("_MaterialBuffer");
            visibleTrianglesBufferID = Shader.PropertyToID("_VisibleTrianglesBuffer");
        }

        void InitializeBuffers()
        {
            // Create buffers
            vertexBuffer = new ComputeBuffer(maxVertexCount, System.Runtime.InteropServices.Marshal.SizeOf<Vertex>());
            triangleBuffer = new ComputeBuffer(maxTriangleCount, System.Runtime.InteropServices.Marshal.SizeOf<Triangle>());
            materialBuffer = new ComputeBuffer(maxMaterialCount, System.Runtime.InteropServices.Marshal.SizeOf<MaterialData>());

            // Buffer for the culling results (indices of visible triangles)
            visibleTrianglesBuffer = new ComputeBuffer(maxTriangleCount, sizeof(uint), ComputeBufferType.Append);

            // Buffer to store the count of visible triangles
            triangleCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        }

        void ResizeBuffersIfNeeded(int vertexCount, int triangleCount, int materialCount)
        {
            bool needResize = false;
            
            // Check if buffers need to be resized
            if (vertexCount > maxVertexCount)
            {
                maxVertexCount = Mathf.NextPowerOfTwo(vertexCount);
                needResize = true;
            }
            
            if (triangleCount > maxTriangleCount)
            {
                maxTriangleCount = Mathf.NextPowerOfTwo(triangleCount);
                needResize = true;
            }
            
            if (materialCount > maxMaterialCount)
            {
                maxMaterialCount = Mathf.NextPowerOfTwo(materialCount);
                needResize = true;
            }
            
            // If resize is needed, release old buffers and create new ones
            if (needResize)
            {
                Debug.Log($"Resizing buffers to: {maxVertexCount} vertices, {maxTriangleCount} triangles, {maxMaterialCount} materials");
                
                // Release old buffers
                ReleaseBuffers();
                
                // Create new buffers
                InitializeBuffers();
            }
        }
        
        void ReleaseBuffers()
        {
            vertexBuffer?.Release();
            triangleBuffer?.Release();
            materialBuffer?.Release();
            visibleTrianglesBuffer?.Release();
            triangleCountBuffer?.Release();
        }

        public void UpdateBuffers(ModelData modelData)
        {
            // Get data from model
           Vertex[] vertices = modelData.Vertices;
           Triangle[] triangles = modelData.Triangles;
           MaterialData[] materials = modelData.Materials;
            
            // Resize buffers if needed based on actual data size
            ResizeBuffersIfNeeded(vertices.Length, triangles.Length, materials.Length);

            // Update buffers with new data
            vertexBuffer.SetData(vertices);
            triangleBuffer.SetData(triangles);
            materialBuffer.SetData(materials);

            Debug.Log($"Buffers updated: {vertices.Length} vertices, {triangles.Length} triangles, {materials.Length} materials");
        }

        public void OnRenderObject()
        {
            PerformCulling();

            UpdateDebugInfo();

            RenderVisibleTriangles();
        }

        void PerformCulling()
        {
            if (modelData == null || modelData.Triangles == null || modelData.Triangles.Length == 0)
            {
                return;
            }
            
            visibleTrianglesBuffer.SetCounterValue(0);

            cullingComputeShader.SetBuffer(0, "_VertexBuffer", vertexBuffer);
            cullingComputeShader.SetBuffer(0, "_TriangleBuffer", triangleBuffer);
            cullingComputeShader.SetBuffer(0, "_VisibleTrianglesBuffer", visibleTrianglesBuffer);

            Camera cam = Camera.main;
            Matrix4x4 vp = cam.projectionMatrix * cam.worldToCameraMatrix;
            cullingComputeShader.SetMatrix("_ViewProjectionMatrix", vp);
            cullingComputeShader.SetVector("_CameraPosition", cam.transform.position);

            int actualTriangleCount = modelData.Triangles.Length;
            int threadGroupSize = 64;
            int threadGroups = Mathf.CeilToInt((float)actualTriangleCount / threadGroupSize);

            threadGroups = Mathf.Max(1, threadGroups);
            
            cullingComputeShader.Dispatch(0, threadGroups, 1, 1);

            ComputeBuffer.CopyCount(visibleTrianglesBuffer, triangleCountBuffer, 0);
        }

        void RenderVisibleTriangles()
        {

            // Set buffers for render shader
            renderMaterial.SetBuffer(vertexBufferID, vertexBuffer);
            renderMaterial.SetBuffer(triangleBufferID, triangleBuffer);
            renderMaterial.SetBuffer(materialBufferID, materialBuffer);
            renderMaterial.SetBuffer(visibleTrianglesBufferID, visibleTrianglesBuffer);

            // Draw the triangles with GPU instancing
            // Get visible triangle count
            int[] visibleCount = new int[1];
            triangleCountBuffer.GetData(visibleCount);
            int visibleTriangles = visibleCount[0];

            if (visibleTriangles > 0)
            {
                // Draw call
                renderMaterial.SetPass(0);
                Graphics.DrawProceduralNow(MeshTopology.Triangles, 3, visibleTriangles);
            }
        }

        void OnDisable()
        {
            // Clean up buffers when disabled
            ReleaseBuffers();
        }

        void OnDestroy()
        {
            // Clean up all resources
            ReleaseBuffers();
            
            if (renderMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(renderMaterial);
                }
                else
                {
                    DestroyImmediate(renderMaterial);
                }
            }
        }

        void UpdateDebugInfo()
        {
            
            
            // Get the count of visible triangles
            int[] counterArray = new int[1];
            triangleCountBuffer.GetData(counterArray);
            visibleTrianglesCount = counterArray[0];
            
           
        }

      
    }
}
