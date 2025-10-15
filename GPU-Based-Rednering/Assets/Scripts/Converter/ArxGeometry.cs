using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArxConverter
{
    [Flags]
    public enum ArxPolygonFlags
    {
        None = 0,
        NoShadow = 1 << 0,
        DoubleSided = 1 << 1,
        Transparent = 1 << 2,
        Water = 1 << 3,
        Glow = 1 << 4,
        Ignore = 1 << 5,
        Quad = 1 << 6,
        Tiled = 1 << 7,
        Metal = 1 << 8,
        Hide = 1 << 9,
        Stone = 1 << 10,
        Wood = 1 << 11,
        Gravel = 1 << 12,
        Earth = 1 << 13,
        NoCollision = 1 << 14,
        Lava = 1 << 15,
        Climbable = 1 << 16,
        Falling = 1 << 17,
        NoPath = 1 << 18,
        NoDraw = 1 << 19,
        PrecisePath = 1 << 20,
        LateMip = 1 << 27
    }

    [Serializable]
    public struct ArxVertex
    {
        public float x;
        public float y;
        public float z;
        public float u;
        public float v;

        // Convert to Unity Vector3
        public Vector3 ToVector3()
        {
            // Note: May need coordinate system conversion depending on Arx's system
            return new Vector3(x, y, z);
        }

        // Convert to Unity Vector2 (UV)
        public Vector2 ToUV()
        {
            return new Vector2(u, v);
        }
    }

    [Serializable]
    public struct ArxVector3
    {
        public float x;
        public float y;
        public float z;

        // Convert to Unity Vector3
        public Vector3 ToVector3()
        {
            // Note: May need coordinate system conversion depending on Arx's system
            return new Vector3(x, y, z);
        }
    }

    [Serializable]
    public struct ArxPolygon
    {
        public ArxVertex[] vertices;
        public int textureContainerId;
        public ArxVector3 norm;
        public ArxVector3 norm2;
        public ArxVector3[] normals;
        public float transval;
        public float area;
        public ArxPolygonFlags flags;
        public int room;

        public bool IsQuad()
        {
            return (flags & ArxPolygonFlags.Quad) != 0;
        }

        public bool IsTransparent()
        {
            return (flags & ArxPolygonFlags.Transparent) != 0;
        }

        public bool IsWater()
        {
            return (flags & ArxPolygonFlags.Water) != 0;
        }
    }

    [Serializable]
    public struct ArxCell
    {
        public int[] anchors;
        public int[] polygonIndices;       
    }

    [Serializable]
    public struct ArxPortalPolygon
    {
        public ArxVector3 min;
        public ArxVector3 max;
        public ArxVector3 norm;
        public ArxVector3 norm2;
        public ArxTextureVertex[] vertices;
        public ArxVector3 center;
    }

    [Serializable]
    public struct ArxTextureVertex
    {
        public ArxVector3 pos;
        public float rhw;

        public Vector3 ToVector3()
        {
            return pos.ToVector3();
        }
    }

    [Serializable]
    public struct ArxPortal
    {
        public ArxPortalPolygon polygon;
        public int room1;
        public int room2;
        public int useportal;
    }

    [Serializable]
    public struct ArxEPData
    {
        public int cellX;
        public int cellY;
        public int polygonIdx;
    }

    [Serializable]
    public struct ArxRoom
    {
        public int[] portals;
        public ArxEPData[] polygons;
    }

    [Serializable]
    public struct ArxTextureContainer
    {
        public int id;
        public string filename;
    }

    [Serializable]
    public class ArxFTS
    {
        // Main geometry data
        public ArxPolygon[] polygons;
        public ArxCell[] cells;
        public ArxAnchor[] anchors;
        public ArxPortal[] portals;
        public ArxRoom[] rooms;
        public ArxTextureContainer[] textureContainers;

        // Scene metadata
        public ArxFtsHeader header;
        public ArxSceneHeader sceneHeader;
    }

    [Serializable]
    public struct ArxSceneHeader
    {
        public ArxVector3 mScenePosition;
    }

    [Serializable]
    public struct ArxFtsHeader
    {
        public int levelIdx;
    }

    [Serializable]
    public struct ArxAnchor
    {
        public ArxAnchorData data;
        public int[] linkedAnchors;
    }

    [Serializable]
    public struct ArxAnchorData
    {
        public ArxVector3 pos;
        public float radius;
        public float height;
        public bool isBlocked;
    }
}