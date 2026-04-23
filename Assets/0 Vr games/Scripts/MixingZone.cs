// MixingZone.cs  — FIXED VERSION
// Key fixes:
//   1. Pool lookup rebuilt lazily before every TryMix (catches Inspector-assigned data)
//   2. Detailed Debug.Log at every decision point so you can trace exactly where it fails
//   3. moleculeName comparison is Trim()+ToLower() tolerant — catches space/caps mismatch
//   4. Null checks on moleculeObject before SetActive

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MoleculePoolEntry
{
    [Tooltip("Must EXACTLY match the moleculeName in MoleculeDatabase (case-insensitive, trimmed)")]
    public string moleculeName;

    [Tooltip("Drag the result molecule GameObject here (a scene object, initially disabled)")]
    public GameObject moleculeObject;

    [HideInInspector] public bool isInUse = false;
}

public class MixingZone : MonoBehaviour
{
    [Header("Database")]
    [Tooltip("Assign the MoleculeDatabase ScriptableObject here")]
    public MoleculeDatabase moleculeDatabase;

    [Header("Molecule Result Pool")]
    [Tooltip("One entry per molecule. moleculeName must match MoleculeDatabase exactly.")]
    public List<MoleculePoolEntry> moleculePool = new List<MoleculePoolEntry>();

    [Header("Spawn Settings")]
    [Tooltip("Where the result molecule appears. If null, spawns above the mixing zone.")]
    public Transform resultSpawnPoint;

    [Tooltip("Seconds result stays visible before auto-hiding (0 = never auto-hide)")]
    public float moleculeDisplayDuration = 8f;

    // Atoms currently inside the trigger volume
    private List<AtomController> _atomsInZone = new List<AtomController>();

    // Lookup rebuilt before every mix attempt
    private Dictionary<string, MoleculePoolEntry> _poolLookup;

