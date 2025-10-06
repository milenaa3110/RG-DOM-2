using Density;
using UnityEngine;

public class TerrainTool : MonoBehaviour
{
    [Header("Terrain Generation")]
    [SerializeField] private int gridDensity;
    [SerializeField] private float terrainAmplitude;
    [SerializeField] private int noiseSeed;
    [SerializeField] private bool autoFlattenTerrain;

    public int GridDensity
    {
        get => gridDensity;
        set
        {
            if (gridDensity != value)
            {
                gridDensity = value;
            
                if (customTerrain != null)
                {
                    customTerrain.NumPointsPerAxis = gridDensity;
                }
            
                RegenerateIfInitialized();
            }
        }
    }
    public float TerrainAmplitude
    {
        get => terrainAmplitude;
        set 
        { 
            terrainAmplitude = value; 
            
            if (autoFlattenTerrain && _terrainGenerated)
            {
                FlattenTerrainToHeight();
            }
        }
    }

    public int NoiseSeed
    {
        get => noiseSeed;
        set
        {
            noiseSeed = value; 
            RegenerateIfInitialized();
        }
    }

    [Header("Brush Settings")]
    public float brushRadius = 2f;
    public float densityDelta = 1f;
    public GameObject brushVisualPrefab;

    [Header("References")]
    public CustomTerrain customTerrain;  
    public ComputeShader readDensityShader;
    public ComputeShader densityGeneratorShader;

    private GameObject _brushVisual;
    private Camera _camera;
    private bool _terrainGenerated = false;
    private NoiseDensity _simplexNoiseDensityGenerator;

    void Start()
    {
        _camera = Camera.main;
        if (brushVisualPrefab)
        {
            _brushVisual = Instantiate(brushVisualPrefab, null);
            _brushVisual.transform.localScale = Vector3.one * brushRadius * 2f;
        }

        GenerateTerrain();
    }

    private void FlattenTerrainToHeight()
    {
        if (customTerrain == null || _simplexNoiseDensityGenerator == null)
            return;

        
        _simplexNoiseDensityGenerator.FlattenToHeight(TerrainAmplitude);
        customTerrain.RequestMeshUpdate();
    }

    private void RegenerateIfInitialized()
    {
        if (_terrainGenerated)
        {
            GenerateTerrain();
        }
    }

    public void GenerateTerrain()
    {
        if (!customTerrain)
        {
            return;
        }
        
        _simplexNoiseDensityGenerator = customTerrain.simplexNoiseDensityGenerator;

        if (_simplexNoiseDensityGenerator)
        {
            _simplexNoiseDensityGenerator.seed = NoiseSeed;

            if (autoFlattenTerrain)
            {
                _simplexNoiseDensityGenerator.FlattenToHeight(TerrainAmplitude);
            }
            else
            {
                _simplexNoiseDensityGenerator.DisableFlatten();
            }
        }

        customTerrain.RequestMeshUpdate();
        _terrainGenerated = true;
    }

    public void ToggleFlatten(bool enabled)
    {
        autoFlattenTerrain = enabled;
        if (enabled && _terrainGenerated)
        {
            FlattenTerrainToHeight();
        }
        else if (_terrainGenerated)
        {
            _simplexNoiseDensityGenerator.DisableFlatten();
            customTerrain.RequestMeshUpdate();
        }
    }

    void Update()
    {
        
        if (Input.GetKeyDown(KeyCode.G))
        {
            NoiseSeed = Random.Range(0, 10000);
            GenerateTerrain();
        }
        
        
        if (Input.GetKeyDown(KeyCode.F))
        {
            ToggleFlatten(!autoFlattenTerrain);
        }

        if (!_terrainGenerated) return;

        if (_camera)
        {
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);

            bool hitSomething = false;
            RaycastHit hit;
            float maxDistance = 100f;

            if (Physics.Raycast(ray, out hit, maxDistance))
            {
                hitSomething = true;
            }

            if (!hitSomething)
            {
                Vector3 direction = ray.direction.normalized;
                float stepSize = 2.0f;
                int maxSteps = 50;

                for (int i = 1; i <= maxSteps; i++)
                {
                    Vector3 pointPosition = ray.origin + direction * (i * stepSize);
                    if (IsPointInEditableArea(pointPosition))
                    {
                        hitSomething = true;
                        hit.point = pointPosition;
                        break;
                    }
                }
            }

            if (hitSomething)
            {
                if (_brushVisual)
                {
                    _brushVisual.SetActive(true);
                    _brushVisual.transform.position = hit.point;
                    _brushVisual.transform.localScale = Vector3.one * (brushRadius * 2f);
                }

                if (Input.GetMouseButton(0))
                    ApplyBrush(hit.point, densityDelta);
                if (Input.GetMouseButton(1))
                    ApplyBrush(hit.point, -densityDelta);
            }
            else
            {
                if (_brushVisual)
                    _brushVisual.SetActive(false);
            }
        }

