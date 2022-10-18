
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct TerrainGeneratorJob : IJob, System.IDisposable
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
    public TerrainGeneratorAsset.NoiseType noiseType;
    public float noiseHeight;
    public float gradientStart;
    public float gradientEnd;
    public float gutterGuardDistance;

    // tree settings
    public float treeSpacing;
    public float minTreeHeight;
    public float maxTreeHeight;
    public float minTreeWidth;
    public float maxTreeWidth;
    public TerrainGeneratorAsset.NoiseType treeNoiseType;
    public float2 treeNoiseScale;
    public float treeNoiseMin;
    public float treeNoiseMax;
    public float treeForcedChance;

    // sigil settings
    public float sigilSpacing;

    // results
    public NativeArray<float> heights;
    public NativeArray<TreeInstance> trees;
    public NativeArray<int> numTrees;
    public NativeArray<float2> sigils;
    public NativeArray<int> numSigils;
    public NativeArray<float> grassAlpha;

    public void Dispose()
    {
        heights.Dispose();
        trees.Dispose();
        numTrees.Dispose();
        sigils.Dispose();
        numSigils.Dispose();
        grassAlpha.Dispose();
    }

    float2 NoiseCoord(float x, float y, float2 scale) => origin + new float2(chunkX + x, chunkZ + y) * scale;

    float Noise(float2 noiseCoord, TerrainGeneratorAsset.NoiseType noiseType) =>
        (noiseType switch
        {
            TerrainGeneratorAsset.NoiseType.Perlin => noise.cnoise(noiseCoord),
            TerrainGeneratorAsset.NoiseType.Simplex => noise.snoise(noiseCoord),
            TerrainGeneratorAsset.NoiseType.WorleyF1 => noise.cellular(noiseCoord).x,
            TerrainGeneratorAsset.NoiseType.WorleyF2 => noise.cellular(noiseCoord).y,
            _ => 0f,
        });

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
                var noiseValue = noiseHeight * Noise(noiseCoord, noiseType);

                if (gutterGuardDistance > 0)
                {
                    var dist = math.max(
                        math.smoothstep(gutterGuardDistance, 0f, z / (float)(chunkSize - 1)),
                        math.smoothstep(1f - gutterGuardDistance, 1f, z / (float)(chunkSize - 1)));
                    noiseValue = math.lerp(noiseValue, noiseHeight, dist);
                }

                heights[x * chunkSize + z] = math.saturate(gradientValue + noiseValue);

                // reduce the bit depth of the edge of the terrain to 15 bits, to hide rounding errors on the seams
                if (x == 0 || x == chunkSize - 1 || z == 0 || z == chunkSize - 1)
                {
                    heights[x * chunkSize + z] = math.round(heights[x * chunkSize + z] * 32767f) / 32767f;
                }
            }
        }

        // grass texture
        for (int x = 0; x < alphamapResolution; x++)
        {
            for (int z = 0; z < alphamapResolution; z++)
            {
                var noiseCoord = NoiseCoord(((float)x + 0.5f) / alphamapResolution, ((float)z + 0.5f) / alphamapResolution, treeNoiseScale);
                var noiseValue = Noise(noiseCoord, treeNoiseType);

                grassAlpha[z * alphamapResolution + x] = math.smoothstep(treeNoiseMin, treeNoiseMax, noiseValue);
            }
        }

        // trees
        var treePositions = Gists.FastPoissonDiskSampling.Sampling(new float2(0, 0), new float2(1, 1), ref rng, treeSpacing / terrainSize.x);
        var numTrees = 0;
        for (var i = 0; i < treePositions.Length && i < trees.Length; ++i)
        {
            var treePosition = treePositions[i];
            var noiseCoord = NoiseCoord(treePosition.x, treePosition.y, treeNoiseScale);
            // var noiseValue = Noise(noiseCoord, treeNoiseType);
            var noiseX = math.clamp((int)(treePosition.x * alphamapResolution), 0, alphamapResolution - 1);
            var noiseZ = math.clamp((int)(treePosition.y * alphamapResolution), 0, alphamapResolution - 1);
            var noiseValue = grassAlpha[noiseZ * alphamapResolution + noiseX];

            if (rng.NextFloat() < noiseValue && (treeForcedChance == 0f || rng.NextFloat() >= treeForcedChance))
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

        // sigils
        var sigilPositions = Gists.FastPoissonDiskSampling.Sampling(new float2(0, 0), new float2(1, 1), ref rng, sigilSpacing / terrainSize.x);
        var numSigils = 0;
        for (var i = 0; i < sigilPositions.Length && i < sigils.Length; ++i)
        {
            var sigilPosition = sigilPositions[i];
            var noiseCoord = NoiseCoord(sigilPosition.x, sigilPosition.y, treeNoiseScale);
            var noiseX = math.clamp((int)(sigilPosition.x * alphamapResolution), 0, alphamapResolution - 1);
            var noiseZ = math.clamp((int)(sigilPosition.y * alphamapResolution), 0, alphamapResolution - 1);
            var noiseValue = grassAlpha[noiseZ * alphamapResolution + noiseX];

            if (rng.NextFloat() >= noiseValue)
            {
                continue;
            }

            sigils[numSigils++] = sigilPosition;
        }
        this.numSigils[0] = numSigils;
    }
}