    // Event: (recipe, success). UIManager and AudioManager listen to this.
    public static System.Action<MoleculeRecipe, bool> OnMixResult;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        DisableAllMolecules();
    }

    private void Start()
    {
        RebuildPoolLookup();
        DebugPrintPoolContents();
    }

    // ─── Pool Helpers ─────────────────────────────────────────────────────────

    private void DisableAllMolecules()
    {
        foreach (var entry in moleculePool)
        {
            if (entry.moleculeObject != null)
                entry.moleculeObject.SetActive(false);
        }
    }

    private void RebuildPoolLookup()
    {
        _poolLookup = new Dictionary<string, MoleculePoolEntry>();
        foreach (var entry in moleculePool)
        {
            if (string.IsNullOrWhiteSpace(entry.moleculeName))
            {
                Debug.LogWarning("[MixingZone] A pool entry has an empty moleculeName — skipped.");
                continue;
            }
            if (entry.moleculeObject == null)
                Debug.LogWarning($"[MixingZone] Pool entry '{entry.moleculeName}' has no moleculeObject assigned!");

            string key = NormalizeKey(entry.moleculeName);
            if (!_poolLookup.ContainsKey(key))
                _poolLookup[key] = entry;
            else
                Debug.LogWarning($"[MixingZone] Duplicate pool entry for key '{key}'.");
        }
    }

    private static string NormalizeKey(string s) => s.Trim().ToLowerInvariant();

    private void DebugPrintPoolContents()
    {
        Debug.Log($"[MixingZone] Pool lookup has {_poolLookup.Count} entries:");
        foreach (var kv in _poolLookup)
        {
            string objName = kv.Value.moleculeObject != null ? kv.Value.moleculeObject.name : "!!! NULL !!!";
            Debug.Log($"  PoolKey='{kv.Key}' -> GameObject='{objName}'");
        }

        if (moleculeDatabase == null)
        {
            Debug.LogError("[MixingZone] moleculeDatabase is NOT assigned!");
            return;
        }
        Debug.Log($"[MixingZone] MoleculeDatabase has {moleculeDatabase.recipes.Count} recipe(s):");
        foreach (var r in moleculeDatabase.recipes)
            Debug.Log($"  Recipe: '{r.moleculeName}' | RecipeKey: '{r.GetRecipeKey()}'");
    }

    // ─── Atom Enter / Exit ────────────────────────────────────────────────────

    public void OnAtomEntered(AtomController atom)
    {
        if (atom == null) return;
        if (!_atomsInZone.Contains(atom))
        {
            _atomsInZone.Add(atom);
            Debug.Log($"[MixingZone] ++ {atom.atomType} entered. Total: {_atomsInZone.Count}");
        }
    }

    public void OnAtomExited(AtomController atom)
    {
        if (atom == null) return;
        _atomsInZone.Remove(atom);
        Debug.Log($"[MixingZone] -- {atom.atomType} exited. Total: {_atomsInZone.Count}");
    }

    // ─── Mix Button ───────────────────────────────────────────────────────────

    public void TryMix()
    {
        Debug.Log($"[MixingZone] TryMix called. Atoms in zone: {_atomsInZone.Count}");

        if (moleculeDatabase == null)
        {
            Debug.LogError("[MixingZone] MoleculeDatabase not assigned!");
            OnMixResult?.Invoke(null, false);
            return;
        }

        if (_atomsInZone.Count == 0)
        {
            Debug.Log("[MixingZone] No atoms in zone.");
            OnMixResult?.Invoke(null, false);
            return;
        }

        var atomTypes = new List<AtomType>();
        foreach (var atom in _atomsInZone)
        {
            atomTypes.Add(atom.atomType);
            Debug.Log($"  Zone atom: {atom.atomType} ({atom.gameObject.name})");
        }

        string inputKey = MoleculeDatabase.BuildKey(atomTypes);
        Debug.Log($"[MixingZone] Input recipe key: '{inputKey}'");

        // Rebuild lookup now in case Inspector changed since Start
        RebuildPoolLookup();

        MoleculeRecipe recipe = moleculeDatabase.FindRecipe(atomTypes);

        if (recipe == null)
        {
            Debug.Log($"[MixingZone] No recipe for key '{inputKey}'. Atoms stay in zone.");
            OnMixResult?.Invoke(null, false);
            return;
        }

        Debug.Log($"[MixingZone] Recipe matched: '{recipe.moleculeName}'");

        // Recycle all atoms
        var toRecycle = new List<AtomController>(_atomsInZone);
        _atomsInZone.Clear();
        foreach (var atom in toRecycle)
            atom.RecycleToPool();

        // Enable result molecule
        SpawnMoleculeResult(recipe);

        OnMixResult?.Invoke(recipe, true);
    }

    // ─── Result Molecule Activation ───────────────────────────────────────────

    private void SpawnMoleculeResult(MoleculeRecipe recipe)
    {
        string key = NormalizeKey(recipe.moleculeName);
        Debug.Log($"[MixingZone] Searching pool for key: '{key}'");

        if (!_poolLookup.TryGetValue(key, out MoleculePoolEntry entry))
        {
            var available = new List<string>(_poolLookup.Keys);
            Debug.LogError(
                $"[MixingZone] POOL MISS for key='{key}'. " +
                $"Available keys in pool: [{string.Join(", ", available)}]. " +
                $"Fix: make sure MixingZone > Molecule Pool > Molecule Name matches '{recipe.moleculeName}' exactly.");
            return;
        }

        if (entry.moleculeObject == null)
        {
            Debug.LogError(
                $"[MixingZone] Pool entry '{entry.moleculeName}' found but moleculeObject is NULL. " +
                "Drag the result GameObject into the Molecule Object field in the Inspector.");
            return;
        }

        Vector3 spawnPos = resultSpawnPoint != null
            ? resultSpawnPoint.position
            : transform.position + Vector3.up * 0.35f;

        entry.moleculeObject.transform.position = spawnPos;
        entry.moleculeObject.transform.rotation = Quaternion.identity;
        entry.moleculeObject.SetActive(true);
        entry.isInUse = true;

        Debug.Log(
            $"[MixingZone] SUCCESS — '{entry.moleculeName}' SetActive(true) at {spawnPos}. " +
            $"activeSelf={entry.moleculeObject.activeSelf}");

        if (moleculeDisplayDuration > 0f)
            StartCoroutine(ReturnAfterDelay(entry, moleculeDisplayDuration));
    }

    private System.Collections.IEnumerator ReturnAfterDelay(MoleculePoolEntry entry, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (entry.moleculeObject != null)
            entry.moleculeObject.SetActive(false);
        entry.isInUse = false;
        Debug.Log($"[MixingZone] Auto-hid '{entry.moleculeName}' after {delay}s.");
    }

    public void ReturnMoleculeToPool(MoleculePoolEntry entry)
    {
        if (entry?.moleculeObject != null)
            entry.moleculeObject.SetActive(false);
        if (entry != null) entry.isInUse = false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.15f);
        Gizmos.DrawCube(transform.position, transform.localScale);
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }
}