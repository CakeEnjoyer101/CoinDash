using UnityEngine;

public sealed class LaneObstacleMover : MonoBehaviour
{
    float leftX = -3.5f;
    float rightX = 3.5f;
    float speed = 3.2f;
    float yPosition;
    float zPosition;
    float startTime;
    Rigidbody body;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    public void Configure(float left, float right, float moveSpeed, float y, float z)
    {
        if (body == null)
            body = GetComponent<Rigidbody>();

        leftX = Mathf.Min(left, right);
        rightX = Mathf.Max(left, right);
        speed = Mathf.Max(0.01f, moveSpeed);
        yPosition = y;
        zPosition = z;
        startTime = Time.time;
        ApplyPosition(0f);
    }

    void FixedUpdate()
    {
        float width = Mathf.Max(0.01f, rightX - leftX);
        float travel = Mathf.PingPong((Time.time - startTime) * speed, width);
        ApplyPosition(travel);
    }

    void ApplyPosition(float travel)
    {
        Vector3 position = transform.position;
        position.x = leftX + travel;
        position.y = yPosition;
        position.z = zPosition;

        if (body != null && body.isKinematic)
            body.MovePosition(position);
        else
            transform.position = position;
    }
}
