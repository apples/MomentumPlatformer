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
    public NoiseType treeNoiseType = NoiseType.WorleyF1;
    public Vector2 treeNoiseScale = new Vector2(1, 1);
    public float treeNoiseMin = 0f;
    public float treeNoiseMax = 1f;
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
}
