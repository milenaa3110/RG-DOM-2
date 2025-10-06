using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Density
{
    public class NoiseDensity : DensityGenerator
    {
        [HideInInspector] public int seed = 5;

        private int numOctaves = 4;
        private float lacunarity = 2;
        private float persistence = .5f;
        private float noiseScale = 3;
        private float noiseWeight = 6;
        private bool closeEdges;
        private float floorOffset = 5f;
        private float weightMultiplier = 1;

        private float hardFloorHeight = -2;
        private float hardFloorWeight = 3;
        private Vector4 shaderParams = new Vector4(1, 0, 0, 0);
        
        [HideInInspector] public bool useFlatten;
        [HideInInspector] public float flattenHeight;

        public void FlattenToHeight(float height)
        {
            flattenHeight = height;
            useFlatten = true;
        }

        public void DisableFlatten()
        {
            useFlatten = false;
        }

        public override ComputeBuffer Generate(ComputeBuffer pointsBuffer, int numPointsPerAxis, float boundsSize,
            Vector3 worldBounds, Vector3 centre, Vector3 offset, float spacing)
        {
            buffersToRelease = new List<ComputeBuffer>();
            
            var prng = new Random(seed);
            var offsets = new Vector3[numOctaves];
            float offsetRange = 1000;
            for (var i = 0; i < numOctaves; i++)
                offsets[i] = new Vector3((float)prng.NextDouble() * 2 - 1, (float)prng.NextDouble() * 2 - 1,
                    (float)prng.NextDouble() * 2 - 1) * offsetRange;

            var offsetsBuffer = new ComputeBuffer(offsets.Length, sizeof(float) * 3);
            offsetsBuffer.SetData(offsets);
            buffersToRelease.Add(offsetsBuffer);

            densityShader.SetVector("centre", new Vector4(centre.x, centre.y, centre.z));
            densityShader.SetInt("octaves", Mathf.Max(1, numOctaves));
            densityShader.SetFloat("lacunarity", lacunarity);
            densityShader.SetFloat("persistence", persistence);
            densityShader.SetFloat("noiseScale", noiseScale);
            densityShader.SetFloat("noiseWeight", noiseWeight);
            densityShader.SetBool("closeEdges", closeEdges);
            densityShader.SetBuffer(0, "offsets", offsetsBuffer);
            densityShader.SetFloat("floorOffset", floorOffset);
            densityShader.SetFloat("weightMultiplier", weightMultiplier);
            densityShader.SetFloat("hardFloor", hardFloorHeight);
            densityShader.SetFloat("hardFloorWeight", hardFloorWeight);
            densityShader.SetBool("useFlatten", useFlatten);
            densityShader.SetFloat("flattenHeight", flattenHeight);

            densityShader.SetVector("params", shaderParams);

            return base.Generate(pointsBuffer, numPointsPerAxis, boundsSize, worldBounds, centre, offset, spacing);
        }
    }
}