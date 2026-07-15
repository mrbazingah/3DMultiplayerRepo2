using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One nature object to spawn, fully decided on the background thread.
/// Only Instantiate() remains for the main thread.
/// </summary>
public struct NatureSpawn
{
    public int layerIndex;
    public int prefabIndex;
    public Vector3 worldPosition;
    public Quaternion rotation;
    public float scale;
}

/// <summary>
/// A structure this chunk owns and must instantiate.
/// </summary>
public struct StructureSpawn
{
    public int typeIndex;
    public Vector3 worldPosition;
    public Quaternion rotation;
}

/// <summary>
/// Everything a chunk needs, computed off the main thread.
/// </summary>
public class ChunkData
{
    public ChunkCoord coord;
    public Vector3[] vertices;
    public Vector3[] normals;
    public List<NatureSpawn> nature;
    public List<StructureSpawn> structures;
}

/// <summary>
/// Pure functions that build chunk data. No Unity scene API is touched here,
/// so everything in this class is safe to run on a background thread.
/// </summary>
public static class ChunkBuilder
{
    // Triangles and UVs are identical for every chunk (same grid topology),
    // so they are built once and shared. Saves memory and time per chunk.
    private static int[] sharedTriangles;
    private static Vector2[] sharedUVs;
    private static int sharedResolution = -1;

    // ------------------------------------------------------------------
    // Height
    // ------------------------------------------------------------------

    /// <summary>
    /// Raw noise height, with no structure flattening applied.
    /// StructureSites uses this to decide what height to flatten a site *to*,
    /// which is why it must stay free of any structure influence - otherwise
    /// the height function would recurse into itself.
    /// </summary>
    public static float BaseHeight(DeterministicNoise noise, TerrainSettings s, float worldX, float worldZ)
    {
        return noise.Fbm(
            worldX * s.noiseScale,
            worldZ * s.noiseScale,
            s.octaves, s.lacunarity, s.persistence) * s.heightScale;
    }

    /// <summary>
    /// Final terrain height: natural noise, flattened toward the local ground
    /// height near any structure site.
    ///
    /// Because this lives in the height function itself, flattening blends
    /// seamlessly across chunk borders for free - the same reason normals are
    /// seamless. Pass the chunk's cached site list; never gather per vertex.
    /// </summary>
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
            if (distSq >= outer * outer) continue; // outside this site's influence

            float dist = Mathf.Sqrt(distSq);

            // t = 0 fully flat (at the center), t = 1 fully natural (at the edge)
            float t = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(site.flattenRadius, outer, dist));

            h = Mathf.Lerp(site.groundHeight, h, t);
        }

        return h;
    }

    /// <summary>
    /// Normal derived from the height *function* (central differences) instead
    /// of Mesh.RecalculateNormals(). Two wins:
    /// 1. No lighting seams at chunk borders, because the normal depends only
    ///    on world position, never on which chunk a vertex belongs to.
    /// 2. Runs on a background thread; RecalculateNormals cannot.
    /// </summary>
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

    // ------------------------------------------------------------------
    // Chunk assembly
    // ------------------------------------------------------------------

    /// <summary>Builds all data for one chunk. Background-thread safe.</summary>
    public static ChunkData Build(DeterministicNoise noise, TerrainSettings s, int worldSeed, ChunkCoord coord)
    {
        // Gather once per chunk (9 cheap RNG rolls), then reuse for every
        // vertex, normal and nature test below.
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

    /// <summary>
    /// Many chunks know a site exists; exactly one instantiates it - the chunk
    /// containing its center. Prevents duplicate villages on chunk borders.
    /// </summary>
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
                // Sits on the flattened plateau, which equals groundHeight at the center.
                worldPosition = new Vector3(site.center.x, site.groundHeight, site.center.y),
                rotation = Quaternion.Euler(0f, site.yRotation, 0f),
            });
        }

        return spawns;
    }

    // ------------------------------------------------------------------
    // Nature
    // ------------------------------------------------------------------

    /// <summary>
    /// Deterministic scatter: the RNG is seeded from world seed + chunk coords
    /// + layer, so a chunk always regenerates the exact same objects after
    /// unloading. Structure clearance radii carve out holes in the scatter.
    /// </summary>
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

            // Unique, deterministic seed per (world, chunk, layer).
            int seed = worldSeed
                ^ (coord.x * 73856093)
                ^ (coord.z * 19349663)
                ^ (layer * 83492791);
            var rng = new System.Random(seed);

            float cosMaxSlope = Mathf.Cos(nl.maxSlopeDegrees * Mathf.Deg2Rad);

            for (int i = 0; i < nl.countPerChunk; i++)
            {
                // IMPORTANT: every random value for this attempt is consumed up
                // front, before any rule can reject the spot. If a rejected tree
                // skipped its rng calls, every later object in the chunk would
                // shift - and determinism would quietly break.
                float localX = (float)rng.NextDouble() * s.chunkSize;
                float localZ = (float)rng.NextDouble() * s.chunkSize;
                int prefabIndex = rng.Next(nl.prefabs.Length);
                float yRot = (float)rng.NextDouble() * 360f;
                float scale = Mathf.Lerp(nl.uniformScaleRange.x, nl.uniformScaleRange.y,
                                         (float)rng.NextDouble());

                float wx = originX + localX;
                float wz = originZ + localZ;

                // Rule: keep clear of structures.
                if (IsBlockedByStructure(sites, wx, wz, nl.structureClearancePadding)) continue;

                // Rule: height band.
                float h = SampleHeight(noise, s, sites, wx, wz);
                if (h < nl.minHeight || h > nl.maxHeight) continue;

                // Rule: slope.
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

    // ------------------------------------------------------------------
    // Shared topology
    // ------------------------------------------------------------------

    /// <summary>Shared triangle indices, built once per resolution.</summary>
    public static int[] GetSharedTriangles(int resolution)
    {
        EnsureShared(resolution);
        return sharedTriangles;
    }

    /// <summary>Shared UVs (0..1 across the chunk), built once per resolution.</summary>
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