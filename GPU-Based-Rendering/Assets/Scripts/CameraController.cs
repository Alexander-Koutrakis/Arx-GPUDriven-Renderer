using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float lookSpeed = 100f;

    void Update()
    {
        // Movement with WASD
        float moveX = 0f;
        float moveZ = 0f;

        if (Input.GetKey(KeyCode.W)) moveZ += 1f;
        if (Input.GetKey(KeyCode.S)) moveZ -= 1f;
        if (Input.GetKey(KeyCode.A)) moveX -= 1f;
        if (Input.GetKey(KeyCode.D)) moveX += 1f;

        Vector3 move = new Vector3(moveX, 0f, moveZ).normalized;
        transform.Translate(move * moveSpeed * Time.deltaTime, Space.Self);

        // Look with arrow keys
        float rotY = 0f;
        float rotX = 0f;

        if (Input.GetKey(KeyCode.LeftArrow)) rotY -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) rotY += 1f;
        if (Input.GetKey(KeyCode.UpArrow)) rotX -= 1f;
        if (Input.GetKey(KeyCode.DownArrow)) rotX += 1f;

        transform.Rotate(Vector3.up, rotY * lookSpeed * Time.deltaTime, Space.World);
        transform.Rotate(Vector3.right, rotX * lookSpeed * Time.deltaTime, Space.Self);
    }
}
