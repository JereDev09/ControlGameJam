using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

public class AsteroidSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera gameCamera;
    [SerializeField] private ShipMovement ship;

    [Header("Spawning")]
    [SerializeField] private float spawnInterval = 1.2f;
    [SerializeField] private float spawnIntervalAtDestination = 0.8f;
    [SerializeField] private float spawnDistance = 24f;
    [SerializeField, Range(0f, 0.25f)] private float screenPadding = 0.08f;
    [SerializeField, Range(0f, 0.3f)] private float spawnSpread = 0.16f;
    [SerializeField] private Vector2 sizeRange = new Vector2(0.8f, 1.6f);
    [Tooltip("Segundos que tarda un asteroide en crecer al aparecer")]
    [SerializeField] private float growDuration = 0.6f;

    [Header("Asteroid movement")]
    [SerializeField] private float asteroidSpeed = 5f;
    [SerializeField] private float asteroidSpeedAtDestination = 6f;
    [SerializeField, Min(1f)] private float boostAsteroidSpeedMultiplier = 1.6f;
    [SerializeField] private Vector2 rotationSpeedRange = new Vector2(12f, 40f);
    [SerializeField, Min(0.1f)] private float shipHitRadius = 0.55f;
    [SerializeField] private int damagePerHit = 25;

    [Header("Depth feedback")]
    [SerializeField, Range(0f, 1f)] private float warningStartApproach = 0.65f;
    [SerializeField] private float slowFlashFrequency = 2f;
    [SerializeField] private float fastFlashFrequency = 12f;
    [SerializeField, Min(0.01f)] private float missFadeDuration = 0.45f;

    private readonly List<Asteroid> asteroids = new List<Asteroid>();
    private float spawnTimer;
    private Material asteroidMaterial;

    private sealed class Asteroid
    {
        public GameObject gameObject;
        public Vector3 direction;
        public Vector3 rotationAxis;
        public float rotationSpeed;
        public float speed;
        public float radius;
        public float targetDepth;
        public float spawnDepth;
        public float baseScale;
        public float flashOffset;
        public float fadeElapsed;
        public Color baseColor;
        public MaterialPropertyBlock colorBlock;
        public Renderer renderer;
        public Collider collider;
        public Mesh mesh;
        public bool hasPassedShip;
    }

    private void Awake()
    {
        if (gameCamera == null)
        {
            gameCamera = Camera.main;
        }

        if (ship == null)
        {
            ship = FindFirstObjectByType<ShipMovement>();
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader != null)
        {
            asteroidMaterial = new Material(shader);
            asteroidMaterial.color = new Color(0.34f, 0.35f, 0.33f);
            SetTransparent(asteroidMaterial);
        }

        if (ship != null)
        {
            gameObject.AddComponent<EnemyShipEncounter>().Initialize(ship, gameCamera);
        }

        spawnTimer = spawnInterval;
    }

    private void Update()
    {
        if (gameCamera == null)
        {
            return;
        }

        spawnTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(ShipState.Progress);
        float currentSpawnInterval = Mathf.Lerp(spawnInterval, spawnIntervalAtDestination, progress);
        if (spawnTimer >= currentSpawnInterval)
        {
            spawnTimer = 0f;
            SpawnAsteroid();
        }

        MoveAsteroids();
    }

    private void SpawnAsteroid()
    {
        Vector3 targetPosition = ship != null
            ? ship.transform.position
            : gameCamera.ViewportToWorldPoint(
                new Vector3(0.5f, 0.5f, gameCamera.nearClipPlane + 1f));
        Vector3 shipViewportPosition = gameCamera.WorldToViewportPoint(targetPosition);
        float viewportX = Mathf.Clamp(
            shipViewportPosition.x + Random.Range(-spawnSpread, spawnSpread),
            screenPadding,
            1f - screenPadding);
        float viewportY = Mathf.Clamp(
            shipViewportPosition.y + Random.Range(-spawnSpread, spawnSpread),
            screenPadding,
            1f - screenPadding);
        float targetDepth = shipViewportPosition.z;
        float spawnDepth = targetDepth + Mathf.Max(0.1f, spawnDistance);
        Vector3 spawnPosition = gameCamera.ViewportToWorldPoint(
            new Vector3(viewportX, viewportY, spawnDepth));

        GameObject asteroidObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        asteroidObject.name = "Asteroid";
        asteroidObject.transform.position = spawnPosition;

        float size = Random.Range(sizeRange.x, sizeRange.y);
        Mesh asteroidMesh = MakeRockShape(asteroidObject);

        // Aparece creciendo desde 0 en vez de surgir de golpe
        asteroidObject.transform.localScale = Vector3.zero;
        asteroidObject.transform.DOScale(size, growDuration).SetEase(Ease.OutBack).SetLink(asteroidObject);

        Renderer asteroidRenderer = asteroidObject.GetComponent<Renderer>();
        if (asteroidMaterial != null)
        {
            asteroidRenderer.sharedMaterial = asteroidMaterial;
        }
        asteroidRenderer.rendererPriority = 1;

        Color rockColor = Color.Lerp(new Color(0.26f, 0.27f, 0.25f), new Color(0.46f, 0.45f, 0.41f), Random.value);
        MaterialPropertyBlock colorBlock = new MaterialPropertyBlock();
        SetAsteroidVisual(asteroidRenderer, colorBlock, rockColor, 0f, 1f, 0f);

        asteroidObject.transform.rotation = Random.rotation;
        asteroids.Add(new Asteroid
        {
            gameObject = asteroidObject,
            direction = (targetPosition - spawnPosition).normalized,
            rotationAxis = Random.onUnitSphere,
            rotationSpeed = Random.Range(rotationSpeedRange.x, rotationSpeedRange.y),
            speed = Mathf.Lerp(asteroidSpeed, asteroidSpeedAtDestination, Mathf.Clamp01(ShipState.Progress)),
            radius = size * 0.5f,
            targetDepth = targetDepth,
            spawnDepth = spawnDepth,
            baseScale = size,
            flashOffset = Random.value,
            baseColor = rockColor,
            colorBlock = colorBlock,
            renderer = asteroidRenderer,
            collider = asteroidObject.GetComponent<Collider>(),
            mesh = asteroidMesh
        });
    }

    private void MoveAsteroids()
    {
        float deltaTime = Time.deltaTime;

        for (int index = asteroids.Count - 1; index >= 0; index--)
        {
            Asteroid asteroid = asteroids[index];
            Transform asteroidTransform = asteroid.gameObject.transform;
            float boostMultiplier = ship != null && ship.IsBoosting ? boostAsteroidSpeedMultiplier : 1f;
            float step = asteroid.speed * boostMultiplier * deltaTime;
            asteroidTransform.position += asteroid.direction * step;
            asteroidTransform.Rotate(asteroid.rotationAxis, asteroid.rotationSpeed * deltaTime, Space.World);

            float cameraDepth = Vector3.Dot(
                asteroidTransform.position - gameCamera.transform.position,
                gameCamera.transform.forward);
            float approach = Mathf.InverseLerp(asteroid.spawnDepth, asteroid.targetDepth, cameraDepth);

            if (!asteroid.hasPassedShip && ship != null)
            {
                float hitDistance = shipHitRadius + asteroid.radius;
                if ((asteroidTransform.position - ship.transform.position).sqrMagnitude <= hitDistance * hitDistance)
                {
                    ship.TakeDamage(damagePerHit);
                    RemoveAsteroid(index);
                    continue;
                }
            }

            if (!asteroid.hasPassedShip && cameraDepth <= asteroid.targetDepth)
            {
                asteroid.hasPassedShip = true;
                asteroid.collider.enabled = false;
            }

            float fade = 0f;
            if (asteroid.hasPassedShip)
            {
                asteroid.fadeElapsed += deltaTime;
                fade = Mathf.Clamp01(asteroid.fadeElapsed / Mathf.Max(0.01f, missFadeDuration));
                asteroidTransform.localScale = Vector3.one * asteroid.baseScale * (1f - fade);
            }

            float warning = Mathf.InverseLerp(warningStartApproach, 1f, approach);
            float flashFrequency = Mathf.Lerp(slowFlashFrequency, fastFlashFrequency, warning);
            float flash = warning * (Mathf.Sin((Time.time + asteroid.flashOffset) * flashFrequency * Mathf.PI * 2f) > 0f ? 1f : 0f);
            SetAsteroidVisual(asteroid.renderer, asteroid.colorBlock, asteroid.baseColor, approach, 1f - fade, flash);

            if (fade >= 1f)
            {
                RemoveAsteroid(index);
            }
        }
    }

    private static void SetAsteroidVisual(Renderer asteroidRenderer, MaterialPropertyBlock colorBlock, Color baseColor, float approach, float alpha, float flash)
    {
        Color distantTint = new Color(0.5f, 0.56f, 0.64f);
        Color depthTint = Color.Lerp(distantTint, Color.white, approach);
        Color color = Color.Lerp(baseColor * depthTint, new Color(1f, 0.02f, 0.01f), flash);
        color.a = alpha;
        colorBlock.SetColor("_BaseColor", color);
        colorBlock.SetColor("_Color", color);
        asteroidRenderer.SetPropertyBlock(colorBlock);
    }

    private static void SetTransparent(Material material)
    {
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Mode", 3f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private void RemoveAsteroid(int index)
    {
        Asteroid asteroid = asteroids[index];
        Destroy(asteroid.gameObject);
        if (asteroid.mesh != null)
        {
            Destroy(asteroid.mesh);
        }

        asteroids.RemoveAt(index);
    }

    private static Mesh MakeRockShape(GameObject asteroidObject)
    {
        MeshFilter meshFilter = asteroidObject.GetComponent<MeshFilter>();
        Mesh mesh = meshFilter.mesh;
        Vector3[] vertices = mesh.vertices;

        for (int index = 0; index < vertices.Length; index++)
        {
            vertices[index] *= Random.Range(0.82f, 1.18f);
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        if (asteroidMaterial != null)
        {
            Destroy(asteroidMaterial);
        }
    }
}
