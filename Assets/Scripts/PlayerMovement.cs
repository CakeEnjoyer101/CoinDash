using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float playerSpeed = 5f;
    public float horizontalSpeed = 5f;
    public float rightLimit = 3.5f;
    public float leftLimit = -3.5f;

    void Update()
    {
        // Forward movement
        transform.Translate(Vector3.forward * playerSpeed * Time.deltaTime, Space.World);

#if UNITY_EDITOR || UNITY_STANDALONE
        // PC Controls
        if (Input.GetKey(KeyCode.A) && transform.position.x > leftLimit)
        {
            MoveLeft();
        }

        if (Input.GetKey(KeyCode.D) && transform.position.x < rightLimit)
        {
            MoveRight();
        }
#else
        // Mobile Controls
        HandleTouchInput();
#endif
    }

    void HandleTouchInput()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.position.x < Screen.width / 2 && transform.position.x > leftLimit)
            {
                MoveLeft();
            }
            else if (touch.position.x > Screen.width / 2 && transform.position.x < rightLimit)
            {
                MoveRight();
            }
        }
    }

    void MoveLeft()
    {
        transform.Translate(Vector3.left * horizontalSpeed * Time.deltaTime);
    }

    void MoveRight()
    {
        transform.Translate(Vector3.right * horizontalSpeed * Time.deltaTime);
    }
}
