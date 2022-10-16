
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public struct TerrainGeneratorJob : IJob
{
    public int chunkX;
    public int chunkZ;
    public int chunkSize;
    public float3 terrainSize;
    public int alphamapResolution;
    public uint randomSeed;

    // surface settings
    public float2 origin;
    public float2 scale;
    public float noiseHeight;
    public float gradientStart;
    public float gradientEnd;

    // tree settings
    public float treeSpacing;
    public float minTreeHeight;
    public float maxTreeHeight;
    public float minTreeWidth;
    public float maxTreeWidth;
    public float2 treeNoiseScale;
    public float treeNoiseMin;
    public float treeNoiseMax;

    // results
    public NativeArray<float> heights;
    public NativeArray<TreeInstance> trees;
    public NativeArray<int> numTrees;
    public NativeArray<float> grassAlpha;

    float2 NoiseCoord(float x, float y, float2 scale) => origin + new float2(chunkX + x, chunkZ + y) * scale;

    public void Execute()
    {
        var rng = new Unity.Mathematics.Random(randomSeed == 0 ? 1 : randomSeed);

        // base terrain
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                var gradientValue = math.lerp(gradientStart, gradientEnd, (float)x / (chunkSize - 1));
                var noiseCoord = NoiseCoord((float)x / (chunkSize - 1), (float)z / (chunkSize - 1), scale);
                var noiseValue = noiseHeight * noise.snoise(noiseCoord);

                heights[x * chunkSize + z] = math.saturate(gradientValue + noiseValue);

                // reduce the bit depth of the terrain to 15 bits, to hide rounding errors on the seams
                heights[x * chunkSize + z] = math.round(heights[x * chunkSize + z] * 32767f) / 32767f;
            }
        }

        // grass texture
        for (int x = 0; x < alphamapResolution; x++)
        {
            for (int z = 0; z < alphamapResolution; z++)
            {
                var noiseCoord = NoiseCoord(((float)z + 0.5f) / alphamapResolution, ((float)x + 0.5f) / alphamapResolution, treeNoiseScale);
                var noiseValue = noise.cellular(noiseCoord).x;

                grassAlpha[x * alphamapResolution + z] = math.saturate(math.lerp(treeNoiseMin, treeNoiseMax, math.smoothstep(0, 1, noiseValue)));
            }
        }

        // trees
        var treePositions = Gists.FastPoissonDiskSampling.Sampling(new float2(0, 0), new float2(1, 1), ref rng, treeSpacing / terrainSize.x);
        var numTrees = 0;
        var minmax = new float2(float.MaxValue, float.MinValue);
        for (var i = 0; i < treePositions.Count && i < trees.Length; ++i)
        {
            var treePosition = treePositions[i];
            var noiseCoord = NoiseCoord(treePosition.x, treePosition.y, treeNoiseScale);
            var noiseValue = noise.cellular(noiseCoord).x;

            if (noiseValue < minmax.x)
            {
                minmax.x = noiseValue;
            }
            if (noiseValue > minmax.y)
            {
                minmax.y = noiseValue;
            }

            if (rng.NextFloat() < math.lerp(treeNoiseMin, treeNoiseMax, math.smoothstep(0, 1, noiseValue)))
            {
                continue;
            }

            var treeHeight = math.lerp(minTreeHeight, maxTreeHeight, rng.NextFloat());
            var treeWidth = math.lerp(minTreeWidth, maxTreeWidth, rng.NextFloat());
            var treeRotation = math.lerp(0f, 2 * Mathf.PI, rng.NextFloat());

            var tree = new TreeInstance
            {
                color = Color.white,
                heightScale = treeHeight,
                widthScale = treeWidth,
                lightmapColor = Color.white,
                position = new Vector3(treePosition.x, 0, treePosition.y),
                rotation = treeRotation,
                prototypeIndex = 0,
            };

            trees[numTrees++] = tree;
        }
        this.numTrees[0] = numTrees;
    }
}
