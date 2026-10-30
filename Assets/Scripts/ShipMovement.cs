using UnityEngine;
using UnityEngine.InputSystem;

public class ShipMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float boostMultiplier = 1.8f;
    [SerializeField] private int maxHealth = 100;

    [Header("Dodge")]
    [SerializeField, Min(0.05f)] private float doubleTapWindow = 0.3f;
    [SerializeField, Min(0.1f)] private float dodgeSpeed = 24f;
    [SerializeField, Min(0.05f)] private float dodgeDuration = 0.18f;

    private int currentHealth;

    [Header("Visual")]
    [SerializeField] private float maxBankAngle = 25f;
    [SerializeField] private float bankSmoothTime = 0.15f;

    [Header("Screen bounds")]
    [SerializeField] private Camera movementCamera;
    [SerializeField, Range(0f, 0.25f)] private float screenPadding = 0.08f;

    private Vector3 movementVelocity;
    private float currentBankAngle;
    private float bankVelocity;
    private Quaternion initialRotation;
    private GUIStyle healthTextStyle;
    private bool isDestroyed;
    private float lastLeftTapTime = -1f;
    private float lastRightTapTime = -1f;
    private float dodgeTimeRemaining;
    private int dodgeDirection;
    private float previousHorizontal;

    // Acciones definidas en Assets/InputSystem_Actions (mapa "Player")
    private InputAction moveAction;
    private InputAction sprintAction;

    // Para que otros scripts (como los fuegos del propulsor) sepan qué hace la nave
    public bool IsBoosting { get; private set; }
    public bool IsDodging => dodgeTimeRemaining > 0f;
    public bool IsDestroyed => isDestroyed;

    private void Awake()
    {
        initialRotation = transform.rotation;
        currentHealth = maxHealth;

        if (movementCamera == null)
        {
            movementCamera = Camera.main;
        }

        moveAction = InputSystem.actions.FindAction("Move");
        sprintAction = InputSystem.actions.FindAction("Sprint");
    }

    private void Update()
    {
        if (isDestroyed)
        {
            return;
        }

        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        float horizontal = moveInput.x;
        float vertical = moveInput.y;

        // "Pulsado este frame" = la entrada horizontal acaba de cruzar la mitad hacia un lado
        bool leftPressed = horizontal < -0.5f && previousHorizontal >= -0.5f;
        bool rightPressed = horizontal > 0.5f && previousHorizontal <= 0.5f;
        previousHorizontal = horizontal;
        CheckForDoubleTap(leftPressed, ref lastLeftTapTime, -1);
        CheckForDoubleTap(rightPressed, ref lastRightTapTime, 1);

        IsBoosting = sprintAction.IsPressed();
        float speed = moveSpeed * (IsBoosting ? boostMultiplier : 1f);
        float deltaTime = Time.deltaTime;

        Vector3 right = movementCamera != null ? movementCamera.transform.right : Vector3.right;
        Vector3 up = movementCamera != null ? movementCamera.transform.up : Vector3.up;
        Vector3 inputDirection = (right * horizontal + up * vertical).normalized;
        Vector3 targetVelocity = inputDirection * speed;

        movementVelocity = Vector3.MoveTowards(movementVelocity, targetVelocity, acceleration * deltaTime);
        Vector3 frameMovement = movementVelocity * deltaTime;
        if (dodgeTimeRemaining > 0f)
        {
            float dodgeStep = Mathf.Min(deltaTime, dodgeTimeRemaining);
            frameMovement += right * dodgeDirection * dodgeSpeed * dodgeStep;
            dodgeTimeRemaining -= deltaTime;
        }

        transform.position += frameMovement;

        currentBankAngle = Mathf.SmoothDamp(currentBankAngle, -horizontal * maxBankAngle, ref bankVelocity, bankSmoothTime);
        Vector3 bankAxis = movementCamera != null ? movementCamera.transform.forward : Vector3.forward;
        transform.rotation = Quaternion.AngleAxis(currentBankAngle, bankAxis) * initialRotation;

        KeepInsideScreen();
    }

    private void CheckForDoubleTap(bool pressed, ref float lastTapTime, int direction)
    {
        if (!pressed)
        {
            return;
        }

        if (Time.time - lastTapTime <= doubleTapWindow)
        {
            dodgeDirection = direction;
            dodgeTimeRemaining = dodgeDuration;
            lastTapTime = -1f;
            return;
        }

        lastTapTime = Time.time;
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
        if (damage <= 0 || currentHealth <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - damage);
        Debug.LogWarning($"¡Impacto! Vida de la nave: {currentHealth}/{maxHealth}", this);

        if (currentHealth == 0)
        {
            Debug.LogWarning("La nave se quedó sin vida.", this);
            isDestroyed = true;
        }
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

        float healthRatio = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
        Rect barRect = new Rect(16f, 16f, 220f, 28f);
        Color previousColor = GUI.color;

        GUI.color = new Color(0.08f, 0.1f, 0.12f, 0.85f);
        GUI.Box(barRect, GUIContent.none);
        GUI.color = Color.Lerp(new Color(0.8f, 0.16f, 0.12f), new Color(0.2f, 0.72f, 0.32f), healthRatio);
        GUI.Box(new Rect(19f, 19f, 214f * healthRatio, 22f), GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(barRect, $"VIDA {currentHealth}/{maxHealth}", healthTextStyle);
        GUI.color = previousColor;
    }
}
