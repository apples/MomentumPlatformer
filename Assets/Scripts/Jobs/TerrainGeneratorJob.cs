
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[BurstCompile]
public struct TerrainGeneratorJob : IJob, System.IDisposable
{
    public int chunkX;
    public int chunkZ;
    public int chunkResolution;
    public float2 terrainSize;
    public float terrainHeight;
    public int alphamapResolution;
    public uint chunkSeed;
    public float3 worldPosition;

    // surface settings
    public float2 origin;
    public float2 scale;
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

    // foliage
    public NativeArray<FoliageLayer.FoliageParams> foliageParams;
    public NativeArray<Bounds> foliageBounds;

    // sigil settings
    public float sigilSpacing;

    // results
    public NativeArray<float> heights;
    public NativeList<float3> sigils;
    public NativeArray<float> alphaMaps;

    public NativeList<FoliageRenderer.MeshInstanceData> foliageInstanceData;
    public NativeList<FoliageInfo> foliageInfo;
    public NativeArray<FoliageResult> foliageResults;

    public struct FoliageInfo
    {
        public float3 position;
        public quaternion rotation;
        public float3 scale;
    }

    public struct FoliageResult
    {
        public int index;
        public int count;
        public Bounds bounds;
    }

    public void Allocate(Allocator allocator)
    {
        var terrainSize = this.terrainSize;
        int MaxPoissonCount(float d) => (int)math.ceil((terrainSize.x + d) * (terrainSize.y + d) * math.sqrt(3f) / (6f * d * d * 0.25f));
        // var maxTreeCount = MaxPoissonCount(treeSpacing);
        var maxSigilCount = MaxPoissonCount(sigilSpacing);
        heights = new NativeArray<float>(chunkResolution * chunkResolution, allocator);
        // treeInstanceData = new NativeList<FoliageRenderer.MeshInstanceData>(maxTreeCount, allocator);
        // trees = new NativeList<FoliageInfo>(maxTreeCount, allocator);
        sigils = new NativeList<float3>(maxSigilCount, allocator);
        alphaMaps = new NativeArray<float>((chunkResolution - 1) * (chunkResolution - 1) * 2, allocator);

        var maxFoliageCount = 0;
        for (int i = 0; i < foliageParams.Length; i++)
        {
            maxFoliageCount += MaxPoissonCount(foliageParams[i].spacing);
        }
        foliageInstanceData = new NativeList<FoliageRenderer.MeshInstanceData>(maxFoliageCount, allocator);
        foliageInfo = new NativeList<FoliageInfo>(maxFoliageCount, allocator);
        foliageResults = new NativeArray<FoliageResult>(maxFoliageCount, allocator);
    }

