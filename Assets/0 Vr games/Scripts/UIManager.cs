// UIManager.cs
// Manages the world-space molecule discovery panel.
// Shows: "2H + 1O = H₂O" style text, tick/checkmark image per molecule.
// Subscribes to MixingZone.OnMixResult to update in real time.
// Attach to a World-Space Canvas GameObject in your scene.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class MoleculeUIEntry
{
    [Tooltip("Molecule name, must match MoleculeDatabase recipe name exactly")]
    public string moleculeName;

    [Tooltip("TMP text showing formula line, e.g. '2H + 1O = H₂O'")]
    public TextMeshProUGUI formulaText;

    [Tooltip("Tick/checkmark Image component — enabled when molecule is discovered")]
    public Image tickImage;

    [HideInInspector] public bool discovered = false;
}

public class UIManager : MonoBehaviour
{
    [Header("Discovery Panel Entries")]
    [Tooltip("Add one entry per molecule. Match moleculeName to MoleculeDatabase.")]
    public List<MoleculeUIEntry> moleculeEntries = new List<MoleculeUIEntry>();

    [Header("Status Display")]
    [Tooltip("Large status TMP text — shows mix result messages")]
    public TextMeshProUGUI statusText;

    [Tooltip("How long the status message stays visible (seconds)")]
    public float statusDisplayDuration = 3f;

    [Header("Colors")]
    public Color successColor = new Color(0.2f, 1f, 0.4f);
    public Color errorColor = new Color(1f, 0.3f, 0.3f);
    public Color infoColor = Color.white;

    // Lookup by molecule name
    private Dictionary<string, MoleculeUIEntry> _entryLookup;
    private Coroutine _statusClearCoroutine;

    private void Awake()
    {
        BuildLookup();
        InitUI();
    }

    private void OnEnable()
    {
        MixingZone.OnMixResult += HandleMixResult;
    }

    private void OnDisable()
    {
        MixingZone.OnMixResult -= HandleMixResult;
    }

    private void BuildLookup()
    {
        _entryLookup = new Dictionary<string, MoleculeUIEntry>();
        foreach (var entry in moleculeEntries)
        {
            if (!string.IsNullOrEmpty(entry.moleculeName))
                _entryLookup[entry.moleculeName] = entry;
        }
    }

    private void InitUI()
    {
        // Set all formula texts and hide all ticks at start
        foreach (var entry in moleculeEntries)
        {
            if (entry.tickImage != null)
                entry.tickImage.enabled = false;

            // Formula text is set via Inspector (pre-filled) or you can set it here:
            // if (entry.formulaText != null)
            //     entry.formulaText.text = entry.formulaDisplayText;
        }

        SetStatus("Drag atoms into the mixing zone, then press MIX!", infoColor);
    }

    // ─── Mix Result Handler ───────────────────────────────────────────────────

    private void HandleMixResult(MoleculeRecipe recipe, bool success)
    {
        if (!success || recipe == null)
        {
            SetStatus("❌ No valid molecule for this combination. Try again!", errorColor);
            return;
        }

        // Show success status
        SetStatus($"✅ {recipe.moleculeName} created!\n{recipe.discoveryText}", successColor);

        // Mark molecule as discovered in the panel
        if (_entryLookup.TryGetValue(recipe.moleculeName, out MoleculeUIEntry entry))
        {
            if (!entry.discovered)
            {
                entry.discovered = true;

                // Enable tick image
                if (entry.tickImage != null)
                    entry.tickImage.enabled = true;

                // Optionally animate the tick (add DOTween here if available)
                // entry.tickImage.transform.DOPunchScale(Vector3.one * 0.3f, 0.4f);

                Debug.Log($"[UIManager] Discovered: {recipe.moleculeName}");
            }
        }
        else
        {
            Debug.LogWarning($"[UIManager] No UI entry found for molecule: {recipe.moleculeName}");
        }
    }

    // ─── Status Text ──────────────────────────────────────────────────────────

    private void SetStatus(string message, Color color)
    {
        if (statusText == null) return;

        statusText.text = message;
        statusText.color = color;

        // Clear previous auto-clear timer
        if (_statusClearCoroutine != null)
            StopCoroutine(_statusClearCoroutine);

        _statusClearCoroutine = StartCoroutine(ClearStatusAfterDelay(statusDisplayDuration));
    }

    private System.Collections.IEnumerator ClearStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (statusText != null)
        {
            statusText.text = "Drag atoms into the mixing zone, then press MIX!";
            statusText.color = infoColor;
        }
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns how many unique molecules have been discovered so far.
    /// </summary>
    public int GetDiscoveredCount()
    {
        int count = 0;
        foreach (var e in moleculeEntries)
            if (e.discovered) count++;
        return count;
    }

    /// <summary>
    /// Resets all discovery state (for a full session reset).
    /// </summary>
    public void ResetAllDiscoveries()
    {
        foreach (var entry in moleculeEntries)
        {
            entry.discovered = false;
            if (entry.tickImage != null)
                entry.tickImage.enabled = false;
        }
        SetStatus("Session reset. Start discovering molecules!", infoColor);
    }
}