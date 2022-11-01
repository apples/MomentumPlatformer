using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;
using UnityEngine.Pool;

[CreateAssetMenu(menuName = "Foliage/Foliage LOD")]
public class FoliageLOD : ScriptableObject
{
    public Mesh[] meshes;
    public Material[] materials;
    public float maxDistance;

    public static Bounds SumBounds(FoliageLOD[] lods)
    {
        var bounds = new Bounds();
        for (int i = 0; i < lods.Length; i++)
        {
            for (int j = 0; j < lods[i].meshes.Length; j++)
            {
                bounds.Encapsulate(lods[i].meshes[j].bounds);
            }
        }
        return bounds;
    }
}