    public void Dispose()
    {
        heights.Dispose();
        // treeInstanceData.Dispose();
        // trees.Dispose();
        sigils.Dispose();
        alphaMaps.Dispose();

        foliageParams.Dispose();
        foliageBounds.Dispose();
        foliageInstanceData.Dispose();
        foliageInfo.Dispose();
        foliageResults.Dispose();
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
        var rng = new Random(chunkSeed == 0 ? 1 : chunkSeed);

        // base terrain

        // calculate noise
        for (int x = 0; x < chunkResolution; x++)
        {
            for (int z = 0; z < chunkResolution; z++)
            {
                var noiseCoord = NoiseCoord((float)x / (chunkResolution - 1), (float)z / (chunkResolution - 1), scale);
                var noiseValue = normalizedNoiseHeight * Noise(noiseCoord, noiseType);
                heights[HeightIndex(x, z)] = noiseValue;
            }
        }

        // apply gutter guard
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

        // apply gradient
        for (int x = 0; x < chunkResolution; x++)
        {
            for (int z = 0; z < chunkResolution; z++)
            {
                var gradientValue = math.lerp(gradientStart, gradientEnd, (float)x / (chunkResolution - 1));
                var noiseValue = heights[HeightIndex(x, z)];
                heights[HeightIndex(x, z)] = math.saturate(gradientValue + noiseValue);
            }
        }

        // reduce the bit depth of the edge of the terrain to 15 bits, to hide rounding errors on the seams
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

        // grass texture
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

        // sigils
        
        var markerSigils = new ProfilerMarker("TerrainGeneratorJob.Execute.Sigils");
        markerSigils.Begin();

        PoissonSampler(ref sigils, ref rng, sigilSpacing, alphaLayer: 1, inverted: true, forcedChance: 0f);

        markerSigils.End();

        // foliage
        
        var markerFoliage = new ProfilerMarker("TerrainGeneratorJob.Execute.Foliage");
        markerFoliage.Begin();

        for (var i = 0; i < foliageParams.Length; ++i)
        {
            var p = foliageParams[i];

            var foliageSample = new NativeList<float3>(Allocator.Temp);
            PoissonSampler(ref foliageSample, ref rng, p.spacing, alphaLayer: p.alphaLayer, inverted: p.inverted, forcedChance: p.forcedChance);

            var result = new FoliageResult();
            result.index = foliageInfo.Length;
            result.count = foliageSample.Length;

            for (var j = 0; j < foliageSample.Length; ++j)
            {
                var position = foliageSample[j];

                var spinY = math.lerp(0f, 2f * math.PI, rng.NextFloat());
                var rotation = quaternion.Euler(0, spinY, 0);
                var meshRotation = quaternion.Euler(p.meshRotationEuler * Mathf.Deg2Rad);
                var combinedRotation = math.mul(rotation, meshRotation);

                var height = math.lerp(p.minHeight, p.maxHeight, rng.NextFloat());
                var width = math.lerp(p.minWidth, p.maxWidth, rng.NextFloat());
                var scale = new float3(width, height, width);

                var matrix = math.mul(math.mul(
                    float4x4.Translate(position),
                    float4x4.Scale(scale)),
                    new float4x4(combinedRotation, float3.zero));

                var meshInstance = new FoliageRenderer.MeshInstanceData
                {
                    objectToWorld = matrix,
                    worldToObject = math.inverse(matrix),
                };

                foliageInstanceData.Add(meshInstance);
                foliageInfo.Add(new FoliageInfo
                {
                    position = position,
                    rotation = rotation,
                    scale = scale,
                });

                if (j == 0)
                {
                    result.bounds = new Bounds(position, Vector3.zero);
                }

                var bounds = foliageBounds[i];
                bounds.center = position;
                result.bounds.Encapsulate(bounds);
            }

            foliageResults[i] = result;
        }


        markerFoliage.End();
    }

    void PoissonSampler(ref NativeList<float3> results, ref Random rng, float spacing, int alphaLayer, bool inverted, float forcedChance)
    {
        var samples = Gists.FastPoissonDiskSampling.Sampling(new float2(0, 0), terrainSize, ref rng, spacing);

        results.Clear();

        if (results.Capacity < samples.Length)
        {
            results.Capacity = samples.Length;
        }

        for (var i = 0; i < samples.Length; ++i)
        {
            var sample = samples[i] / terrainSize;
            var noiseX = math.clamp((int)(sample.x * alphamapResolution), 0, alphamapResolution - 1);
            var noiseZ = math.clamp((int)(sample.y * alphamapResolution), 0, alphamapResolution - 1);
            var noiseValue = alphaMaps[AlphaIndex(noiseX, noiseZ, alphaLayer)];

            if (!inverted == (rng.NextFloat() < noiseValue) && (forcedChance == 0f || rng.NextFloat() >= forcedChance))
            {
                continue;
            }

            var hxb = math.clamp(sample.x * (chunkResolution - 1), 0, chunkResolution - 1);
            var hyb = math.clamp(sample.y * (chunkResolution - 1), 0, chunkResolution - 1);
            var hxf = math.clamp(math.frac(hxb), 0, 1);
            var hyf = math.clamp(math.frac(hyb), 0, 1);

            if (hxb == chunkResolution - 1)
            {
                hxb -= 1;
                hxf = 1;
            }

            if (hyb == chunkResolution - 1)
            {
                hyb -= 1;
                hyf = 1;
            }

            var h00 = heights[HeightIndex((int)hxb, (int)hyb)];
            var h01 = heights[HeightIndex((int)hxb, (int)hyb + 1)];
            var h10 = heights[HeightIndex((int)hxb + 1, (int)hyb)];
            var h11 = heights[HeightIndex((int)hxb + 1, (int)hyb + 1)];

            var h = math.lerp(
                math.lerp(h00, h01, hyf),
                math.lerp(h10, h11, hyf),
                hxf);

            var y = h * terrainHeight;

            var position = new float3(sample.x * terrainSize.x, y, sample.y * terrainSize.y) + worldPosition;

            results.Add(position);
        }
    }
}
