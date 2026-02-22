using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class SpiderMovementController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 120f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float deceleration = 15f;

    [Header("Camera Settings")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private float touchSensitivity = 0.2f;
    [SerializeField] private float cameraDistance = 8f;
    [SerializeField] private float cameraHeight = 3f;
    [SerializeField] private float cameraMinHeight = 1f;
    [SerializeField] private float cameraMaxHeight = 8f;
    [SerializeField] private float cameraSmoothSpeed = 10f;

    [Header("Input Settings")]
    [SerializeField] private bool invertTouchY = false;
    [SerializeField] private float runSpeedMultiplier = 2f;

    [Header("Mobile Joystick (Movement Only)")]
    [SerializeField] private bool useJoystick = true;
    [SerializeField] private VirtualJoystick movementJoystick;

    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform spiderBody;

    // Movement state
    private Vector3 currentVelocity;
    private Vector3 targetVelocity;

    // Camera state
    private float cameraRotationX;
    private float cameraRotationY;
    private Vector3 cameraVelocity;

    // Input state
    private float horizontal;
    private float vertical;
    private bool isRunning;

    // Touch camera drag state
    private int cameraTouchId = -1;
    private Vector2 lastTouchPosition;

    // New Input System — mouse delta for editor
    private Vector2 mouseDelta;
    private bool rightMouseHeld;

    // Animation helpers
    [HideInInspector] public float currentSpeed;
    [HideInInspector] public bool isMoving;

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    void Start()
    {
        InitializeCamera();

#if UNITY_STANDALONE || UNITY_EDITOR
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
#endif
    }

    void Update()
    {
        ReadMovementInput();
        ReadCameraInput();

        HandleMovement();
        HandleRotation();
        HandleCamera();
    }

    // -------------------------------------------------------
    // INPUT
    // -------------------------------------------------------

    void ReadMovementInput()
    {
        if (useJoystick && movementJoystick != null)
        {
            Vector2 input = movementJoystick.Direction;
            horizontal = input.x;
            vertical = input.y;
            isRunning = false;
        }
        else
        {
            // New Input System keyboard
            var kb = Keyboard.current;
            if (kb == null) return;

            horizontal = 0f;
            vertical = 0f;

            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) horizontal -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) horizontal += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) vertical -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) vertical += 1f;

            isRunning = kb.leftShiftKey.isPressed;
        }
    }

    void ReadCameraInput()
    {
        float lookX = 0f;
        float lookY = 0f;

#if UNITY_EDITOR || UNITY_STANDALONE
        // Right-mouse drag in editor / standalone via new Input System
        var mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            lookX = delta.x * touchSensitivity;
            lookY = delta.y * touchSensitivity;
        }
