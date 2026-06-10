using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Handles camera-centered interactions with InteractableState objects for the local player.
/// </summary>
public class PlayerInteraction : NetworkBehaviour
{
    [Header("Input")]
    [Tooltip("Key the player presses to interact when using the legacy input manager.")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Raycast")]
    [Tooltip("Maximum distance from the camera center to detect interactables.")]
    [SerializeField] private float interactDistance = 3f;

    [Tooltip("Physics layers checked by the center-camera interaction ray.")]
    [SerializeField] private LayerMask interactableLayers = ~0;

    [Header("UI Prompt")]
    [Tooltip("Optional GameObject shown when an interaction is available.")]
    [SerializeField] private GameObject interactPromptUI;

    [Header("Center Marker")]
    [Tooltip("Optional sprite shown at screen center when the camera is targeting an interactable.")]
    [SerializeField] private Sprite centerMarkerSprite;

    [Tooltip("Size in pixels for the temporary center marker.")]
    private Vector2 centerMarkerSize = new Vector2(200f, 200f);

    private InteractableState lookedInteractable;
    private Canvas centerMarkerCanvas;
    private Image centerMarkerImage;

    private void Update()
    {
        if (!IsOwner)
        {
            HideInteractionUI();
            return;
        }

        EnsureCenterMarkerUI();
        lookedInteractable = FindLookedInteractable();

        bool canInteract = lookedInteractable != null && lookedInteractable.CanInteractFromLook();

        if (interactPromptUI != null)
        {
            interactPromptUI.SetActive(canInteract);
        }

        if (centerMarkerImage != null)
        {
            centerMarkerImage.enabled = canInteract;
        }

        if (canInteract && WasInteractPressedThisFrame())
        {
            lookedInteractable.InteractFromLook();
        }
    }

    public override void OnNetworkDespawn()
    {
        HideInteractionUI();
        DestroyCenterMarkerUI();
        lookedInteractable = null;
    }

    private void OnDisable()
    {
        HideInteractionUI();
    }

    private InteractableState FindLookedInteractable()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return null;
        }

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            interactDistance,
            interactableLayers,
            QueryTriggerInteraction.Collide);

        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            InteractableState interactable = hit.collider.GetComponent<InteractableState>();
            interactable = interactable != null ? interactable : hit.collider.GetComponentInParent<InteractableState>();

            if (interactable != null && interactable.CanInteractFromLook())
            {
                return interactable;
            }
        }

        return null;
    }

    private void EnsureCenterMarkerUI()
    {
        if (centerMarkerImage != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject($"InteractableCenterMarker_{OwnerClientId}");
        centerMarkerCanvas = canvasObject.AddComponent<Canvas>();
        centerMarkerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        centerMarkerCanvas.sortingOrder = 500;

        CanvasScaler canvasScaler = canvasObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject markerObject = new GameObject("Marker");
        markerObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rectTransform = markerObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = centerMarkerSize;

        centerMarkerImage = markerObject.AddComponent<Image>();
        centerMarkerImage.sprite = centerMarkerSprite;
        centerMarkerImage.color = Color.white;
        centerMarkerImage.raycastTarget = false;
        centerMarkerImage.enabled = false;
    }

    private void HideInteractionUI()
    {
        if (interactPromptUI != null)
        {
            interactPromptUI.SetActive(false);
        }

        if (centerMarkerImage != null)
        {
            centerMarkerImage.enabled = false;
        }
    }

    private void DestroyCenterMarkerUI()
    {
        if (centerMarkerCanvas != null)
        {
            Destroy(centerMarkerCanvas.gameObject);
        }

        centerMarkerCanvas = null;
        centerMarkerImage = null;
    }

    private bool WasInteractPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.eKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(interactKey);
#else
        return false;
#endif
    }
}
