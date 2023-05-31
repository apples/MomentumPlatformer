using System;
using System.Collections;
using System.Collections.Generic;
using SOUP;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

[RequireComponent(typeof(Terrain))]
[RequireComponent(typeof(FoliagePool))]
public class FoliageAdaptor : MonoBehaviour
{
    [SerializeField] private FoliageLayer foliageLayer;
    [SerializeField] private GameObjectValue playerGameObjectValue;
    [SerializeField] private Vector2 chunkSize;

    private Terrain terrain;
    private TerrainCollider terrainCollider;
    private FoliagePool foliagePool;

    private Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();

    private int chunkItemCountGuess;

    private class Chunk
    {
        public List<TerrainGeneratorJob.FoliageInfo> infos;
        public List<FoliageRenderer.MeshInstanceData> instances;
        public Bounds bounds;
    }

    void Awake()
    {
        terrain = GetComponent<Terrain>();
        terrainCollider = GetComponent<TerrainCollider>();
        foliagePool = GetComponent<FoliagePool>();
    }

    void Start()
    {
        chunkItemCountGuess = (int)(2.0 * (float)terrain.terrainData.treeInstanceCount / (terrain.terrainData.size.x * terrain.terrainData.size.z / (chunkSize.x * chunkSize.y)));

        // disable terrain trees (and all other foliage because unity is terrible)
        terrain.drawTreesAndFoliage = false;

        // prepare chunks

        var markerPrepareChunks = new ProfilerMarker("PrepareChunks");
        markerPrepareChunks.Begin();

        var meshBounds = FoliageLOD.SumBounds(foliageLayer.lods);

        var count = terrain.terrainData.treeInstanceCount;
        var treeInstances = terrain.terrainData.treeInstances;

        for (var j = 0; j < count; j++)
        {
            var tree = treeInstances[j];

            var position = Vector3.Scale(tree.position, terrain.terrainData.size) + terrain.transform.position;
            var rotation = Quaternion.AngleAxis(tree.rotation, Vector3.up);
            var scale = new Vector3(tree.widthScale, tree.heightScale, tree.widthScale);

            var chunk = GetChunkForPosition(position);

            chunk.infos.Add(new TerrainGeneratorJob.FoliageInfo
            {
                position = position,
                rotation = rotation,
                scale = scale,
            });

            var meshRotationEuler = new float3(-90f, 0f, 0f); // TODO: get from foliage layer
            var meshRotation = quaternion.Euler(meshRotationEuler * Mathf.Deg2Rad);

            var matrix = math.mul(
                float4x4.TRS(position, rotation, scale),
                new float4x4(meshRotation, float3.zero));

            chunk.instances.Add(new FoliageRenderer.MeshInstanceData
            {
                objectToWorld = matrix,
                worldToObject = math.inverse(matrix),
            });

            if (chunk.bounds == default)
            {
                chunk.bounds = new Bounds(position, Vector3.zero);
            }

            var treeBounds = meshBounds;
            treeBounds.center += position;

            chunk.bounds.Encapsulate(treeBounds);
        }

        markerPrepareChunks.End();

        // instantiate chunks

        Console.WriteLine($"Chunk count: {chunks.Count}");

        var markerInstantiateChunks = new ProfilerMarker("InstantiateChunks");
        markerInstantiateChunks.Begin();

        foreach (var p in chunks)
        {
            InstantiateChunk(p.Key, p.Value);
        }

        markerInstantiateChunks.End();
    }

    private Chunk GetChunkForPosition(Vector3 pos)
    {
        var xi = Mathf.FloorToInt(pos.x / chunkSize.x);
        var zi = Mathf.FloorToInt(pos.z / chunkSize.y);

        var coord = new Vector2Int(xi, zi);

        if (!chunks.TryGetValue(coord, out var chunk)) {
            chunk = new Chunk
            {
                infos = new List<TerrainGeneratorJob.FoliageInfo>(chunkItemCountGuess),
                instances = new List<FoliageRenderer.MeshInstanceData>(chunkItemCountGuess),
                bounds = default,
            };

            chunks[coord] = chunk;
        }

        return chunk;
    }

    private void InstantiateChunk(Vector2Int coord, Chunk chunk)
    {
        var foliageRoot = new GameObject($"Foliage_{foliageLayer.name} ({coord.x}, {coord.y})");
        foliageRoot.transform.SetParent(this.transform, false);
        foliageRoot.AddComponent<FoliageRoot>().foliageLayer = foliageLayer;
        var renderer = foliageRoot.AddComponent<FoliageRenderer>();
        renderer.playerGameObjectValue = playerGameObjectValue;

        var foliageObject = foliageRoot.transform;

        var foliageInstanceRenderer = foliageObject.GetComponent<FoliageRenderer>();

        foliageInstanceRenderer.layerData = foliageLayer;

        // spawn prefabs

        if (foliageLayer.prefab != null)
        {
            var pool = foliagePool.GetPool(foliageLayer);
            for (var j = 0; j < chunk.infos.Count; j++)
            {
                var info = chunk.infos[j];

                var foliageObj = pool.Get();
                foliageObj.transform.SetParent(foliageObject);
                foliageObj.transform.position = info.position;
                foliageObj.transform.rotation = info.rotation;
                foliageObj.transform.localScale = info.scale;
            }
        }

        foliageInstanceRenderer.SetMeshInstances(chunk.instances.ToArray(), chunk.bounds);
    }
}