        if (Input.GetKey(KeyCode.Equals)) brushRadius += Time.deltaTime * 2f;
        if (Input.GetKey(KeyCode.Minus)) brushRadius = Mathf.Max(0.1f, brushRadius - Time.deltaTime * 2f);
    }

    bool IsPointInEditableArea(Vector3 point)
    {
        if (customTerrain == null) return false;

        
        Vector3 chunkCenter = customTerrain.transform.position;
        float halfSize = customTerrain.boundsSize * 0.5f;

        return (point.x >= chunkCenter.x - halfSize && point.x <= chunkCenter.x + halfSize &&
                point.y >= chunkCenter.y - halfSize && point.y <= chunkCenter.y + halfSize &&
                point.z >= chunkCenter.z - halfSize && point.z <= chunkCenter.z + halfSize);
    }

    void ApplyBrush(Vector3 center, float delta)
    {
        if (customTerrain == null || customTerrain.uploadDensityShader == null || customTerrain.densityTexture3D == null)
            return;

        int pointsPerAxis = GridDensity;
        float boundsSize = customTerrain.boundsSize;
        float spacing = boundsSize / (pointsPerAxis - 1);

        ComputeBuffer pointsBuffer = new ComputeBuffer(pointsPerAxis * pointsPerAxis * pointsPerAxis, sizeof(float) * 4);

        
        RenderTexture tempRT = RenderTexture.GetTemporary(
            customTerrain.densityTexture3D.width,
            customTerrain.densityTexture3D.height,
            0,
            customTerrain.densityTexture3D.format);
        tempRT.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        tempRT.volumeDepth = customTerrain.densityTexture3D.volumeDepth;
        tempRT.enableRandomWrite = true;
        tempRT.Create();

        Graphics.CopyTexture(customTerrain.densityTexture3D, tempRT);

        
        readDensityShader.SetBuffer(0, "points", pointsBuffer);
        readDensityShader.SetTexture(0, "DensityTexture", tempRT);
        readDensityShader.SetInts("chunkCoord", customTerrain.coord.x, customTerrain.coord.y, customTerrain.coord.z);
        readDensityShader.SetInt("numPointsPerAxis", pointsPerAxis);

        int groups = Mathf.CeilToInt(pointsPerAxis / 8f);
        readDensityShader.Dispatch(0, groups, groups, groups);

        Vector4[] points = new Vector4[pointsPerAxis * pointsPerAxis * pointsPerAxis];
        pointsBuffer.GetData(points);

        RenderTexture.ReleaseTemporary(tempRT);

        bool modified = false;
        float baseStrength = 5.0f;

        
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 pos = new Vector3(points[i].x, points[i].y, points[i].z);
            float distSqr = (pos - center).sqrMagnitude;
            if (distSqr <= brushRadius * brushRadius)
            {
                float t = distSqr / (brushRadius * brushRadius);
                float falloff = 1 - t * t;
                points[i].w += delta * falloff * baseStrength;
                modified = true;
            }
        }

        if (modified)
        {
            pointsBuffer.SetData(points);
            var upload = customTerrain.uploadDensityShader;
            upload.SetBuffer(0, "points", pointsBuffer);
            upload.SetTexture(0, "Result", customTerrain.densityTexture3D);
            upload.SetInts("chunkCoord", customTerrain.coord.x, customTerrain.coord.y, customTerrain.coord.z);
            upload.SetInt("numPointsPerAxis", pointsPerAxis);

            upload.Dispatch(0, groups, groups, groups);

            customTerrain.UpdateMesh();
        }

        pointsBuffer.Release();
    }

#if UNITY_EDITOR
    
    private void OnValidate()
    {
        if (customTerrain != null && customTerrain.NumPointsPerAxis != GridDensity)
        {
            customTerrain.NumPointsPerAxis = GridDensity;
        }
        RegenerateIfInitialized();
    }
#endif
}
