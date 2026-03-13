using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(UniversalAdditionalCameraData))]
public class WeaponViewCameraController : MonoBehaviour
{
    [Header("Layer")]
    [SerializeField] private string weaponLayerName = "Weapon";

    [Header("Overlay Camera")]
    [SerializeField] private string overlayCameraName = "Weapon Overlay Camera";
    [SerializeField] private float overlayNearClipPlane = 0.01f;
    [SerializeField] private float overlayFarClipPlane = 10f;
    [SerializeField] private bool keepOverlayDisabledWhenNoWeapon = false;

    private Camera baseCamera;
    private Camera overlayCamera;

    private void Awake()
    {
        SetupCameras();
    }

    private void OnEnable()
    {
        SetupCameras();
    }

    private void LateUpdate()
    {
        if (baseCamera == null || overlayCamera == null)
        {
            return;
        }

        overlayCamera.fieldOfView = baseCamera.fieldOfView;

        if (keepOverlayDisabledWhenNoWeapon)
        {
            overlayCamera.enabled = HasWeaponOnLayer();
        }
    }

    private void SetupCameras()
    {
        baseCamera = GetComponent<Camera>();
        if (baseCamera == null)
        {
            enabled = false;
            return;
        }

        int weaponLayer = LayerMask.NameToLayer(weaponLayerName);
        if (weaponLayer < 0)
        {
            Debug.LogWarning($"Layer '{weaponLayerName}' was not found. Create it before using the weapon overlay camera.", this);
            enabled = false;
            return;
        }

        overlayCamera = FindOrCreateOverlayCamera();
        if (overlayCamera == null)
        {
            enabled = false;
            return;
        }

        ConfigureBaseCamera(weaponLayer);
        ConfigureOverlayCamera(weaponLayer);
        EnsureCameraStack();
    }

    private Camera FindOrCreateOverlayCamera()
    {
        Transform child = transform.Find(overlayCameraName);
        Camera foundCamera = child != null ? child.GetComponent<Camera>() : null;
        if (foundCamera != null)
        {
            return foundCamera;
        }

        GameObject overlayObject = new GameObject(overlayCameraName);
        overlayObject.transform.SetParent(transform, false);
        overlayObject.transform.localPosition = Vector3.zero;
        overlayObject.transform.localRotation = Quaternion.identity;

        return overlayObject.AddComponent<Camera>();
    }

    private void ConfigureBaseCamera(int weaponLayer)
    {
        baseCamera.cullingMask &= ~(1 << weaponLayer);

        UniversalAdditionalCameraData baseCameraData = baseCamera.GetUniversalAdditionalCameraData();
        baseCameraData.renderType = CameraRenderType.Base;
    }

    private void ConfigureOverlayCamera(int weaponLayer)
    {
        overlayCamera.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        overlayCamera.cullingMask = 1 << weaponLayer;
        overlayCamera.clearFlags = CameraClearFlags.Depth;
        overlayCamera.nearClipPlane = overlayNearClipPlane;
        overlayCamera.farClipPlane = overlayFarClipPlane;
        overlayCamera.fieldOfView = baseCamera.fieldOfView;
        overlayCamera.depth = baseCamera.depth + 1f;
        overlayCamera.allowHDR = baseCamera.allowHDR;
        overlayCamera.allowMSAA = baseCamera.allowMSAA;
        overlayCamera.orthographic = baseCamera.orthographic;
        overlayCamera.orthographicSize = baseCamera.orthographicSize;

        AudioListener listener = overlayCamera.GetComponent<AudioListener>();
        if (listener != null)
        {
            Destroy(listener);
        }

        UniversalAdditionalCameraData overlayCameraData = overlayCamera.GetUniversalAdditionalCameraData();
        overlayCameraData.renderType = CameraRenderType.Overlay;

        overlayCamera.enabled = !keepOverlayDisabledWhenNoWeapon || HasWeaponOnLayer();
    }

    private void EnsureCameraStack()
    {
        UniversalAdditionalCameraData baseCameraData = baseCamera.GetUniversalAdditionalCameraData();
        if (!baseCameraData.cameraStack.Contains(overlayCamera))
        {
            baseCameraData.cameraStack.Add(overlayCamera);
        }
    }

    private bool HasWeaponOnLayer()
    {
        if (overlayCamera == null)
        {
            return false;
        }

        int weaponMask = overlayCamera.cullingMask;
        if (weaponMask == 0)
        {
            return false;
        }

        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer currentRenderer = renderers[i];
            if (currentRenderer == null || !currentRenderer.enabled)
            {
                continue;
            }

            if ((weaponMask & (1 << currentRenderer.gameObject.layer)) != 0)
            {
                return true;
            }
        }

        return false;
    }
}
