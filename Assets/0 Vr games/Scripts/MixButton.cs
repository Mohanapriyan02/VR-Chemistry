// MixButton.cs
// Attach to a world-space Button (or any XR interactable) to trigger mixing.
// Works with XR Interaction Toolkit's XRSimpleInteractable or a UI Button's OnClick.
// For a 3D physical button: add XRSimpleInteractable + this script.
// For a world-space UI Button: call TriggerMix() from the Button's OnClick event.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class MixButton : MonoBehaviour
{
    [Tooltip("The MixingZone this button triggers")]
    public MixingZone mixingZone;

    [Tooltip("Optional visual press effect transform (scales down on press)")]
    public Transform buttonVisual;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable _interactable;
    private Vector3 _originalScale;

    private void Awake()
    {
        if (buttonVisual != null)
            _originalScale = buttonVisual.localScale;

        _interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        if (_interactable != null)
        {
            _interactable.selectEntered.AddListener(OnPressed);
            _interactable.selectExited.AddListener(OnReleased);
        }
    }

    private void OnDestroy()
    {
        if (_interactable != null)
        {
            _interactable.selectEntered.RemoveListener(OnPressed);
            _interactable.selectExited.RemoveListener(OnReleased);
        }
    }

    private void OnPressed(SelectEnterEventArgs args)
    {
        TriggerMix();

        // Press visual
        if (buttonVisual != null)
            buttonVisual.localScale = _originalScale * 0.88f;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        // Restore visual
        if (buttonVisual != null)
            buttonVisual.localScale = _originalScale;
    }

    /// <summary>
    /// Public so Unity UI Button OnClick can also call this.
    /// </summary>
    public void TriggerMix()
    {
        if (mixingZone != null)
            mixingZone.TryMix();
        else
            Debug.LogWarning("[MixButton] No MixingZone assigned!");
    }
}