

using System.Collections;
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

    [Header("Atom Capacity")]
    [Tooltip("Maximum number of atoms allowed inside the zone at the same time (1-10)")]
    [Range(1, 10)]
    public int maxAtomsInZone = 10;

    [Header("Particle Effect")]
    [Tooltip("Drag a ParticleSystem GameObject from the scene hierarchy here. " +
             "It will be enabled, played on a successful mix, then disabled after the delay.")]
    public ParticleSystem mixParticleEffect;

    [Tooltip("Seconds to wait before disabling the particle GameObject after playing. " +
             "Set to 0 to use the particle system's own Stop Action / duration automatically.")]
    public float particleDisableDelay = 3f;

    // ─── Private State ────────────────────────────────────────────────────────

    private List<AtomController> _atomsInZone = new List<AtomController>();
    private Dictionary<string, MoleculePoolEntry> _poolLookup;

    // Event: (recipe, success). UIManager and AudioManager listen to this.
    public static System.Action<MoleculeRecipe, bool> OnMixResult;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        DisableAllMolecules();

        // Make sure particle is off at start
        if (mixParticleEffect != null)
            mixParticleEffect.gameObject.SetActive(false);
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

    /// <summary>
    /// Returns true if the atom was accepted into the zone, false if the zone is full.
    /// AtomController checks the return value to decide whether to apply glow.
    /// </summary>
    public bool OnAtomEntered(AtomController atom)
    {
        if (atom == null) return false;

        if (_atomsInZone.Contains(atom)) return true; // already tracked

        if (_atomsInZone.Count >= maxAtomsInZone)
        {
            Debug.Log($"[MixingZone] Zone full ({maxAtomsInZone} atoms). Rejected: {atom.atomType}");
            return false;
        }

        _atomsInZone.Add(atom);
        Debug.Log($"[MixingZone] ++ {atom.atomType} entered. Total: {_atomsInZone.Count}/{maxAtomsInZone}");
        return true;
    }

    public void OnAtomExited(AtomController atom)
    {
        if (atom == null) return;
        _atomsInZone.Remove(atom);
        Debug.Log($"[MixingZone] -- {atom.atomType} exited. Total: {_atomsInZone.Count}/{maxAtomsInZone}");
    }

    // ─── Reset Button ─────────────────────────────────────────────────────────

    /// <summary>
    /// Clears the mixing zone — all atoms currently inside are recycled back
    /// to their pools and the zone is left empty and ready for a new attempt.
    /// Called by ResetButton.TriggerReset().
    /// </summary>
    public void ResetZone()
    {
        if (_atomsInZone.Count == 0)
        {
            Debug.Log("[MixingZone] ResetZone called but zone is already empty.");
            return;
        }

        Debug.Log($"[MixingZone] ResetZone — recycling {_atomsInZone.Count} atom(s).");

        var toRecycle = new List<AtomController>(_atomsInZone);
        _atomsInZone.Clear();

        foreach (var atom in toRecycle)
        {
            if (atom != null)
                atom.RecycleToPool();
        }
    }

    // ─── Mix Button ───────────────────────────────────────────────────────────

    public void TryMix()
    {
        Debug.Log($"[MixingZone] TryMix called. Atoms in zone: {_atomsInZone.Count}/{maxAtomsInZone}");

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

        // Rebuild lookup in case Inspector changed since Start
        RebuildPoolLookup();

        MoleculeRecipe recipe = moleculeDatabase.FindRecipe(atomTypes);

        if (recipe == null)
        {
            Debug.Log($"[MixingZone] No recipe for key '{inputKey}'. Atoms stay in zone.");
            OnMixResult?.Invoke(null, false);
            return;
        }

        Debug.Log($"[MixingZone] Recipe matched: '{recipe.moleculeName}'");

        // ── Play atom-consumed sound immediately ──────────────────────────────
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMix();

        // ── Recycle all atoms ─────────────────────────────────────────────────
        var toRecycle = new List<AtomController>(_atomsInZone);
        _atomsInZone.Clear();
        foreach (var atom in toRecycle)
            atom.RecycleToPool();

        // ── Particle effect ───────────────────────────────────────────────────
        PlayMixParticle();

        // ── Result molecule ───────────────────────────────────────────────────
        SpawnMoleculeResult(recipe);

        // ── Broadcast (AudioManager + UIManager listen here) ──────────────────
        OnMixResult?.Invoke(recipe, true);
    }

    // ─── Particle Helpers ─────────────────────────────────────────────────────

    private void PlayMixParticle()
    {
        if (mixParticleEffect == null) return;

        // Position particle at result spawn point or above zone center
        Vector3 pos = resultSpawnPoint != null
            ? resultSpawnPoint.position
            : transform.position + Vector3.up * 0.35f;

        mixParticleEffect.transform.position = pos;
        mixParticleEffect.gameObject.SetActive(true);
        mixParticleEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        mixParticleEffect.Play();

        float delay = particleDisableDelay > 0f
            ? particleDisableDelay
            : mixParticleEffect.main.duration + mixParticleEffect.main.startLifetime.constantMax;

        StartCoroutine(DisableParticleAfterDelay(delay));
    }

    private IEnumerator DisableParticleAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (mixParticleEffect != null)
            mixParticleEffect.gameObject.SetActive(false);
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

    private IEnumerator ReturnAfterDelay(MoleculePoolEntry entry, float delay)
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