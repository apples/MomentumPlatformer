using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

[RequireComponent(typeof(FoliagePool))]
public class RuntimeTerrainGenerator : MonoBehaviour
{
    [SerializeField] private Transform viewpoint;
    [SerializeField] private float viewRadius;
    [SerializeField] private TerrainGeneratorAsset asset;
    [SerializeField] private GameObject terrainChunkPrefab;

    private Dictionary<Vector2Int, Chunk> terrainChunks = new Dictionary<Vector2Int, Chunk>(32);
    private List<Chunk> pendingChunks = new List<Chunk>(32);
    private List<Vector2Int> pendingRemovals = new List<Vector2Int>(32);

    private ObjectPool<GameObject> terrainPool;
    // private ObjectPool<GameObject> treePool;
    private ObjectPool<GameObject> sigilPool;

    private FoliagePool foliagePool;

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

    void Awake()
    {
        foliagePool = GetComponent<FoliagePool>();
    }

    void Start()
    {
        var terrainSize = asset.chunkSize;
        var chunkViewWidth = Mathf.CeilToInt(viewRadius * 2f / terrainSize.x + 1f);
        var maxPooledTerrains = chunkViewWidth * chunkViewWidth;
        int MaxPoissonCount(float d) => (int)Mathf.Ceil((terrainSize.x + d) * (terrainSize.y + d) * Mathf.Sqrt(3f) / (6f * d * d * 0.25f));
        var maxTreeCount = MaxPoissonCount(asset.treeSpacing);
        var maxSigilCount = MaxPoissonCount(asset.sigilSpacing);

        terrainPool = new ObjectPool<GameObject>(
            createFunc: this.CreateTerrain,
            actionOnGet: this.ActivateTerrain,
            actionOnRelease: this.DeactivateTerrain,
            actionOnDestroy: GameObject.Destroy,
            defaultCapacity: 32,
            maxSize: maxPooledTerrains);
        // treePool = new ObjectPool<GameObject>(
        //     createFunc: this.CreateTree,
        //     actionOnGet: this.ActivateTree,
        //     actionOnRelease: this.DeactivateTree,
        //     actionOnDestroy: GameObject.Destroy,
        //     defaultCapacity: 32,
        //     maxSize: maxPooledTerrains * maxTreeCount);
        sigilPool = new ObjectPool<GameObject>(
            createFunc: this.CreateSigil,
            actionOnGet: this.ActivateSigil,
            actionOnRelease: this.DeactivateSigil,
            actionOnDestroy: GameObject.Destroy,
            defaultCapacity: 32,
            maxSize: maxPooledTerrains * maxSigilCount);
    }

    void OnDestroy()
    {
        foreach (var chunk in pendingChunks)
        {
            chunk.chunkJob.handle.Complete();
            chunk.chunkJob.job.Dispose();
        }

        terrainPool.Dispose();
        // treePool.Dispose();
        sigilPool.Dispose();
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
                    terrainPool.Release(chunk.gameObject);
                    terrainChunks.Remove(coord);
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

                    chunk.gameObject = terrainPool.Get();

                    markerResolvePendingUpdate.Begin();

                    // must set position before setting terrain data (tree positions are relative to terrain position)
                    chunk.gameObject.transform.SetParent(this.transform, true);
                    asset.ApplyToGameObject(ref chunk.chunkJob.job, chunk.gameObject, sigilPool: sigilPool, foliagePool: foliagePool);

                    markerResolvePendingUpdate.End();
                }
                finally
                {
                    chunk.chunkJob.job.Dispose();
                }

                chunk.chunkJob = null;

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

    private GameObject CreateSigil()
    {
        return Instantiate(asset.sigilPrefab);
    }

    private void ActivateSigil(GameObject obj)
    {
        obj.GetComponent<Brazier>().IsAlive = true;
        obj.SetActive(true);
    }

    private void DeactivateSigil(GameObject obj)
    {
        obj.SetActive(false);
    }

    // private GameObject CreateTree()
    // {
    //     return Instantiate(asset.treePrefab);
    // }

    // private void ActivateTree(GameObject obj)
    // {
    //     obj.SetActive(true);
    // }

    // private void DeactivateTree(GameObject obj)
    // {
    //     obj.SetActive(false);
    // }

    private GameObject CreateTerrain()
    {
        var obj = Instantiate(terrainChunkPrefab);
        obj.transform.SetParent(this.transform, true);
        var terrainData = new TerrainData();
        obj.GetComponent<Terrain>().terrainData = terrainData;
        obj.GetComponent<TerrainCollider>().terrainData = terrainData;

        for (var i = 0; i < asset.foliage.Count; ++i)
        {
            var foliage = asset.foliage[i];
            var foliageRoot = new GameObject($"Foliage_{foliage.name}");
            foliageRoot.transform.SetParent(obj.transform, false);
            foliageRoot.AddComponent<FoliageRoot>().foliageLayer = foliage;
            foliageRoot.AddComponent<FoliageRenderer>();
        }

        return obj;
    }

    private void ActivateTerrain(GameObject obj)
    {
        obj.SetActive(true);
    }

    private void DeactivateTerrain(GameObject obj)
    {
        obj.SetActive(false);

        // var trees = obj.transform.Find("Trees");
        // while (trees.childCount > 0)
        // {
        //     var child = trees.GetChild(0);
        //     child.SetParent(this.transform, false);
        //     treePool.Release(child.gameObject);
        // }

        var sigils = obj.transform.Find("Sigils");
        while (sigils.childCount > 0)
        {
            var child = sigils.GetChild(0);
            child.SetParent(this.transform, false);
            sigilPool.Release(child.gameObject);
        }

        var foliageRoots = obj.GetComponentsInChildren<FoliageRoot>();

        foreach (var foliageRoot in foliageRoots)
        {
            var pool = foliagePool.GetPool(foliageRoot.foliageLayer);
            while (foliageRoot.transform.childCount > 0)
            {
                var child = foliageRoot.transform.GetChild(0);
                child.SetParent(this.transform, false);
                pool.Release(child.gameObject);
            }
        }
    }
}
