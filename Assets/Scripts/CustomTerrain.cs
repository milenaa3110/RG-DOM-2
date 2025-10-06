using Density;
using UnityEngine;

public class CustomTerrain : MonoBehaviour
{
    [Header("Settings")]
    private int _numPointsPerAxis = 30;
    public float boundsSize = 150;
    public float isoLevel = 0;
    public Vector3Int coord;
    public Vector3 offset = Vector3.zero;

    [Header("References")]
    public NoiseDensity simplexNoiseDensityGenerator;
    public Material material;
    public ComputeShader marchingCubesShader;
    public ComputeShader uploadDensityShader;
    
    [HideInInspector] public RenderTexture densityTexture3D;
    [HideInInspector] public Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private bool generateCollider = true;

    
    private ComputeBuffer triangleBuffer;
    private ComputeBuffer pointsBuffer;
    private ComputeBuffer triCountBuffer;
    
    private const int ThreadGroupSize = 8;
    public int NumPointsPerAxis 
    {
        get => _numPointsPerAxis;
        set => _numPointsPerAxis = value;
    }
    void Awake()
    {
        SetUpComponents();
        CreateBuffers();
        GenerateMesh();
    }

    void OnDestroy()
    {
        ReleaseBuffers();
    }

    public void SetUpComponents()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();

        if (!meshFilter)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        if (meshCollider == null && generateCollider)
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }

        mesh = meshFilter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            meshFilter.sharedMesh = mesh;
        }

        meshRenderer.material = material;
    }

    void CreateBuffers()
    {
        int pointsPerAxis = Mathf.Max(2, _numPointsPerAxis);
        int numPoints = pointsPerAxis * pointsPerAxis * pointsPerAxis;
        int numVoxelsPerAxis = pointsPerAxis - 1;
        int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
        int maxTriangleCount = numVoxels * 5;

        
        if (densityTexture3D != null)
        {
            if (densityTexture3D.IsCreated()) densityTexture3D.Release();
            DestroyImmediate(densityTexture3D);
        }

        densityTexture3D = new RenderTexture(pointsPerAxis, pointsPerAxis, 0)
        {
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = pointsPerAxis,
            enableRandomWrite = true,
            format = RenderTextureFormat.ARGBFloat
        };
        densityTexture3D.Create();

        
        if (triangleBuffer != null) ReleaseBuffers();
        
        triangleBuffer = new ComputeBuffer(maxTriangleCount, sizeof(float) * 3 * 3, ComputeBufferType.Append);
        pointsBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4);
        triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
    }

    void ReleaseBuffers()
    {
        if (triangleBuffer != null)
        {
            triangleBuffer.Release();
            pointsBuffer.Release();
            triCountBuffer.Release();
        }
    }

    public void GenerateMesh()
    {
        if (simplexNoiseDensityGenerator == null || marchingCubesShader == null || uploadDensityShader == null)
        {
            Debug.LogError("Missing required components on Chunk");
            return;
        }

        
        int pointsPerAxis = Mathf.Max(2, _numPointsPerAxis);
        float pointSpacing = boundsSize / (pointsPerAxis - 1);
        Vector3 centre = transform.position;

        
        simplexNoiseDensityGenerator.Generate(pointsBuffer, pointsPerAxis, boundsSize, Vector3.one * boundsSize, centre, offset, pointSpacing);

        
        uploadDensityShader.SetBuffer(0, "points", pointsBuffer);        uploadDensityShader.SetTexture(0, "Result", densityTexture3D);
        uploadDensityShader.SetInt("numPointsPerAxis", pointsPerAxis);
        uploadDensityShader.SetInts("chunkCoord", coord.x, coord.y, coord.z);

        int groups = Mathf.CeilToInt(pointsPerAxis / 8f);
        uploadDensityShader.Dispatch(0, groups, groups, groups);

        
        CreateMeshFromDensity();
    }

    public void UpdateMesh()
    {
        
        CreateMeshFromDensity();
    }

    private void CreateMeshFromDensity()
    {
        int pointsPerAxis = Mathf.Max(2, _numPointsPerAxis);
        int numVoxelsPerAxis = pointsPerAxis - 1;
        int numThreadsPerAxis = Mathf.CeilToInt(numVoxelsPerAxis / (float)ThreadGroupSize);

        triangleBuffer.SetCounterValue(0);

        
        marchingCubesShader.SetTexture(0, "densityTex", densityTexture3D);
        marchingCubesShader.SetBuffer(0, "triangles", triangleBuffer);
        marchingCubesShader.SetInt("numPointsPerAxis", pointsPerAxis);
        marchingCubesShader.SetFloat("isoLevel", isoLevel);
        marchingCubesShader.SetInts("chunkCoord", coord.x, coord.y, coord.z);

        
        marchingCubesShader.Dispatch(0, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);

        
        ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
        int[] triCountArray = { 0 };
        triCountBuffer.GetData(triCountArray);
        int numTris = triCountArray[0];

        if (numTris > 0)
        {
            
            Triangle[] tris = new Triangle[numTris];
            triangleBuffer.GetData(tris, 0, 0, numTris);

            mesh.Clear();

            var vertices = new Vector3[numTris * 3];
            var meshTriangles = new int[numTris * 3];
            var colors = new Color[numTris * 3];

            
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;

            for (int i = 0; i < numTris; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    float y = tris[i][j].y;
                    minHeight = Mathf.Min(minHeight, y);
                    maxHeight = Mathf.Max(maxHeight, y);
                }
            }

            float heightRange = Mathf.Max(0.1f, maxHeight - minHeight);

            for (int i = 0; i < numTris; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    meshTriangles[i * 3 + j] = i * 3 + j;
                    vertices[i * 3 + j] = tris[i][j];
                    
                    
                    float normalizedHeight = (tris[i][j].y - minHeight) / heightRange;
                    colors[i * 3 + j] = Color.HSVToRGB(normalizedHeight * 0.83f, 1.0f, 1.0f);
                }
            }

            mesh.vertices = vertices;
            mesh.triangles = meshTriangles;
            mesh.colors = colors;
            mesh.RecalculateNormals();
            
            if (generateCollider && meshCollider != null)
            {
                meshCollider.sharedMesh = mesh;
            }
        }
    }
    

    public void RequestMeshUpdate()
    {
        SetUpComponents();
        CreateBuffers();
        GenerateMesh();
    }

    private struct Triangle
    {
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;

        public Vector3 this[int i]
        {
            get
            {
                switch (i)
                {
                    case 0: return a;
                    case 1: return b;
                    default: return c;
                }
            }
        }
    }
}
