using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

public class RuntimeTerrainGenerator : MonoBehaviour
{
    [SerializeField] private Transform viewpoint;
    [SerializeField] private float viewRadius;
    [SerializeField] private TerrainGeneratorAsset asset;

    private Dictionary<Vector2Int, Chunk> terrainChunks = new Dictionary<Vector2Int, Chunk>(8);
    private List<Chunk> pendingChunks = new List<Chunk>(8);
    private List<Vector2Int> pendingRemovals = new List<Vector2Int>(8);
    private List<GameObject> freeTerrainObjects = new List<GameObject>(8);

    private struct IntRect
    {
        public Vector2Int min;
        public Vector2Int max;

        public static bool operator!=(IntRect a, IntRect b) => a.min != b.min || a.max != b.max;
        public static bool operator==(IntRect a, IntRect b) => a.min == b.min && a.max == b.max;
        public override bool Equals(object obj) => obj is IntRect rect && this == rect;
        public override int GetHashCode() => HashCode.Combine(min.GetHashCode(), max.GetHashCode());
    }

    private IntRect? lastViewRect = null;

    public class Chunk
    {
        public Vector2Int coord;
        public GameObject gameObject;
        public ChunkJob chunkJob;
    }

    public class ChunkJob
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
        if (asset == null)
        {
            return;
        }

        Vector2Int ToChunk(Vector3 pos) => new Vector2Int(
            Mathf.FloorToInt(pos.x / asset.chunkSize.x),
            Mathf.FloorToInt(pos.z / asset.chunkSize.y));

        var viewpointChunk = ToChunk(viewpoint.position);
        var viewMin = ToChunk(viewpoint.position - Vector3.one * viewRadius);
        var viewMax = ToChunk(viewpoint.position + Vector3.one * viewRadius);

        // add chunks out of view to the pending removals

        var markerRemoveChunks = new ProfilerMarker("RemoveChunks");
        markerRemoveChunks.Begin();
        foreach (var coord in terrainChunks.Keys)
        {
            if (coord.x < viewMin.x ||
                coord.x > viewMax.x ||
                coord.y < viewMin.y ||
                coord.y > viewMax.y)
            {
                pendingRemovals.Add(coord);

#if DEBUG
                Debug.Log("Removing chunk " + coord);
#endif
            }
        }
        markerRemoveChunks.End();

        // add pending removals which are completed to the free list

        var markerFreeChunks = new ProfilerMarker("FreeChunks");
        markerFreeChunks.Begin();
        foreach (var coord in pendingRemovals)
        {
            if (terrainChunks.TryGetValue(coord, out var chunk))
            {
                if (chunk.chunkJob == null)
                {
                    freeTerrainObjects.Add(chunk.gameObject);
                    chunk.gameObject.SetActive(false);

                    terrainChunks.Remove(coord);

#if DEBUG
                    Debug.Log("Freeing chunk " + coord);
#endif
                }
            }
        }

        pendingRemovals.RemoveAll(coord => !terrainChunks.ContainsKey(coord));
        markerFreeChunks.End();

        // require visible chunks

        var markerRequireChunks = new ProfilerMarker("RequireChunks");
        markerRequireChunks.Begin();

        var viewRect = new IntRect
        {
            min = viewMin,
            max = viewMax,
        };

        if (viewRect != lastViewRect)
        {
            lastViewRect = viewRect;
            for (int x = viewMin.x; x <= viewMax.x; x++)
            {
                for (int z = viewMin.y; z <= viewMax.y; z++)
                {
                    var chunkPos = new Vector2Int(x, z);
                    RequireChunk(chunkPos);
                }
            }
        }

        markerRequireChunks.End();

        // resolve pending

        var markerResolvePending = new ProfilerMarker("ResolvePending");
        var markerResolvePendingUseFree = new ProfilerMarker("ResolvePendingUseFree");
        var markerResolvePendingCreateNew = new ProfilerMarker("ResolvePendingCreateNew");
        var markerResolvePendingUpdate = new ProfilerMarker("ResolvePendingUpdate");
        markerResolvePending.Begin();
        for (int i = 0; i < pendingChunks.Count; i++)
        {
            var chunk = pendingChunks[i];
            if (chunk.chunkJob.handle.IsCompleted)
            {
                chunk.chunkJob.handle.Complete();
                pendingChunks.RemoveAt(i);

                try
                {
                    var chunkPos = chunk.coord;

                    Debug.Assert(chunk.gameObject == null);

                    if (freeTerrainObjects.Count > 0)
                    {
                        markerResolvePendingUseFree.Begin();
                        chunk.gameObject = freeTerrainObjects[freeTerrainObjects.Count - 1];
                        freeTerrainObjects.RemoveAt(freeTerrainObjects.Count - 1);
                        asset.ApplyTerrainData(ref chunk.chunkJob.job, chunk.gameObject.GetComponent<Terrain>().terrainData);
                        markerResolvePendingUseFree.End();
                    }
                    else
                    {
                        markerResolvePendingCreateNew.Begin();
                        var terrainData = new TerrainData();
                        asset.ApplyTerrainData(ref chunk.chunkJob.job, terrainData);
                        chunk.gameObject = Terrain.CreateTerrainGameObject(terrainData);
                        markerResolvePendingCreateNew.End();
                    }

                    markerResolvePendingUpdate.Begin();

                    chunk.gameObject.name = $"{asset.terrainName}_{chunkPos.x}_{chunkPos.y}";
                    chunk.gameObject.transform.position = asset.GetChunkPosition(chunkPos.x, chunkPos.y);
                    chunk.gameObject.transform.SetParent(this.transform, true);

                    chunk.gameObject.SetActive(true);
                    chunk.gameObject.GetComponent<Terrain>().treeDistance = asset.terrainTreeDrawDistance;
                    chunk.gameObject.GetComponent<Terrain>().Flush();

                    markerResolvePendingUpdate.End();
                }
                finally
                {
                    chunk.chunkJob.job.Dispose();
                }

                chunk.chunkJob = null;

#if DEBUG
                Debug.Log("Completed adding chunk " + chunk.coord);
#endif
                break;
            }
        }
        markerResolvePending.End();
    }

    private Chunk RequireChunk(Vector2Int chunkPos)
    {
        if (terrainChunks.TryGetValue(chunkPos, out var chunk))
        {
            return chunk;
        }
#if DEBUG
        Debug.Log("Required chunk " + chunkPos);
#endif

        var newChunk = new Chunk();
        newChunk.coord = chunkPos;
        newChunk.chunkJob = new ChunkJob();
        newChunk.chunkJob.handle = asset.StartJob(chunkPos.x, chunkPos.y, out newChunk.chunkJob.job);
        pendingChunks.Add(newChunk);
        terrainChunks.Add(chunkPos, newChunk);
        return newChunk;
    }

    public Chunk GetChunk(Vector2Int coord)
    {
        return RequireChunk(coord);
    }
}
