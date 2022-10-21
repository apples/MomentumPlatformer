using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;

[CreateAssetMenu(menuName = "Terrain Generator Asset")]
public class TerrainGeneratorAsset : ScriptableObject
{
    public int numChunks = 0;
    public int chunkResolution = 1025;
    public Vector2 chunkSize = new Vector2(1024, 1024);
    public float slopeGrade = 50f;
    public NoiseType noiseType = NoiseType.Simplex;
    public float noiseHeight = 0.01f;
    public Vector2 noiseScale = new Vector2(1, 1);
    public float gutterGuardDistance = 0f;
    public NoiseType treeNoiseType = NoiseType.WorleyF1;
    public Vector2 treeNoiseScale = new Vector2(1, 1);
    public float treeNoiseMin = 0f;
    public float treeNoiseMax = 1f;
    public float treeForcedChance = 0f;
    public float sigilSpacing = 100f;
    public string terrainName = "Terrain";
    public float originRange = 80000;
    public float terrainTreeDrawDistance = 1000;
    public uint seed = 0;
    public List<GameObject> treePrefabs = new List<GameObject>(8);
    public TerrainLayer baseLayer;
    public List<TerrainLayer> terrainLayers = new List<TerrainLayer>(8);

    private uint cacheSeed = 0;
    private float2 cacheOrigin = new float2(0, 0);
    private uint cacheChunkSeedBase = 0;

    private (float2, uint) GetOriginAndSeed(int chunkX, int chunkZ)
    {
        if (seed != cacheSeed)
        {
            cacheSeed = seed;
            var attempts = 5;
            while (cacheSeed == 0)
            {
                cacheSeed = MakeRandomSeed();
                if (--attempts == 0)
                {
                    cacheSeed = 1;
                    Debug.LogWarning("Failed to generate a random seed, using 1 instead");
                    break;
                }
            }
            var rng = new Unity.Mathematics.Random(cacheSeed);
            cacheOrigin = new float2((float)((rng.NextDouble() - 0.5) * originRange), (float)((rng.NextDouble() - 0.5) * originRange));
            cacheChunkSeedBase = rng.NextUInt();
        }

        return (cacheOrigin, cacheChunkSeedBase ^ (uint)(chunkX << 16) ^ (uint)chunkZ);
    }

    public enum NoiseType
    {
        Perlin,
        Simplex,
        WorleyF1,
        WorleyF2,
    }

    public static uint MakeRandomSeed()
    {
        Span<byte> bytes = stackalloc byte[4];
        bytes[0] = (byte)UnityEngine.Random.Range(0, 255);
        bytes[1] = (byte)UnityEngine.Random.Range(0, 255);
        bytes[2] = (byte)UnityEngine.Random.Range(0, 255);
        bytes[3] = (byte)UnityEngine.Random.Range(0, 255);
        return BitConverter.ToUInt32(bytes);
    }

    public Vector3 GetTerrainSize()
    {
        var rise = slopeGrade / 100f * this.chunkSize.x;
        var terrainHeight = rise + noiseHeight * 2f;
        return new Vector3(this.chunkSize.x, terrainHeight, this.chunkSize.y);
    }

    public Vector3 GetChunkPosition(int chunkX, int chunkZ)
    {
        var rise = slopeGrade / 100f * this.chunkSize.x;
        return new Vector3(chunkX * chunkSize.x, rise * chunkX, chunkZ * chunkSize.y);
    }

    public JobHandle StartJob(int chunkX, int chunkZ, out TerrainGeneratorJob job)
    {
        Debug.Assert(seed != 0);

        var (origin, chunkSeed) = GetOriginAndSeed(chunkX, chunkZ);

        var allocator = Allocator.Persistent;

        var terrainSize = GetTerrainSize();

        var normalizedNoiseHeight = this.noiseHeight / terrainSize.y;

        job = new TerrainGeneratorJob
        {
            chunkX = chunkX,
            chunkZ = chunkZ,
            chunkResolution = chunkResolution,
            terrainSize = chunkSize,
            chunkSeed = chunkSeed,
            origin = origin,
            scale = noiseScale,
            noiseType = noiseType,
            normalizedNoiseHeight = normalizedNoiseHeight,
            gradientStart = normalizedNoiseHeight,
            gradientEnd = 1f - normalizedNoiseHeight,
            gutterGuardDistance = gutterGuardDistance,
            treeSpacing = 10,
            minTreeHeight = 1,
            maxTreeHeight = 2,
            minTreeWidth = 1,
            maxTreeWidth = 2,
            treeNoiseType = treeNoiseType,
            treeNoiseScale = treeNoiseScale,
            treeNoiseMin = treeNoiseMin,
            treeNoiseMax = treeNoiseMax,
            treeForcedChance = treeForcedChance,
            sigilSpacing = sigilSpacing,
            heights = new NativeArray<float>(chunkResolution * chunkResolution, allocator),
            trees = new NativeArray<TreeInstance>(chunkResolution * chunkResolution, allocator),
            numTrees = new NativeReference<int>(0, allocator),
            sigils = new NativeArray<float2>(chunkResolution * chunkResolution, allocator),
            numSigils = new NativeReference<int>(0, allocator),
            alphamapResolution = chunkResolution - 1,
            alphaMaps = new NativeArray<float>((chunkResolution - 1) * (chunkResolution - 1) * 2, allocator),
        };

        return job.Schedule();
    }

