using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Generic networked interactable state machine for doors, lights, levers, etc.
/// Attach to any GameObject that needs synchronized state across all clients.
/// </summary>
public class InteractableState : NetworkBehaviour
{
    // ─────────────────────────────────────────────
    //  Inspector Configuration
    // ─────────────────────────────────────────────

    [Header("─── Interaction Settings ───")]
    [Tooltip("Tag required on the player to interact")]
    [SerializeField] private string playerTag = "Player";

    // ── Position / Rotation Transform ──
    [Header("─── Transform Animation ───")]
    [Tooltip("Enable to move/rotate an object between two states")]
    [SerializeField] private bool useTransformAnimation = false;

    [Tooltip("The object that will move or rotate (can be this object or a child)")]
    [SerializeField] private Transform animatedTransform;

    [Tooltip("Enable to change the position of the animated object")]
    [SerializeField] private bool animatePosition = false;

    [Tooltip("Target LOCAL position when the interactable is OPEN/ON")]
    [SerializeField] private Vector3 openPosition = Vector3.zero;

    [Tooltip("Base LOCAL position when the interactable is CLOSED/OFF")]
    [SerializeField] private Vector3 closedPosition = Vector3.zero;

    [Tooltip("Enable to change the rotation of the animated object")]
    [SerializeField] private bool animateRotation = false;

    [Tooltip("Target LOCAL euler rotation when the interactable is OPEN/ON")]
    [SerializeField] private Vector3 openRotation = Vector3.zero;

    [Tooltip("Base LOCAL euler rotation when the interactable is CLOSED/OFF")]
    [SerializeField] private Vector3 closedRotation = Vector3.zero;

    [Tooltip("Duration in seconds of the position/rotation transition")]
    [SerializeField] private float animationDuration = 1f;

    // ── Collider (Door) ──
    [Header("─── Collider (e.g. Door Blocker) ───")]
    [Tooltip("Enable to toggle a collider when activated (e.g. door physics blocker)")]
    [SerializeField] private bool useColliderToggle = false;

    [Tooltip("The collider to enable/disable (enabled = CLOSED, disabled = OPEN)")]
    [SerializeField] private Collider toggledCollider;

    // ── Light ──
    [Header("─── Light Toggle ───")]
    [Tooltip("Enable to toggle a Light component")]
    [SerializeField] private bool useLightToggle = false;

    [Tooltip("The Light component to toggle")]
    [SerializeField] private Light toggledLight;

    // ─────────────────────────────────────────────
    //  Network State
    // ─────────────────────────────────────────────

    /// <summary>
    /// Networked state: false = closed/off, true = open/on.
    /// All clients react to changes via OnStateChanged.
    /// </summary>
    private NetworkVariable<bool> _isActivated = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ─────────────────────────────────────────────
    //  Private Runtime
    // ─────────────────────────────────────────────

    private bool _playerInTrigger = false;
    private bool _isAnimating = false;
    private Coroutine _animCoroutine;

    // ─────────────────────────────────────────────
    //  Unity & Netcode Lifecycle
    // ─────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        // Subscribe so every client (including late joiners) syncs state
        _isActivated.OnValueChanged += OnStateChanged;

