using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Attach this to the Player prefab.
/// Detects nearby InteractableState objects and triggers them on action input.
/// Only runs logic for the LOCAL player (IsOwner check).
/// </summary>
public class PlayerInteraction : NetworkBehaviour
{
    // ─────────────────────────────────────────────
    //  Inspector Configuration
    // ─────────────────────────────────────────────

    [Header("─── Input ───")]
    [Tooltip("Key the player presses to interact")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("─── UI Prompt (optional) ───")]
    [Tooltip("Optional GameObject shown when an interaction is available (e.g. 'Press E')")]
    [SerializeField] private GameObject interactPromptUI;

    // ─────────────────────────────────────────────
    //  Private Runtime
    // ─────────────────────────────────────────────

    // All InteractableState triggers the player is currently inside
    // (one player can overlap multiple triggers simultaneously)
    private System.Collections.Generic.List<InteractableState> _nearbyInteractables
        = new System.Collections.Generic.List<InteractableState>();

    // ─────────────────────────────────────────────
    //  Update Loop
    // ─────────────────────────────────────────────

    private void Update()
    {
        // Only the local player handles input
        if (!IsOwner) return;

        // Clean up destroyed interactables from the list
        _nearbyInteractables.RemoveAll(i => i == null);

        bool canInteractWithAny = _nearbyInteractables.Exists(i => i.CanInteract());

        // Show/hide UI prompt
        if (interactPromptUI != null)
            interactPromptUI.SetActive(canInteractWithAny);

        // Interact on key press
        if (canInteractWithAny && Input.GetKeyDown(interactKey))
        {
            // Interact with the first available interactable
            // If you want priority logic (closest, etc.) sort the list here
            foreach (var interactable in _nearbyInteractables)
            {
                if (interactable.CanInteract())
                {
                    interactable.Interact();
                    break; // Only one at a time
                }
            }
        }
    }

    // ─────────────────────────────────────────────
    //  Trigger Registration
    //  The InteractableState's trigger collider fires these
    //  BUT we also detect here so PlayerInteraction knows which
    //  interactables are nearby without depending on callbacks from them.
    //
    //  NOTE: If your player has a separate collider child, move this
    //  script there or use Physics.OverlapSphere instead.
    // ─────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner) return;

        var interactable = other.GetComponent<InteractableState>();
        if (interactable == null)
            interactable = other.GetComponentInParent<InteractableState>();

        if (interactable != null && !_nearbyInteractables.Contains(interactable))
            _nearbyInteractables.Add(interactable);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsOwner) return;

        var interactable = other.GetComponent<InteractableState>();
        if (interactable == null)
            interactable = other.GetComponentInParent<InteractableState>();

        if (interactable != null)
            _nearbyInteractables.Remove(interactable);
    }

    // ─────────────────────────────────────────────
    //  Cleanup on despawn
    // ─────────────────────────────────────────────

    public override void OnNetworkDespawn()
    {
        _nearbyInteractables.Clear();
        if (interactPromptUI != null)
            interactPromptUI.SetActive(false);
    }
}
