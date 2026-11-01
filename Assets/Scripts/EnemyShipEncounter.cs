using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class EnemyShipEncounter : MonoBehaviour
{
    [Header("Encounter timing")]
    [SerializeField, Range(0f, 1f)] private float encounterStartProgress = 0.2f;
    [SerializeField] private Vector2 encounterInterval = new Vector2(5f, 9f);
    [SerializeField, Min(1)] private int maxActiveEnemies = 3;

    [Header("Enemy movement")]
    [SerializeField, Min(0.1f)] private float enemyScale = 0.65f;
    [SerializeField, Min(1f)] private float entrySpeed = 18f;
    [SerializeField, Min(1f)] private float distanceBehindCamera = 18f;
    [SerializeField, Min(1f)] private float distanceInFrontOfPlayer = 12f;
    [SerializeField, Min(1f)] private float formationSpacing = 3.5f;
    [SerializeField, Min(1)] private int enemyHealth = 6;
    [SerializeField, Min(0.1f)] private float enemyFireInterval = 0.9f;
    [SerializeField, Min(0.1f)] private float enemyProjectileSpeed = 8f;
    [SerializeField, Min(1)] private int enemyDamage = 8;

    [Header("Player weapon")]
    [SerializeField, Min(0.05f)] private float playerFireInterval = 0.3f;
    [SerializeField, Min(1f)] private float playerProjectileSpeed = 30f;
    [SerializeField, Min(0.1f)] private float playerProjectileDamage = 1f;

    private readonly List<Enemy> enemies = new List<Enemy>();
    private readonly List<Projectile> projectiles = new List<Projectile>();
    private ShipMovement ship;
    private Camera gameCamera;
    private InputAction attackAction;
    private GameObject enemyPrefab;
    private Material playerProjectileMaterial;
    private Material enemyProjectileMaterial;
    private Material playerTrailMaterial;
    private Material enemyTrailMaterial;
    private float encounterTimer;
    private float playerFireTimer;
    private bool initialized;
    private bool hasShownWeaponHint;
    private CanvasGroup weaponHintGroup;

    private sealed class Enemy
    {
        public GameObject gameObject;
        public int health;
        public float elapsed;
        public float nextShot;
        public Vector3 destinationPosition;
        public Quaternion destinationRotation;
        public int formationSlot;
        public bool hasReachedPosition;
    }

    private sealed class Projectile
    {
        public GameObject gameObject;
        public Vector3 direction;
        public float speed;
        public float damage;
        public float lifetime;
        public bool targetsPlayer;
        public Enemy target;
    }

    public void Initialize(ShipMovement playerShip, Camera cameraForEncounter)
    {
        if (initialized)
        {
            return;
        }

        ship = playerShip;
        gameCamera = cameraForEncounter;
        enemyPrefab = Resources.Load<GameObject>("EnemyShip");
        if (enemyPrefab == null)
        {
            Debug.LogError("No se encontró Assets/Resources/EnemyShip.fbx; no se podrán iniciar encuentros enemigos.", this);
            enabled = false;
            return;
        }

        attackAction = InputSystem.actions.FindAction("Attack");
        if (attackAction == null)
        {
            Debug.LogError("No se encontró la acción de entrada 'Attack'; no se podrá disparar a las naves enemigas.", this);
            enabled = false;
            return;
        }

        playerProjectileMaterial = CreateProjectileMaterial(
            "PlayerProjectileMaterial", new Color(0.35f, 0.95f, 1f), false);
        enemyProjectileMaterial = CreateProjectileMaterial(
            "EnemyProjectileMaterial", new Color(1f, 0.28f, 0.08f), false);
        playerTrailMaterial = CreateProjectileMaterial(
            "PlayerTrailMaterial", new Color(0.12f, 0.75f, 1f), true);
        enemyTrailMaterial = CreateProjectileMaterial(
            "EnemyTrailMaterial", new Color(1f, 0.12f, 0.025f), true);
        encounterTimer = 0f;
        initialized = true;
    }

    private void Update()
    {
        if (!ShipState.IsPiloting || ShipState.IsDestroyed)
        {
            ClearEncounter();
            return;
        }

        if (PauseMenu.IsPaused)
        {
            return;
        }

        if (!initialized || ship == null || !ship.isActiveAndEnabled || !ShipState.TutorialFinished)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        playerFireTimer -= deltaTime;

        if (ShipState.Progress >= encounterStartProgress
            && enemies.Count < Mathf.Min(maxActiveEnemies, 3))
        {
            encounterTimer -= deltaTime;
            if (encounterTimer <= 0f)
            {
                SpawnEnemy();
            }
        }

        UpdateEnemies(deltaTime);
        UpdateProjectiles(deltaTime);

        if (ShipState.WeaponsUnlocked && playerFireTimer <= 0f && attackAction.WasPressedThisFrame())
        {
            FirePlayerWeapon();
        }
    }

    private void SpawnEnemy()
    {
        Vector3 forward = GetCameraForward();
        Vector3 right = gameCamera != null ? gameCamera.transform.right : ship.transform.right;
        int formationSlot = FindAvailableFormationSlot();
        float horizontalOffset = (formationSlot - 1) * formationSpacing;
        Vector3 positionOffset = right * horizontalOffset;
        Vector3 entryPosition = gameCamera != null
            ? gameCamera.transform.position - forward * distanceBehindCamera + positionOffset
            : ship.transform.position - forward * distanceBehindCamera + positionOffset;
        Vector3 arrivalPosition = ship.transform.position + forward * distanceInFrontOfPlayer + positionOffset;

        GameObject enemyObject = Instantiate(enemyPrefab, entryPosition, Quaternion.identity);
        enemyObject.name = "EnemyShip";
        enemyObject.transform.localScale *= enemyScale;
        enemyObject.transform.rotation = FaceDirection(arrivalPosition - entryPosition);

        Enemy enemy = new Enemy
        {
            gameObject = enemyObject,
            health = enemyHealth,
            nextShot = Random.Range(enemyFireInterval * 0.35f, enemyFireInterval * 0.65f),
            destinationPosition = arrivalPosition,
            formationSlot = formationSlot
        };
        enemies.Add(enemy);
        encounterTimer = Random.Range(encounterInterval.x, encounterInterval.y);
        GameAudio.PlaySfx(GameAudio.Sounds.enemyDetected);

        if (!ShipState.WeaponsUnlocked)
        {
            ShipState.WeaponsUnlocked = true;
            ShowWeaponHint();
        }
    }

    private void UpdateEnemies(float deltaTime)
    {
        for (int index = enemies.Count - 1; index >= 0; index--)
        {
            Enemy enemy = enemies[index];
            if (enemy.gameObject == null)
            {
                enemies.RemoveAt(index);
                continue;
            }

            enemy.elapsed += deltaTime;
            Transform enemyTransform = enemy.gameObject.transform;
            if (!enemy.hasReachedPosition)
            {
                Vector3 toDestination = enemy.destinationPosition - enemyTransform.position;
                enemyTransform.position = Vector3.MoveTowards(
                    enemyTransform.position,
                    enemy.destinationPosition,
                    entrySpeed * deltaTime);

                if (toDestination.sqrMagnitude <= 0.09f
                    || (enemy.destinationPosition - enemyTransform.position).sqrMagnitude <= 0.09f)
                {
                    enemy.hasReachedPosition = true;
                    enemyTransform.position = enemy.destinationPosition;
                    enemy.destinationRotation = enemyTransform.rotation;
                }
                Vector3 directionToPlayer = ship.transform.position - enemyTransform.position;
                if (!enemy.hasReachedPosition && directionToPlayer.sqrMagnitude > 0.01f)
                {
                    enemyTransform.rotation = Quaternion.Slerp(
                        enemyTransform.rotation,
                        FaceDirection(directionToPlayer),
                        deltaTime * 2f);
                }
            }

            if (enemy.hasReachedPosition)
            {
                enemyTransform.position = enemy.destinationPosition;
                enemyTransform.rotation = enemy.destinationRotation;
                enemy.nextShot -= deltaTime;
                if (enemy.nextShot <= 0f)
                {
                    FireEnemyWeapon(enemy);
                    enemy.nextShot = Random.Range(enemyFireInterval * 0.65f, enemyFireInterval * 0.9f);
                }
            }

            if (enemy.elapsed >= 30f)
            {
                Destroy(enemy.gameObject);
                enemies.RemoveAt(index);
                ScheduleNextEncounter();
            }
        }
    }

    private void FirePlayerWeapon()
    {
        Enemy target = FindNearestEnemy();
        if (target == null)
        {
            return;
        }

        Vector3 origin = ship.transform.position + GetCameraForward() * 0.8f;
        Vector3 direction = (target.gameObject.transform.position - origin).normalized;
        CreateProjectile(origin, direction, playerProjectileSpeed, playerProjectileDamage, false, target);
        playerFireTimer = playerFireInterval;
        GameAudio.PlaySfx(GameAudio.Sounds.shot, 0.7f, Random.Range(1f, 1.1f));
    }

    private void FireEnemyWeapon(Enemy enemy)
    {
        Vector3 origin = enemy.gameObject.transform.position;
        Vector3 direction = (ship.transform.position - origin).normalized;
        CreateProjectile(origin, direction, enemyProjectileSpeed, enemyDamage, true, null);
        // Los disparos enemigos suenan más graves y bajos, para distinguirlos de los tuyos
        GameAudio.PlaySfx(GameAudio.Sounds.shot, 0.5f, Random.Range(0.7f, 0.8f));
    }

    private void CreateProjectile(
        Vector3 position,
        Vector3 direction,
        float speed,
        float damage,
        bool targetsPlayer,
        Enemy target)
    {
        GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectileObject.name = targetsPlayer ? "EnemyProjectile" : "PlayerProjectile";
        projectileObject.transform.position = position;
        projectileObject.transform.rotation = Quaternion.LookRotation(direction);
        projectileObject.transform.localScale = targetsPlayer
            ? new Vector3(0.32f, 0.32f, 0.8f)
            : new Vector3(0.2f, 0.2f, 0.65f);

        Collider projectileCollider = projectileObject.GetComponent<Collider>();
        if (projectileCollider != null)
        {
            projectileCollider.enabled = false;
        }

        Renderer projectileRenderer = projectileObject.GetComponent<Renderer>();
        Material projectileMaterial = targetsPlayer ? enemyProjectileMaterial : playerProjectileMaterial;
        if (projectileMaterial != null)
        {
            projectileRenderer.sharedMaterial = projectileMaterial;
        }

        TrailRenderer trail = projectileObject.AddComponent<TrailRenderer>();
        trail.time = targetsPlayer ? 0.42f : 0.28f;
        trail.startWidth = targetsPlayer ? 0.3f : 0.2f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.04f;
        trail.numCapVertices = 4;
        trail.numCornerVertices = 4;
        trail.alignment = LineAlignment.View;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.sharedMaterial = targetsPlayer ? enemyTrailMaterial : playerTrailMaterial;
        trail.colorGradient = CreateTrailGradient(
            targetsPlayer ? new Color(1f, 0.12f, 0.025f) : new Color(0.12f, 0.75f, 1f));

        projectiles.Add(new Projectile
        {
            gameObject = projectileObject,
            direction = direction,
            speed = speed,
            damage = damage,
            lifetime = 4f,
            targetsPlayer = targetsPlayer,
            target = target
        });
    }

    private void UpdateProjectiles(float deltaTime)
    {
        for (int index = projectiles.Count - 1; index >= 0; index--)
        {
            Projectile projectile = projectiles[index];
            if (projectile.gameObject == null)
            {
                projectiles.RemoveAt(index);
                continue;
            }

            projectile.lifetime -= deltaTime;
            projectile.gameObject.transform.position += projectile.direction * projectile.speed * deltaTime;

            bool hit = false;
            if (projectile.targetsPlayer)
            {
                hit = Vector3.Distance(projectile.gameObject.transform.position, ship.transform.position) <= 0.8f;
                if (hit && !ShipState.IsDestroyed)
                {
                    ship.TakeDamage(Mathf.CeilToInt(projectile.damage));
                }
            }
            else if (projectile.target != null && projectile.target.gameObject != null)
            {
                hit = Vector3.Distance(
                    projectile.gameObject.transform.position,
                    projectile.target.gameObject.transform.position) <= 1f;
                if (hit)
                {
                    DamageEnemy(projectile.target, projectile.damage);
                }
            }
            else
            {
                hit = true;
            }

            if (hit || projectile.lifetime <= 0f)
            {
                Destroy(projectile.gameObject);
                projectiles.RemoveAt(index);
            }
        }
    }

    private void DamageEnemy(Enemy enemy, float damage)
    {
        enemy.health -= Mathf.Max(1, Mathf.CeilToInt(damage));
        if (enemy.health > 0)
        {
            enemy.gameObject.transform.DOPunchScale(Vector3.one * 0.12f, 0.15f, 4)
                .SetLink(enemy.gameObject);
            return;
        }

        // Enemigo destruido: la explosión de la nave, más aguda y baja
        GameAudio.PlaySfx(GameAudio.Sounds.shipExplosion, 0.5f, 1.4f);
        enemies.Remove(enemy);
        Destroy(enemy.gameObject);
        ScheduleNextEncounter();
    }

    private Enemy FindNearestEnemy()
    {
        Enemy nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (Enemy enemy in enemies)
        {
            if (enemy.gameObject == null)
            {
                continue;
            }

            float distance = (enemy.gameObject.transform.position - ship.transform.position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearest = enemy;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private void ScheduleNextEncounter()
    {
        encounterTimer = Random.Range(encounterInterval.x, encounterInterval.y);
    }

    private int FindAvailableFormationSlot()
    {
        bool[] occupiedSlots = new bool[3];
        foreach (Enemy enemy in enemies)
        {
            if (enemy.gameObject != null)
            {
                occupiedSlots[enemy.formationSlot] = true;
            }
        }

        int[] preferredSlots = { 1, 0, 2 };
        foreach (int slot in preferredSlots)
        {
            if (!occupiedSlots[slot])
            {
                return slot;
            }
        }

        return preferredSlots[0];
    }

    private Vector3 GetCameraForward()
    {
        return gameCamera != null ? gameCamera.transform.forward : ship.transform.forward;
    }

    private Quaternion FaceDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.001f)
        {
            return Quaternion.identity;
        }

        Vector3 up = gameCamera != null ? gameCamera.transform.up : Vector3.up;
        return Quaternion.LookRotation(direction, up) * Quaternion.Euler(180f, 90f, 0f);
    }

    private Material CreateProjectileMaterial(string materialName, Color color, bool transparent)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            Debug.LogError($"No se encontró un shader sin iluminación para {materialName}.", this);
            return null;
        }

        Material material = new Material(shader)
        {
            name = materialName,
            color = color
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * (transparent ? 1.5f : 2f));
        }

        if (transparent && material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        return material;
    }

    private Gradient CreateTrailGradient(Color color)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(color, 0.2f),
                new GradientColorKey(color * 0.55f, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.35f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private void ShowWeaponHint()
    {
        if (hasShownWeaponHint)
        {
            return;
        }

        hasShownWeaponHint = true;
        GameObject canvasObject = new GameObject("WeaponUnlockedHint");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject textObject = new GameObject("HintText");
        textObject.transform.SetParent(canvas.transform, false);
        Text hintText = textObject.AddComponent<Text>();
        hintText.text = "ATAQUE HABILITADO  ·  CLIC IZQUIERDO PARA DISPARAR";
        hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hintText.fontSize = 22;
        hintText.fontStyle = FontStyle.Bold;
        hintText.color = new Color(0.55f, 0.9f, 1f);
        hintText.alignment = TextAnchor.MiddleCenter;
        hintText.raycastTarget = false;

        RectTransform rect = hintText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 100f);
        rect.sizeDelta = new Vector2(800f, 50f);

        weaponHintGroup = canvasObject.AddComponent<CanvasGroup>();
        weaponHintGroup.alpha = 0f;
        weaponHintGroup.DOFade(1f, 0.2f).SetLink(canvasObject);
        weaponHintGroup.DOFade(0f, 0.45f).SetDelay(4f).OnComplete(() =>
        {
            if (canvasObject != null)
            {
                Destroy(canvasObject);
            }
        }).SetLink(canvasObject);
    }

    private void ClearEncounter()
    {
        foreach (Enemy enemy in enemies)
        {
            if (enemy.gameObject != null)
            {
                Destroy(enemy.gameObject);
            }
        }
        enemies.Clear();

        foreach (Projectile projectile in projectiles)
        {
            if (projectile.gameObject != null)
            {
                Destroy(projectile.gameObject);
            }
        }
        projectiles.Clear();
    }

    private void OnDestroy()
    {
        ClearEncounter();
        if (playerProjectileMaterial != null)
        {
            Destroy(playerProjectileMaterial);
        }
        if (enemyProjectileMaterial != null)
        {
            Destroy(enemyProjectileMaterial);
        }
        if (playerTrailMaterial != null)
        {
            Destroy(playerTrailMaterial);
        }
        if (enemyTrailMaterial != null)
        {
            Destroy(enemyTrailMaterial);
        }
        if (weaponHintGroup != null)
        {
            weaponHintGroup.DOKill();
        }
    }
}
