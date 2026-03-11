using UnityEngine;

public class CameraPos : MonoBehaviour
{
    public Transform cameraPos;
    public Transform cameraHolder;

    private void LateUpdate()
    {
        if (cameraPos == null || cameraHolder == null)
        {
            return;
        }

        cameraHolder.position = cameraPos.position;
    }
}
