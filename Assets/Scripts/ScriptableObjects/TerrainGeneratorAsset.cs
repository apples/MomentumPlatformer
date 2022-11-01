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
using UnityEngine.Pool;

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
    public float sigilSpacing = 500f;
    public string terrainName = "Terrain";
    public float originRange = 80000;
    public float terrainTreeDrawDistance = 1000;
    public uint seed = 0;
    public TerrainLayer baseLayer;
    public List<TerrainLayer> terrainLayers = new List<TerrainLayer>();
    public GameObject sigilPrefab;
    public List<FoliageLayer> foliage = new List<FoliageLayer>();

    private uint cacheSeed = 0;
    private float2 cacheOrigin = new float2(0, 0);
    private uint cacheChunkSeedBase = 0;

    private (float2, uint) GetOriginAndSeed(int chunkX, int chunkZ)
    {
        if (cacheSeed == 0)
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
            Debug.Log($"Generated new origin {cacheOrigin} and seed {cacheSeed}");
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
        var (origin, chunkSeed) = GetOriginAndSeed(chunkX, chunkZ);

        var allocator = Allocator.Persistent;

        var terrainSize = GetTerrainSize();

        var normalizedNoiseHeight = this.noiseHeight / terrainSize.y;

        var foliageParams = new NativeArray<FoliageLayer.FoliageParams>(this.foliage.Count, allocator);
        var foliageBounds = new NativeArray<Bounds>(this.foliage.Count, allocator);
        for (var i = 0; i < this.foliage.Count; i++)
        {
            foliageParams[i] = this.foliage[i].foliageParams;
            foliageBounds[i] = FoliageLOD.SumBounds(this.foliage[i].lods);
        }

        job = new TerrainGeneratorJob
        {
            chunkX = chunkX,
            chunkZ = chunkZ,
            chunkResolution = chunkResolution,
            terrainSize = chunkSize,
            terrainHeight = terrainSize.y,
            chunkSeed = chunkSeed,
            worldPosition = GetChunkPosition(chunkX, chunkZ),
            origin = origin,
            scale = noiseScale,
            noiseType = noiseType,
            normalizedNoiseHeight = normalizedNoiseHeight,
            gradientStart = normalizedNoiseHeight,
            gradientEnd = 1f - normalizedNoiseHeight,
            gutterGuardDistance = gutterGuardDistance,
            treeNoiseType = treeNoiseType,
            treeNoiseScale = treeNoiseScale,
            treeNoiseMin = treeNoiseMin,
            treeNoiseMax = treeNoiseMax,
            sigilSpacing = sigilSpacing,
            alphamapResolution = chunkResolution - 1,
            foliageParams = foliageParams,
            foliageBounds = foliageBounds,
        };
        job.Allocate(allocator);

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

        markerCopyAlpha.End();

        var markerAlphamap = new ProfilerMarker("TerrainGeneratorAsset.ApplyTerrainData.SetAlphamaps");
        markerAlphamap.Begin();

        terrainData.SetAlphamaps(0, 0, alphamaps);

        markerAlphamap.End();
        markerSetTextures.End();

        marker.End();
    }

    public void ApplyToGameObject(ref TerrainGeneratorJob job, GameObject gameObject, ObjectPool<GameObject> sigilPool, FoliagePool foliagePool)
    {
        var terrainData = gameObject.GetComponent<Terrain>().terrainData;
        ApplyTerrainData(ref job, terrainData);
        // gameObject.SetActive(true);

        gameObject.name = $"{terrainName}_{job.chunkX}_{job.chunkZ}";
        gameObject.transform.position = GetChunkPosition(job.chunkX, job.chunkZ);

        gameObject.GetComponent<Terrain>().treeDistance = terrainTreeDrawDistance;
        gameObject.GetComponent<Terrain>().Flush();

        // foliage

        for (var i = 0; i < foliage.Count; ++i)
        {
            var foliageData = foliage[i];
            var foliageResult = job.foliageResults[i];

            var foliageObject = gameObject.transform.Find($"Foliage_{foliageData.name}");

            var foliageInstanceRenderer = foliageObject.GetComponent<FoliageRenderer>();

            foliageInstanceRenderer.lods = foliageData.lods;

            var foliageBounds = foliageResult.bounds;

            // spawn prefabs

            if (foliageData.prefab != null)
            {
                var pool = foliagePool.GetPool(foliageData);
                for (var j = 0; j < foliageResult.count; j++)
                {
                    var k = foliageResult.index + j;
                    var foliageInfo = job.foliageInfo[k];
                    var foliageObj = pool.Get();

                    foliageObj.transform.SetParent(foliageObject);
                    foliageObj.transform.position = foliageInfo.position;
                    foliageObj.transform.rotation = foliageInfo.rotation;
                    foliageObj.transform.localScale = foliageInfo.scale;
                }
            }

            var instances = new NativeSlice<FoliageRenderer.MeshInstanceData>(job.foliageInstanceData, foliageResult.index, foliageResult.count);

            foliageInstanceRenderer.SetMeshInstances(instances.ToArray(), foliageBounds);
        }

        // spawn sigils

        var sigils = gameObject.transform.Find("Sigils");

        for (var i = 0; i < job.sigils.Length; i++)
        {
            var sigil = job.sigils[i];

            var y = terrainData.GetInterpolatedHeight(sigil.x, sigil.y);
            var normal = terrainData.GetInterpolatedNormal(sigil.x, sigil.y);

            var sigilObject = sigilPool.Get();

            sigilObject.transform.SetParent(sigils);
            sigilObject.transform.localPosition = new Vector3(sigil.x * terrainData.size.x, y, sigil.y * terrainData.size.z);
            sigilObject.transform.Find("BrazierModel").up = normal;
        }
    }
}
