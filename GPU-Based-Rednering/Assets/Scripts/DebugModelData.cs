using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rendering.Data;

public class DebugModelData : MonoBehaviour
{

    
    public ModelData modelData;

    private void OnDrawGizmosSelected()
    {
        if (modelData == null)
            return;
      
        Gizmos.color = Color.green;


        DebugTriangles();
        
    }

    private void DebugTriangles()
    {
        for(int i = 0;i<modelData.Triangles.Length; i++)
        {
            Triangle triangle = modelData.Triangles[i];

            Vertex v1 = modelData.Vertices[(int)triangle.vertexIndex1];
            Vertex v2 = modelData.Vertices[(int)triangle.vertexIndex2];
            Vertex v3 = modelData.Vertices[(int)triangle.vertexIndex3];

            Gizmos.DrawLine(v1.position, v2.position);
            Gizmos.DrawLine(v2.position, v3.position);
            Gizmos.DrawLine(v3.position, v1.position);
        }
    }

   
}