        // Apply the current state immediately for late joiners
        ApplyState(_isActivated.Value, instant: true);
    }

    public override void OnNetworkDespawn()
    {
        _isActivated.OnValueChanged -= OnStateChanged;
    }

    // ─────────────────────────────────────────────
    //  Trigger Detection (Player Proximity)
    // ─────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        // Only care about the LOCAL player's collider
        if (!IsLocalPlayer(other.gameObject)) return;

        _playerInTrigger = true;
        // Optional: show UI prompt here
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (!IsLocalPlayer(other.gameObject)) return;

        _playerInTrigger = false;
    }

    // ─────────────────────────────────────────────
    //  Public API — called by PlayerInteraction.cs
    // ─────────────────────────────────────────────

    /// <summary>
    /// Returns true if the local player is inside the trigger zone.
    /// PlayerInteraction polls this before sending the interact command.
    /// </summary>
    public bool CanInteract() => _playerInTrigger && !_isAnimating;

    /// <summary>
    /// Returns true when this interactable can be triggered by a camera-centered look ray.
    /// </summary>
    public bool CanInteractFromLook() => !_isAnimating;

    /// <summary>
    /// Called by the local PlayerInteraction when the player presses the action button.
    /// Routes the request to the server via RPC.
    /// </summary>
    public void Interact()
    {
        if (!CanInteract()) return;
        RequestInteractServerRpc();
    }

    /// <summary>
    /// Called by the local PlayerInteraction when the player presses the action button while looking at this object.
    /// </summary>
    public void InteractFromLook()
    {
        if (!CanInteractFromLook()) return;
        RequestInteractServerRpc();
    }

    // ─────────────────────────────────────────────
    //  Server RPC — Authority Logic
    // ─────────────────────────────────────────────

    [ServerRpc(RequireOwnership = false)]
    private void RequestInteractServerRpc()
    {
        // Toggle the networked state; OnValueChanged fires on all clients
        _isActivated.Value = !_isActivated.Value;
    }

    // ─────────────────────────────────────────────
    //  State Change — Runs on ALL Clients
    // ─────────────────────────────────────────────

    private void OnStateChanged(bool previousValue, bool newValue)
    {
        ApplyState(newValue, instant: false);
    }

    /// <summary>
    /// Applies the visual/physical state on the local machine.
    /// </summary>
    /// <param name="activated">True = open / on state.</param>
    /// <param name="instant">Skip animation (used on spawn sync).</param>
    private void ApplyState(bool activated, bool instant)
    {
        // ── Transform animation ──
        if (useTransformAnimation && animatedTransform != null)
        {
            Vector3 targetPos = activated ? openPosition : closedPosition;
            Vector3 targetRot = activated ? openRotation : closedRotation;

            if (instant)
            {
                if (animatePosition) animatedTransform.localPosition = targetPos;
                if (animateRotation) animatedTransform.localEulerAngles = targetRot;
            }
            else
            {
                if (_animCoroutine != null) StopCoroutine(_animCoroutine);
                _animCoroutine = StartCoroutine(AnimateTransform(targetPos, targetRot));
            }
        }

        // ── Collider toggle (door blocker) ──
        if (useColliderToggle && toggledCollider != null)
        {
            // Collider ON = closed/blocked; OFF = open/passable
            toggledCollider.enabled = !activated;
        }

        // ── Light toggle ──
        if (useLightToggle && toggledLight != null)
        {
            toggledLight.enabled = activated;
        }
    }

    // ─────────────────────────────────────────────
    //  Animation Coroutine
    // ─────────────────────────────────────────────

    private IEnumerator AnimateTransform(Vector3 targetPos, Vector3 targetRot)
    {
        _isAnimating = true;

        Vector3 startPos = animatedTransform.localPosition;
        Quaternion startRot = animatedTransform.localRotation;
        Quaternion endRot   = Quaternion.Euler(targetRot);

        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animationDuration);

            if (animatePosition)
                animatedTransform.localPosition = Vector3.Lerp(startPos, targetPos, t);

            if (animateRotation)
                animatedTransform.localRotation = Quaternion.Slerp(startRot, endRot, t);

            yield return null;
        }

        // Snap to exact target to avoid floating-point drift
        if (animatePosition) animatedTransform.localPosition = targetPos;
        if (animateRotation) animatedTransform.localEulerAngles = targetRot;

        _isAnimating = false;
    }

    // ─────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────

    /// <summary>
    /// Checks if the given GameObject belongs to the LOCAL player.
    /// Works with both a single-object player and a player with child colliders.
    /// </summary>
    private bool IsLocalPlayer(GameObject obj)
    {
        // Try direct NetworkObject check
        var netObj = obj.GetComponent<NetworkObject>();
        if (netObj != null) return netObj.IsLocalPlayer;

        // Try parent hierarchy (for child trigger colliders on the player)
        netObj = obj.GetComponentInParent<NetworkObject>();
        if (netObj != null) return netObj.IsLocalPlayer;

        return false;
    }

    // ─────────────────────────────────────────────
    //  Editor Helper — Capture current transform
    // ─────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("📌 Capture CLOSED values from current transform")]
    private void CaptureClosedState()
    {
        if (animatedTransform == null) { Debug.LogWarning("Assign animatedTransform first."); return; }
        closedPosition = animatedTransform.localPosition;
        closedRotation = animatedTransform.localEulerAngles;
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[InteractableState] Closed state captured → pos:{closedPosition} rot:{closedRotation}");
    }

    [ContextMenu("📌 Capture OPEN values from current transform")]
    private void CaptureOpenState()
    {
        if (animatedTransform == null) { Debug.LogWarning("Assign animatedTransform first."); return; }
        openPosition = animatedTransform.localPosition;
        openRotation = animatedTransform.localEulerAngles;
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[InteractableState] Open state captured → pos:{openPosition} rot:{openRotation}");
    }
#endif
}
