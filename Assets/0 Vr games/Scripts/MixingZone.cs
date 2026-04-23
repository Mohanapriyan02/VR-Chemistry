// MixingZone.cs
// Attach to the glass box GameObject. Set its Collider to "Is Trigger".
// Tracks which atoms are inside, validates recipes on Mix button press,
// recycles atoms back to pools, and enables/spawns the correct molecule.

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MoleculePoolEntry
{
    [Tooltip("Must match the moleculeName in MoleculeDatabase recipe exactly")]
    public string moleculeName;

    [Tooltip("The result molecule GameObject (initially disabled in the scene or as pooled prefab)")]
    public GameObject moleculeObject;

    [HideInInspector] public bool isInUse = false;
}

public class MixingZone : MonoBehaviour
{
    [Header("Database")]
    [Tooltip("Assign the MoleculeDatabase ScriptableObject here")]
    public MoleculeDatabase moleculeDatabase;

    [Header("Molecule Result Pool")]
    [Tooltip("Map every molecule name to its pooled result GameObject. " +
             "Name must exactly match moleculeName in MoleculeDatabase.")]
    public List<MoleculePoolEntry> moleculePool = new List<MoleculePoolEntry>();

    [Header("Spawn Settings")]
    [Tooltip("Where the result molecule appears when mixing succeeds")]
    public Transform resultSpawnPoint;

    [Tooltip("Seconds the molecule stays visible before auto-returning to pool (0 = never auto-return)")]
    public float moleculeDisplayDuration = 8f;

    // Atoms currently inside the mixing zone
    private List<AtomController> _atomsInZone = new List<AtomController>();

    // Lookup by name for fast pool access
    private Dictionary<string, MoleculePoolEntry> _poolLookup;

    // Callback to UIManager
    public static System.Action<MoleculeRecipe, bool> OnMixResult;

    private void Awake()
    {
        BuildPoolLookup();

        // Disable all molecule objects at start
        foreach (var entry in moleculePool)
        {
            if (entry.moleculeObject != null)
                entry.moleculeObject.SetActive(false);
        }
    }

    private void BuildPoolLookup()
    {
        _poolLookup = new Dictionary<string, MoleculePoolEntry>();
        foreach (var entry in moleculePool)
        {
            if (!string.IsNullOrEmpty(entry.moleculeName))
                _poolLookup[entry.moleculeName] = entry;
        }
    }

    // ─── Atom Entry / Exit ────────────────────────────────────────────────────

    public void OnAtomEntered(AtomController atom)
    {
        if (!_atomsInZone.Contains(atom))
        {
            _atomsInZone.Add(atom);
            Debug.Log($"[MixingZone] {atom.atomType} entered. Zone has {_atomsInZone.Count} atom(s).");
        }
    }

    public void OnAtomExited(AtomController atom)
    {
        _atomsInZone.Remove(atom);
        Debug.Log($"[MixingZone] {atom.atomType} exited. Zone has {_atomsInZone.Count} atom(s).");
    }

    // ─── Mix Button ───────────────────────────────────────────────────────────

    /// <summary>
    /// Call this from the UI Mix button's OnClick event.
    /// </summary>
    public void TryMix()
    {
        if (_atomsInZone.Count == 0)
        {
            Debug.Log("[MixingZone] No atoms in zone.");
            OnMixResult?.Invoke(null, false);
            return;
        }

        // Build the atom type list from zone contents
        var atomTypes = new List<AtomType>();
        foreach (var atom in _atomsInZone)
            atomTypes.Add(atom.atomType);

        // Look up a valid recipe
        MoleculeRecipe recipe = moleculeDatabase.FindRecipe(atomTypes);

        if (recipe == null)
        {
            // Invalid combination — do NOT remove atoms, just notify UI
            Debug.Log("[MixingZone] No valid molecule for this combination.");
            OnMixResult?.Invoke(null, false);
            return;
        }

        // Valid recipe found — recycle all atoms to their pools
        var atomsToRecycle = new List<AtomController>(_atomsInZone);
        _atomsInZone.Clear();
        foreach (var atom in atomsToRecycle)
            atom.RecycleToPool();

        // Spawn / enable the result molecule from pool
        SpawnMoleculeResult(recipe);

        // Notify UI of success
        OnMixResult?.Invoke(recipe, true);
    }

    // ─── Molecule Pool ────────────────────────────────────────────────────────

    private void SpawnMoleculeResult(MoleculeRecipe recipe)
    {
        if (!_poolLookup.TryGetValue(recipe.moleculeName, out MoleculePoolEntry entry))
        {
            Debug.LogWarning($"[MixingZone] No pool entry for molecule: {recipe.moleculeName}");
            return;
        }

        if (entry.isInUse)
        {
            Debug.Log($"[MixingZone] {recipe.moleculeName} is already displayed. Resetting position.");
            // Reset position of the existing one
        }

        GameObject mol = entry.moleculeObject;
        if (mol == null) return;

        Vector3 spawnPos = resultSpawnPoint != null ? resultSpawnPoint.position : transform.position + Vector3.up * 0.3f;
        mol.transform.position = spawnPos;
        mol.transform.rotation = Quaternion.identity;
        mol.SetActive(true);
        entry.isInUse = true;

        // Optional: Play spawn animation via Animator or DOTween here
        // mol.GetComponent<Animator>()?.SetTrigger("Spawn");

        // Auto-return to pool after display duration
        if (moleculeDisplayDuration > 0f)
            StartCoroutine(ReturnMoleculeAfterDelay(entry, moleculeDisplayDuration));
    }

    private System.Collections.IEnumerator ReturnMoleculeAfterDelay(MoleculePoolEntry entry, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnMoleculeToPool(entry);
    }

    /// <summary>
    /// Call this to manually return a molecule (e.g. player resets it).
    /// </summary>
    public void ReturnMoleculeToPool(MoleculePoolEntry entry)
    {
        if (entry.moleculeObject != null)
            entry.moleculeObject.SetActive(false);
        entry.isInUse = false;
    }

    // ─── Debug Gizmo ─────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.2f);
        Gizmos.DrawCube(transform.position, transform.localScale);
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }
}