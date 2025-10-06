using System.Collections.Generic;
using UnityEngine;

public abstract class DensityGenerator : MonoBehaviour {

    private const int ThreadGroupSize = 8;
    public ComputeShader densityShader;
    protected List<ComputeBuffer> buffersToRelease;

    void OnValidate() {
        CustomTerrain customTerrain = FindAnyObjectByType<CustomTerrain>();
        if (customTerrain != null && customTerrain.isActiveAndEnabled) {
            
            if (Application.isPlaying) {
                customTerrain.GenerateMesh();
            } else {
                customTerrain.RequestMeshUpdate();
            }
        }
    }

    public virtual ComputeBuffer Generate(ComputeBuffer pointsBuffer, int numPointsPerAxis, float boundsSize, Vector3 worldBounds, Vector3 centre, Vector3 offset, float spacing) {
        int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
        int numThreadsPerAxis = Mathf.CeilToInt(numPointsPerAxis / (float)ThreadGroupSize);
        
        if (densityShader == null) {
            Debug.LogError("Density shader is null on " + gameObject.name);
            return pointsBuffer;
        }
        
        if (pointsBuffer == null) {
            pointsBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4);
        }
        
        densityShader.SetBuffer(0, "points", pointsBuffer);
        densityShader.SetInt("numPointsPerAxis", numPointsPerAxis);
        densityShader.SetFloat("boundsSize", boundsSize);
        densityShader.SetVector("centre", new Vector4(centre.x, centre.y, centre.z));
        densityShader.SetVector("offset", new Vector4(offset.x, offset.y, offset.z));
        densityShader.SetFloat("spacing", spacing);
        densityShader.SetVector("worldSize", worldBounds);
        
        densityShader.Dispatch(0, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);
        
        if (buffersToRelease != null) {
            foreach (var b in buffersToRelease) {
                if (b != null) {
                    b.Release();
                }
            }
        }
        
        return pointsBuffer;
    }
}
