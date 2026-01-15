using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float playerSpeed = 5f;
    public float horizontalSpeed = 5f;
    public float rightLimit = 3.5f;
    public float leftLimit = -3.5f;

    // Jump variables
    public float jumpForce = 8f;
    public float gravity = 20f;
    private bool isGrounded = true;
    private float verticalVelocity = 0f;

    // Touch variables for jump
    private float touchHoldTimer = 0f;
    public float touchHoldForJump = 0.3f; // Hold time for jump on mobile
    private bool isTouching = false;

    // Jump control variables
    private bool canJump = true;
    public float jumpCooldown = 0.1f;
    private float lastJumpTime = 0f;

    void Update()
    {
        // Apply gravity
        if (!isGrounded)
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

        // Calculate vertical movement
        Vector3 verticalMovement = new Vector3(0, verticalVelocity * Time.deltaTime, 0);

        // Forward movement
        Vector3 forwardMovement = Vector3.forward * playerSpeed * Time.deltaTime;

        // Apply forward and vertical movement
        transform.Translate(forwardMovement + verticalMovement, Space.World);

#if UNITY_EDITOR || UNITY_STANDALONE
        // PC Controls
        HandlePCControls();
#else
        // Mobile Controls
        HandleMobileControls();
#endif

        // Check if grounded
        CheckGrounded();

        // Update jump cooldown
        if (Time.time - lastJumpTime > jumpCooldown)
        {
            canJump = true;
        }
    }

    void HandlePCControls()
    {
        // Horizontal movement
        if (Input.GetKey(KeyCode.A) && transform.position.x > leftLimit)
        {
            MoveLeft();
        }

        if (Input.GetKey(KeyCode.D) && transform.position.x < rightLimit)
        {
            MoveRight();
        }

        // Jump with Space key
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && canJump)
        {
            Jump();
        }
    }

    void HandleMobileControls()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            // Check touch phase
            if (touch.phase == TouchPhase.Began)
            {
                isTouching = true;
                touchHoldTimer = 0f;
            }
            else if (touch.phase == TouchPhase.Stationary || touch.phase == TouchPhase.Moved)
            {
                touchHoldTimer += Time.deltaTime;

                // Horizontal movement based on screen position
                if (touch.position.x < Screen.width / 2 && transform.position.x > leftLimit)
                {
                    MoveLeft();
                }
                else if (touch.position.x > Screen.width / 2 && transform.position.x < rightLimit)
                {
                    MoveRight();
                }
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                // Check if this was a jump (short tap on bottom half of screen)
                if (touchHoldTimer < touchHoldForJump &&
                    touch.position.y < Screen.height / 3 &&
                    isGrounded &&
                    canJump)
                {
                    Jump();
                }
                isTouching = false;
            }
        }
        else
        {
            isTouching = false;
        }

        // Alternative: Double tap for jump (optional)
        if (Input.touchCount == 1 && Input.GetTouch(0).tapCount == 2 &&
            Input.GetTouch(0).phase == TouchPhase.Ended &&
            isGrounded && canJump)
        {
            Jump();
        }
    }

    void Jump()
    {
        if (isGrounded && canJump)
        {
            verticalVelocity = jumpForce;
            isGrounded = false;
            lastJumpTime = Time.time;
            canJump = false;
        }
    }

    void CheckGrounded()
    {
        // Raycast downwards to check if grounded
        float raycastDistance = 0.6f; // Adjust based on your character's height
        Vector3 raycastOrigin = transform.position + Vector3.up * 0.1f; // Slightly above feet

        // Cast ray in multiple positions for better detection
        Vector3[] raycastPositions = new Vector3[]
        {
            raycastOrigin,
            raycastOrigin + Vector3.left * 0.3f,
            raycastOrigin + Vector3.right * 0.3f,
            raycastOrigin + Vector3.forward * 0.3f,
            raycastOrigin + Vector3.back * 0.3f
        };

        bool wasGrounded = isGrounded;
        isGrounded = false;

        foreach (Vector3 pos in raycastPositions)
        {
            if (Physics.Raycast(pos, Vector3.down, raycastDistance))
            {
                isGrounded = true;

                // Reset vertical velocity when landing
                if (!wasGrounded && verticalVelocity < 0)
                {
                    verticalVelocity = 0f;
                }
                break;
            }
        }

        // Optional: Visualize raycasts in editor
#if UNITY_EDITOR
        foreach (Vector3 pos in raycastPositions)
        {
            Debug.DrawRay(pos, Vector3.down * raycastDistance, isGrounded ? Color.green : Color.red);
        }
#endif
    }

    void MoveLeft()
    {
        transform.Translate(Vector3.left * horizontalSpeed * Time.deltaTime, Space.World);
    }

    void MoveRight()
    {
        transform.Translate(Vector3.right * horizontalSpeed * Time.deltaTime, Space.World);
    }

    // Ensure player stays within limits
    void LateUpdate()
    {
        Vector3 position = transform.position;
        position.x = Mathf.Clamp(position.x, leftLimit, rightLimit);
        transform.position = position;
    }
}