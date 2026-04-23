// AtomController.cs
// Attach this to every atom prefab (H, O, C, N spheres).
// Works with XR Interaction Toolkit's XRGrabInteractable.
// Handles: atom identity, grab events, mixing zone entry/exit, visual highlight, pool callback.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class AtomController : MonoBehaviour
{
    [Header("Atom Identity")]
    [Tooltip("Select which element this atom represents")]
    public AtomType atomType;

    [Header("Visual")]
    [Tooltip("Renderer to tint when highlighted")]
    public Renderer atomRenderer;

    [Tooltip("Default material color (set automatically from prefab)")]
    public Color defaultColor = Color.white;

    [Tooltip("Highlight color when grabbed or inside mixing zone")]
    public Color highlightColor = Color.yellow;

    // Reference back to the pool that owns this atom (set by AtomPool)
    [HideInInspector] public AtomPool ownerPool;

    // Whether this atom is the idle one sitting at the spawn point
    private bool _isIdleAtSpawnPoint = false;

    // Whether this atom is currently inside a mixing zone
    private MixingZone _currentMixingZone = null;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grabInteractable;
    private MaterialPropertyBlock _propBlock;

    private void Awake()
    {
        _grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        _propBlock = new MaterialPropertyBlock();

        // Cache default color from renderer
        if (atomRenderer != null)
        {
            atomRenderer.GetPropertyBlock(_propBlock);
            defaultColor = atomRenderer.material.color;
        }

        // Subscribe to XR grab/release events
        _grabInteractable.selectEntered.AddListener(OnGrabbed);
        _grabInteractable.selectExited.AddListener(OnReleased);
    }

    private void OnDestroy()
    {
        _grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        _grabInteractable.selectExited.RemoveListener(OnReleased);
    }

    // Called when this atom is first placed at the spawn point (idle state)
    public void SetIdleAtSpawnPoint(bool idle)
    {
        _isIdleAtSpawnPoint = idle;
    }

    // ─── XR Grab Events ────────────────────────────────────────────────────────

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        SetHighlight(true);

        // If this was the idle spawn-point atom, notify pool to spawn another
        if (_isIdleAtSpawnPoint && ownerPool != null)
        {
            _isIdleAtSpawnPoint = false;
            ownerPool.OnAtomGrabbed();
        }
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        SetHighlight(false);
    }

    // ─── Mixing Zone Trigger ────────────────────────────────────────────────────

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
            SetHighlight(false);
        }
    }

    // ─── Visual ────────────────────────────────────────────────────────────────

    private void SetHighlight(bool on)
    {
        if (atomRenderer == null) return;
        atomRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor("_BaseColor", on ? highlightColor : defaultColor);
        atomRenderer.SetPropertyBlock(_propBlock);
    }

    /// <summary>
    /// Called by MixingZone when a mix is complete.
    /// Disables this atom and returns it to the pool.
    /// </summary>
    public void RecycleToPool()
    {
        _currentMixingZone = null;
        SetHighlight(false);
        if (ownerPool != null)
            ownerPool.ReturnAtom(gameObject);
        else
            gameObject.SetActive(false);
    }
}