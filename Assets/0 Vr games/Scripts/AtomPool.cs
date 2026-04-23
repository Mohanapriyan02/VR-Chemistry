// AtomPool.cs
// Manages a fixed-size pool of atom GameObjects for a single AtomType.
// Spawns one atom at the spawn point; when grabbed, schedules a replacement.
// When pool is exhausted, recycles the oldest active atom.
//
// FIX SUMMARY:
//   - ActivateAtom now calls SetActive(true) BEFORE touching the Rigidbody.
//     Previously the Rigidbody was configured while the object was inactive,
//     which meant XRGrabInteractable.OnEnable() fired AFTER and reset our values.
//   - Physics idle state (kinematic=true, gravity=false) is now owned entirely
//     by AtomController.SetIdleAtSpawnPoint(), keeping pool and controller in sync.
//   - GetComponent calls are cached on prefab instantiation to avoid runtime overhead.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AtomPool : MonoBehaviour
{
    // ─── Inspector ───────────────────────────────────────────────────────────────

    [Header("Pool Configuration")]
    public AtomType   atomType;
    public GameObject atomPrefab;
    [Min(1)]
    public int        poolSize  = 6;

    [Header("Spawn")]
    public Transform  spawnPoint;
    [Min(0f)]
    public float      spawnDelay = 2f;

    // ─── Internal State ──────────────────────────────────────────────────────────

    // All atoms ever created (active or inactive)
    private readonly List<AtomController>   _pool        = new List<AtomController>();
    // Atoms currently visible/active in the scene, in spawn order
    private readonly Queue<AtomController>  _activeQueue = new Queue<AtomController>();

    // ─── Unity Lifecycle ─────────────────────────────────────────────────────────

    private void Awake()
    {
        if (spawnPoint == null)
            spawnPoint = transform;

        InitPool();
        SpawnNewAtom(); // place the first idle atom immediately
    }

    // ─── Pool Initialisation ──────────────────────────────────────────────────────

    private void InitPool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(atomPrefab);
            obj.name = $"{atomType}_Atom_{i}";

            AtomController controller = obj.GetComponent<AtomController>();
            if (controller != null)
            {
                controller.atomType  = atomType;
                controller.ownerPool = this;
            }

            obj.SetActive(false);
            _pool.Add(controller); // cache controller reference; avoids GetComponent at runtime
        }
    }

    // ─── Public API ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by AtomController when the idle spawn-point atom is grabbed.
    /// Schedules a replacement atom to appear after spawnDelay seconds.
    /// </summary>
    public void OnAtomGrabbed()
    {
        StartCoroutine(SpawnWithDelay());
    }

    // ─── Spawn Logic ──────────────────────────────────────────────────────────────

    private IEnumerator SpawnWithDelay()
    {
        yield return new WaitForSeconds(spawnDelay);
        SpawnNewAtom();
    }

    private void SpawnNewAtom()
    {
        AtomController controller;

        if (_activeQueue.Count >= poolSize)
        {
            // Pool exhausted — forcibly recycle the oldest active atom
            controller = _activeQueue.Dequeue();
            controller.ForceResetFromPool();
            controller.gameObject.SetActive(false); // ensure clean reactivation below
        }
        else
        {
            controller = GetInactiveAtom();
        }

        if (controller == null)
        {
            Debug.LogError($"[AtomPool] No available atom for {atomType}. Increase pool size.");
            return;
        }

        ActivateAtom(controller);
        _activeQueue.Enqueue(controller);
    }

    private AtomController GetInactiveAtom()
    {
        foreach (var controller in _pool)
        {
            if (!controller.gameObject.activeSelf)
                return controller;
        }
        return null; // caller handles null
    }

    /// <summary>
    /// Positions the atom and activates it.
    ///
    /// ORDER IS CRITICAL:
    ///   1. Set transform (while still inactive — safe, no physics events)
    ///   2. SetActive(true)  ← XRGrabInteractable.OnEnable() fires here and
    ///                          snapshots the Rigidbody state
    ///   3. SetIdleAtSpawnPoint() ← runs AFTER OnEnable, so our kinematic=true
    ///                              state is applied LAST and sticks correctly
    /// </summary>
    private void ActivateAtom(AtomController controller)
    {
        GameObject obj = controller.gameObject;

        // Step 1 — position while inactive (no physics events, safe)
        obj.transform.position = spawnPoint.position;
        obj.transform.rotation = spawnPoint.rotation;

        // Step 2 — activate (XRGrabInteractable.OnEnable snapshots Rigidbody here)
        obj.SetActive(true);

        // Step 3 — apply idle physics AFTER OnEnable so our values win
        controller.SetIdleAtSpawnPoint(true);
    }
}