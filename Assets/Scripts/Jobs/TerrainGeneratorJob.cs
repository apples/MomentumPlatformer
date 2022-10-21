
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

[BurstCompile]
public struct TerrainGeneratorJob : IJob, System.IDisposable
{
    public int chunkX;
    public int chunkZ;
    public int chunkResolution;
    public float2 terrainSize;
    public int alphamapResolution;
    public uint chunkSeed;

    // surface settings
    public float2 origin;
    public float2 scale;
    public float slopeGrade;
    public TerrainGeneratorAsset.NoiseType noiseType;
    public float normalizedNoiseHeight;
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
    public NativeReference<int> numTrees;
    public NativeArray<float2> sigils;
    public NativeReference<int> numSigils;
    public NativeArray<float> alphaMaps;

    public void Dispose()
    {
        heights.Dispose();
        trees.Dispose();
        numTrees.Dispose();
        sigils.Dispose();
        numSigils.Dispose();
        alphaMaps.Dispose();
    }

    float2 NoiseCoord(float x, float y, float2 scale) => origin + new float2(chunkX + x, chunkZ + y) * terrainSize / scale;

    private ProfilerMarker noiseMarker;
    float Noise(float2 noiseCoord, TerrainGeneratorAsset.NoiseType noiseType) => (noiseType switch
        {
            TerrainGeneratorAsset.NoiseType.Perlin => noise.cnoise(noiseCoord),
            TerrainGeneratorAsset.NoiseType.Simplex => noise.snoise(noiseCoord),
            TerrainGeneratorAsset.NoiseType.WorleyF1 => noise.cellular(noiseCoord).x,
            TerrainGeneratorAsset.NoiseType.WorleyF2 => noise.cellular(noiseCoord).y,
            _ => 0f,
        });
    private static float NoisePerlin(float2 noiseCoord) => noise.cnoise(noiseCoord);
    private static float NoiseSimplex(float2 noiseCoord) => noise.snoise(noiseCoord);
    private static float NoiseWorleyF1(float2 noiseCoord) => noise.cellular(noiseCoord).x;
    private static float NoiseWorleyF2(float2 noiseCoord) => noise.cellular(noiseCoord).y;
    private static float NoiseZero(float2 noiseCoord) => 0f;

    private int HeightIndex(int x, int z) => z * chunkResolution + x;
    private int AlphaIndex(int x, int z, int layer) => (z * alphamapResolution + x) * 2 + layer;

