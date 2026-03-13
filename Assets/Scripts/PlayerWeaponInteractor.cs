using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class PlayerWeaponInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform weaponHolder;

    [Header("Pickup Detection")]
    [SerializeField] private float pickupRange = 2f;
    [SerializeField] private float maxPickupAngle = 35f;
    [SerializeField] private LayerMask obstructionMask = Physics.DefaultRaycastLayers;

    [Header("Drop")]
    [SerializeField] private float dropDistance = 1.1f;
    [SerializeField] private float dropHeightOffset = 0.12f;
    [SerializeField] private float dropGroundCheckHeight = 2f;
    [SerializeField] private float dropGroundCheckDistance = 6f;

    [Header("Prompt UI")]
    [SerializeField] private Vector2 promptSize = new Vector2(360f, 64f);
    [SerializeField] private Vector2 promptScreenOffset = new Vector2(0f, -90f);

    private WeaponPickup currentWeapon;
    private WeaponPickup hoveredWeapon;
    private ProjectileGun templateGun;
    private Canvas promptCanvas;
    private CanvasGroup promptCanvasGroup;
    private Text promptText;

    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponent<Camera>();
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (weaponHolder == null)
        {
            Transform holder = transform.Find("WeaponHolder");
            if (holder == null && playerCamera != null)
            {
                holder = playerCamera.transform.Find("WeaponHolder");
            }

            weaponHolder = holder;
        }

        CreatePromptUiIfNeeded();
    }

    private void Start()
    {
        BootstrapExistingWeapons();
    }

    private void Update()
    {
        hoveredWeapon = FindBestPickupCandidate();
        UpdatePrompt();

        if (WasInteractPressedThisFrame() && hoveredWeapon != null)
        {
            PickupWeapon(hoveredWeapon);
        }

        if (WasDropPressedThisFrame())
        {
            DropCurrentWeapon();
        }
    }

    private void BootstrapExistingWeapons()
    {
        WeaponPickup[] weapons = FindObjectsByType<WeaponPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < weapons.Length; i++)
        {
            weapons[i].InitializeIfNeeded();
        }

        if (weaponHolder == null)
        {
            return;
        }

        WeaponPickup heldWeapon = weaponHolder.GetComponentInChildren<WeaponPickup>(true);
        if (heldWeapon == null && weaponHolder.childCount > 0)
        {
            heldWeapon = weaponHolder.GetChild(0).gameObject.AddComponent<WeaponPickup>();
        }

        if (heldWeapon == null)
        {
            return;
        }

        templateGun = heldWeapon.GetComponent<ProjectileGun>();
        if (templateGun == null)
        {
            templateGun = FindAnyObjectByType<ProjectileGun>();
        }

        heldWeapon.InitializeIfNeeded();
        heldWeapon.EquipTo(weaponHolder, playerCamera, templateGun);
        currentWeapon = heldWeapon;

        WeaponPickup[] sceneWeapons = FindObjectsByType<WeaponPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sceneWeapons.Length; i++)
        {
            if (sceneWeapons[i] == currentWeapon)
            {
                continue;
            }

            sceneWeapons[i].PrepareFromTemplate(templateGun, playerCamera);
            sceneWeapons[i].Drop(sceneWeapons[i].transform.position, sceneWeapons[i].transform.rotation, Vector3.zero, false);
        }
    }

    private WeaponPickup FindBestPickupCandidate()
    {
        if (playerCamera == null)
        {
            return null;
        }

        WeaponPickup[] weapons = FindObjectsByType<WeaponPickup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        WeaponPickup bestCandidate = null;
        float bestScore = float.MaxValue;

        Vector3 origin = playerCamera.transform.position;
        Vector3 forward = playerCamera.transform.forward;

        for (int i = 0; i < weapons.Length; i++)
        {
            WeaponPickup weapon = weapons[i];
            if (weapon == null || weapon.IsEquipped)
            {
                continue;
            }

            Vector3 targetPoint = weapon.GetPickupPoint();
            Vector3 toWeapon = targetPoint - origin;
            float distance = toWeapon.magnitude;
            if (distance > pickupRange || distance <= 0.001f)
            {
                continue;
            }

            float angle = Vector3.Angle(forward, toWeapon);
            if (angle > maxPickupAngle)
            {
                continue;
            }

            if (Physics.Raycast(origin, toWeapon.normalized, out RaycastHit hit, distance + 0.1f, obstructionMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform != weapon.transform && !hit.transform.IsChildOf(weapon.transform))
                {
                    continue;
                }
            }

            float score = distance + angle * 0.05f;
            if (score < bestScore)
            {
                bestScore = score;
                bestCandidate = weapon;
            }
        }

        return bestCandidate;
    }

    private void PickupWeapon(WeaponPickup weapon)
    {
        if (weapon == null || weaponHolder == null || playerCamera == null)
        {
            return;
        }

        GetDropPose(out Vector3 droppedPosition, out Quaternion droppedRotation);

        if (currentWeapon != null && currentWeapon != weapon)
        {
            currentWeapon.Drop(droppedPosition, droppedRotation, playerCamera.transform.forward);
        }

        if (templateGun == null)
        {
            templateGun = currentWeapon != null ? currentWeapon.GetComponent<ProjectileGun>() : FindAnyObjectByType<ProjectileGun>();
        }

        weapon.EquipTo(weaponHolder, playerCamera, templateGun);
        currentWeapon = weapon;

        if (templateGun == null)
        {
            templateGun = currentWeapon.GetComponent<ProjectileGun>();
        }
    }

    private void DropCurrentWeapon()
    {
        if (currentWeapon == null || playerCamera == null)
        {
            return;
        }

        GetDropPose(out Vector3 dropPosition, out Quaternion dropRotation);
        WeaponPickup weaponToDrop = currentWeapon;

        currentWeapon = null;
        weaponToDrop.Drop(dropPosition, dropRotation, playerCamera.transform.forward);
    }

    private void CreatePromptUiIfNeeded()
    {
        if (promptCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("WeaponPromptCanvas");
        canvasObject.transform.SetParent(transform, false);
        promptCanvas = canvasObject.AddComponent<Canvas>();
        promptCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        promptCanvas.sortingOrder = 2000;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();
        promptCanvasGroup = canvasObject.AddComponent<CanvasGroup>();
        promptCanvasGroup.alpha = 0f;

        GameObject panelObject = new GameObject("PromptPanel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.06f, 0.08f, 0.1f, 0.85f);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = promptSize;
        panelRect.anchoredPosition = promptScreenOffset;

        Outline panelOutline = panelObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(1f, 1f, 1f, 0.1f);
        panelOutline.effectDistance = new Vector2(1f, -1f);

        GameObject textObject = new GameObject("PromptText");
        textObject.transform.SetParent(panelObject.transform, false);
        promptText = textObject.AddComponent<Text>();
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        promptText.fontSize = 25;
        promptText.fontStyle = FontStyle.Bold;
        promptText.color = new Color(0.95f, 0.97f, 1f, 1f);
        promptText.supportRichText = true;

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 10f);
        textRect.offsetMax = new Vector2(-18f, -10f);
    }

    private void UpdatePrompt()
    {
        if (promptCanvasGroup == null || promptText == null)
        {
            return;
        }

        bool shouldShow = hoveredWeapon != null;
        promptCanvasGroup.alpha = shouldShow ? 1f : 0f;

        if (!shouldShow)
        {
            return;
        }

        string actionText = currentWeapon == null ? "Pick up" : "Swap weapon";
        promptText.text = $"<b>[Press E]</b> {actionText} <color=#9FE8FF>{hoveredWeapon.WeaponDisplayName}</color>";
    }

    private static bool WasInteractPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.eKey.wasPressedThisFrame;
        }
#endif
        return Input.GetKeyDown(KeyCode.E);
    }

    private bool WasDropPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.qKey.wasPressedThisFrame;
        }
#endif
        return Input.GetKeyDown(KeyCode.Q);
    }

    private void GetDropPose(out Vector3 dropPosition, out Quaternion dropRotation)
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
        if (flatForward.sqrMagnitude < 0.001f)
        {
            flatForward = playerCamera.transform.forward.z >= 0f ? Vector3.forward : Vector3.back;
        }

        Vector3 desiredPosition = playerCamera.transform.position + flatForward * dropDistance;
        Vector3 rayOrigin = desiredPosition + Vector3.up * dropGroundCheckHeight;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit, dropGroundCheckDistance, obstructionMask, QueryTriggerInteraction.Ignore))
        {
            dropPosition = groundHit.point + Vector3.up * dropHeightOffset;
        }
        else
        {
            dropPosition = desiredPosition + Vector3.down * 0.5f;
        }

        dropRotation = Quaternion.LookRotation(flatForward, Vector3.up);
    }
}
