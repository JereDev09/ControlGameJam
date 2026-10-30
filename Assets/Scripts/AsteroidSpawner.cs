using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class AsteroidSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera gameCamera;
    [SerializeField] private ShipMovement ship;

    [Header("Spawning")]
    [SerializeField] private float spawnInterval = 1.5f;
    [SerializeField] private float spawnDistance = 12f;
    [SerializeField, Range(0f, 0.25f)] private float screenPadding = 0.08f;
    [SerializeField, Range(0f, 0.3f)] private float spawnSpread = 0.12f;
    [SerializeField] private Vector2 sizeRange = new Vector2(0.8f, 1.6f);
    [Tooltip("Segundos que tarda un asteroide en crecer al aparecer")]
    [SerializeField] private float growDuration = 0.6f;

    [Header("Asteroid movement")]
    [SerializeField] private float asteroidSpeed = 4.5f;
    [SerializeField] private Vector2 rotationSpeedRange = new Vector2(12f, 40f);
    [SerializeField] private float shipHitRadius = 1.2f;
    [SerializeField] private int damagePerHit = 25;

    private readonly List<Asteroid> asteroids = new List<Asteroid>();
    private float spawnTimer;
    private Material asteroidMaterial;

    private sealed class Asteroid
    {
        public GameObject gameObject;
        public Vector3 direction;
        public Vector3 rotationAxis;
        public float rotationSpeed;
        public float radius;
        public float targetDepth;
        public Mesh mesh;
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
        if (spawnTimer >= spawnInterval)
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

        MaterialPropertyBlock colorBlock = new MaterialPropertyBlock();
        Color rockColor = Color.Lerp(new Color(0.26f, 0.27f, 0.25f), new Color(0.46f, 0.45f, 0.41f), Random.value);
        colorBlock.SetColor("_BaseColor", rockColor);
        colorBlock.SetColor("_Color", rockColor);
        asteroidRenderer.SetPropertyBlock(colorBlock);

        asteroidObject.transform.rotation = Random.rotation;
        asteroids.Add(new Asteroid
        {
            gameObject = asteroidObject,
            direction = (targetPosition - spawnPosition).normalized,
            rotationAxis = Random.onUnitSphere,
            rotationSpeed = Random.Range(rotationSpeedRange.x, rotationSpeedRange.y),
            radius = size * 0.5f,
            targetDepth = targetDepth,
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
            float step = asteroidSpeed * deltaTime;
            asteroidTransform.position += asteroid.direction * step;
            asteroidTransform.Rotate(asteroid.rotationAxis, asteroid.rotationSpeed * deltaTime, Space.World);

            if (ship != null)
            {
                float hitDistance = shipHitRadius + asteroid.radius;
                if ((asteroidTransform.position - ship.transform.position).sqrMagnitude <= hitDistance * hitDistance)
                {
                    ship.TakeDamage(damagePerHit);
                    RemoveAsteroid(index);
                    continue;
                }
            }

            float cameraDepth = Vector3.Dot(
                asteroidTransform.position - gameCamera.transform.position,
                gameCamera.transform.forward);
            if (cameraDepth <= Mathf.Max(gameCamera.nearClipPlane, asteroid.targetDepth))
            {
                RemoveAsteroid(index);
            }
        }
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
