using UnityEngine;

[DisallowMultipleComponent]
public class WeaponPickup : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private string weaponDisplayName = "Weapon";

    [Header("Held Pose")]
    [SerializeField] private Vector3 heldLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 heldLocalEulerAngles = Vector3.zero;

    [Header("Drop")]
    [SerializeField] private float dropForwardForce = 0f;
    [SerializeField] private float dropUpwardForce = 0f;
    [SerializeField] private float dropTorque = 0f;
    [SerializeField] private Vector3 droppedEulerOffset = new Vector3(0f, 0f, 90f);

    private Rigidbody weaponRigidbody;
    private Collider[] weaponColliders;
    private ProjectileGun projectileGun;
    private Transform shootPoint;
    private Light muzzleFlashLight;
    private ParticleSystem muzzleFlashParticles;
    private bool initialized;
    private bool equipped;

    public string WeaponDisplayName => string.IsNullOrWhiteSpace(weaponDisplayName) ? gameObject.name : weaponDisplayName;
    public bool IsEquipped => equipped;

    private void Awake()
    {
        InitializeIfNeeded();
    }

    public void InitializeIfNeeded()
    {
        if (initialized)
        {
            return;
        }

        weaponRigidbody = GetComponent<Rigidbody>();
        if (weaponRigidbody == null)
        {
            weaponRigidbody = gameObject.AddComponent<Rigidbody>();
        }

        weaponColliders = GetComponentsInChildren<Collider>(true);
        if (weaponColliders == null || weaponColliders.Length == 0)
        {
            BoxCollider generatedCollider = gameObject.AddComponent<BoxCollider>();
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Bounds localBounds = meshFilter.sharedMesh.bounds;
                generatedCollider.center = localBounds.center;
                generatedCollider.size = localBounds.size;
            }

            weaponColliders = new Collider[] { generatedCollider };
        }

        projectileGun = GetComponent<ProjectileGun>();
        CacheExistingRuntimeParts();
        ApplyStateImmediately(IsHeldWeaponAtStartup());

        initialized = true;
    }

    public Vector3 GetPickupPoint()
    {
        InitializeIfNeeded();

        if (weaponColliders != null && weaponColliders.Length > 0)
        {
            Bounds combinedBounds = weaponColliders[0].bounds;
            for (int i = 1; i < weaponColliders.Length; i++)
            {
                if (weaponColliders[i] != null)
                {
                    combinedBounds.Encapsulate(weaponColliders[i].bounds);
                }
            }

            return combinedBounds.center;
        }

        Renderer renderer = GetComponentInChildren<Renderer>();
        return renderer != null ? renderer.bounds.center : transform.position;
    }

    public void PrepareFromTemplate(ProjectileGun templateGun, Camera playerCamera)
    {
        InitializeIfNeeded();
        EnsureRuntimeParts(templateGun);

        if (projectileGun == null)
        {
            projectileGun = gameObject.AddComponent<ProjectileGun>();
        }

        projectileGun.ApplySettingsFrom(templateGun);
        projectileGun.ConfigureReferences(shootPoint, muzzleFlashLight, muzzleFlashParticles, playerCamera, templateGun != null ? templateGun.BulletPrefab : null);
    }

    public void EquipTo(Transform holder, Camera playerCamera, ProjectileGun templateGun)
    {
        PrepareFromTemplate(templateGun, playerCamera);

        equipped = true;
        transform.SetParent(holder, false);
        transform.localPosition = heldLocalPosition;
        transform.localRotation = Quaternion.Euler(heldLocalEulerAngles);

        if (weaponRigidbody != null)
        {
            ResetVelocityIfDynamic();
            weaponRigidbody.isKinematic = true;
            weaponRigidbody.useGravity = false;
        }

        SetCollidersEnabled(false);

        if (projectileGun != null)
        {
            projectileGun.enabled = true;
        }
    }

    public void Drop(Vector3 worldPosition, Quaternion worldRotation, Vector3 forward, bool applyImpulse = false)
    {
        InitializeIfNeeded();

        equipped = false;
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(worldPosition, worldRotation * Quaternion.Euler(droppedEulerOffset));

        if (projectileGun != null)
        {
            projectileGun.enabled = false;
        }

        SetCollidersEnabled(true);

        if (weaponRigidbody != null)
        {
            weaponRigidbody.isKinematic = false;
            weaponRigidbody.useGravity = true;
            ResetVelocityIfDynamic();

            if (applyImpulse)
            {
                weaponRigidbody.AddForce(forward * dropForwardForce + Vector3.up * dropUpwardForce, ForceMode.Impulse);
                weaponRigidbody.AddTorque(Random.insideUnitSphere * dropTorque, ForceMode.Impulse);
            }
        }
    }

    private void CacheExistingRuntimeParts()
    {
        shootPoint = transform.Find("ShootPoint");
        if (shootPoint != null)
        {
            Transform lightTransform = shootPoint.Find("MuzzleFlashLight");
            if (lightTransform != null)
            {
                muzzleFlashLight = lightTransform.GetComponent<Light>();
            }

            Transform particlesTransform = shootPoint.Find("MuzzleFlashVFX");
            if (particlesTransform != null)
            {
                muzzleFlashParticles = particlesTransform.GetComponent<ParticleSystem>();
            }
        }
    }

    private void EnsureRuntimeParts(ProjectileGun templateGun)
    {
        if (shootPoint == null && templateGun != null && templateGun.ShootPoint != null)
        {
            CloneTemplateShootPoint(templateGun.ShootPoint);
        }

        shootPoint = transform.Find("ShootPoint");
        if (shootPoint == null)
        {
            GameObject shootPointObject = new GameObject("ShootPoint");
            shootPoint = shootPointObject.transform;
            shootPoint.SetParent(transform, false);
            shootPoint.localPosition = new Vector3(0f, 0.03f, 0.62f);
            shootPoint.localRotation = Quaternion.identity;
        }

        Transform lightTransform = shootPoint.Find("MuzzleFlashLight");
        if (lightTransform == null)
        {
            GameObject lightObject = new GameObject("MuzzleFlashLight");
            lightTransform = lightObject.transform;
            lightTransform.SetParent(shootPoint, false);
            lightTransform.localPosition = Vector3.zero;

            muzzleFlashLight = lightObject.AddComponent<Light>();
            muzzleFlashLight.type = LightType.Point;
            muzzleFlashLight.range = 2.5f;
            muzzleFlashLight.intensity = 5f;
            muzzleFlashLight.color = new Color(1f, 0.82f, 0.55f);
            muzzleFlashLight.enabled = false;
        }
        else
        {
            muzzleFlashLight = lightTransform.GetComponent<Light>();
        }

        Transform particlesTransform = shootPoint.Find("MuzzleFlashVFX");
        if (particlesTransform == null)
        {
            GameObject particlesObject = new GameObject("MuzzleFlashVFX");
            particlesTransform = particlesObject.transform;
            particlesTransform.SetParent(shootPoint, false);
            particlesTransform.localPosition = Vector3.zero;

            muzzleFlashParticles = particlesObject.AddComponent<ParticleSystem>();
            muzzleFlashParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = muzzleFlashParticles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.08f;
            main.startLifetime = 0.05f;
            main.startSpeed = 0.2f;
            main.startSize = 0.08f;
            main.startColor = new Color(1f, 0.82f, 0.55f, 0.85f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = muzzleFlashParticles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

            var shape = muzzleFlashParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.01f;

            var velocityOverLifetime = muzzleFlashParticles.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(1.5f);

            var colorOverLifetime = muzzleFlashParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.9f, 0.65f), 0f),
                    new GradientColorKey(new Color(1f, 0.45f, 0.15f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;
        }
        else
        {
            muzzleFlashParticles = particlesTransform.GetComponent<ParticleSystem>();
        }

        if (projectileGun != null)
        {
            projectileGun.ConfigureReferences(shootPoint, muzzleFlashLight, muzzleFlashParticles, Camera.main, null);
        }
    }

    private void CloneTemplateShootPoint(Transform templateShootPoint)
    {
        Transform existingShootPoint = transform.Find("ShootPoint");
        if (existingShootPoint != null)
        {
            DestroyImmediate(existingShootPoint.gameObject);
        }

        GameObject clonedShootPoint = Instantiate(templateShootPoint.gameObject, transform);
        clonedShootPoint.name = "ShootPoint";
        clonedShootPoint.transform.localPosition = templateShootPoint.localPosition;
        clonedShootPoint.transform.localRotation = templateShootPoint.localRotation;
        clonedShootPoint.transform.localScale = templateShootPoint.localScale;

        shootPoint = clonedShootPoint.transform;
        muzzleFlashLight = shootPoint.GetComponentInChildren<Light>(true);
        muzzleFlashParticles = shootPoint.GetComponentInChildren<ParticleSystem>(true);

        if (muzzleFlashLight != null)
        {
            muzzleFlashLight.enabled = false;
        }

        if (muzzleFlashParticles != null)
        {
            muzzleFlashParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void SetCollidersEnabled(bool isEnabled)
    {
        if (weaponColliders == null)
        {
            return;
        }

        for (int i = 0; i < weaponColliders.Length; i++)
        {
            if (weaponColliders[i] != null)
            {
                weaponColliders[i].enabled = isEnabled;
            }
        }
    }

    private void ApplyStateImmediately(bool shouldBeEquipped)
    {
        equipped = shouldBeEquipped;

        if (weaponRigidbody != null)
        {
            ResetVelocityIfDynamic();
            weaponRigidbody.isKinematic = shouldBeEquipped;
            weaponRigidbody.useGravity = !shouldBeEquipped;
        }

        SetCollidersEnabled(!shouldBeEquipped);

        if (projectileGun != null)
        {
            projectileGun.enabled = shouldBeEquipped;
        }
    }

    private bool IsHeldWeaponAtStartup()
    {
        return projectileGun != null
            && transform.parent != null
            && (transform.parent.name == "WeaponHolder" || transform.parent.GetComponentInParent<Camera>() != null);
    }

    private void ResetVelocityIfDynamic()
    {
        if (weaponRigidbody == null || weaponRigidbody.isKinematic)
        {
            return;
        }

        weaponRigidbody.linearVelocity = Vector3.zero;
        weaponRigidbody.angularVelocity = Vector3.zero;
    }
}
