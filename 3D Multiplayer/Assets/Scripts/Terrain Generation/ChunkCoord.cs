using System;
using UnityEngine;

/// <summary>
/// Integer grid coordinate of a chunk. Used as dictionary key,
/// so it implements fast equality and hashing.
/// </summary>
[Serializable]
public readonly struct ChunkCoord : IEquatable<ChunkCoord>
{
    public readonly int x;
    public readonly int z;

    public ChunkCoord(int x, int z)
    {
        this.x = x;
        this.z = z;
    }

    public bool Equals(ChunkCoord other) => x == other.x && z == other.z;
    public override bool Equals(object obj) => obj is ChunkCoord c && Equals(c);
    public override int GetHashCode() => (x * 73856093) ^ (z * 19349663);
    public override string ToString() => $"({x}, {z})";

    public static ChunkCoord FromWorld(Vector3 pos, float chunkSize)
    {
        return new ChunkCoord(
            Mathf.FloorToInt(pos.x / chunkSize),
            Mathf.FloorToInt(pos.z / chunkSize));
    }
}