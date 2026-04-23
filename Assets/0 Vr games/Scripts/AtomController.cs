// AtomController.cs
// Attach this to every atom prefab (H, O, C, N spheres).
// Works with XR Interaction Toolkit's XRGrabInteractable.
// Handles: atom identity, grab events, mixing zone entry/exit, visual highlight, pool callback.
//
// FIX SUMMARY:
//   - Spawned atoms now use isKinematic=true so they float at spawn point cleanly.
//   - On grab: isKinematic=false + useGravity=true applied ONE FRAME after XRI
//     takes control (via coroutine), so XRI's internal state snapshot cannot undo us.
//   - On release: same one-frame-delay coroutine re-applies useGravity=true +
//     isKinematic=false AFTER XRI restores its cached snapshot.
//   - Removed the per-frame Update() hack that was fighting XRI every tick.
//   - ForceResetFromPool correctly restores the idle kinematic state.

using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public class AtomController : MonoBehaviour
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Atom Identity")]
    [Tooltip("Select which element this atom represents")]
    public AtomType atomType;

    [Header("Visual")]
    [Tooltip("Renderer to tint when highlighted")]
    public Renderer atomRenderer;

    [Tooltip("Default material color (set automatically from prefab on first enable)")]
    public Color defaultColor = Color.white;

    [Tooltip("Highlight color when grabbed or inside mixing zone")]
    public Color highlightColor = Color.yellow;

    // ─── Internal State ──────────────────────────────────────────────────────────

    // Set by AtomPool — used to call back when grabbed from spawn point
    [HideInInspector] public AtomPool ownerPool;

    private bool _isIdleAtSpawnPoint = false;
    private bool _isGrabbed          = false;
    private MixingZone _currentMixingZone = null;

    private XRGrabInteractable  _grabInteractable;
    private Rigidbody           _rigidbody;
    private MaterialPropertyBlock _propBlock;

    // ─── Unity Lifecycle ─────────────────────────────────────────────────────────

    private void Awake()
    {
        _grabInteractable = GetComponent<XRGrabInteractable>();
        _rigidbody        = GetComponent<Rigidbody>();
        _propBlock        = new MaterialPropertyBlock();

        // Cache the prefab's base color once
        if (atomRenderer != null)
            defaultColor = atomRenderer.sharedMaterial.color;

        // Subscribe to XRI events
        _grabInteractable.selectEntered.AddListener(OnGrabbed);
        _grabInteractable.selectExited.AddListener(OnReleased);
    }

    private void OnDestroy()
    {
        _grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        _grabInteractable.selectExited.RemoveListener(OnReleased);
    }

    // ─── Pool API ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by AtomPool.ActivateAtom() after positioning.
    /// Sets the atom into the "idle at spawn point" state:
    ///   isKinematic = true  → floats in place, no physics drift
    ///   useGravity  = false → obviously no falling
    /// XRGrabInteractable works fine with a kinematic Rigidbody for pickup;
    /// it will switch to non-kinematic itself during grab tracking.
    /// </summary>
    public void SetIdleAtSpawnPoint(bool idle)
    {
        _isIdleAtSpawnPoint = idle;

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;   // Float at spawn — XRI can grab kinematic objects
            _rigidbody.useGravity  = false;
            _rigidbody.linearVelocity  = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// Called by AtomPool when recycling an atom that may still be active/grabbed.
    /// Fully resets physics and visual state before the pool re-activates it.
    /// </summary>
    public void ForceResetFromPool()
    {
        // Cancel any pending coroutines so stale callbacks don't fire
        StopAllCoroutines();

        _currentMixingZone  = null;
        _isGrabbed          = false;
        _isIdleAtSpawnPoint = false;

        SetHighlight(false);

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;    // Will be set to idle properly by ActivateAtom → SetIdleAtSpawnPoint
            _rigidbody.useGravity  = false;
            _rigidbody.linearVelocity  = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// Called by MixingZone when a reaction completes.
    /// Disables the atom and returns it to the pool's inactive list.
    /// </summary>
    public void RecycleToPool()
    {
        StopAllCoroutines();

        _currentMixingZone  = null;
        _isGrabbed          = false;

        SetHighlight(false);

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity  = false;
            _rigidbody.linearVelocity  = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        gameObject.SetActive(false);
    }

    // ─── XR Grab Events ──────────────────────────────────────────────────────────

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        _isGrabbed = true;
        SetHighlight(true);

        // Notify pool to spawn a replacement
        if (_isIdleAtSpawnPoint && ownerPool != null)
        {
            _isIdleAtSpawnPoint = false;
            ownerPool.OnAtomGrabbed();
        }

        // KEY FIX: XRI snapshots and overrides Rigidbody state during selectEntered.
        // We apply our physics settings ONE frame later so XRI's snapshot is already done
        // and our values stick without being overwritten.
        StartCoroutine(ApplyGrabbedPhysics());
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        _isGrabbed = false;
        SetHighlight(false);

        // KEY FIX: XRI restores its cached Rigidbody snapshot on selectExited,
        // which would re-enable isKinematic or disable gravity.
        // Wait one frame for XRI to finish restoring, then apply our desired state.
        StartCoroutine(ApplyReleasedPhysics());
    }

    // ─── Physics Coroutines (the core fix) ───────────────────────────────────────

    /// <summary>
    /// Waits one frame after XRI grabs the atom, then sets physics for "being held":
    ///   isKinematic = false  (XRI needs this for velocity/position tracking)
    ///   useGravity  = true   (so it falls if XRI releases tracking)
    /// </summary>
    private IEnumerator ApplyGrabbedPhysics()
    {
        yield return null; // skip one frame — XRI finishes its own Rigidbody setup

        if (_rigidbody != null && _isGrabbed)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity  = true;
        }
    }

    /// <summary>
    /// Waits one frame after XRI releases the atom, then sets physics for "falling":
    ///   isKinematic = false  (must be false so gravity and collisions work)
    ///   useGravity  = true   (atom drops naturally after release)
    /// </summary>
    private IEnumerator ApplyReleasedPhysics()
    {
        yield return null; // skip one frame — XRI finishes its own Rigidbody restore

        if (_rigidbody != null && !_isGrabbed)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity  = true;
        }
    }

    // ─── Mixing Zone Triggers ─────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        MixingZone zone = other.GetComponent<MixingZone>();
        if (zone != null && _currentMixingZone == null)
        {
            _currentMixingZone = zone;
            zone.OnAtomEntered(this);
            SetHighlight(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        MixingZone zone = other.GetComponent<MixingZone>();
        if (zone != null && _currentMixingZone == zone)
        {
            zone.OnAtomExited(this);
            _currentMixingZone = null;
            SetHighlight(_isGrabbed); // keep highlight if still grabbed
        }
    }

    // ─── Visual ───────────────────────────────────────────────────────────────────

    private void SetHighlight(bool on)
    {
        if (atomRenderer == null) return;
        atomRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor("_BaseColor", on ? highlightColor : defaultColor);
        atomRenderer.SetPropertyBlock(_propBlock);
    }
}