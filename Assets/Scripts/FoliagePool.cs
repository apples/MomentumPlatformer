using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class FoliagePool : MonoBehaviour
{
    [SerializeField] private int initialPoolCapacity = 100;
    [SerializeField] private int maxPoolSize = 1000;

    private Dictionary<string, Pool> foliagePools = new Dictionary<string, Pool>(32);

    private class Pool : IDisposable
    {
        public ObjectPool<GameObject> pool;
        public GameObject prefab;

        public Pool(int initialCapacity, int maxSize, GameObject prefab)
        {
            this.pool = new ObjectPool<GameObject>(
                createFunc: this.FoliageCreate,
                actionOnGet: this.FoliageActivate,
                actionOnRelease: this.FoliageDeactivate,
                actionOnDestroy: GameObject.Destroy,
                defaultCapacity: initialCapacity,
                maxSize: maxSize);
            this.prefab = prefab;
        }

        private GameObject FoliageCreate()
        {
            return Instantiate(prefab);
        }

        private void FoliageActivate(GameObject obj)
        {
            obj.SetActive(true);
        }

        private void FoliageDeactivate(GameObject obj)
        {
            obj.SetActive(false);
        }

        public void Dispose()
        {
            pool.Dispose();
        }
    }

    void OnDestroy()
    {
        foreach (var pool in foliagePools.Values)
        {
            pool.Dispose();
        }
    }

    public ObjectPool<GameObject> GetPool(FoliageLayer foliage)
    {
        if (!foliagePools.TryGetValue(foliage.name, out var pool))
        {
            pool = new Pool(initialPoolCapacity, maxPoolSize, foliage.prefab);
            foliagePools.Add(foliage.name, pool);
        }
        return pool.pool;
    }
}
