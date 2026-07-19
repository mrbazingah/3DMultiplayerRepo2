using System.Collections.Generic;
using UnityEngine;

public struct NatureSpawn
{
    public int layerIndex;
    public int prefabIndex;
    public Vector3 worldPosition;
    public Quaternion rotation;
    public float scale;
}

public struct StructureSpawn
{
    public int typeIndex;
    public Vector3 worldPosition;
    public Quaternion rotation;
}

public class ChunkData
{
    public ChunkCoord coord;
    public Vector3[] vertices;
    public Vector3[] normals;
    public List<NatureSpawn> nature;
    public List<StructureSpawn> structures;
}

// Pure functions only (no Unity scene API), so everything here is background-thread safe.
public static class ChunkBuilder
{
    // Identical topology across chunks, so triangles/UVs are built once and shared.
    private static int[] sharedTriangles;
    private static Vector2[] sharedUVs;
    private static int sharedResolution = -1;

    // No structure flattening here - sites flatten *to* this, so it must not recurse.
    public static float BaseHeight(DeterministicNoise noise, TerrainSettings s, float worldX, float worldZ)
    {
        return noise.Fbm(
            worldX * s.noiseScale,
            worldZ * s.noiseScale,
            s.octaves, s.lacunarity, s.persistence) * s.heightScale;
    }

    // Pass the chunk's cached site list; never gather per vertex.
    public static float SampleHeight(
        DeterministicNoise noise, TerrainSettings s,
        List<StructureSite> sites, float worldX, float worldZ)
    {
        float h = BaseHeight(noise, s, worldX, worldZ);
        if (sites == null) return h;

        for (int i = 0; i < sites.Count; i++)
        {
            StructureSite site = sites[i];

            float dx = worldX - site.center.x;
            float dz = worldZ - site.center.y;
            float distSq = dx * dx + dz * dz;

            float outer = site.flattenRadius + site.flattenFalloff;
            if (distSq >= outer * outer) continue;

            float dist = Mathf.Sqrt(distSq);

            // t=0 at center (flat), t=1 at edge (natural).
            float t = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(site.flattenRadius, outer, dist));

            h = Mathf.Lerp(site.groundHeight, h, t);
        }

