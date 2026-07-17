using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A structure placed in the world. Computed purely from
/// (worldSeed, region coords) so any chunk can discover it without that
/// structure's own chunk being loaded.
/// </summary>
public struct StructureSite
{
    public int typeIndex;          // index into TerrainSettings.structureTypes
    public Vector2 center;         // world XZ of the structure's center
    public float groundHeight;     // natural terrain height at the center
    public float clearanceRadius;
    public float flattenRadius;
    public float flattenFalloff;
    public float yRotation;
}

/// <summary>
/// Deterministic structure placement on a coarse region grid.
///
/// The world is divided into regionSize x regionSize cells. Each cell rolls,
/// from a seed derived only from (worldSeed, regionX, regionZ), whether it
/// contains a structure and where. This means:
/// - No chunk generation order dependency. Chunk (1000, -500) can be the very
///   first chunk generated and still know about a village straddling it.
/// - Unload/reload is identical, forever.
///
/// Everything here is pure C# and thread-safe, so it runs on the same
/// background threads as chunk generation.
/// </summary>
public static class StructureSites
{
    /// <summary>
    /// Rolls the structure (if any) for a single region cell.
    /// Returns false if this region is empty.
    /// </summary>
    public static bool TryGetSite(
        DeterministicNoise noise, TerrainSettings s, int worldSeed,
        int regionX, int regionZ, out StructureSite site)
    {
        site = default;
        if (s.structureTypes == null || s.structureTypes.Length == 0) return false;

        // Unique deterministic seed per (world, region).
        int seed = worldSeed ^ (regionX * 6971) ^ (regionZ * 41333);
        var rng = new System.Random(seed);

        // Pick which type this region would host, then roll its chance.
        // Values are consumed in a fixed order so results never shift.
        int typeIndex = rng.Next(s.structureTypes.Length);
        double roll = rng.NextDouble();
        float u = (float)rng.NextDouble();
        float v = (float)rng.NextDouble();
        float yRot = (float)rng.NextDouble() * 360f;

        StructureType type = s.structureTypes[typeIndex];
        if (type.prefab == null) return false;

        Debug.Log($"Site rolled at region ({regionX},{regionZ})");

        if (roll > type.chancePerRegion) return false;

        Debug.Log($"Site rolled at region ({regionX},{regionZ})");

        // Keep the structure away from the region border so its clearance
        // radius cannot reach past the 3x3 neighbourhood other chunks search.
        float margin = Mathf.Max(type.clearanceRadius, type.flattenRadius + type.flattenFalloff);
        float usable = s.regionSize - margin * 2f;
        if (usable <= 0f) return false; // regionSize too small for this structure

        Debug.Log($"Site rolled at region ({regionX},{regionZ})");

        float wx = regionX * s.regionSize + margin + u * usable;
        float wz = regionZ * s.regionSize + margin + v * usable;

        // Natural (unflattened) height at the center. This is what the terrain
        // gets flattened *to*, so it must never itself be flattened - otherwise
        // the height function would recurse.
        float ground = ChunkBuilder.BaseHeight(noise, s, wx, wz);
        if (ground < type.minHeight || ground > type.maxHeight) return false;

        site = new StructureSite
        {
            typeIndex = typeIndex,
            center = new Vector2(wx, wz),
            groundHeight = ground,
            clearanceRadius = type.clearanceRadius,
            flattenRadius = type.flattenRadius,
            flattenFalloff = type.flattenFalloff,
            yRotation = type.randomYRotation ? yRot : 0f,
        };
        
        Debug.Log($"Site rolled at region ({regionX},{regionZ}) -> world {wx:F0},{wz:F0} ground={ground:F1}");

        return true;
    }

    /// <summary>
    /// Every site that could possibly influence the given chunk: this chunk's
    /// region plus the 8 neighbours. Because site placement is inset by its own
    /// radius (see margin above), no structure outside this 3x3 block can reach
    /// into the chunk.
    ///
    /// 9 cheap RNG rolls per chunk. Call once per chunk and reuse the list.
    /// </summary>
    public static List<StructureSite> GatherNearby(
        DeterministicNoise noise, TerrainSettings s, int worldSeed, ChunkCoord coord)
    {
        var results = new List<StructureSite>();

        // Which region does this chunk sit in?
        float chunkCenterX = coord.x * s.chunkSize + s.chunkSize * 0.5f;
        float chunkCenterZ = coord.z * s.chunkSize + s.chunkSize * 0.5f;
        int rx = Mathf.FloorToInt(chunkCenterX / s.regionSize);
        int rz = Mathf.FloorToInt(chunkCenterZ / s.regionSize);

        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (TryGetSite(noise, s, worldSeed, rx + dx, rz + dz, out StructureSite site))
                    results.Add(site);
            }
        }

        return results;
    }

    /// <summary>
    /// True if the given world position lies inside this chunk's bounds.
    /// Used so a structure prefab is instantiated exactly once, by the chunk
    /// that owns its center - even though many chunks know it exists.
    /// </summary>
    public static bool OwnsSite(TerrainSettings s, ChunkCoord coord, Vector2 center)
    {
        float minX = coord.x * s.chunkSize;
        float minZ = coord.z * s.chunkSize;
        return center.x >= minX && center.x < minX + s.chunkSize
            && center.y >= minZ && center.y < minZ + s.chunkSize;
    }
}