using System.Collections.Generic;
using UnityEngine;

public class RayTracingMaster : MonoBehaviour
{
    public ComputeShader RayTracingShader;
    public Texture SkyboxTexture;
    public Light DirectionalLight;

    private RenderTexture _target;
    private Camera _camera;
    
    private uint _currentSample = 0;
    private Material _addMaterial;
    
    public Vector2 SphereRadius = new Vector2(3.0f, 8.0f);
    public uint SpheresMax = 100;
    public float SpherePlacementRadius = 100.0f;
    private ComputeBuffer _sphereBuffer;
    
    struct Sphere
    {
        public Vector3 position;
        public float radius;
        public Vector3 albedo;
        public Vector3 specular;
    };
    
    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }
    
    private void Update()
    {
        if (transform.hasChanged || DirectionalLight.transform.hasChanged)
        {
            _currentSample = 0;
            transform.hasChanged = false;
            DirectionalLight.transform.hasChanged = false;
        }
    }
    
    private void OnEnable()
    {
        _currentSample = 0;
        SetUpScene();
    }
    private void OnDisable()
    {
        if (_sphereBuffer != null)
            _sphereBuffer.Release();
    }
    private void SetUpScene()
    {
        List<Sphere> spheres = new List<Sphere>();
        // Add a number of random spheres
        for (int i = 0; i < SpheresMax; i++)
        {
            Sphere sphere = new Sphere();
            // Radius and radius
            sphere.radius = SphereRadius.x + Random.value * (SphereRadius.y - SphereRadius.x);
            Vector2 randomPos = Random.insideUnitCircle * SpherePlacementRadius;
            sphere.position = new Vector3(randomPos.x, sphere.radius, randomPos.y);
            // Reject spheres that are intersecting others
            foreach (Sphere other in spheres)
            {
                float minDist = sphere.radius + other.radius;
                if (Vector3.SqrMagnitude(sphere.position - other.position) < minDist * minDist)
                    goto SkipSphere;
            }
            // Albedo and specular color
            Color color = Random.ColorHSV();
            bool metal = Random.value < 0.5f;
            sphere.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
            sphere.specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f;
            // Add the sphere to the list
            spheres.Add(sphere);
            SkipSphere:
            continue;
        }
        // Assign to compute buffer
        _sphereBuffer = new ComputeBuffer(spheres.Count, 40);
        _sphereBuffer.SetData(spheres);
    }
    
    private void SetShaderParameters()
    {
        RayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        RayTracingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));
        Vector3 l = DirectionalLight.transform.forward;
        RayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, DirectionalLight.intensity));
        RayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);
    }

    // The OnRenderImage function is automatically called by Unity whenever the camera has finished rendering.
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();
        Render(destination);
    }

    private void Render(RenderTexture destination)
    {
        // To render, we first create a render target of appropriate dimensions
        // Make sure we have a current render target
        InitRenderTexture();

        // tell the compute shader about it. The 0 is the index of the compute shader’s kernel function – we have only one.
        // Set the target and dispatch the compute shader
        RayTracingShader.SetTexture(0, "Result", _target);
        
        // Next, we dispatch the shader. This means that we are telling the GPU to get busy with a number of thread groups executing our shader code.
        // Each thread group consists of a number of threads which is set in the shader itself.
        // The size and number of thread groups can be specified in up to three dimensions, which makes it easy to apply compute shaders to problems of either dimensionality.
        // In our case, we want to spawn one thread per pixel of the render target.
        // The default thread group size as defined in the Unity compute shader template is [numthreads(8,8,1)], so we’ll stick to that and spawn one thread group per 8×8 pixels. 
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        // Finally, we write our result to the screen using Graphics.Blit
        // Blit the result texture to the screen
        //Graphics.Blit(_target, destination);
        
        // Blit the result texture to the screen
        if (_addMaterial == null)
            _addMaterial = new Material(Shader.Find("Hidden/AddShader"));
        _addMaterial.SetFloat("_Sample", _currentSample);
        Graphics.Blit(_target, destination, _addMaterial);
        _currentSample++;
    }

    private void InitRenderTexture()
    {
        if (_target == null || _target.width != Screen.width || _target.height != Screen.height)
        {
            // Release render texture if we already have one
            if (_target != null)
                _target.Release();

            // Get a render target for Ray Tracing
            _target = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();
            
            _currentSample = 0;
        }
    }
}