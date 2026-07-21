using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TerrainSettings settings;
    [SerializeField] private Material terrainMaterial;
    [SerializeField] private Transform player;

    [Header("World")]
    [SerializeField] private int worldSeed = 12345;

    [Header("Streaming")]
    [Tooltip("Chunks loaded in each direction around the player. Mesh only beyond the tiers below.")]
    [SerializeField] private int viewDistance = 6;

    [Tooltip("Chunks within this radius get MeshColliders. Keep small - only where the player can walk.")]
    [SerializeField] private int collisionDistance = 2;

    [Tooltip("Chunks within this radius get nature objects and structures.")]
    [SerializeField] private int detailDistance = 3;

    [Header("Per-frame budgets")]
    [Tooltip("Finished chunks uploaded to meshes per frame.")]
    [SerializeField] private int chunksAppliedPerFrame = 2;

    [Tooltip("Nature prefabs instantiated per frame.")]
    [SerializeField] private int natureSpawnsPerFrame = 60;

    [Tooltip("Structure prefabs instantiated per frame. Keep low - these are heavy.")]
    [SerializeField] private int structureSpawnsPerFrame = 1;

    private DeterministicNoise noise;

    private readonly Dictionary<ChunkCoord, Chunk> loaded = new();
    private readonly HashSet<ChunkCoord> pending = new();
    private readonly ConcurrentQueue<ChunkData> completed = new();
    private readonly Queue<(Chunk chunk, NatureSpawn spawn)> natureQueue = new();
    private readonly Queue<(Chunk chunk, StructureSpawn spawn)> structureQueue = new();
    private readonly List<(Task bake, Chunk chunk)> bakingColliders = new();
    private readonly Stack<Chunk> pool = new();

    private ChunkCoord lastPlayerChunk = new ChunkCoord(int.MinValue, int.MinValue);

    private class Chunk
    {
        public ChunkCoord coord;
        public GameObject go;
        public MeshFilter filter;
        public MeshCollider collider;
        public Transform detailRoot;
        public Mesh mesh;

        // Retained so details can spawn later when the chunk enters the detail tier.
        public List<NatureSpawn> natureData;
        public List<StructureSpawn> structureData;

        public bool hasCollider;
        public bool hasDetails;
    }

    public void SetSeed(int seed)
    {
        worldSeed = seed;
        noise = new DeterministicNoise(worldSeed);
        lastPlayerChunk = new ChunkCoord(int.MinValue, int.MinValue);
    }

    private void Awake()
    {
        if (noise == null) noise = new DeterministicNoise(worldSeed);
    }

    public void AssignPlayer(Transform player)
    {
        this.player = player;
    }

    private void Update()
    {
        if (player == null || settings == null) return;

        // Only recompute needed chunks when the player crosses into a new chunk.
        ChunkCoord playerChunk = ChunkCoord.FromWorld(player.position, settings.chunkSize);
        if (!playerChunk.Equals(lastPlayerChunk))
        {
            lastPlayerChunk = playerChunk;
            RefreshNeededChunks(playerChunk);
            UpdateTiers(playerChunk);
        }

        ApplyCompletedChunks(playerChunk);

        FinishColliderBakes();

        SpawnQueuedStructures();
        SpawnQueuedNature();
    }

    private void RefreshNeededChunks(ChunkCoord center)
    {
        var needed = new HashSet<ChunkCoord>();
        for (int z = -viewDistance; z <= viewDistance; z++)
            for (int x = -viewDistance; x <= viewDistance; x++)
                needed.Add(new ChunkCoord(center.x + x, center.z + z));

        var toUnload = new List<ChunkCoord>();
        foreach (var kv in loaded)
            if (!needed.Contains(kv.Key))
                toUnload.Add(kv.Key);
        foreach (var coord in toUnload)
            Unload(coord);

        var missing = new List<ChunkCoord>();
        foreach (var coord in needed)
            if (!loaded.ContainsKey(coord) && !pending.Contains(coord))
                missing.Add(coord);

        // Nearest first, so the ground under the player arrives before distant scenery.
        missing.Sort((a, b) =>
        {
            int da = (a.x - center.x) * (a.x - center.x) + (a.z - center.z) * (a.z - center.z);
            int db = (b.x - center.x) * (b.x - center.x) + (b.z - center.z) * (b.z - center.z);
            return da.CompareTo(db);
        });

        foreach (var coord in missing)
        {
            pending.Add(coord);
            // capture a copy for the closure
            ChunkCoord c = coord;
            Task.Run(() =>
            {
                ChunkData data = ChunkBuilder.Build(noise, settings, worldSeed, c);
                completed.Enqueue(data);
            });
        }
    }

    // Re-evaluate tiers every frame, or a chunk generated far away would never gain a collider as you approach.
    private void UpdateTiers(ChunkCoord center)
    {
        foreach (var kv in loaded)
            UpdateTiersFor(kv.Value, center);
    }

    private void UpdateTiersFor(Chunk chunk, ChunkCoord center)
    {
        int dist = ChebyshevDistance(chunk.coord, center);

        if (dist <= collisionDistance && !chunk.hasCollider)
        {
            chunk.hasCollider = true;

            // Bake off-thread; assigning sharedMesh afterwards reuses it with no main-thread hitch.
            int meshId = chunk.mesh.GetInstanceID();
            Task bake = Task.Run(() => Physics.BakeMesh(meshId, false));
            bakingColliders.Add((bake, chunk));
        }
        else if (dist > collisionDistance && chunk.hasCollider)
        {
            chunk.hasCollider = false;
            chunk.collider.sharedMesh = null;
        }

        if (dist <= detailDistance && !chunk.hasDetails)
        {
            chunk.hasDetails = true;

            if (chunk.structureData != null)
                foreach (StructureSpawn s in chunk.structureData)
                    structureQueue.Enqueue((chunk, s));

            if (chunk.natureData != null)
                foreach (NatureSpawn s in chunk.natureData)
                    natureQueue.Enqueue((chunk, s));
        }
        else if (dist > detailDistance && chunk.hasDetails)
        {
            chunk.hasDetails = false;
            ClearDetails(chunk);
        }
    }

    private void ClearDetails(Chunk chunk)
    {
        for (int i = chunk.detailRoot.childCount - 1; i >= 0; i--)
            Destroy(chunk.detailRoot.GetChild(i).gameObject);
    }

    private void ApplyCompletedChunks(ChunkCoord center)
    {
        int budget = chunksAppliedPerFrame;
        while (budget > 0 && completed.TryDequeue(out ChunkData data))
        {
            pending.Remove(data.coord);

            // The player may have moved on while this was generating.
            if (!InRange(data.coord, center) || loaded.ContainsKey(data.coord))
                continue;

            Chunk chunk = GetPooledChunk();
            chunk.coord = data.coord;
            chunk.natureData = data.nature;
            chunk.structureData = data.structures;
            chunk.hasCollider = false;
            chunk.hasDetails = false;
            chunk.go.name = $"Chunk {data.coord}";
            chunk.go.transform.position = new Vector3(
                data.coord.x * settings.chunkSize, 0f, data.coord.z * settings.chunkSize);

            Mesh mesh = chunk.mesh;
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = data.vertices;
            mesh.normals = data.normals;
            mesh.uv = ChunkBuilder.GetSharedUVs(settings.resolution);
            mesh.triangles = ChunkBuilder.GetSharedTriangles(settings.resolution);
            mesh.RecalculateBounds();

            chunk.filter.sharedMesh = mesh;
            chunk.go.SetActive(true);
            loaded.Add(data.coord, chunk);

            UpdateTiersFor(chunk, center);

            budget--;
        }
    }

    private void FinishColliderBakes()
    {
        for (int i = bakingColliders.Count - 1; i >= 0; i--)
        {
            (Task bake, Chunk chunk) = bakingColliders[i];
            if (!bake.IsCompleted) continue;

            bakingColliders.RemoveAt(i);

            // The chunk may have been unloaded or left the collision tier while baking.
            if (chunk.hasCollider && chunk.go.activeSelf
                && loaded.TryGetValue(chunk.coord, out Chunk current) && current == chunk)
            {
                chunk.collider.sharedMesh = chunk.mesh;
            }
        }
    }

    private void SpawnQueuedStructures()
    {
        int budget = structureSpawnsPerFrame;
        while (budget > 0 && structureQueue.Count > 0)
        {
            (Chunk chunk, StructureSpawn s) = structureQueue.Dequeue();
            if (!IsChunkStillDetailed(chunk)) continue;

            StructureType type = settings.structureTypes[s.typeIndex];
            if (type.prefab == null) continue;

            Instantiate(type.prefab, s.worldPosition, s.rotation, chunk.detailRoot);
            budget--;
        }
    }

    private void SpawnQueuedNature()
    {
        int budget = natureSpawnsPerFrame;
        while (budget > 0 && natureQueue.Count > 0)
        {
            (Chunk chunk, NatureSpawn s) = natureQueue.Dequeue();
            if (!IsChunkStillDetailed(chunk)) continue;

            NatureLayer layer = settings.natureLayers[s.layerIndex];
            GameObject prefab = layer.prefabs[s.prefabIndex];

            GameObject obj = Instantiate(prefab, s.worldPosition, s.rotation, chunk.detailRoot);
            obj.transform.localScale = Vector3.one * s.scale;

            budget--;
        }
    }

    // Skip spawns whose chunk was unloaded, recycled, or left the detail tier.
    private bool IsChunkStillDetailed(Chunk chunk)
    {
        return chunk.hasDetails
            && loaded.TryGetValue(chunk.coord, out Chunk current)
            && current == chunk;
    }

    private void Unload(ChunkCoord coord)
    {
        if (!loaded.TryGetValue(coord, out Chunk chunk)) return;
        loaded.Remove(coord);

        ClearDetails(chunk);

        chunk.hasDetails = false;
        chunk.hasCollider = false;
        chunk.collider.sharedMesh = null;
        chunk.natureData = null;
        chunk.structureData = null;
        chunk.go.SetActive(false);
        // GameObject and Mesh are pooled and reused; only the detail objects were destroyed.
        pool.Push(chunk);
    }

    private Chunk GetPooledChunk()
    {
        if (pool.Count > 0) return pool.Pop();

        var go = new GameObject("Chunk");
        go.transform.SetParent(transform, false);

        var chunk = new Chunk
        {
            go = go,
            filter = go.AddComponent<MeshFilter>(),
            collider = go.AddComponent<MeshCollider>(),
            mesh = new Mesh(),
        };

        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = terrainMaterial;

        var detailRoot = new GameObject("Details");
        detailRoot.transform.SetParent(go.transform, false);
        chunk.detailRoot = detailRoot.transform;

        return chunk;
    }

    private bool InRange(ChunkCoord coord, ChunkCoord center)
    {
        return ChebyshevDistance(coord, center) <= viewDistance;
    }

    private static int ChebyshevDistance(ChunkCoord a, ChunkCoord b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.z - b.z));
    }
}