    public void Execute()
    {
        var rng = new Unity.Mathematics.Random(chunkSeed == 0 ? 1 : chunkSeed);

        var marker = new ProfilerMarker("TerrainGeneratorJob.Execute");
        marker.Begin();
        noiseMarker = new ProfilerMarker("TerrainGeneratorJob.Execute.Noise");

        // base terrain
        var markerTerrain = new ProfilerMarker("TerrainGeneratorJob.Execute.Terrain");
        markerTerrain.Begin();

        // calculate noise
        var markerTerrainNoise = new ProfilerMarker("TerrainGeneratorJob.Execute.Terrain.Noise");
        markerTerrainNoise.Begin();
        for (int x = 0; x < chunkResolution; x++)
        {
            for (int z = 0; z < chunkResolution; z++)
            {
                var noiseCoord = NoiseCoord((float)x / (chunkResolution - 1), (float)z / (chunkResolution - 1), scale);
                var noiseValue = normalizedNoiseHeight * Noise(noiseCoord, noiseType);
                heights[HeightIndex(x, z)] = noiseValue;
            }
        }
        markerTerrainNoise.End();

        // apply gutter guard
        var markerTerrainGutterGuard = new ProfilerMarker("TerrainGeneratorJob.Execute.Terrain.GutterGuard");
        markerTerrainGutterGuard.Begin();
        if (gutterGuardDistance > 0)
        {
            for (int x = 0; x < chunkResolution; x++)
            {
                for (int z = 0; z < chunkResolution; z++)
                {
                    var dist = math.max(
                        math.smoothstep(gutterGuardDistance, 0f, z / (float)(chunkResolution - 1)),
                        math.smoothstep(1f - gutterGuardDistance, 1f, z / (float)(chunkResolution - 1)));
                    var noiseValue = heights[HeightIndex(x, z)];
                    noiseValue = math.lerp(noiseValue, normalizedNoiseHeight, dist);
                    heights[HeightIndex(x, z)] = noiseValue;
                }
            }
        }
        markerTerrainGutterGuard.End();

        // apply gradient
        var markerTerrainGradient = new ProfilerMarker("TerrainGeneratorJob.Execute.Terrain.Gradient");
        markerTerrainGradient.Begin();
        for (int x = 0; x < chunkResolution; x++)
        {
            for (int z = 0; z < chunkResolution; z++)
            {
                var gradientValue = math.lerp(gradientStart, gradientEnd, (float)x / (chunkResolution - 1));
                var noiseValue = heights[HeightIndex(x, z)];
                heights[HeightIndex(x, z)] = math.saturate(gradientValue + noiseValue);
            }
        }
        markerTerrainGradient.End();

        // reduce the bit depth of the edge of the terrain to 15 bits, to hide rounding errors on the seams
        var markerTerrainEdge = new ProfilerMarker("TerrainGeneratorJob.Execute.Terrain.Edge");
        markerTerrainEdge.Begin();
        for (int x = 0; x < chunkResolution; x++)
        {
            heights[HeightIndex(x, 0)] = math.round(heights[HeightIndex(x, 0)] * 32767f) / 32767f;
            heights[HeightIndex(x, chunkResolution - 1)] = math.round(heights[HeightIndex(x, chunkResolution - 1)] * 32767f) / 32767f;
        }
        for (int z = 0; z < chunkResolution; z++)
        {
            heights[HeightIndex(0, z)] = math.round(heights[HeightIndex(0, z)] * 32767f) / 32767f;
            heights[HeightIndex(chunkResolution - 1, z)] = math.round(heights[HeightIndex(chunkResolution - 1, z)] * 32767f) / 32767f;
        }
        markerTerrainEdge.End();

        markerTerrain.End();

        // grass texture
        var markerGrass = new ProfilerMarker("TerrainGeneratorJob.Execute.Grass");
        markerGrass.Begin();
        for (int x = 0; x < alphamapResolution; x++)
        {
            for (int z = 0; z < alphamapResolution; z++)
            {
                var noiseCoord = NoiseCoord(((float)x + 0.5f) / alphamapResolution, ((float)z + 0.5f) / alphamapResolution, treeNoiseScale);
                var noiseValue = Noise(noiseCoord, treeNoiseType);

                alphaMaps[AlphaIndex(x, z, 0)] = 1f;
                alphaMaps[AlphaIndex(x, z, 1)] = math.smoothstep(treeNoiseMin, treeNoiseMax, noiseValue);
            }
        }
        markerGrass.End();

        // trees
        var markerTrees = new ProfilerMarker("TerrainGeneratorJob.Execute.Trees");
        markerTrees.Begin();
        var treePositions = Gists.FastPoissonDiskSampling.Sampling(new float2(0, 0), new float2(1, 1), ref rng, treeSpacing / terrainSize.x);
        var numTrees = 0;
        for (var i = 0; i < treePositions.Length && i < trees.Length; ++i)
        {
            var treePosition = treePositions[i];
            var noiseCoord = NoiseCoord(treePosition.x, treePosition.y, treeNoiseScale);
            // var noiseValue = Noise(noiseCoord, treeNoiseType);
            var noiseX = math.clamp((int)(treePosition.x * alphamapResolution), 0, alphamapResolution - 1);
            var noiseZ = math.clamp((int)(treePosition.y * alphamapResolution), 0, alphamapResolution - 1);
            var noiseValue = alphaMaps[AlphaIndex(noiseX, noiseZ, 1)];

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
        this.numTrees.Value = numTrees;
        markerTrees.End();

        // sigils
        var markerSigils = new ProfilerMarker("TerrainGeneratorJob.Execute.Sigils");
        markerSigils.Begin();
        var sigilPositions = Gists.FastPoissonDiskSampling.Sampling(new float2(0, 0), new float2(1, 1), ref rng, sigilSpacing / terrainSize.x);
        var numSigils = 0;
        for (var i = 0; i < sigilPositions.Length && i < sigils.Length; ++i)
        {
            var sigilPosition = sigilPositions[i];
            var noiseCoord = NoiseCoord(sigilPosition.x, sigilPosition.y, treeNoiseScale);
            var noiseX = math.clamp((int)(sigilPosition.x * alphamapResolution), 0, alphamapResolution - 1);
            var noiseZ = math.clamp((int)(sigilPosition.y * alphamapResolution), 0, alphamapResolution - 1);
            var noiseValue = alphaMaps[AlphaIndex(noiseX, noiseZ, 1)];

            if (rng.NextFloat() >= noiseValue)
            {
                continue;
            }

            sigils[numSigils++] = sigilPosition;
        }
        this.numSigils.Value = numSigils;
        markerSigils.End();

        marker.End();
    }
}
