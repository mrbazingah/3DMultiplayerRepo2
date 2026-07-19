using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Terrain/Terrain Settings")]
public class TerrainSettings : ScriptableObject
{
    [Header("Chunk")]
    [Tooltip("World units per chunk edge.")]
    public int chunkSize = 64;

    [Tooltip("Quads per chunk edge. Higher = more detailed, more expensive.")]
    public int resolution = 64;

    [Header("Height")]
    public float heightScale = 25f;

    [Tooltip("Lower = larger, smoother features.")]
    public float noiseScale = 0.008f;

    [Range(1, 8)] public int octaves = 4;
    [Range(1.5f, 3.5f)] public float lacunarity = 2f;
    [Range(0.1f, 0.9f)] public float persistence = 0.5f;

    [Header("Structures")]
    [Tooltip("World units per region cell. Each region may hold one structure. "
           + "Must be comfortably larger than the biggest structure radius.")]
    public int regionSize = 512;

    public StructureType[] structureTypes;

    [Header("Nature")]
    public NatureLayer[] natureLayers;
}

[Serializable]
public class StructureType
{
    public string name = "Village";

    [Tooltip("The pre-made prefab. Its pivot should be at ground level, centered.")]
    public GameObject prefab;

    [Tooltip("Chance that any given region contains this structure. 0-1.")]
    [Range(0f, 1f)] public float chancePerRegion = 0.35f;

    [Tooltip("Nature objects are excluded within this radius.")]
    public float clearanceRadius = 40f;

    [Header("Terrain flattening")]
    [Tooltip("Terrain is fully flat within this radius, then blends back to natural.")]
    public float flattenRadius = 30f;

    [Tooltip("Distance over which the flattened area blends back into natural terrain.")]
    public float flattenFalloff = 25f;

    [Tooltip("Only place the structure where the natural ground is at or above this.")]
    public float minHeight = 2f;

    [Tooltip("Only place the structure where the natural ground is at or below this.")]
    public float maxHeight = 1000f;

    public bool randomYRotation = true;
}

[Serializable]
public class NatureLayer
{
    public string name = "Trees";
    public GameObject[] prefabs;

    [Tooltip("Attempted placements per chunk.")]
    public int countPerChunk = 12;

    [Header("Placement rules")]
    [Tooltip("Only place if terrain height is at or above this.")]
    public float minHeight = 0f;

    [Tooltip("Only place if terrain height is at or below this.")]
    public float maxHeight = 1000f;

    [Tooltip("Only place on ground flatter than this slope.")]
    [Range(0f, 90f)] public float maxSlopeDegrees = 35f;

    [Tooltip("Extra margin added to a structure's clearance radius for this layer. "
           + "Use a larger value for trees than for grass.")]
    public float structureClearancePadding = 0f;

    [Header("Variation")]
    public Vector2 uniformScaleRange = new Vector2(0.9f, 1.25f);
    public bool randomYRotation = true;
    public bool alignToGroundNormal = false;
}