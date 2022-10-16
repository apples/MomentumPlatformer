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

public class TerrainGenerator : EditorWindow
{
    private TerrainGeneratorAsset asset;
    private Editor terrainGeneratorAssetEditor;

    [Serializable]
    public class TerrainObjects : ScriptableObject
    {
        public List<GameObject> terrainObjects = new List<GameObject>(8);
    }

    private TerrainObjects terrainObjects;
    private Editor terrainObjectsEditor;

    [MenuItem("Tools/Terrain Generator")]
    public static void ShowWindow()
    {
        GetWindow<TerrainGenerator>("Terrain Generator");
    }

    void OnEnable()
    {
        if (asset != null)
        {
            terrainGeneratorAssetEditor = Editor.CreateEditor(asset);
        }
        terrainObjects = ScriptableObject.CreateInstance<TerrainObjects>();
        terrainObjectsEditor = Editor.CreateEditor(terrainObjects);
    }

    void OnGUI()
    {
        GUILayout.Label("Terrain Generator", EditorStyles.boldLabel);

        asset = (TerrainGeneratorAsset)EditorGUILayout.ObjectField("Terrain Generator Asset", asset, typeof(TerrainGeneratorAsset), false);

        if (asset != null)
        {
            if (terrainGeneratorAssetEditor == null)
            {
                terrainGeneratorAssetEditor = Editor.CreateEditor(asset);
            }
            terrainGeneratorAssetEditor.OnInspectorGUI();
        }
        else
        {
            if (terrainGeneratorAssetEditor != null)
            {
                terrainGeneratorAssetEditor = null;
            }

            return;
        }

        terrainObjectsEditor.OnInspectorGUI();

        if (GUILayout.Button("Randomize Seed"))
        {
            var seed = MakeRandomSeed();
            if (seed == 0)
            {
                seed = 1;
            }
            terrainGeneratorAssetEditor.serializedObject.FindProperty("seed").uintValue = seed;
        }


        terrainGeneratorAssetEditor.serializedObject.ApplyModifiedProperties();
        terrainObjectsEditor.serializedObject.ApplyModifiedProperties();

        if (GUILayout.Button("Generate"))
        {
            Generate();
        }
    }

    private uint MakeRandomSeed()
    {
        Span<byte> bytes = stackalloc byte[4];
        bytes[0] = (byte)UnityEngine.Random.Range(0, 255);
        bytes[1] = (byte)UnityEngine.Random.Range(0, 255);
        bytes[2] = (byte)UnityEngine.Random.Range(0, 255);
        bytes[3] = (byte)UnityEngine.Random.Range(0, 255);
        return BitConverter.ToUInt32(bytes);
    }

