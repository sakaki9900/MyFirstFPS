using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ProjectileGun : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private Transform shootPoint;
    [SerializeField] private GameObject bullet;
    [SerializeField] private Camera playerCam;
    [SerializeField] private Light muzzleFlashLight;
    [SerializeField] private ParticleSystem muzzleFlashParticles;
    [SerializeField] private float fallbackBulletScale = 0.1f;
    [SerializeField] private float fallbackBulletLifetime = 5f;

    [Header("Muzzle Flash")]
    [SerializeField] private bool useMuzzleFlash = true;
    [SerializeField] private float muzzleFlashDuration = 0.03f;

    [Header("GunProperty")]
    [SerializeField] private float shootForce = 20f;
    [SerializeField] private float timeBetweenShooting = 0.15f;
    [SerializeField] private float timeBetweenShots = 0.05f;
    [SerializeField] private float spread = 0f;
    [SerializeField] private float reloadTime = 1f;
    [SerializeField] private int magazineSize = 12;
    [SerializeField] private int bulletsPerTap = 1;
    [SerializeField] private bool allowButtonHold = false;

    [SerializeField] private LayerMask ignoreLayer;

    private int bulletsShot;
    private int bulletsLeft;

    private bool shooting;
    private bool readyToShoot;

    public bool reloading;
    public bool allowInvoke = true;

    private void Reset()
    {
        AutoAssignReferences();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            AutoAssignReferences();
        }
    }
#endif

    private void Awake()
    {
        AutoAssignReferences();
    }

    private void Start()
    {
        bulletsLeft = magazineSize;
        readyToShoot = true;

        if (shootPoint == null)
        {
            Debug.LogError("ProjectileGun requires a ShootPoint reference.", this);
            enabled = false;
            return;
        }

        if (playerCam == null)
        {
            Debug.LogError("ProjectileGun could not find the player camera.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        InputHandler();
    }

    private void InputHandler()
    {
        shooting = allowButtonHold ? IsFireHeld() : WasFirePressedThisFrame();

        if (readyToShoot && shooting && !reloading && bulletsLeft > 0)
        {
            bulletsShot = 0;
            Shoot();
        }

        if (WasReloadPressedThisFrame() && bulletsLeft < magazineSize && !reloading)
        {
            Reload();
        }

        if (readyToShoot && shooting && !reloading && bulletsLeft <= 0)
        {
            Reload();
        }
    }

    private void Shoot()
    {
        readyToShoot = false;

        Ray ray = playerCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;

        Vector3 targetPoint;
        if (Physics.Raycast(ray, out hit, 1000f, ~ignoreLayer, QueryTriggerInteraction.Ignore))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint = ray.GetPoint(100f);
        }

        Vector3 directionWithoutSpread = targetPoint - shootPoint.position;

        float x = Random.Range(-spread, spread);
        float y = Random.Range(-spread, spread);
        Vector3 directionWithSpread = directionWithoutSpread + new Vector3(x, y, 0f);

        GameObject currentBullet = CreateBulletInstance();
        currentBullet.transform.forward = directionWithSpread.normalized;

        Rigidbody bulletRigidbody = currentBullet.GetComponent<Rigidbody>();
        if (bulletRigidbody != null)
        {
            bulletRigidbody.AddForce(directionWithSpread.normalized * shootForce, ForceMode.Impulse);
        }

        PlayMuzzleFlash();

        bulletsLeft--;
        bulletsShot++;

        if (allowInvoke)
        {
            Invoke(nameof(ResetShot), timeBetweenShooting);
            allowInvoke = false;
        }

        if (bulletsShot < bulletsPerTap && bulletsLeft > 0)
        {
            Invoke(nameof(Shoot), timeBetweenShots);
        }
    }

    private void ResetShot()
    {
        readyToShoot = true;
        allowInvoke = true;
    }

    private void Reload()
    {
        reloading = true;
        Invoke(nameof(ReloadFinished), reloadTime);
    }

    private void ReloadFinished()
    {
        bulletsLeft = magazineSize;
        reloading = false;
    }

    private void PlayMuzzleFlash()
    {
        if (!useMuzzleFlash)
        {
            return;
        }

        if (muzzleFlashParticles == null || muzzleFlashLight == null)
        {
            AutoAssignReferences();
        }

        if (muzzleFlashParticles != null)
        {
            muzzleFlashParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            muzzleFlashParticles.Play(true);
        }

        if (muzzleFlashLight != null)
        {
            CancelInvoke(nameof(DisableMuzzleFlash));
            muzzleFlashLight.enabled = true;
            Invoke(nameof(DisableMuzzleFlash), muzzleFlashDuration);
        }
    }

    private void DisableMuzzleFlash()
    {
        if (muzzleFlashLight != null)
        {
            muzzleFlashLight.enabled = false;
        }
    }

    private GameObject CreateBulletInstance()
    {
        if (bullet != null)
        {
            return Instantiate(bullet, shootPoint.position, Quaternion.identity);
        }

        GameObject fallbackBullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fallbackBullet.name = "RuntimeBullet";
        fallbackBullet.transform.SetPositionAndRotation(shootPoint.position, Quaternion.identity);
        fallbackBullet.transform.localScale = Vector3.one * fallbackBulletScale;

        Rigidbody fallbackRigidbody = fallbackBullet.AddComponent<Rigidbody>();
        fallbackRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Destroy(fallbackBullet, fallbackBulletLifetime);
        return fallbackBullet;
    }

    private void AutoAssignReferences()
    {
        if (shootPoint == null)
        {
            Transform foundShootPoint = transform.Find("ShootPoint");
            if (foundShootPoint != null)
            {
                shootPoint = foundShootPoint;
            }
        }

        if (muzzleFlashLight == null && shootPoint != null)
        {
            Transform flashTransform = shootPoint.Find("MuzzleFlashLight");
            if (flashTransform != null)
            {
                muzzleFlashLight = flashTransform.GetComponent<Light>();
            }
        }

        if (muzzleFlashParticles == null && shootPoint != null)
        {
            Transform particlesTransform = shootPoint.Find("MuzzleFlashVFX");
            if (particlesTransform != null)
            {
                muzzleFlashParticles = particlesTransform.GetComponent<ParticleSystem>();
            }
        }

        if (playerCam == null)
        {
            playerCam = Camera.main;
            if (playerCam == null)
            {
                GameObject mainCameraObject = GameObject.Find("Main Camera");
                if (mainCameraObject != null)
                {
                    playerCam = mainCameraObject.GetComponent<Camera>();
                }
            }
        }

#if UNITY_EDITOR
        if (bullet == null)
        {
            bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Bullet.prefab");
        }
#endif
    }

    private static bool IsFireHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.isPressed;
        }
#endif
        return Input.GetMouseButton(0);
    }

    private static bool WasFirePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.wasPressedThisFrame;
        }
#endif
        return Input.GetMouseButtonDown(0);
    }

    private static bool WasReloadPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.rKey.wasPressedThisFrame;
        }
#endif
        return Input.GetKeyDown(KeyCode.R);
    }
}
