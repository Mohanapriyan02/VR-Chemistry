// AtomPool.cs
// Place one AtomPool component on a SpawnPoint GameObject for each atom type.
// It keeps 1 atom visible at the spawn point at all times.
// When the player grabs that atom, the pool spawns the next one automatically.
// Atoms are never destroyed — they are disabled and returned to the pool.

using System.Collections.Generic;
using UnityEngine;

public class AtomPool : MonoBehaviour
{
    [Header("Pool Configuration")]
    [Tooltip("Which atom type this spawn point manages")]
    public AtomType atomType;

    [Tooltip("The atom prefab to pool (must have AtomController)")]
    public GameObject atomPrefab;

    [Tooltip("Maximum number of this atom that can be active at once")]
    public int poolSize = 6;

    [Header("Spawn Point")]
    [Tooltip("Where new atoms appear. Defaults to this transform if not set.")]
    public Transform spawnPoint;

    // Internal pool
    private List<GameObject> _pool = new List<GameObject>();
    private Queue<GameObject> _available = new Queue<GameObject>();

    private void Awake()
    {
        if (spawnPoint == null)
            spawnPoint = transform;

        InitPool();
    }

    private void InitPool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(atomPrefab, spawnPoint.position, spawnPoint.rotation);
            obj.name = $"{atomType}_Atom_{i}";

            var controller = obj.GetComponent<AtomController>();
            if (controller != null)
            {
                controller.atomType = atomType;
                controller.ownerPool = this;
            }

            obj.SetActive(false);
            _pool.Add(obj);
            _available.Enqueue(obj);
        }

        // Spawn the first atom visibly at the spawn point
        SpawnNextAtSpawnPoint();
    }

    /// <summary>
    /// Called by AtomController when the atom is grabbed.
    /// Spawns the next available atom at the spawn point.
    /// </summary>
    public void OnAtomGrabbed()
    {
        SpawnNextAtSpawnPoint();
    }

    /// <summary>
    /// Returns a disabled atom back to the pool so it can be reused.
    /// Called when a mix completes and atoms are recycled.
    /// </summary>
    public void ReturnAtom(GameObject atom)
    {
        if (atom == null) return;
        atom.SetActive(false);
        atom.transform.position = spawnPoint.position;
        atom.transform.rotation = spawnPoint.rotation;

        // Reset Rigidbody velocity if present
        var rb = atom.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        _available.Enqueue(atom);
    }

    private void SpawnNextAtSpawnPoint()
    {
        if (_available.Count == 0)
        {
            Debug.Log($"[AtomPool] Pool exhausted for {atomType}. Max pool size: {poolSize}");
            return;
        }

        GameObject next = _available.Dequeue();
        next.transform.position = spawnPoint.position;
        next.transform.rotation = spawnPoint.rotation;
        next.SetActive(true);

        // Mark it as the "idle at spawn point" atom
        var controller = next.GetComponent<AtomController>();
        if (controller != null)
            controller.SetIdleAtSpawnPoint(true);
    }
}