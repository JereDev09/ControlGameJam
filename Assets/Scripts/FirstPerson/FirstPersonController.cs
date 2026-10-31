using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

// Movimiento en primera persona: andar, correr, saltar y mirar con el ratón.
// Va en el objeto del jugador, con la cámara como hijo.
// Al empezar la escena la cámara sube un poco, como al levantarse del asiento del piloto.
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] private float maxLookAngle = 85f;

    [Header("Al entrar en la escena")]
    [Tooltip("Cuánto más abajo empieza la cámara (0 = sin animación)")]
    [SerializeField] private float standUpHeight = 0.5f;
    [SerializeField] private float standUpDuration = 1.2f;

    private CharacterController characterController;
    private float verticalVelocity;
    private float cameraPitch;

    // Acciones definidas en Assets/InputSystem_Actions (mapa "Player")
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (cameraTransform == null)
        {
            cameraTransform = GetComponentInChildren<Camera>().transform;
        }

        moveAction = InputSystem.actions.FindAction("Move");
        lookAction = InputSystem.actions.FindAction("Look");
        jumpAction = InputSystem.actions.FindAction("Jump");
        sprintAction = InputSystem.actions.FindAction("Sprint");
    }

    // La cámara sube suavemente hasta su altura mientras se aclara el fundido de entrada
    private void Start()
    {
        Vector3 cameraPosition = cameraTransform.localPosition;
        cameraTransform.localPosition = cameraPosition + Vector3.down * standUpHeight;
        cameraTransform.DOLocalMove(cameraPosition, standUpDuration).SetEase(Ease.OutCubic).SetLink(gameObject);
    }

    private void OnEnable()
    {
        // Esconde el ratón y lo deja fijo en el centro de la pantalla
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (PauseMenu.IsPaused)
        {
            return;
        }

        Look();
        Move();
    }

    private void Look()
    {
        Vector2 lookInput = lookAction.ReadValue<Vector2>() * lookSensitivity;

        // Izquierda/derecha gira todo el cuerpo
        transform.Rotate(Vector3.up, lookInput.x);

        // Arriba/abajo gira solo la cámara, con un límite para no dar la vuelta
        cameraPitch = Mathf.Clamp(cameraPitch - lookInput.y, -maxLookAngle, maxLookAngle);
        cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void Move()
    {
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        float speed = sprintAction.IsPressed() ? sprintSpeed : walkSpeed;
        Vector3 velocity = (transform.right * moveInput.x + transform.forward * moveInput.y) * speed;

        if (characterController.isGrounded)
        {
            // Un poco de fuerza hacia abajo para que se quede pegado al suelo
            if (verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (jumpAction.WasPressedThisFrame())
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        verticalVelocity += gravity * Time.deltaTime;
        velocity.y = verticalVelocity;

        characterController.Move(velocity * Time.deltaTime);
    }
}
