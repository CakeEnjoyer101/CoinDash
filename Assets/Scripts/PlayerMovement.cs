using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float playerSpeed = 5f;
    public float laneSwitchSpeed = 12f;
    public float rightLimit = 3.5f;
    public float leftLimit = -3.5f;
    public float laneSnapTolerance = 0.04f;

    public float jumpForce = 8f;
    public float gravity = 20f;
    public float landingSnapThreshold = 0.12f;
    public float groundStickDistance = 0.08f;
    public float landingSmoothSpeed = 16f;
    public float groundFollowSpeed = 42f;
    public float groundGraceTime = 0.06f;
    public float runtimeGroundLift = 0.001f;
    private bool isGrounded = true;
    private float verticalVelocity = 0f;
    private float lastGroundDetectedTime = -10f;
    private bool useRuntimeGroundPlane;
    private float runtimeGroundY;

    private bool canJump = true;
    public float jumpCooldown = 0.1f;
    private float lastJumpTime = 0f;
    private int currentLane = 1;
    private int targetLane = 1;
    private float[] lanePositions = new float[3];
    private Vector2 swipeStart;
    private bool trackingSwipe;
    public float swipeThresholdRatio = 0.08f;
    public float swipeDirectionBias = 1.12f;

    public int CurrentLane => currentLane;
    public int TargetLane => targetLane;
    public bool IsGrounded => isGrounded;
    public float CurrentLaneWorldX => lanePositions[Mathf.Clamp(targetLane, 0, lanePositions.Length - 1)];

    void Awake()
    {
        RebuildLanePositions();
        SnapToClosestLane();
    }

    void OnValidate()
    {
        RebuildLanePositions();
    }

    void Update()
    {
        float deltaTime = Time.deltaTime;
        if (!isGrounded)
            verticalVelocity -= gravity * deltaTime;

        Vector3 position = transform.position;
        position.z += playerSpeed * deltaTime;
        position.y += verticalVelocity * deltaTime;
        transform.position = position;
        UpdateLanePosition();

#if UNITY_EDITOR || UNITY_STANDALONE
        HandlePCControls();
#else
        HandleMobileControls();
#endif

        CheckGrounded();

        if (Time.time - lastJumpTime > jumpCooldown)
            canJump = true;
    }

    void HandlePCControls()
    {
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            ShiftLane(-1);

        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            ShiftLane(1);

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && canJump)
            Jump();
    }

    void HandleMobileControls()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                swipeStart = touch.position;
                trackingSwipe = true;
            }
            else if (touch.phase == TouchPhase.Stationary || touch.phase == TouchPhase.Moved)
            {
                if (trackingSwipe)
                {
                    Vector2 delta = touch.position - swipeStart;
                    float minSwipeDistance = Mathf.Min(Screen.width, Screen.height) * swipeThresholdRatio;
                    float absX = Mathf.Abs(delta.x);
                    float absY = Mathf.Abs(delta.y);

                    if (delta.y > 0f && absY >= minSwipeDistance && absY > absX * swipeDirectionBias)
                    {
                        if (isGrounded && canJump)
                            Jump();
                        trackingSwipe = false;
                    }
                    else if (absX >= minSwipeDistance && absX > absY * swipeDirectionBias)
                    {
                        ShiftLane(delta.x > 0f ? 1 : -1);
                        trackingSwipe = false;
                    }
                }
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                trackingSwipe = false;
            }
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
        if (useRuntimeGroundPlane)
        {
            CheckGroundedAgainstRuntimePlane();
            return;
        }

        float raycastDistance = 0.6f;
        Vector3 raycastOrigin = transform.position + Vector3.up * 0.1f;

        Vector3[] raycastPositions = new Vector3[]
        {
            raycastOrigin,
            raycastOrigin + Vector3.left * 0.3f,
            raycastOrigin + Vector3.right * 0.3f,
            raycastOrigin + Vector3.forward * 0.3f,
            raycastOrigin + Vector3.back * 0.3f
        };

        bool wasGrounded = isGrounded;
        bool detectedGround = false;
        float groundedY = float.NegativeInfinity;

        foreach (Vector3 pos in raycastPositions)
        {
            if (Physics.Raycast(pos, Vector3.down, out RaycastHit hit, raycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                detectedGround = true;
                groundedY = Mathf.Max(groundedY, hit.point.y);
            }
        }

        if (detectedGround)
            lastGroundDetectedTime = Time.time;

        if (!detectedGround)
        {
            // Keep a tiny grace window to avoid 1-frame grounded flicker on segment seams.
            if (wasGrounded && (Time.time - lastGroundDetectedTime) <= groundGraceTime)
            {
                isGrounded = true;
                return;
            }

            isGrounded = false;
            return;
        }

        float distanceToGround = transform.position.y - groundedY;
        bool descending = verticalVelocity <= 0f;
        bool shouldSnapToGround = wasGrounded
            ? distanceToGround <= groundStickDistance
            : distanceToGround <= landingSnapThreshold;

        if (descending && shouldSnapToGround && float.IsFinite(groundedY))
        {
            Vector3 position = transform.position;
            float snapSpeed = wasGrounded ? groundFollowSpeed : landingSmoothSpeed;
            position.y = Mathf.MoveTowards(position.y, groundedY, snapSpeed * Time.deltaTime);
            transform.position = position;

            isGrounded = true;
            verticalVelocity = 0f;
            return;
        }

        isGrounded = false;

#if UNITY_EDITOR
        foreach (Vector3 pos in raycastPositions)
            Debug.DrawRay(pos, Vector3.down * raycastDistance, detectedGround ? Color.green : Color.red);
#endif
    }

    void CheckGroundedAgainstRuntimePlane()
    {
        float targetGroundY = runtimeGroundY + runtimeGroundLift;
        bool wasGrounded = isGrounded;
        float distanceToGround = transform.position.y - targetGroundY;
        bool descending = verticalVelocity <= 0f;
        bool ascending = verticalVelocity > 0.01f;

        // Let jump take off cleanly before any ground snapping logic kicks in.
        if (ascending)
        {
            isGrounded = false;
            return;
        }

        if (distanceToGround <= 0f)
        {
            Vector3 clamped = transform.position;
            clamped.y = targetGroundY;
            transform.position = clamped;
            verticalVelocity = 0f;
            isGrounded = true;
            lastGroundDetectedTime = Time.time;
            return;
        }

        bool shouldSnapToGround = wasGrounded
            ? distanceToGround <= groundStickDistance
            : distanceToGround <= landingSnapThreshold;

        if (descending && shouldSnapToGround)
        {
            Vector3 position = transform.position;
            float snapSpeed = wasGrounded ? groundFollowSpeed : landingSmoothSpeed;
            position.y = Mathf.MoveTowards(position.y, targetGroundY, snapSpeed * Time.deltaTime);
            transform.position = position;

            isGrounded = true;
            verticalVelocity = 0f;
            lastGroundDetectedTime = Time.time;
            return;
        }

        if (wasGrounded && (Time.time - lastGroundDetectedTime) <= groundGraceTime)
        {
            isGrounded = true;
            return;
        }

        isGrounded = false;
    }

    public void SetRuntimeGroundPlane(float groundY)
    {
        useRuntimeGroundPlane = true;
        runtimeGroundY = groundY;
        lastGroundDetectedTime = Time.time;
    }

    public void ClearRuntimeGroundPlane()
    {
        useRuntimeGroundPlane = false;
    }

    void UpdateLanePosition()
    {
        float targetX = lanePositions[Mathf.Clamp(targetLane, 0, lanePositions.Length - 1)];
        Vector3 position = transform.position;
        position.x = Mathf.MoveTowards(position.x, targetX, laneSwitchSpeed * Time.deltaTime);
        position.x = Mathf.Clamp(position.x, leftLimit, rightLimit);

        if (Mathf.Abs(position.x - targetX) <= laneSnapTolerance)
        {
            position.x = targetX;
            currentLane = targetLane;
        }

        transform.position = position;
    }

    public void ShiftLane(int direction)
    {
        if (currentLane != targetLane)
            return;

        SetLane(currentLane + direction);
    }

    public void SetLane(int laneIndex)
    {
        if (currentLane != targetLane)
            return;

        targetLane = Mathf.Clamp(laneIndex, 0, lanePositions.Length - 1);
    }

    public void SnapToLane(int laneIndex)
    {
        laneIndex = Mathf.Clamp(laneIndex, 0, lanePositions.Length - 1);
        currentLane = laneIndex;
        targetLane = laneIndex;
        Vector3 position = transform.position;
        position.x = lanePositions[laneIndex];
        transform.position = position;
    }

    public int GetLaneClosestTo(float worldX)
    {
        int bestLane = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < lanePositions.Length; i++)
        {
            float distance = Mathf.Abs(worldX - lanePositions[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestLane = i;
            }
        }

        return bestLane;
    }

    public float GetLaneWorldX(int laneIndex)
    {
        laneIndex = Mathf.Clamp(laneIndex, 0, lanePositions.Length - 1);
        return lanePositions[laneIndex];
    }

    void RebuildLanePositions()
    {
        float center = (leftLimit + rightLimit) * 0.5f;
        lanePositions[0] = leftLimit;
        lanePositions[1] = center;
        lanePositions[2] = rightLimit;
    }

    void SnapToClosestLane()
    {
        currentLane = GetLaneClosestTo(transform.position.x);
        targetLane = currentLane;
        Vector3 position = transform.position;
        position.x = lanePositions[currentLane];
        transform.position = position;
    }
}
