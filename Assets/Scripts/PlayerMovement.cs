using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float sprintingSpeed = 12f;
    [SerializeField] private float crouchSpeed = 3f;
    [SerializeField] private float crouchTransitionDuration = 0.5f;
    [SerializeField] private float groundDrag = 5f;
    [SerializeField] private float jumpForce = 7f;

    [Header("Ground Check")]
    [SerializeField] private float playerHeight = 2f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask ground = 1 << 3;

    [Header("References")]
    [SerializeField] private Transform orientation;
    [SerializeField] private Transform cameraPos;

    private Rigidbody rb;
    private Collider[] cachedColliders;
    private CapsuleCollider playerCollider;
    private Transform playerVisual;
    private Vector2 moveInput;
    private bool grounded;
    private bool jumpQueued;
    private bool sprinting;
    private bool crouching;
    private float crouchBlend;
    private float crouchBlendVelocity;
    private Vector3 standingCameraLocalPosition;
    private Vector3 standingVisualLocalScale;
    private Vector3 standingVisualLocalPosition;
    private Vector3 standingColliderCenter;
    private float standingColliderHeight;
    private float standingEyeHeightFromFeet;
    private float crouchingEyeHeightFromFeet;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError("PlayerMovement requires a Rigidbody.");
            enabled = false;
            return;
        }

        rb.freezeRotation = true;
        cachedColliders = GetComponentsInChildren<Collider>();

        if (orientation == null)
        {
            Transform orientationChild = transform.Find("Orientation");
            orientation = orientationChild != null ? orientationChild : transform;
        }

        if (cameraPos == null)
        {
            cameraPos = transform.Find("CameraPos");
        }

        if (cameraPos != null)
        {
            standingCameraLocalPosition = cameraPos.localPosition;
        }

        playerCollider = GetComponentInChildren<CapsuleCollider>();
        if (playerCollider != null)
        {
            playerVisual = playerCollider.transform;
            standingColliderHeight = playerCollider.height;
            standingColliderCenter = playerCollider.center;
            standingEyeHeightFromFeet = standingCameraLocalPosition.y - StandingFeetLocalY();
            crouchingEyeHeightFromFeet = standingEyeHeightFromFeet * 0.5f;

            if (playerVisual != null)
            {
                standingVisualLocalScale = playerVisual.localScale;
                standingVisualLocalPosition = playerVisual.localPosition;
            }
        }
    }

    private void Update()
    {
        grounded = IsGrounded();
        crouching = IsCrouchHeld();
        sprinting = grounded && !crouching && IsSprintHeld();
        SetDrag(grounded ? groundDrag : 0f);

        ProcessInput();
        UpdateCrouchState(Time.deltaTime);
        SpeedControl();
    }

    private void FixedUpdate()
    {
        ApplyJump();
        MovePlayer();
    }

    private void ProcessInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            float horizontal = 0f;
            float vertical = 0f;

            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                horizontal -= 1f;
            }

            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                horizontal += 1f;
            }

            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            {
                vertical -= 1f;
            }

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            {
                vertical += 1f;
            }

            moveInput = new Vector2(horizontal, vertical);
            if (Keyboard.current.spaceKey.wasPressedThisFrame && grounded)
            {
                jumpQueued = true;
            }
            return;
        }
