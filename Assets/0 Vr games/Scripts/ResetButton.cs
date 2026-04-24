

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ResetButton : MonoBehaviour
{
    [Tooltip("The MixingZone this button clears")]
    public MixingZone mixingZone;

    [Tooltip("Optional visual press effect transform (scales down on press)")]
    public Transform buttonVisual;

    private XRSimpleInteractable _interactable;
    private Vector3 _originalScale;

    private void Awake()
    {
        if (buttonVisual != null)
            _originalScale = buttonVisual.localScale;

        _interactable = GetComponent<XRSimpleInteractable>();
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
        TriggerReset();

        if (buttonVisual != null)
            buttonVisual.localScale = _originalScale * 0.88f;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (buttonVisual != null)
            buttonVisual.localScale = _originalScale;
    }

    /// <summary>
    /// Public so a Unity UI Button OnClick can also call this.
    /// </summary>
    public void TriggerReset()
    {
        if (mixingZone != null)
            mixingZone.ResetZone();
        else
            Debug.LogWarning("[ResetButton] No MixingZone assigned!");
    }
}