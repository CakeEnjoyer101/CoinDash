using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
   public float playerSpeed = 5;
   public float horitontalSpeed = 5;
   public float rightLimit = 3.5f;
   public float leftLimit = -3.5f;

    // Update is called once per frame
    void Update()
    {
        transform.Translate(Vector3.forward * Time.deltaTime * playerSpeed, Space.World);
        if (Input.GetKey(KeyCode.A))
        {
            if (this.gameObject.transform.position.x > leftLimit)
            {
                transform.Translate(Vector3.left * Time.deltaTime * horitontalSpeed);
            }

        }
        if (Input.GetKey(KeyCode.D))
        {
            if (this.gameObject.transform.position.x < rightLimit)
            {
                transform.Translate(Vector3.left * Time.deltaTime * horitontalSpeed * -1);
            }
        }
    }
}