        return h;
    }

    // Central differences on the height function: no border seams, and runs off-thread.
    public static Vector3 SampleNormal(
        DeterministicNoise noise, TerrainSettings s,
        List<StructureSite> sites, float worldX, float worldZ)
    {
        const float e = 0.5f;
        float hL = SampleHeight(noise, s, sites, worldX - e, worldZ);
        float hR = SampleHeight(noise, s, sites, worldX + e, worldZ);
        float hD = SampleHeight(noise, s, sites, worldX, worldZ - e);
        float hU = SampleHeight(noise, s, sites, worldX, worldZ + e);
        return new Vector3(hL - hR, 2f * e, hD - hU).normalized;
    }

    public static ChunkData Build(DeterministicNoise noise, TerrainSettings s, int worldSeed, ChunkCoord coord)
    {
        // Gather once per chunk, then reuse for every vertex/normal/nature test.
        List<StructureSite> sites = StructureSites.GatherNearby(noise, s, worldSeed, coord);

        int res = s.resolution;
        int verts = res + 1;
        float step = (float)s.chunkSize / res;
        float originX = coord.x * s.chunkSize;
        float originZ = coord.z * s.chunkSize;

        var data = new ChunkData
        {
            coord = coord,
            vertices = new Vector3[verts * verts],
            normals = new Vector3[verts * verts],
        };

        for (int z = 0, i = 0; z < verts; z++)
        {
            for (int x = 0; x < verts; x++, i++)
            {
                // Sample in WORLD space so adjacent chunks match at the edges.
                float wx = originX + x * step;
                float wz = originZ + z * step;

                float h = SampleHeight(noise, s, sites, wx, wz);

                // Vertex is chunk-local; the chunk GameObject sits at the origin.
                data.vertices[i] = new Vector3(x * step, h, z * step);
                data.normals[i] = SampleNormal(noise, s, sites, wx, wz);
            }
        }

        data.structures = CollectOwnedStructures(s, coord, sites);
        data.nature = PlaceNature(noise, s, worldSeed, coord, originX, originZ, sites);
        return data;
    }

    // Only the chunk owning a site's center instantiates it - no duplicates on borders.
    private static List<StructureSpawn> CollectOwnedStructures(
        TerrainSettings s, ChunkCoord coord, List<StructureSite> sites)
    {
        var spawns = new List<StructureSpawn>();

        for (int i = 0; i < sites.Count; i++)
        {
            StructureSite site = sites[i];
            if (!StructureSites.OwnsSite(s, coord, site.center)) continue;

            spawns.Add(new StructureSpawn
            {
                typeIndex = site.typeIndex,
                worldPosition = new Vector3(site.center.x, site.groundHeight, site.center.y),
                rotation = Quaternion.Euler(0f, site.yRotation, 0f),
            });
        }

        return spawns;
    }

    // Deterministic scatter seeded per (world, chunk, layer); structure clearances carve holes.
    private static List<NatureSpawn> PlaceNature(
        DeterministicNoise noise, TerrainSettings s, int worldSeed,
        ChunkCoord coord, float originX, float originZ, List<StructureSite> sites)
    {
        var spawns = new List<NatureSpawn>();
        if (s.natureLayers == null) return spawns;

        for (int layer = 0; layer < s.natureLayers.Length; layer++)
        {
            NatureLayer nl = s.natureLayers[layer];
            if (nl.prefabs == null || nl.prefabs.Length == 0) continue;

            int seed = worldSeed
                ^ (coord.x * 73856093)
                ^ (coord.z * 19349663)
                ^ (layer * 83492791);
            var rng = new System.Random(seed);

            float cosMaxSlope = Mathf.Cos(nl.maxSlopeDegrees * Mathf.Deg2Rad);

            for (int i = 0; i < nl.countPerChunk; i++)
            {
                // Consume every rng value up front, before any reject - or later objects shift and determinism breaks.
                float localX = (float)rng.NextDouble() * s.chunkSize;
                float localZ = (float)rng.NextDouble() * s.chunkSize;
                int prefabIndex = rng.Next(nl.prefabs.Length);
                float yRot = (float)rng.NextDouble() * 360f;
                float scale = Mathf.Lerp(nl.uniformScaleRange.x, nl.uniformScaleRange.y,
                                         (float)rng.NextDouble());

                float wx = originX + localX;
                float wz = originZ + localZ;

                if (IsBlockedByStructure(sites, wx, wz, nl.structureClearancePadding)) continue;

                float h = SampleHeight(noise, s, sites, wx, wz);
                if (h < nl.minHeight || h > nl.maxHeight) continue;

                Vector3 groundNormal = SampleNormal(noise, s, sites, wx, wz);
                if (groundNormal.y < cosMaxSlope) continue;

                Quaternion rot = Quaternion.identity;
                if (nl.alignToGroundNormal)
                    rot = Quaternion.FromToRotation(Vector3.up, groundNormal);
                if (nl.randomYRotation)
                    rot *= Quaternion.Euler(0f, yRot, 0f);

                spawns.Add(new NatureSpawn
                {
                    layerIndex = layer,
                    prefabIndex = prefabIndex,
                    worldPosition = new Vector3(wx, h, wz),
                    rotation = rot,
                    scale = scale,
                });
            }
        }

        return spawns;
    }

    private static bool IsBlockedByStructure(List<StructureSite> sites, float wx, float wz, float padding)
    {
        for (int i = 0; i < sites.Count; i++)
        {
            StructureSite site = sites[i];
            float r = site.clearanceRadius + padding;
            float dx = wx - site.center.x;
            float dz = wz - site.center.y;
            if (dx * dx + dz * dz < r * r) return true;
        }
        return false;
    }

    public static int[] GetSharedTriangles(int resolution)
    {
        EnsureShared(resolution);
        return sharedTriangles;
    }

    public static Vector2[] GetSharedUVs(int resolution)
    {
        EnsureShared(resolution);
        return sharedUVs;
    }

    private static void EnsureShared(int resolution)
    {
        if (sharedResolution == resolution) return;
        sharedResolution = resolution;

        int verts = resolution + 1;

        sharedTriangles = new int[resolution * resolution * 6];
        for (int z = 0, v = 0, t = 0; z < resolution; z++, v++)
        {
            for (int x = 0; x < resolution; x++, v++, t += 6)
            {
                sharedTriangles[t + 0] = v;
                sharedTriangles[t + 1] = v + verts;
                sharedTriangles[t + 2] = v + 1;
                sharedTriangles[t + 3] = v + 1;
                sharedTriangles[t + 4] = v + verts;
                sharedTriangles[t + 5] = v + verts + 1;
            }
        }

        sharedUVs = new Vector2[verts * verts];
        for (int z = 0, i = 0; z < verts; z++)
            for (int x = 0; x < verts; x++, i++)
                sharedUVs[i] = new Vector2((float)x / resolution, (float)z / resolution);
    }
}