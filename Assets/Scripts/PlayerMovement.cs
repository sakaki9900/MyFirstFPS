using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float groundDrag = 5f;

    [Header("Ground Check")]
    [SerializeField] private float playerHeight = 2f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask ground = 1 << 3;

    [Header("References")]
    [SerializeField] private Transform orientation;

    private Rigidbody rb;
    private Collider[] cachedColliders;
    private Vector2 moveInput;
    private bool grounded;

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
    }

    private void Update()
    {
        grounded = IsGrounded();
        SetDrag(grounded ? groundDrag : 0f);

        ProcessInput();
        SpeedControl();
    }

    private void FixedUpdate()
    {
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
            return;
        }
#endif

        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");
    }

    private void MovePlayer()
    {
        Transform moveReference = orientation != null ? orientation : transform;

        Vector3 forward = moveReference.forward;
        Vector3 right = moveReference.right;
        forward.y = 0f;
        right.y = 0f;

        Vector3 moveDirection = forward.normalized * moveInput.y + right.normalized * moveInput.x;
        rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
    }

    private void SpeedControl()
    {
        Vector3 velocity = GetVelocity();
        Vector3 flatVelocity = new Vector3(velocity.x, 0f, velocity.z);

        if (flatVelocity.magnitude <= moveSpeed)
        {
            return;
        }

        Vector3 limitedVelocity = flatVelocity.normalized * moveSpeed;
        SetVelocity(new Vector3(limitedVelocity.x, velocity.y, limitedVelocity.z));
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
}
