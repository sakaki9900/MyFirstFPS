using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCam : MonoBehaviour
{
    public Transform orientation;
    public Transform CamHolder;
    public float mouseSensitivity = 100f;

    private float xRotation;
    private float yRotation;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (Mouse.current == null)
        {
            return;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        float mouseX = mouseDelta.x * mouseSensitivity * 0.01f;
        float mouseY = mouseDelta.y * mouseSensitivity * 0.01f;

        yRotation += mouseX;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        if (CamHolder != null)
        {
            CamHolder.rotation = Quaternion.Euler(xRotation, yRotation, 0f);
        }

        if (orientation != null)
        {
            orientation.rotation = Quaternion.Euler(0f, yRotation, 0f);
        }
    }
}