#endif

        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");
        if (Input.GetKeyDown(KeyCode.Space) && grounded)
        {
            jumpQueued = true;
        }
    }

    private void ApplyJump()
    {
        if (!jumpQueued)
        {
            return;
        }

        jumpQueued = false;

        Vector3 velocity = GetVelocity();
        SetVelocity(new Vector3(velocity.x, 0f, velocity.z));
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private void MovePlayer()
    {
        Transform moveReference = orientation != null ? orientation : transform;

        Vector3 forward = moveReference.forward;
        Vector3 right = moveReference.right;
        forward.y = 0f;
        right.y = 0f;

        Vector3 moveDirection = forward.normalized * moveInput.y + right.normalized * moveInput.x;
        rb.AddForce(moveDirection.normalized * CurrentMoveSpeed() * 10f, ForceMode.Force);
    }

    private void SpeedControl()
    {
        Vector3 velocity = GetVelocity();
        Vector3 flatVelocity = new Vector3(velocity.x, 0f, velocity.z);
        float currentMoveSpeed = CurrentMoveSpeed();

        if (flatVelocity.magnitude <= currentMoveSpeed)
        {
            return;
        }

        Vector3 limitedVelocity = flatVelocity.normalized * currentMoveSpeed;
        SetVelocity(new Vector3(limitedVelocity.x, velocity.y, limitedVelocity.z));
    }

    private float CurrentMoveSpeed()
    {
        if (crouching)
        {
            return crouchSpeed;
        }

        return sprinting ? sprintingSpeed : walkSpeed;
    }

    private void UpdateCrouchState(float deltaTime)
    {
        if (playerCollider == null)
        {
            return;
        }

        float targetBlend = crouching ? 1f : 0f;
        float smoothTime = Mathf.Max(0.01f, crouchTransitionDuration);
        crouchBlend = Mathf.SmoothDamp(crouchBlend, targetBlend, ref crouchBlendVelocity, smoothTime, Mathf.Infinity, deltaTime);
        if (Mathf.Abs(targetBlend - crouchBlend) < 0.001f)
        {
            crouchBlend = targetBlend;
        }

        float colliderHeight = Mathf.Lerp(standingColliderHeight, standingColliderHeight * 0.5f, crouchBlend);
        Vector3 colliderCenter = standingColliderCenter;
        colliderCenter.y = standingColliderCenter.y - (standingColliderHeight - colliderHeight) * 0.5f;
        playerCollider.height = colliderHeight;
        playerCollider.center = colliderCenter;

        if (playerVisual != null)
        {
            Vector3 visualScale = standingVisualLocalScale;
            visualScale.y = Mathf.Lerp(standingVisualLocalScale.y, standingVisualLocalScale.y * 0.5f, crouchBlend);

            Vector3 visualPosition = standingVisualLocalPosition;
            visualPosition.y = standingVisualLocalPosition.y - (standingColliderHeight - colliderHeight) * 0.5f;

            playerVisual.localScale = visualScale;
            playerVisual.localPosition = visualPosition;
        }

        if (cameraPos != null)
        {
            Vector3 targetCameraLocalPosition = standingCameraLocalPosition;
            float eyeHeightFromFeet = Mathf.Lerp(standingEyeHeightFromFeet, crouchingEyeHeightFromFeet, crouchBlend);
            targetCameraLocalPosition.y = FeetLocalY(colliderCenter, colliderHeight) + eyeHeightFromFeet;
            cameraPos.localPosition = targetCameraLocalPosition;
        }
    }

    private float StandingFeetLocalY()
    {
        return FeetLocalY(standingColliderCenter, standingColliderHeight);
    }

    private static float FeetLocalY(Vector3 colliderCenter, float colliderHeight)
    {
        return colliderCenter.y - colliderHeight * 0.5f;
    }

    private bool IsGrounded()
    {
        if (cachedColliders != null)
        {
            for (int i = 0; i < cachedColliders.Length; i++)
            {
                Collider col = cachedColliders[i];
                if (col == null || col.isTrigger)
                {
                    continue;
                }

                Bounds bounds = col.bounds;
                Vector3 origin = bounds.center;
                float rayLength = bounds.extents.y + groundCheckDistance;

                if (Physics.Raycast(origin, Vector3.down, rayLength, ground, QueryTriggerInteraction.Ignore))
                {
                    return true;
                }
            }
        }

        return Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + groundCheckDistance, ground, QueryTriggerInteraction.Ignore);
    }

    private Vector3 GetVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    private void SetVelocity(Vector3 velocity)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = velocity;
#else
        rb.velocity = velocity;
#endif
    }

    private void SetDrag(float dragAmount)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = dragAmount;
#else
        rb.drag = dragAmount;
#endif
    }

    private bool IsSprintHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
        }
#endif

        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    private bool IsCrouchHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
        }
#endif

        return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
    }
}
