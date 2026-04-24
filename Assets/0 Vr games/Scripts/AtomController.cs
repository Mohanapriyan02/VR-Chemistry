

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

    [Header("Visual — Material Swap")]
    [Tooltip("Renderer on the atom sphere")]
    public Renderer atomRenderer;

    [Tooltip("Default (idle) material — assigned automatically from the prefab's current material on Awake")]
    public Material defaultMaterial;

    [Tooltip("Glow material applied when the atom is grabbed or inside a mixing zone")]
    public Material glowMaterial;

    // ─── Internal State ──────────────────────────────────────────────────────────

    [HideInInspector] public AtomPool ownerPool;

    private bool _isIdleAtSpawnPoint = false;
    private bool _isGrabbed          = false;
    private bool _isInMixZone        = false;
    private MixingZone _currentMixingZone = null;

    private XRGrabInteractable _grabInteractable;
    private Rigidbody          _rigidbody;

    // ─── Unity Lifecycle ─────────────────────────────────────────────────────────

    private void Awake()
    {
        _grabInteractable = GetComponent<XRGrabInteractable>();
        _rigidbody        = GetComponent<Rigidbody>();

        // Cache the prefab's base material once if not already assigned in Inspector
        if (defaultMaterial == null && atomRenderer != null)
            defaultMaterial = atomRenderer.sharedMaterial;

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
    /// Called by AtomPool.ActivateAtom() — sets atom into idle floating state.
    /// </summary>
    public void SetIdleAtSpawnPoint(bool idle)
    {
        _isIdleAtSpawnPoint = idle;

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity  = false;
            _rigidbody.linearVelocity  = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        SetGlow(false);
    }

    /// <summary>
    /// Called by AtomPool when forcibly recycling an active/grabbed atom.
    /// </summary>
    public void ForceResetFromPool()
    {
        StopAllCoroutines();

        _currentMixingZone  = null;
        _isGrabbed          = false;
        _isInMixZone        = false;
        _isIdleAtSpawnPoint = false;

        SetGlow(false);

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity  = false;
            _rigidbody.linearVelocity  = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// Called by MixingZone when a reaction completes — disables and returns to pool.
    /// </summary>
    public void RecycleToPool()
    {
        StopAllCoroutines();

        _currentMixingZone  = null;
        _isGrabbed          = false;
        _isInMixZone        = false;

        SetGlow(false);

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
        UpdateGlowState();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGrab();

        if (_isIdleAtSpawnPoint && ownerPool != null)
        {
            _isIdleAtSpawnPoint = false;
            ownerPool.OnAtomGrabbed();
        }

        StartCoroutine(ApplyGrabbedPhysics());
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        _isGrabbed = false;
        UpdateGlowState();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayRelease();

        StartCoroutine(ApplyReleasedPhysics());
    }

    // ─── Physics Coroutines ───────────────────────────────────────────────────────

    private IEnumerator ApplyGrabbedPhysics()
    {
        yield return null;
        if (_rigidbody != null && _isGrabbed)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity  = true;
        }
    }

    private IEnumerator ApplyReleasedPhysics()
    {
        yield return null;
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
            // Zone will reject if full — only update state if accepted
            bool accepted = zone.OnAtomEntered(this);
            if (accepted)
            {
                _currentMixingZone = zone;
                _isInMixZone = true;
                UpdateGlowState();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        MixingZone zone = other.GetComponent<MixingZone>();
        if (zone != null && _currentMixingZone == zone)
        {
            zone.OnAtomExited(this);
            _currentMixingZone = null;
            _isInMixZone = false;
            UpdateGlowState();
        }
    }

    // ─── Material / Glow ─────────────────────────────────────────────────────────

    /// <summary>
    /// Glow is ON when grabbed OR inside a mixing zone.
    /// Glow is OFF when neither (idle or dropped outside zone).
    /// </summary>
    private void UpdateGlowState()
    {
        SetGlow(_isGrabbed || _isInMixZone);
    }

    private void SetGlow(bool on)
    {
        if (atomRenderer == null) return;

        Material target = on ? glowMaterial : defaultMaterial;
        if (target != null)
            atomRenderer.material = target;
    }
}