    private void Generate()
    {
        var terrainObjectsProp = terrainObjectsEditor.serializedObject.FindProperty("terrainObjects");
        if (this.terrainObjects.terrainObjects.Count < asset.numChunks)
        {
            terrainObjectsProp.arraySize = asset.numChunks;
        }

        var rng = new Unity.Mathematics.Random(asset.seed == 0 ? MakeRandomSeed() : asset.seed);

        var origin = new float2((float)((rng.NextDouble() - 0.5) * asset.originRange), (float)((rng.NextDouble() - 0.5) * asset.originRange));

        var jobs = new List<TerrainGeneratorJob>(asset.numChunks);
        var jobHandles = new List<JobHandle>(asset.numChunks);
        for (int i = 0; i < asset.numChunks; i++)
        {
            var job = new TerrainGeneratorJob
            {
                chunkX = i,
                chunkZ = 0,
                chunkSize = asset.chunkResolution,
                terrainSize = asset.terrainSize,
                randomSeed = rng.NextUInt(),
                origin = origin,
                scale = asset.noiseScale,
                noiseHeight = asset.noiseHeight,
                gradientStart = Mathf.Lerp(asset.noiseHeight, 1f - asset.noiseHeight, 0 / (float)asset.numChunks),
                gradientEnd = Mathf.Lerp(asset.noiseHeight, 1f - asset.noiseHeight, (0 + 1) / (float)asset.numChunks),
                treeSpacing = 10,
                minTreeHeight = 1,
                maxTreeHeight = 2,
                minTreeWidth = 1,
                maxTreeWidth = 2,
                treeNoiseScale = asset.treeNoiseScale,
                treeNoiseMin = asset.treeNoiseMin,
                treeNoiseMax = asset.treeNoiseMax,
                heights = new NativeArray<float>(asset.chunkResolution * asset.chunkResolution, Allocator.TempJob),
                trees = new NativeArray<TreeInstance>(asset.chunkResolution * asset.chunkResolution, Allocator.TempJob),
                numTrees = new NativeArray<int>(1, Allocator.TempJob),
                alphamapResolution = asset.chunkResolution - 1,
                grassAlpha = new NativeArray<float>((asset.chunkResolution - 1) * (asset.chunkResolution - 1), Allocator.TempJob),
            };

            jobs.Add(job);
            jobHandles.Add(job.Schedule());
        }

        try
        {
            for (int i = 0; i < jobHandles.Count; i++)
            {
                jobHandles[i].Complete();
            }

            for (int i = 0; i < jobHandles.Count; i++)
            {
                var job = jobs[i];

                var obj = (GameObject)terrainObjectsProp.GetArrayElementAtIndex(i).objectReferenceValue;

                if (obj == null)
                {
                    var terrainData = new TerrainData();
                    AssetDatabase.CreateAsset(terrainData, $"Assets/Terrain/{asset.terrainName}_{i}_TerrainData.asset");
                    ApplyTerrainData(ref job, terrainData);
                    obj = Terrain.CreateTerrainGameObject(terrainData);
                    terrainObjectsProp.GetArrayElementAtIndex(i).objectReferenceValue = obj;
                    obj.name = $"{asset.terrainName}_{i}";
                    obj.transform.position = new Vector3(job.chunkX * asset.terrainSize.x, Mathf.Lerp(asset.noiseHeight, 1f - asset.noiseHeight, i / (float)asset.numChunks) * asset.terrainSize.y, job.chunkZ * asset.terrainSize.z);
                }
                else
                {
                    var terrainData = obj.GetComponent<Terrain>().terrainData;
                    ApplyTerrainData(ref job, terrainData);
                    //obj.transform.position = new Vector3(job.chunkX * terrainSize.x, 0, job.chunkZ * terrainSize.z);
                    obj.transform.position = new Vector3(job.chunkX * asset.terrainSize.x, Mathf.Lerp(asset.noiseHeight, 1f - asset.noiseHeight, i / (float)asset.numChunks) * asset.terrainSize.y, job.chunkZ * asset.terrainSize.z);
                }

                obj.GetComponent<Terrain>().treeDistance = asset.terrainTreeDrawDistance;
                obj.GetComponent<Terrain>().Flush();
            }

            AssetDatabase.SaveAssets();
        }
        finally
        {
            foreach (var job in jobs)
            {
                job.heights.Dispose();
                job.trees.Dispose();
                job.numTrees.Dispose();
                job.grassAlpha.Dispose();
            }

            terrainObjectsEditor.serializedObject.ApplyModifiedProperties();
        }

    }

    private void ApplyTerrainData(ref TerrainGeneratorJob job, TerrainData terrainData)
    {
        var heights = new float[asset.chunkResolution, asset.chunkResolution];
        for (int x = 0; x < asset.chunkResolution; x++)
        {
            for (int z = 0; z < asset.chunkResolution; z++)
            {
                heights[z, x] = job.heights[x * asset.chunkResolution + z];
            }
        }

        // heightmap

        terrainData.heightmapResolution = asset.chunkResolution;
        terrainData.size = asset.terrainSize;
        terrainData.SetHeights(0, 0, heights);

        // textures

        terrainData.alphamapResolution = asset.chunkResolution - 1;
        terrainData.terrainLayers = asset.terrainLayers.Prepend(asset.baseLayer).ToArray();

        var alphamaps = new float[terrainData.alphamapResolution, terrainData.alphamapResolution, terrainData.alphamapLayers];

        for (int x = 0; x < terrainData.alphamapResolution; x++)
        {
            for (int z = 0; z < terrainData.alphamapResolution; z++)
            {
                alphamaps[x, z, 0] = 1f;
                alphamaps[x, z, 1] = job.grassAlpha[x * terrainData.alphamapResolution + z];
            }
        }

        terrainData.SetAlphamaps(0, 0, alphamaps);

        // trees

        var treePrototypes = new TreePrototype[asset.treePrefabs.Count];
        for (int i = 0; i < asset.treePrefabs.Count; i++)
        {
            treePrototypes[i] = new TreePrototype
            {
                prefab = asset.treePrefabs[i],
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

        Debug.Log($"Set trees: {job.numTrees}");
    }
}
