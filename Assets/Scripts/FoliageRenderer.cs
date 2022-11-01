using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.VFX;

public class FoliageRenderer : MonoBehaviour
{
    public FoliageLOD[] lods;

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
            for (int i = 0; i < lods.Length; i++)
            {
                if (distance <= lods[i].maxDistance)
                {
                    return (lods[i], i);
                }
            }
            return (null, lods.Length);
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
        if (lods.Length == 0)
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

        meshArgBuffers = new ComputeBuffer[lods.Length][];
        materialInstances = new Material[lods.Length][];
        for (int i = 0; i < lods.Length; i++)
        {
            meshArgBuffers[i] = new ComputeBuffer[lods[i].meshes.Length];
            materialInstances[i] = new Material[lods[i].meshes.Length];
            for (int j = 0; j < lods[i].meshes.Length; j++)
            {
                var mesh = lods[i].meshes[j];
                var material = lods[i].materials[j];

                uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
                args[0] = (uint)mesh.GetIndexCount(0);
                args[1] = (uint)numInstances;
                args[2] = (uint)mesh.GetIndexStart(0);
                args[3] = (uint)mesh.GetBaseVertex(0);

                meshArgBuffers[i][j] = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
                meshArgBuffers[i][j].SetData(args);

                materialInstances[i][j] = Instantiate(material);
                materialInstances[i][j].SetBuffer("_PerInstanceData", perInstanceDataBuffer);
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
