using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class ShipMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float boostMultiplier = 1.8f;
    [SerializeField] private int maxHealth = 100;

    [Header("Visual")]
    [SerializeField] private float maxBankAngle = 25f;
    [SerializeField] private float bankSmoothTime = 0.15f;

    [Header("Screen bounds")]
    [SerializeField] private Camera movementCamera;
    [SerializeField, Range(0f, 0.25f)] private float screenPadding = 0.08f;

    [Header("Velocidad de viaje")]
    [Tooltip("1 = velocidad normal. Los fallos de la nave la podrán bajar (0.5 = mitad). Mueve la barra del viaje y el fuego")]
    [SerializeField, Range(0f, 2f)] private float speedMultiplier = 1f;

    [Header("Golpes")]
    [Tooltip("Segundos que la nave parpadea tras un golpe sin recibir más daño (para no encadenar choques)")]
    [SerializeField] private float invulnerableTime = 0.8f;

    [Header("Explosión (la crea Tools > Nave)")]
    [SerializeField] private ParticleSystem explosionParticles;
    [SerializeField] private float cameraShakeStrength = 0.4f;
    public UnityEvent onDestroyed;

    // Avisa a otros scripts (efectos de pantalla, icono del viaje...) de que la nave ha recibido un golpe
    public event System.Action Damaged;

    private Vector3 movementVelocity;
    private float currentBankAngle;
    private float bankVelocity;
    private Quaternion initialRotation;
    private GUIStyle healthTextStyle;
    private bool hasExploded;
    private Renderer[] shipRenderers;
    private float invulnerableTimer;
    private float damageFlashTimer;

    // Acciones definidas en Assets/InputSystem_Actions (mapa "Player")
    private InputAction moveAction;
    private InputAction sprintAction;

    // Para que otros scripts (como los fuegos del propulsor) sepan qué hace la nave
    public bool IsBoosting { get; private set; }

    // La vida vive en ShipState para que se mantenga al cambiar de escena
    public bool IsDestroyed => ShipState.IsDestroyed;

    public float SpeedMultiplier
    {
        get => speedMultiplier;
        set => speedMultiplier = Mathf.Max(0f, value);
    }

    // Velocidad de viaje actual: 0 si la nave está destruida y más alta al acelerar (Sprint)
    public float TravelSpeed => IsDestroyed ? 0f : speedMultiplier * (IsBoosting ? boostMultiplier : 1f);

    // Las averías los cambian (ShipFailureEffects): controles que no responden, daño extra por impacto
    // y motor calado durante un tirón (los fuegos se apagan)
    public bool ControlsJammed { get; set; }
    public float DamageMultiplier { get; set; } = 1f;
    public bool EngineStalled { get; set; }

    private void Awake()
    {
        initialRotation = transform.rotation;
        ShipState.MaxHealth = maxHealth;

        if (movementCamera == null)
        {
            movementCamera = Camera.main;
        }

        moveAction = InputSystem.actions.FindAction("Move");
        sprintAction = InputSystem.actions.FindAction("Sprint");

        // Las partes visibles de la nave (sin contar partículas), para hacerla parpadear al chocar
        shipRenderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(), shipRenderer => !(shipRenderer is ParticleSystemRenderer));
    }

    // Si se desactiva el control (por ejemplo durante las animaciones de cámara), deja de acelerar
    // y, si estaba parpadeando por un golpe, vuelve a verse
    private void OnDisable()
    {
        IsBoosting = false;

        if (invulnerableTimer > 0f && !hasExploded)
        {
            invulnerableTimer = 0f;
            foreach (Renderer shipRenderer in shipRenderers)
            {
                if (shipRenderer != null)
                {
                    shipRenderer.enabled = true;
                }
            }
        }
    }

    private void Update()
    {
        // La nave puede destruirse por un asteroide o por las averías (desde cualquier escena)
        if (IsDestroyed)
        {
            if (!hasExploded)
            {
                Explode();
            }

            return;
        }

        if (PauseMenu.IsPaused)
        {
            return;
        }

        UpdateInvulnerability();
        damageFlashTimer -= Time.deltaTime;

        // Con los controles atascados la nave no responde y sigue con la inercia que llevaba
        Vector2 moveInput = ControlsJammed ? Vector2.zero : moveAction.ReadValue<Vector2>();
        float horizontal = moveInput.x;
        float vertical = moveInput.y;

        IsBoosting = !ControlsJammed && sprintAction.IsPressed();
        float speed = moveSpeed * (IsBoosting ? boostMultiplier : 1f);
        float deltaTime = Time.deltaTime;

        Vector3 right = movementCamera != null ? movementCamera.transform.right : Vector3.right;
        Vector3 up = movementCamera != null ? movementCamera.transform.up : Vector3.up;
        Vector3 inputDirection = (right * horizontal + up * vertical).normalized;
        Vector3 targetVelocity = inputDirection * speed;

        movementVelocity = Vector3.MoveTowards(movementVelocity, targetVelocity, acceleration * deltaTime);
        transform.position += movementVelocity * deltaTime;

        currentBankAngle = Mathf.SmoothDamp(currentBankAngle, -horizontal * maxBankAngle, ref bankVelocity, bankSmoothTime);
        Vector3 bankAxis = movementCamera != null ? movementCamera.transform.forward : Vector3.forward;
        transform.rotation = Quaternion.AngleAxis(currentBankAngle, bankAxis) * initialRotation;

        KeepInsideScreen();
    }

    private void KeepInsideScreen()
    {
        if (movementCamera == null)
        {
            return;
        }

        Vector3 viewportPosition = movementCamera.WorldToViewportPoint(transform.position);
        if (viewportPosition.z <= 0f)
        {
            return;
        }

        viewportPosition.x = Mathf.Clamp(viewportPosition.x, screenPadding, 1f - screenPadding);
        viewportPosition.y = Mathf.Clamp(viewportPosition.y, screenPadding, 1f - screenPadding);
        transform.position = movementCamera.ViewportToWorldPoint(viewportPosition);
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || IsDestroyed || invulnerableTimer > 0f)
        {
            return;
        }

        ShipState.Damage(damage * DamageMultiplier);
        Debug.LogWarning($"¡Impacto! Vida de la nave: {Mathf.CeilToInt(ShipState.Health)}/{maxHealth}", this);

        invulnerableTimer = invulnerableTime;
        damageFlashTimer = 0.5f;

        // La nave da un salto (relativo a su tamaño) y avisa a los demás efectos
        transform.DOComplete();
        transform.DOPunchScale(transform.localScale * 0.25f, 0.3f, 10).SetLink(gameObject);
        Damaged?.Invoke();

        // Cuanto más daño hace el golpe (casco debilitado), más tiembla la cámara
        if (movementCamera != null && !IsDestroyed)
        {
            movementCamera.transform.DOComplete();
            movementCamera.transform.DOShakePosition(0.3f, cameraShakeStrength * 0.6f * DamageMultiplier, 25).SetLink(movementCamera.gameObject);
        }
    }

    // Tras un golpe la nave parpadea un momento y no recibe más daño
    private void UpdateInvulnerability()
    {
        if (invulnerableTimer <= 0f)
        {
            return;
        }

        invulnerableTimer -= Time.deltaTime;
        bool visible = invulnerableTimer <= 0f || Mathf.Repeat(invulnerableTimer, 0.12f) > 0.06f;
        foreach (Renderer shipRenderer in shipRenderers)
        {
            if (shipRenderer != null)
            {
                shipRenderer.enabled = visible;
            }
        }
    }

    // Partículas de explosión, la nave desaparece y la cámara tiembla
    private void Explode()
    {
        hasExploded = true;
        Debug.LogWarning("La nave se quedó sin vida.", this);

        if (explosionParticles != null)
        {
            explosionParticles.Play(true);
        }

        foreach (Renderer shipRenderer in GetComponentsInChildren<Renderer>())
        {
            if (!(shipRenderer is ParticleSystemRenderer))
            {
                shipRenderer.enabled = false;
            }
        }

        if (movementCamera != null)
        {
            movementCamera.transform.DOShakePosition(0.6f, cameraShakeStrength, 20).SetLink(movementCamera.gameObject);
        }

        onDestroyed.Invoke();
    }

    private void OnGUI()
    {
        if (healthTextStyle == null)
        {
            healthTextStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        int currentHealth = Mathf.CeilToInt(ShipState.Health);
        float healthRatio = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
        Rect barRect = new Rect(16f, 16f, 220f, 28f);
        Color previousColor = GUI.color;

        // Al recibir un golpe el fondo de la barra parpadea en blanco
        bool flashing = damageFlashTimer > 0f && Mathf.Repeat(damageFlashTimer, 0.1f) > 0.05f;
        GUI.color = flashing ? Color.white : new Color(0.08f, 0.1f, 0.12f, 0.85f);
        GUI.Box(barRect, GUIContent.none);

        // Con poca vida, el relleno late en rojo
        Color fillColor = Color.Lerp(new Color(0.8f, 0.16f, 0.12f), new Color(0.2f, 0.72f, 0.32f), healthRatio);
        if (healthRatio < 0.3f)
        {
            fillColor = Color.Lerp(fillColor, new Color(0.35f, 0f, 0f), Mathf.PingPong(Time.time * 3f, 1f));
        }

        GUI.color = fillColor;
        GUI.Box(new Rect(19f, 19f, 214f * healthRatio, 22f), GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(barRect, $"VIDA {currentHealth}/{maxHealth}", healthTextStyle);
        GUI.color = previousColor;
    }
}
