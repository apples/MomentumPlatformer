// Copied and modified from:
// https://gist.github.com/a3geek/8532817159b77c727040cf67c92af322

using System.Collections.Generic;
using Unity.Mathematics;

namespace Gists
{
    // The algorithm is from the "Fast Poisson Disk Sampling in Arbitrary Dimensions" paper by Robert Bridson.
    // https://www.cs.ubc.ca/~rbridson/docs/bridson-siggraph07-poissondisk.pdf

    public static class FastPoissonDiskSampling
    {
        public const float InvertRootTwo = 0.70710678118f; // Becaust two dimension grid.
        public const int DefaultIterationPerPoint = 30;

        #region "Structures"
        private class Settings
        {
            public float2 BottomLeft;
            public float2 TopRight;
            public float2 Center;
            public rect Dimension;

            public float MinimumDistance;
            public int IterationPerPoint;

            public float CellSize;
            public int GridWidth;
            public int GridHeight;
        }

        private class Bags
        {
            public float2?[,] Grid;
            public List<float2> SamplePoints;
            public List<float2> ActivePoints;
        }

        private struct rect
        {
            public float2 BottomLeft;
            public float2 TopRight;

            public rect(float2 bottomLeft, float2 size)
            {
                BottomLeft = bottomLeft;
                TopRight = bottomLeft + size;
            }

            public bool Contains(float2 point)
            {
                return point.x >= BottomLeft.x && point.x < TopRight.x && point.y >= BottomLeft.y && point.y < TopRight.y;
            }
        }
        #endregion

        
        public static List<float2> Sampling(float2 bottomLeft, float2 topRight, ref Random rng, float minimumDistance)
        {
            return Sampling(bottomLeft, topRight, ref rng, minimumDistance, DefaultIterationPerPoint);
        }

        public static List<float2> Sampling(float2 bottomLeft, float2 topRight, ref Random rng, float minimumDistance, int iterationPerPoint)
        {
            var settings = GetSettings(
                bottomLeft,
                topRight,
                minimumDistance,
                iterationPerPoint <= 0 ? DefaultIterationPerPoint : iterationPerPoint
            );

            var bags = new Bags()
            {
                Grid = new float2?[settings.GridWidth + 1, settings.GridHeight + 1],
                SamplePoints = new List<float2>(),
                ActivePoints = new List<float2>()
            };

            GetFirstPoint(settings, bags, ref rng);
            
            do
            {
                var index = rng.NextInt(0, bags.ActivePoints.Count);

                var point = bags.ActivePoints[index];

                var found = false;
                for(var k = 0; k < settings.IterationPerPoint; k++)
                {
                    found = found | GetNextPoint(point, settings, bags, ref rng);
                }

                if(found == false)
                {
                    bags.ActivePoints.RemoveAt(index);
                }
            }
            while(bags.ActivePoints.Count > 0);

            return bags.SamplePoints;
        }

        #region "Algorithm Calculations"
        private static bool GetNextPoint(float2 point, Settings set, Bags bags, ref Random rng)
        {
            var found = false;
            var p = GetRandPosInCircle(set.MinimumDistance, 2f * set.MinimumDistance, ref rng) + point;

            if(set.Dimension.Contains(p) == false)
            {
                return false;
            }

            var minimum = set.MinimumDistance * set.MinimumDistance;
            var index = GetGridIndex(p, set);
            var drop = false;

            // Although it is Mathf.CeilToInt(set.MinimumDistance / set.CellSize) in the formula, It will be 2 after all.
            var around = 2;
            var fieldMin = new int2(math.max(0, index.x - around), math.max(0, index.y - around));
            var fieldMax = new int2(math.min(set.GridWidth, index.x + around), math.min(set.GridHeight, index.y + around));

            for(var i = fieldMin.x; i <= fieldMax.x && drop == false; i++)
            {
                for(var j = fieldMin.y; j <= fieldMax.y && drop == false; j++)
                {
                    var q = bags.Grid[i, j];
                    if(q.HasValue == true && math.lengthsq(q.Value - p) <= minimum)
                    {
                        drop = true;
                    }
                }
            }

            if(drop == false)
            {
                found = true;

                bags.SamplePoints.Add(p);
                bags.ActivePoints.Add(p);
                bags.Grid[index.x, index.y] = p;
            }

            return found;
        }

        private static void GetFirstPoint(Settings set, Bags bags, ref Random rng)
        {
            var first = new float2(
                rng.NextFloat(set.BottomLeft.x, set.TopRight.x),
                rng.NextFloat(set.BottomLeft.y, set.TopRight.y)
            );

            var index = GetGridIndex(first, set);

            bags.Grid[index.x, index.y] = first;
            bags.SamplePoints.Add(first);
            bags.ActivePoints.Add(first);
        }
        #endregion

        #region "Utils"
        private static int2 GetGridIndex(float2 point, Settings set)
        {
            return new int2(
                (int)math.floor((point.x - set.BottomLeft.x) / set.CellSize),
                (int)math.floor((point.y - set.BottomLeft.y) / set.CellSize)
            );
        }

        private static Settings GetSettings(float2 bl, float2 tr, float min, int iteration)
        {
            var dimension = (tr - bl);
            var cell = min * InvertRootTwo;

            return new Settings()
            {
                BottomLeft = bl,
                TopRight = tr,
                Center = (bl + tr) * 0.5f,
                Dimension = new rect(new float2(bl.x, bl.y), new float2(dimension.x, dimension.y)),

                MinimumDistance = min,
                IterationPerPoint = iteration,

                CellSize = cell,
                GridWidth = (int)math.ceil(dimension.x / cell),
                GridHeight = (int)math.ceil(dimension.y / cell)
            };
        }

        private static float2 GetRandPosInCircle(float fieldMin, float fieldMax, ref Random rng)
        {
            var theta = rng.NextFloat(0f, math.PI * 2f);
            var radius = math.sqrt(rng.NextFloat(fieldMin * fieldMin, fieldMax * fieldMax));

            return new float2(radius * math.cos(theta), radius * math.sin(theta));
        }
        #endregion
    }
}