    public void ApplyTerrainData(ref TerrainGeneratorJob job, TerrainData terrainData)
    {
        var marker = new ProfilerMarker("TerrainGeneratorAsset.ApplyTerrainData");
        marker.Begin();

        var terrainSize = GetTerrainSize();

        var markerCopyHeights = new ProfilerMarker("TerrainGeneratorAsset.ApplyTerrainData.CopyHeights");
        markerCopyHeights.Begin();

        var heights = new float[chunkResolution, chunkResolution];

        unsafe
        {
            UnsafeUtility.MemCpy(
                UnsafeUtility.AddressOf(ref heights[0, 0]),
                job.heights.GetUnsafeReadOnlyPtr(),
                chunkResolution * chunkResolution * sizeof(float));
        }

        // for (int x = 0; x < chunkResolution; x++)
        // {
        //     for (int z = 0; z < chunkResolution; z++)
        //     {
        //         heights[z, x] = job.heights[x * chunkResolution + z];
        //     }
        // }

        markerCopyHeights.End();

        // heightmap

        var markerSetHeights = new ProfilerMarker("TerrainGeneratorAsset.ApplyTerrainData.SetHeights");
        markerSetHeights.Begin();

        terrainData.heightmapResolution = chunkResolution;
        terrainData.size = terrainSize;
        terrainData.SetHeights(0, 0, heights);

        markerSetHeights.End();

        // textures

        var markerSetTextures = new ProfilerMarker("TerrainGeneratorAsset.ApplyTerrainData.SetTextures");
        markerSetTextures.Begin();

        terrainData.alphamapResolution = chunkResolution - 1;
        terrainData.terrainLayers = terrainLayers.Prepend(baseLayer).ToArray();

        var alphamaps = new float[terrainData.alphamapResolution, terrainData.alphamapResolution, terrainData.alphamapLayers];

        var markerCopyAlpha = new ProfilerMarker("TerrainGeneratorAsset.ApplyTerrainData.CopyAlpha");
        markerCopyAlpha.Begin();

        unsafe
        {
            UnsafeUtility.MemCpy(
                UnsafeUtility.AddressOf(ref alphamaps[0, 0, 0]),
                job.alphaMaps.GetUnsafeReadOnlyPtr(),
                terrainData.alphamapResolution * terrainData.alphamapResolution * terrainData.alphamapLayers * sizeof(float));
        }

        // for (int x = 0; x < terrainData.alphamapResolution; x++)
        // {
        //     for (int z = 0; z < terrainData.alphamapResolution; z++)
        //     {
        //         alphamaps[z, x, 0] = 1f;
        //         alphamaps[z, x, 1] = job.alphaMaps[z * terrainData.alphamapResolution + x];
        //     }
        // }

        markerCopyAlpha.End();

        var markerAlphamap = new ProfilerMarker("TerrainGeneratorAsset.ApplyTerrainData.SetAlphamaps");
        markerAlphamap.Begin();

        terrainData.SetAlphamaps(0, 0, alphamaps);

        markerAlphamap.End();
        markerSetTextures.End();

        // trees

        var markerSetTrees = new ProfilerMarker("TerrainGeneratorAsset.ApplyTerrainData.SetTrees");
        markerSetTrees.Begin();

        var treePrototypes = new TreePrototype[treePrefabs.Count];
        for (int i = 0; i < treePrefabs.Count; i++)
        {
            treePrototypes[i] = new TreePrototype
            {
                prefab = treePrefabs[i],
            };
        }
        terrainData.treePrototypes = treePrototypes;
        terrainData.RefreshPrototypes();

        var trees = new TreeInstance[job.numTrees.Value];
        for (int i = 0; i < job.numTrees.Value; i++)
        {
            trees[i] = job.trees[i];
        }
        terrainData.SetTreeInstances(trees, true);

        markerSetTrees.End();

        marker.End();
    }
}
