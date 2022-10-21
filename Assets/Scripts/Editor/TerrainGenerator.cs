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

        if (terrainObjects == null)
        {
            terrainObjects = ScriptableObject.CreateInstance<TerrainObjects>();
            terrainObjectsEditor = Editor.CreateEditor(terrainObjects);
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
            TerrainGeneratorJob job;

            var handle = asset.StartJob(i, 0, out job);

            jobs.Add(job);
            jobHandles.Add(handle);
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

                var terrainPos = asset.GetChunkPosition(i, 0);

                if (obj == null)
                {
                    var terrainData = new TerrainData();
                    AssetDatabase.CreateAsset(terrainData, $"Assets/Terrain/{asset.terrainName}_{i}_TerrainData.asset");
                    asset.ApplyTerrainData(ref job, terrainData);
                    obj = Terrain.CreateTerrainGameObject(terrainData);
                    terrainObjectsProp.GetArrayElementAtIndex(i).objectReferenceValue = obj;
                    obj.name = $"{asset.terrainName}_{i}";
                }
                else
                {
                    var terrainData = obj.GetComponent<Terrain>().terrainData;
                    asset.ApplyTerrainData(ref job, terrainData);
                }

                obj.transform.position = terrainPos;

                obj.GetComponent<Terrain>().treeDistance = asset.terrainTreeDrawDistance;
                obj.GetComponent<Terrain>().Flush();
            }

            AssetDatabase.SaveAssets();
        }
        finally
        {
            foreach (var job in jobs)
            {
                job.Dispose();
            }

            terrainObjectsEditor.serializedObject.ApplyModifiedProperties();
        }

    }
}
