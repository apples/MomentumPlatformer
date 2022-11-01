using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Foliage/Foliage Layer")]
public class FoliageLayer : ScriptableObject
{
    public FoliageParams foliageParams = new FoliageParams()
    {
        spacing = 10f,
        minHeight = 1f,
        maxHeight = 1f,
        minWidth = 1f,
        maxWidth = 1f,
        alphaLayer = 1,
        inverted = false,
        forcedChance = 0f,
        meshRotationEuler = new Vector3(-90f, 0f, 0f),
    };
    public FoliageLOD[] lods;
    public GameObject prefab;

    [System.Serializable]
    public struct FoliageParams
    {
        public float spacing;
        public float minHeight;
        public float maxHeight;
        public float minWidth;
        public float maxWidth;
        public int alphaLayer;
        public bool inverted;
        public float forcedChance;
        public Vector3 meshRotationEuler;
    }

}
