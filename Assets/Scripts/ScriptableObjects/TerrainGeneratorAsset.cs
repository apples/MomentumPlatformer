using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;

[CreateAssetMenu(menuName = "Terrain Generator Asset")]
public class TerrainGeneratorAsset : ScriptableObject
{
    public int numChunks = 0;
    public int chunkResolution = 1025;
    public Vector3 terrainSize = new Vector3(1024, 1024, 1024);
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

    public JobHandle StartJob(int chunkX, int chunkZ, out TerrainGeneratorJob job)
    {
        Debug.Assert(seed != 0);

        var chunkSeed = seed ^ (uint)(chunkX << 16) ^ (uint)chunkZ;
        if (chunkSeed == 0)
        {
            chunkSeed = 1;
        }

        var rng = new Unity.Mathematics.Random(chunkSeed);

        var origin = new float2((float)((rng.NextDouble() - 0.5) * originRange), (float)((rng.NextDouble() - 0.5) * originRange));

        job = new TerrainGeneratorJob
        {
            chunkX = chunkX,
            chunkZ = chunkZ,
            chunkSize = chunkResolution,
            terrainSize = terrainSize,
            randomSeed = rng.NextUInt(),
            origin = origin,
            scale = noiseScale,
            noiseType = noiseType,
            noiseHeight = noiseHeight,
            gradientStart = noiseHeight,
            gradientEnd = 1f - noiseHeight,
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
            heights = new NativeArray<float>(chunkResolution * chunkResolution, Allocator.TempJob),
            trees = new NativeArray<TreeInstance>(chunkResolution * chunkResolution, Allocator.TempJob),
            numTrees = new NativeArray<int>(1, Allocator.TempJob),
            sigils = new NativeArray<float2>(chunkResolution * chunkResolution, Allocator.TempJob),
            numSigils = new NativeArray<int>(1, Allocator.TempJob),
            alphamapResolution = chunkResolution - 1,
            grassAlpha = new NativeArray<float>((chunkResolution - 1) * (chunkResolution - 1), Allocator.TempJob),
        };

        return job.Schedule();
    }

    public void ApplyTerrainData(ref TerrainGeneratorJob job, TerrainData terrainData)
    {
        var heights = new float[chunkResolution, chunkResolution];
        for (int x = 0; x < chunkResolution; x++)
        {
            for (int z = 0; z < chunkResolution; z++)
            {
                heights[z, x] = job.heights[x * chunkResolution + z];
            }
        }

        // heightmap

        terrainData.heightmapResolution = chunkResolution;
        terrainData.size = terrainSize;
        terrainData.SetHeights(0, 0, heights);

        // textures

        terrainData.alphamapResolution = chunkResolution - 1;
        terrainData.terrainLayers = terrainLayers.Prepend(baseLayer).ToArray();

        var alphamaps = new float[terrainData.alphamapResolution, terrainData.alphamapResolution, terrainData.alphamapLayers];

        for (int x = 0; x < terrainData.alphamapResolution; x++)
        {
            for (int z = 0; z < terrainData.alphamapResolution; z++)
            {
                alphamaps[z, x, 0] = 1f;
                alphamaps[z, x, 1] = job.grassAlpha[z * terrainData.alphamapResolution + x];
            }
        }

        terrainData.SetAlphamaps(0, 0, alphamaps);

        // trees

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

        var trees = new TreeInstance[job.numTrees[0]];
        for (int i = 0; i < job.numTrees[0]; i++)
        {
            trees[i] = job.trees[i];
        }
        terrainData.SetTreeInstances(trees, true);
    }
}
