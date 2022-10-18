using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.UI;

public class RuntimeTerrainGenerator : MonoBehaviour
{
    [SerializeField] private Transform viewpoint;
    [SerializeField] private float viewRadius;
    [SerializeField] private TerrainGeneratorAsset asset;

    private TerrainGeneratorAsset workingAsset;
    private Dictionary<Vector2Int, Chunk> terrainChunks = new Dictionary<Vector2Int, Chunk>(8);
    private List<Chunk> pendingChunks = new List<Chunk>(8);
    private List<GameObject> freeTerrainObjects = new List<GameObject>(8);

    private Vector2Int? lastChunkPos = null;

    private class Chunk
    {
        public Vector2Int coord;
        public GameObject gameObject;
        public ChunkJob chunkJob;
    }

    private class ChunkJob
    {
        public TerrainGeneratorJob job;
        public JobHandle handle;
    }

    void Start()
    {
    }

    void OnDestroy()
    {
        foreach (var chunk in pendingChunks)
        {
            chunk.chunkJob.handle.Complete();
            chunk.chunkJob.job.Dispose();
        }
    }

    void Update()
    {
        var workingAsset = GetWorkingAsset();
        if (workingAsset == null)
        {
            return;
        }

        Vector2Int ToChunk(Vector3 pos) => new Vector2Int(
            Mathf.FloorToInt(pos.x / workingAsset.terrainSize.x),
            Mathf.FloorToInt(pos.z / workingAsset.terrainSize.z));

        var viewpointChunk = ToChunk(viewpoint.position);

        var viewMin = ToChunk(viewpoint.position - Vector3.one * viewRadius);
        var viewMax = ToChunk(viewpoint.position + Vector3.one * viewRadius);

        if (viewpointChunk != lastChunkPos)
        {
            lastChunkPos = viewpointChunk;
            for (int x = viewMin.x; x <= viewMax.x; x++)
            {
                for (int z = viewMin.y; z <= viewMax.y; z++)
                {
                    var chunkPos = new Vector2Int(x, z);
                    if (!terrainChunks.ContainsKey(chunkPos))
                    {
                        var chunk = new Chunk();
                        chunk.coord = chunkPos;
                        chunk.chunkJob = new ChunkJob();
                        chunk.chunkJob.handle = workingAsset.StartJob(x, z, out chunk.chunkJob.job);
                        pendingChunks.Add(chunk);
                        terrainChunks.Add(chunkPos, chunk);
                    }
                }
            }
        }

        // resolve pending

        for (int i = 0; i < pendingChunks.Count; i++)
        {
            var chunk = pendingChunks[i];
            if (chunk.chunkJob.handle.IsCompleted)
            {
                pendingChunks.RemoveAt(i);
                i--;

                try
                {
                    var chunkPos = chunk.coord;

                    Debug.Assert(chunk.gameObject == null);

                    if (freeTerrainObjects.Count > 0)
                    {
                        chunk.gameObject = freeTerrainObjects[freeTerrainObjects.Count - 1];
                        freeTerrainObjects.RemoveAt(freeTerrainObjects.Count - 1);
                        workingAsset.ApplyTerrainData(ref chunk.chunkJob.job, chunk.gameObject.GetComponent<Terrain>().terrainData);
                    }
                    else
                    {
                        var terrainData = new TerrainData();
                        workingAsset.ApplyTerrainData(ref chunk.chunkJob.job, terrainData);
                        chunk.gameObject = Terrain.CreateTerrainGameObject(terrainData);
                    }

                    chunk.gameObject.name = $"{workingAsset.terrainName}_{chunkPos.x}_{chunkPos.y}";
                    chunk.gameObject.transform.position = new Vector3(chunkPos.x * asset.terrainSize.x, chunkPos.x * (1f - 2f * asset.noiseHeight) * asset.terrainSize.y, chunkPos.y * asset.terrainSize.z);

                    chunk.gameObject.SetActive(true);
                    chunk.gameObject.GetComponent<Terrain>().Flush();
                }
                finally
                {
                    chunk.chunkJob.job.Dispose();
                }
            }
        }
    }

    private TerrainGeneratorAsset GetWorkingAsset()
    {
        if (workingAsset == null && asset != null)
        {
            workingAsset = Instantiate(asset);
            workingAsset.seed = TerrainGeneratorAsset.MakeRandomSeed();
        }
        return workingAsset;
    }
}
