using System.Collections.Generic;
using UnityEngine;

public struct StructureSite
{
    public int typeIndex;
    public Vector2 center;
    public float groundHeight;
    public float clearanceRadius;
    public float flattenRadius;
    public float flattenFalloff;
    public float yRotation;
}

// Placement depends only on (worldSeed, region), so it's independent of chunk load order.
public static class StructureSites
{
    public static bool TryGetSite(
        DeterministicNoise noise, TerrainSettings s, int worldSeed,
        int regionX, int regionZ, out StructureSite site)
    {
        site = default;
        if (s.structureTypes == null || s.structureTypes.Length == 0) return false;

        int seed = worldSeed ^ (regionX * 6971) ^ (regionZ * 41333);
        var rng = new System.Random(seed);

        // Consume rng values in a fixed order so results never shift.
        int typeIndex = rng.Next(s.structureTypes.Length);
        double roll = rng.NextDouble();
        float u = (float)rng.NextDouble();
        float v = (float)rng.NextDouble();
        float yRot = (float)rng.NextDouble() * 360f;

        StructureType type = s.structureTypes[typeIndex];
        if (type.prefab == null) return false;

        if (roll > type.chancePerRegion) return false;

        // Inset by margin so a structure's radius can't reach past the 3x3 block chunks search.
        float margin = Mathf.Max(type.clearanceRadius, type.flattenRadius + type.flattenFalloff);
        float usable = s.regionSize - margin * 2f;
        if (usable <= 0f) return false;

        float wx = regionX * s.regionSize + margin + u * usable;
        float wz = regionZ * s.regionSize + margin + v * usable;

        // Unflattened height - must not be flattened itself, or the height function recurses.
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

        return true;
    }

    public static List<StructureSite> GatherNearby(
        DeterministicNoise noise, TerrainSettings s, int worldSeed, ChunkCoord coord)
    {
        var results = new List<StructureSite>();

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

    public static bool OwnsSite(TerrainSettings s, ChunkCoord coord, Vector2 center)
    {
        float minX = coord.x * s.chunkSize;
        float minZ = coord.z * s.chunkSize;
        return center.x >= minX && center.x < minX + s.chunkSize
            && center.y >= minZ && center.y < minZ + s.chunkSize;
    }
}