using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using SOUP;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.VFX;

public class FoliageRenderer : MonoBehaviour
{
    private static byte[] defaultDeformationPixel = new byte[] { 127, 127, 0 };
    private static Dictionary<int, byte[]> cachedDeformationTextureInitializers;

    private byte[] GetCachedDeformationTextureData(int size)
    {
        if (cachedDeformationTextureInitializers == null)
        {
            cachedDeformationTextureInitializers = new Dictionary<int, byte[]>(1);
        }
        if (cachedDeformationTextureInitializers.TryGetValue(size, out var cached))
        {
            return cached;
        }

        var pix = new byte[size * size * 3];
        for (int i = 0; i < size * size * 3; ++i)
        {
            pix[i] = defaultDeformationPixel[i % 3];
        }

        cachedDeformationTextureInitializers[size] = pix;

        return pix;
    }

    public FoliageLayer layerData;
    public GameObjectValue playerGameObjectValue;

    private ComputeBuffer perInstanceDataBuffer;
    private ComputeBuffer[][] meshArgBuffers;
    private Material[][] materialInstances;

    private Bounds bounds;

    [Serializable]
    public struct MeshInstanceData
    {
        public Matrix4x4 objectToWorld;
        public Matrix4x4 worldToObject;

        public static int Size => 4 * 4 * 2;
    }

    public (FoliageLOD, int) CurrentLOD
    {
        get
        {
            var closest = bounds.ClosestPoint(Camera.main.transform.position);
            var distance = Vector3.Distance(closest, Camera.main.transform.position);
            for (int i = 0; i < layerData.lods.Length; i++)
            {
                if (distance <= layerData.lods[i].maxDistance)
                {
                    return (layerData.lods[i], i);
                }
            }
            return (null, layerData.lods.Length);
        }
    }

    public void SetMeshInstances(MeshInstanceData[] meshInstances, Bounds bounds)
    {
        if (perInstanceDataBuffer != null)
        {
            perInstanceDataBuffer.Dispose();
            perInstanceDataBuffer = null;
        }
        if (meshInstances.Length == 0)
        {
            return;
        }
        perInstanceDataBuffer = new ComputeBuffer(meshInstances.Length, sizeof(float) * MeshInstanceData.Size);
        perInstanceDataBuffer.SetData(meshInstances);
        this.bounds = bounds;

        MakeArgBuffers(meshInstances.Length);
    }

    void OnDestroy()
    {
        if (perInstanceDataBuffer != null)
        {
            perInstanceDataBuffer.Dispose();
            perInstanceDataBuffer = null;
        }

        if (meshArgBuffers != null)
        {
            for (int i = 0; i < meshArgBuffers.Length; i++)
            {
                for (int j = 0; j < meshArgBuffers[i].Length; j++)
                {
                    if (meshArgBuffers[i][j] != null)
                    {
                        meshArgBuffers[i][j].Dispose();
                    }
                }
            }
            meshArgBuffers = null;
        }

        bounds = new Bounds();
    }

    void Update()
    {
        if (perInstanceDataBuffer == null)
        {
            return;
        }

        var (lod, i) = CurrentLOD;

        if (lod == null)
        {
            return;
        }

        for (int j = 0; j < lod.meshes.Length; j++)
        {
            if (layerData.foliageParams.enablePlayerDeformation && playerGameObjectValue != null && playerGameObjectValue.Value != null)
            {
                materialInstances[i][j].SetVector("_DeformationSourcePos", playerGameObjectValue.Value.transform.position);
            }

            Graphics.DrawMeshInstancedIndirect(
                lod.meshes[j],
                0,
                materialInstances[i][j],
                bounds,
                meshArgBuffers[i][j],
                0,
                null,
                UnityEngine.Rendering.ShadowCastingMode.On,
                true,
                0,
                null,
                LightProbeUsage.Off,
                null
            );
        }
    }

    private void MakeArgBuffers(int numInstances)
    {
        if (layerData.lods.Length == 0)
        {
            return;
        }

        if (meshArgBuffers != null)
        {
            for (int i = 0; i < meshArgBuffers.Length; i++)
            {
                for (int j = 0; j < meshArgBuffers[i].Length; j++)
                {
                    if (meshArgBuffers[i][j] != null)
                    {
                        meshArgBuffers[i][j].Dispose();
                    }
                }
            }
        }

        if (materialInstances != null)
        {
            for (int i = 0; i < materialInstances.Length; i++)
            {
                for (int j = 0; j < materialInstances[i].Length; j++)
                {
                    if (materialInstances[i][j] != null)
                    {
                        Destroy(materialInstances[i][j]);
                    }
                }
            }
        }

        meshArgBuffers = new ComputeBuffer[layerData.lods.Length][];
        materialInstances = new Material[layerData.lods.Length][];
        for (int lodIndex = 0; lodIndex < layerData.lods.Length; lodIndex++)
        {
            meshArgBuffers[lodIndex] = new ComputeBuffer[layerData.lods[lodIndex].meshes.Length];
            materialInstances[lodIndex] = new Material[layerData.lods[lodIndex].meshes.Length];
            for (int materialIndex = 0; materialIndex < layerData.lods[lodIndex].meshes.Length; materialIndex++)
            {
                var mesh = layerData.lods[lodIndex].meshes[materialIndex];
                var material = layerData.lods[lodIndex].materials[materialIndex];

                uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
                args[0] = (uint)mesh.GetIndexCount(0);
                args[1] = (uint)numInstances;
                args[2] = (uint)mesh.GetIndexStart(0);
                args[3] = (uint)mesh.GetBaseVertex(0);

                meshArgBuffers[lodIndex][materialIndex] = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
                meshArgBuffers[lodIndex][materialIndex].SetData(args);

                materialInstances[lodIndex][materialIndex] = Instantiate(material);
                materialInstances[lodIndex][materialIndex].SetBuffer("_PerInstanceData", perInstanceDataBuffer);

                if (layerData.foliageParams.enablePlayerDeformation)
                {
                    materialInstances[lodIndex][materialIndex].SetFloat("_EnableDeformation", 1);
                    materialInstances[lodIndex][materialIndex].SetFloat("_DeformationRadius", layerData.foliageParams.playerDeformationRadius);
                    if (playerGameObjectValue != null && playerGameObjectValue.Value != null)
                    {
                        materialInstances[lodIndex][materialIndex].SetVector("_DeformationSourcePos", playerGameObjectValue.Value.transform.position);
                    }
                    else
                    {
                        materialInstances[lodIndex][materialIndex].SetVector("_DeformationSourcePos", Vector3.positiveInfinity);
                    }
                }
            }
        }
    }

    #if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
    #endif
}