#else
        // Mobile — finger drag on the RIGHT half of the screen
        foreach (Touch touch in Touch.activeTouches)
        {
            // Skip fingers on the left half (joystick side)
            if (touch.screenPosition.x < Screen.width * 0.5f)
                continue;

            if (touch.phase == TouchPhase.Began)
            {
                cameraTouchId     = touch.touchId;
                lastTouchPosition = touch.screenPosition;
            }
            else if (touch.phase == TouchPhase.Moved && touch.touchId == cameraTouchId)
            {
                Vector2 delta = touch.screenPosition - lastTouchPosition;
                lookX             =  delta.x * touchSensitivity;
                lookY             =  delta.y * touchSensitivity;
                lastTouchPosition = touch.screenPosition;
            }
            else if ((touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                     && touch.touchId == cameraTouchId)
            {
                cameraTouchId = -1;
            }
        }
#endif

        if (invertTouchY) lookY = -lookY;

        cameraRotationY += lookX;
        cameraRotationX -= lookY;
        cameraRotationX = Mathf.Clamp(cameraRotationX, -30f, 60f);
    }

    // -------------------------------------------------------
    // MOVEMENT
    // -------------------------------------------------------

    void HandleMovement()
    {
        Vector3 cameraForward = Vector3.ProjectOnPlane(
            playerCamera.transform.forward, Vector3.up).normalized;

        Vector3 cameraRight = Vector3.ProjectOnPlane(
            playerCamera.transform.right, Vector3.up).normalized;

        Vector3 moveDirection =
            (cameraForward * vertical + cameraRight * horizontal).normalized;

        float speed = moveSpeed;
        if (isRunning) speed *= runSpeedMultiplier;

        targetVelocity = moveDirection * speed;

        float accel = targetVelocity.magnitude > currentVelocity.magnitude
            ? acceleration : deceleration;

        currentVelocity = Vector3.MoveTowards(
            currentVelocity, targetVelocity, accel * Time.deltaTime);

        if (currentVelocity.magnitude > 0.01f)
        {
            transform.Translate(currentVelocity * Time.deltaTime, Space.World);
            isMoving = true;
        }
        else
        {
            isMoving = false;
        }

        currentSpeed = currentVelocity.magnitude;
    }

    // -------------------------------------------------------
    // ROTATION
    // -------------------------------------------------------

    void HandleRotation()
    {
        if (currentVelocity.magnitude < 0.1f) return;

        Vector3 direction = currentVelocity.normalized;
        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float angle = Mathf.DeltaAngle(spiderBody.eulerAngles.y, targetAngle);
        float rotationStep = rotationSpeed * Time.deltaTime;
        float smoothAngle = Mathf.Clamp(angle, -rotationStep, rotationStep);

        spiderBody.Rotate(Vector3.up * smoothAngle);
    }

    // -------------------------------------------------------
    // CAMERA
    // -------------------------------------------------------

    void HandleCamera()
    {
        Vector3 desiredPos = CalculateCameraPosition();

        playerCamera.transform.position = Vector3.SmoothDamp(
            playerCamera.transform.position,
            desiredPos,
            ref cameraVelocity,
            1f / cameraSmoothSpeed);

        Quaternion targetRot = Quaternion.Euler(cameraRotationX, cameraRotationY, 0f);
        playerCamera.transform.rotation = Quaternion.Slerp(
            playerCamera.transform.rotation,
            targetRot,
            cameraSmoothSpeed * Time.deltaTime);
    }

    Vector3 CalculateCameraPosition()
    {
        Vector3 basePos = cameraTarget.position;

        Vector3 offset =
            Quaternion.Euler(cameraRotationX, cameraRotationY, 0f) *
            Vector3.back * cameraDistance;

        offset += Vector3.up * Mathf.Clamp(cameraHeight, cameraMinHeight, cameraMaxHeight);

        Vector3 desiredPos = basePos + offset;

        if (Physics.Raycast(
            basePos,
            (desiredPos - basePos).normalized,
            out RaycastHit hit,
            Vector3.Distance(basePos, desiredPos)))
        {
            desiredPos = hit.point - (desiredPos - basePos).normalized * 0.4f;
        }

        return desiredPos;
    }

    // -------------------------------------------------------
    // SETUP
    // -------------------------------------------------------

    void InitializeCamera()
    {
        if (!playerCamera) playerCamera = Camera.main;
        if (!cameraTarget) cameraTarget = transform;
        if (!spiderBody) spiderBody = transform;

        cameraRotationY = transform.eulerAngles.y;
        cameraRotationX = 15f;
    }

    // -------------------------------------------------------
    // PUBLIC API
    // -------------------------------------------------------

    public float GetMovementSpeed() => currentSpeed;
    public bool IsMoving() => isMoving;
    public Vector3 GetMovementDirection() => currentVelocity.normalized;

    public void SetCameraDistance(float distance)
        => cameraDistance = Mathf.Clamp(distance, 2f, 15f);

    public void SetCameraHeight(float height)
        => cameraHeight = Mathf.Clamp(height, cameraMinHeight, cameraMaxHeight);
}