using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    private const float GroundedGravity = -2f;

    [Header("Movement")]
    [SerializeField] private bool canMove = true;
    [SerializeField, Min(0f)] private float walkMovementSpeed = 4f;
    [SerializeField, Min(0.1f)] private float standingHeight = 2f;

    private Vector2 _moveInput;
    private Vector3 _moveDirection;
    private float _currentSpeed;

    [Header("Sprint")]
    [SerializeField] private bool canSprint = true;
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
    [SerializeField, Min(0f)] private float sprintMovementSpeed = 10f;

    [Header("Crouch")]
    [SerializeField] private bool canCrouch = true;
    [SerializeField] private KeyCode crouchKey = KeyCode.C;
    [SerializeField, Min(0f)] private float crouchMovementSpeed = 2.5f;
    [SerializeField, Min(0.1f)] private float crouchHeight = 1f;
    [SerializeField] private float crouchCenterY = -0.5f;
    [SerializeField, Min(0.01f)] private float crouchTransitionSpeed = 20f;

    private bool _isCrouching;

    [Header("Jump")]
    [SerializeField] private bool canJump = true;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField, Min(0f)] private float jumpForce = 4f;
    [SerializeField, Min(0f)] private float gravity = 10f;

    private bool _isGrounded;

    [Header("Camera")]
    [SerializeField] private bool canRotateCamera = true;
    [SerializeField, Range(30f, 120f)] private float fieldOfView = 60f;
    [SerializeField, Min(0f)] private float mouseSensitivity = 2f;
    
    [Tooltip("Changes the smoothness of camera rotation. The higher the value, the greater the sharpness. The recommended value is 100.")]
    [SerializeField, Min(0f)] private float snappiness = 100f;

    [SerializeField] private Transform playerCameraTransform;
    private Camera playerCamera;

    [SerializeField] private float standingCameraHeight = 0.5f;
    [SerializeField] private float crouchingCameraHeight = -0.5f;
    

    private float _currentCameraHeight;
    private float _rotationX;
    private float _rotationY;
    private float _cameraRotationX;
    private float _cameraRotationY;
    

    [Header("Ceiling Check")]
    [SerializeField, Min(0f)] private float ceilingCheckRadius = 0.3f;
    [SerializeField] private Transform ceilingCheckTransform;
    [SerializeField] private LayerMask ceilingObstacleMask;

    private bool _hasObstacleAbove;

    [Header("Head Bob")]
    [SerializeField] private bool enableHeadBob = true;
    [SerializeField, Min(0f)] private float bobSpeed = 10f;
    [SerializeField, Min(0f)] private float bobAmount = 0.05f;

    private float _bobTimer;
    private float _headBobOffset;

    private CharacterController _characterController;

    public bool IsGrounded => _isGrounded;
    public bool IsCrouching => _isCrouching;
    public float CurrentSpeed => _currentSpeed;
    public bool CanMove => canMove;
    public bool CanRotateCamera => canRotateCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        _characterController = GetComponent<CharacterController>();

        SetupCharacterController();
        SetupCamera();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        UpdateGroundedState();
        UpdateCeilingCheck();

        if (canMove)
            HandleMovement();
        else
            ApplyGravity();

        if (canRotateCamera)
            HandleCamera();
        

        UpdateCameraHeight();
        HandleHeadBob();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        standingHeight = Mathf.Max(standingHeight, 0.1f);
        crouchHeight = Mathf.Clamp(crouchHeight, 0.1f, standingHeight);

        walkMovementSpeed = Mathf.Max(walkMovementSpeed, 0f);
        sprintMovementSpeed = Mathf.Max(sprintMovementSpeed, 0f);
        crouchMovementSpeed = Mathf.Max(crouchMovementSpeed, 0f);

        jumpForce = Mathf.Max(jumpForce, 0f);
        gravity = Mathf.Max(gravity, 0f);

        mouseSensitivity = Mathf.Max(mouseSensitivity, 0f);
        snappiness = Mathf.Max(snappiness, 0f);
        

        ceilingCheckRadius = Mathf.Max(ceilingCheckRadius, 0f);

        bobSpeed = Mathf.Max(bobSpeed, 0f);
        bobAmount = Mathf.Max(bobAmount, 0f);

        crouchTransitionSpeed = Mathf.Max(crouchTransitionSpeed, 0.01f);

        if (playerCamera == null && playerCameraTransform != null)
            playerCamera = playerCameraTransform.GetComponentInChildren<Camera>();

        if (playerCamera != null)
            playerCamera.fieldOfView = fieldOfView;
    }

    private void SetupCharacterController()
    {
        _characterController.height = standingHeight;
        _characterController.center = Vector3.zero;

        if (ceilingCheckTransform == null)
        {
            Debug.LogError("No ceiling check transform assigned. Please assign it in Inspector.");
        }
    }

    private void SetupCamera()
    {
        playerCamera = playerCameraTransform.GetComponentInChildren<Camera>();
        if (playerCamera == null)
        {
            Debug.LogError("Player camera not found in Player Camera Transform Children. Please add Main Camera to Children of Player Camera Transform.");
        }
        if (playerCameraTransform == null)
        {
            Debug.LogError(
                $"{nameof(PlayerController)}: Player Camera Transform is not assigned. Please assign it in Inspector.",
                this
            );

            return;
        }
        
        
        
        playerCamera.fieldOfView = fieldOfView;

        _currentCameraHeight = standingCameraHeight;

        playerCameraTransform.localPosition = new Vector3(
            0f,
            _currentCameraHeight,
            0f
        );
    }

    private void UpdateGroundedState()
    {
        _isGrounded = _characterController.isGrounded;
    }

    private void UpdateCeilingCheck()
    {
        if (ceilingCheckTransform == null)
        {
            _hasObstacleAbove = false;
            return;
        }

        _hasObstacleAbove = Physics.CheckSphere(
            ceilingCheckTransform.position,
            ceilingCheckRadius,
            ceilingObstacleMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private void HandleMovement()
    {
        _moveInput.x = Input.GetAxisRaw("Horizontal");
        _moveInput.y = Input.GetAxisRaw("Vertical");

        _moveInput = Vector2.ClampMagnitude(_moveInput, 1f);

        _currentSpeed = walkMovementSpeed;

        HandleCrouch();
        HandleSprint();
        HandleJump();

        Vector3 direction = new Vector3(
            _moveInput.x,
            0f,
            _moveInput.y
        );

        Vector3 moveVector = transform.TransformDirection(direction);

        moveVector = Vector3.ClampMagnitude(
            moveVector,
            1f
        );

        _moveDirection.x = moveVector.x * _currentSpeed;
        _moveDirection.z = moveVector.z * _currentSpeed;

        ApplyGravity();

        _characterController.Move(
            _moveDirection * Time.deltaTime
        );
    }
    

    private void ApplyGravity()
    {
        if (_isGrounded && _moveDirection.y < 0f)
            _moveDirection.y = GroundedGravity;

        _moveDirection.y -= gravity * Time.deltaTime;
    }

    private void HandleSprint()
    {
        if (!_isGrounded)
            return;

        if (!_isCrouching && canSprint && Input.GetKey(sprintKey))
            _currentSpeed = sprintMovementSpeed;
    }

    private void HandleCrouch()
    {
        if (!canCrouch)
        {
            _isCrouching = false;
            return;
        }

        bool crouchPressed = Input.GetKey(crouchKey);

        if (crouchPressed)
            _isCrouching = true;
        else if (!crouchPressed && !_hasObstacleAbove)
            _isCrouching = false;

        if (_isCrouching)
            _currentSpeed = crouchMovementSpeed;

        float targetHeight = _isCrouching
            ? crouchHeight
            : standingHeight;

        Vector3 targetCenter = _isCrouching
            ? new Vector3(0f, crouchCenterY, 0f)
            : Vector3.zero;

        _characterController.height = Mathf.Lerp(
            _characterController.height,
            targetHeight,
            crouchTransitionSpeed * Time.deltaTime
        );

        _characterController.center = Vector3.Lerp(
            _characterController.center,
            targetCenter,
            crouchTransitionSpeed * Time.deltaTime
        );
    }

    private void HandleJump()
    {
        if (!canJump)
            return;

        if (!_isGrounded)
            return;
        

        if (Input.GetKeyDown(jumpKey))
            _moveDirection.y = jumpForce;
    }

    private void HandleCamera()
    {
        if (playerCamera == null || playerCameraTransform == null)
        {
            return;
        }

        playerCamera.fieldOfView = fieldOfView;

        float mouseX =
            Input.GetAxis("Mouse X") *
            100f *
            mouseSensitivity *
            Time.deltaTime;

        float mouseY =
            Input.GetAxis("Mouse Y") *
            100f *
            mouseSensitivity *
            Time.deltaTime;

        _rotationX += mouseX;
        _rotationY -= mouseY;

        _rotationY = Mathf.Clamp(
            _rotationY,
            -90f,
            90f
        );

        float smoothSpeed = snappiness * Time.deltaTime;

        _cameraRotationX = Mathf.Lerp(
            _cameraRotationX,
            _rotationX,
            smoothSpeed
        );

        _cameraRotationY = Mathf.Lerp(
            _cameraRotationY,
            _rotationY,
            smoothSpeed
        );

        transform.rotation = Quaternion.Euler(
            0f,
            _cameraRotationX,
            0f
        );

        playerCameraTransform.localRotation = Quaternion.Euler(
            _cameraRotationY,
            0f,
            0f
        );
    }

    private void UpdateCameraHeight()
    {
        if (playerCameraTransform == null)
            return;

        float targetHeight = _isCrouching
            ? crouchingCameraHeight
            : standingCameraHeight;

        _currentCameraHeight = Mathf.Lerp(
            _currentCameraHeight,
            targetHeight,
            crouchTransitionSpeed * Time.deltaTime
        );
    }

    private void HandleHeadBob()
    {
        if (playerCameraTransform == null)
            return;

        Vector3 targetPosition = new Vector3(
            0f,
            _currentCameraHeight,
            0f
        );

        if (!enableHeadBob)
        {
            _bobTimer = 0f;
            _headBobOffset = 0f;

            playerCameraTransform.localPosition = Vector3.Lerp(
                playerCameraTransform.localPosition,
                targetPosition,
                crouchTransitionSpeed * Time.deltaTime
            );

            return;
        }

        if (!_isGrounded)
        {
            _bobTimer = 0f;
            _headBobOffset = 0f;
        }
        else
        {
            Vector3 horizontalVelocity =
                _characterController.velocity;

            horizontalVelocity.y = 0f;

            if (horizontalVelocity.magnitude > 0.1f)
            {
                float speedMultiplier =
                    Mathf.Max(
                        _currentSpeed / Mathf.Max(walkMovementSpeed, 0.01f),
                        0.1f
                    );

                _bobTimer +=
                    Time.deltaTime *
                    bobSpeed *
                    speedMultiplier;

                _headBobOffset =
                    Mathf.Sin(_bobTimer) * bobAmount;
            }
            else
            {
                _bobTimer = 0f;

                _headBobOffset = Mathf.Lerp(
                    _headBobOffset,
                    0f,
                    crouchTransitionSpeed * Time.deltaTime
                );
            }
        }

        targetPosition.y += _headBobOffset;

        playerCameraTransform.localPosition = Vector3.Lerp(
            playerCameraTransform.localPosition,
            targetPosition,
            crouchTransitionSpeed * Time.deltaTime
        );
    }
    
    ///<summary>
    /// Provides methods to enable or disable character movement capabilities and controls.
    /// </summary>
    

    public void SetMovementEnabled(bool enabled)
    {
        canMove = enabled;

        if (!enabled)
        {
            _moveInput = Vector2.zero;
            _moveDirection.x = 0f;
            _moveDirection.z = 0f;
        }
    }

    public void SetCameraRotationEnabled(bool enabled)
    {
        canRotateCamera = enabled;
    }

    public void SetSprintEnabled(bool enabled)
    {
        canSprint = enabled;
    }

    public void SetCrouchEnabled(bool enabled)
    {
        canCrouch = enabled;
    }

    public void SetJumpEnabled(bool enabled)
    {
        canJump = enabled;
    }
    
    
    
    ///<summary>
    /// Gizmos Settings and nothing more >_<
    /// </summary>
    private void OnDrawGizmos()
    {
        if (ceilingCheckTransform == null)
            return;
        
        
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            ceilingCheckTransform.position,
            ceilingCheckRadius
        );
    }
}