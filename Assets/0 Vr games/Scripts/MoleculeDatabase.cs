// MoleculeDatabase.cs
// ScriptableObject that holds all valid molecule recipes.
// Create via: Assets > Create > VRMolecularLab > MoleculeDatabase
// Then assign molecule prefabs and recipes in the Inspector.

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MoleculeRecipe
{
    [Tooltip("Display name, e.g. 'Water'")]
    public string moleculeName;

    [Tooltip("Chemical formula string, e.g. 'H₂O'")]
    public string formula;

    [Tooltip("Bond type description shown in UI, e.g. 'Covalent'")]
    public string bondType;

    [Tooltip("Human-readable equation shown in discovery panel, e.g. '2H + 1O = H₂O'")]
    public string discoveryText;

    [Tooltip("The result molecule prefab to spawn/enable from the pool")]
    public GameObject moleculePrefab;

    [Tooltip("List of atoms required. Add duplicates for multiples, e.g. H, H, O for Water")]
    public List<AtomType> requiredAtoms = new List<AtomType>();

    /// <summary>
    /// Returns a sorted key string from an atom list for fast dictionary lookup.
    /// E.g. [H, H, O] -> "H:2|O:1"
    /// </summary>
    public string GetRecipeKey()
    {
        return MoleculeDatabase.BuildKey(requiredAtoms);
    }
}

[CreateAssetMenu(fileName = "MoleculeDatabase", menuName = "VRMolecularLab/MoleculeDatabase")]
public class MoleculeDatabase : ScriptableObject
{
    [Header("All Valid Molecule Recipes")]
    public List<MoleculeRecipe> recipes = new List<MoleculeRecipe>();

    // Runtime lookup dictionary built on first use
    private Dictionary<string, MoleculeRecipe> _lookupCache;

    private void OnEnable()
    {
        BuildCache();
    }

    private void BuildCache()
    {
        _lookupCache = new Dictionary<string, MoleculeRecipe>();
        foreach (var recipe in recipes)
        {
            string key = recipe.GetRecipeKey();
            if (!_lookupCache.ContainsKey(key))
                _lookupCache[key] = recipe;
            else
                Debug.LogWarning($"[MoleculeDatabase] Duplicate recipe key detected: {key}");
        }
    }

    /// <summary>
    /// Try to find a matching recipe for the given atom list.
    /// Returns null if no valid molecule matches.
    /// </summary>
    public MoleculeRecipe FindRecipe(List<AtomType> atoms)
    {
        if (_lookupCache == null || _lookupCache.Count == 0)
            BuildCache();

        string key = BuildKey(atoms);
        _lookupCache.TryGetValue(key, out MoleculeRecipe recipe);
        return recipe;
    }

    /// <summary>
    /// Builds a canonical sorted key from an atom list.
    /// Sorting ensures [H, O, H] and [O, H, H] produce the same key.
    /// </summary>
    public static string BuildKey(List<AtomType> atoms)
    {
        // Count occurrences of each atom type
        var counts = new Dictionary<AtomType, int>();
        foreach (var atom in atoms)
        {
            if (!counts.ContainsKey(atom))
                counts[atom] = 0;
            counts[atom]++;
        }

        // Sort by AtomType name for consistency
        var sorted = new List<AtomType>(counts.Keys);
        sorted.Sort((a, b) => a.ToString().CompareTo(b.ToString()));

        var parts = new List<string>();
        foreach (var atomType in sorted)
            parts.Add($"{atomType}:{counts[atomType]}");

        return string.Join("|", parts);
    }
}