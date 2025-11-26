using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;

/// <summary>
/// DustSpawner_Full (patched)
/// - Spawns N dust prefabs on floor within a BoxCollider bounds (room area).
/// - Uses raycast to find floor; falls back to Terrain.SampleHeight if raycast misses.
/// - Logs detailed debug info for misses and placement failures.
/// - Avoids overlapping using minDistanceBetween; has fallback placement if attempts fail.
/// - AFTER spawning, registers spawned dusts with DustContainerCheckerAuto (if present).
/// </summary>
[ExecuteAlways]
public class DustSpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject dustPrefab;           // Dust prefab (pivot at base recommended)
    public Transform dustParent;            // Parent for spawned dusts (optional)
    public BoxCollider roomBounds;          // BoxCollider that defines X/Z spawn area (required)

    [Header("Container Checker")]
    [Tooltip("Optional: reference to DustContainerCheckerAuto on the container. If left null the spawner will try to find one.")]
    public Component containerChecker; // use Component so compile works even if DustContainerCheckerAuto not present

    [Tooltip("If true the spawner will wait a frame after spawning before registering (lets DustAuto Awake run).")]
    public bool waitOneFrameBeforeRegister = true;

    [Header("Floor / Physics")]
    public LayerMask floorLayer;            // Layers considered floor for raycast (must include your cube/terrain layer)
    [Tooltip("If true, this script will try Terrain sampling when raycast misses.")]
    public bool useTerrainFallback = true;

    [Header("Spawn Settings")]
    public int spawnCount = 6;              // how many dust spots to spawn
    public int maxAttemptsPerSpot = 30;     // attempts per dust before fallback
    public float minDistanceBetween = 0.5f; // minimum allowed distance between dusts
    public Vector2 randomScaleRange = new Vector2(0.8f, 1.2f);
    public bool randomRotationY = true;

    [Header("Raycast Settings")]
    public float raycastHeight = 3f;        // how high above candidate to start ray
    public float raycastDownDistance = 6f;  // how far down the raycast should go
    [Tooltip("Small offset above sampled terrain height so the dust doesn't clip inside the terrain.")]
    public float terrainSamplePadding = 0.01f;

    [Header("Editor / Debug")]
    public bool spawnOnStart = true;        // spawn on Start() (if in play mode)
    public bool clearExisting = true;       // clear previously spawned
    public bool drawDebugRays = true;       // draw ray lines in Scene view

    // internal list of spawned transforms
    private List<Transform> spawned = new List<Transform>();

    void Start()
    {
        if (Application.isPlaying && spawnOnStart)
        {
            SpawnAll();
        }
    }

    #region Public Context Menu Actions

    [ContextMenu("Spawn All Dust")]
    public void SpawnAll()
    {
        if (!ValidateSetup()) return;
        if (clearExisting) ClearSpawned();

        // try to auto-find containerChecker if not set
        EnsureContainerChecker();

        for (int i = 0; i < spawnCount; i++)
        {
            bool placed = TryPlaceDustWithAttempts(i + 1);
            if (!placed)
            {
                Debug.LogWarning($"[DustSpawner_Full] Failed to place dust #{i + 1} after {maxAttemptsPerSpot} attempts (no valid floor hit).");
            }
        }

        // register spawned dusts with containerChecker (wait frame if requested)
        if (waitOneFrameBeforeRegister && Application.isPlaying)
        {
            StartCoroutine(RegisterAfterFrame());
        }
        else
        {
            RegisterSpawnedImmediately();
        }

        Debug.Log($"[DustSpawner_Full] Spawn finished. Spawned count: {spawned.Count}");
    }

    [ContextMenu("Clear Spawned")]
    public void ClearSpawned()
    {
        // Attempt to deregister first (best-effort)
        TryDeregisterSpawned();

        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(spawned[i].gameObject);
                else
#endif
                    Destroy(spawned[i].gameObject);
            }
            spawned.RemoveAt(i);
        }
        Debug.Log("[DustSpawner_Full] Cleared spawned dusts.");
    }

    [ContextMenu("Test Ray At Bounds Center")]
    public void TestRayAtBoundsCenter()
    {
        if (!roomBounds)
        {
            Debug.LogError("[DustSpawner_Full] roomBounds not assigned.");
            return;
        }

        Vector3 worldCenter = roomBounds.transform.TransformPoint(roomBounds.center);
        Vector3 rayOrigin = new Vector3(worldCenter.x, worldCenter.y + raycastHeight, worldCenter.z);

        if (drawDebugRays) Debug.DrawLine(rayOrigin, rayOrigin + Vector3.down * raycastDownDistance, Color.red, 5f);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDownDistance, floorLayer))
        {
            Debug.Log($"[DustSpawner_Full] TestRay: Ray HIT '{hit.collider.name}' (layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}) at {hit.point}");
        }
        else
        {
            Debug.LogWarning($"[DustSpawner_Full] TestRay: Raycast MISSED. Origin={rayOrigin}, Dist={raycastDownDistance}, LayerMask={floorLayer}.");
            if (useTerrainFallback)
            {
                if (TryGetTerrainPosition(worldCenter, out Vector3 terrainPos))
                    Debug.Log($"[DustSpawner_Full] TestRay: Terrain fallback found pos {terrainPos}");
                else
                    Debug.LogWarning("[DustSpawner_Full] TestRay: Terrain fallback also found nothing.");
            }
        }
    }

    #endregion

    #region Core Placement Logic

    // Try to place one dust with multiple attempts; returns true if placed
    bool TryPlaceDustWithAttempts(int index)
    {
        Vector3 bestCandidate = Vector3.zero;
        float bestCandidateScore = -Mathf.Infinity; // prefer candidates far from existing dusts
        bool foundAnyHit = false;

        for (int attempt = 0; attempt < maxAttemptsPerSpot; attempt++)
        {
            Vector3 candidateXZ = RandomPointInBounds(roomBounds);
            Vector3 rayOrigin = new Vector3(candidateXZ.x, candidateXZ.y + raycastHeight, candidateXZ.z);

            if (drawDebugRays) Debug.DrawLine(rayOrigin, rayOrigin + Vector3.down * raycastDownDistance, Color.cyan, 2f);

            // 1) Try raycast first
            if (TryRaycastFloor(rayOrigin, out Vector3 hitPos, out Vector3 hitNormal, out Collider hitCollider))
            {
                foundAnyHit = true;
                float score = ComputeCandidateScore(hitPos);
                if (score > bestCandidateScore)
                {
                    bestCandidateScore = score;
                    bestCandidate = hitPos;
                }

                float nearestDistance = NearestSpawnDistance(hitPos);
                if (nearestDistance >= minDistanceBetween)
                {
                    CreateDust(hitPos, hitNormal);
                    Debug.Log($"[DustSpawner_Full] Placed dust #{index} at {hitPos} (attempt {attempt + 1}). Hit collider '{hitCollider.name}'.");
                    return true;
                }
                else
                {
                    Debug.Log($"[DustSpawner_Full] Attempt {attempt + 1} for dust #{index}: hit '{hitCollider.name}' at {hitPos} but too close ({nearestDistance:F2}m) to existing dusts.");
                }
            }
            else
            {
                // 2) Raycast missed
                Debug.LogWarning($"[DustSpawner_Full] Attempt {attempt + 1} for dust #{index}: Raycast MISSED. origin={rayOrigin}, downDist={raycastDownDistance}, floorMask={floorLayer}");
                // Try Terrain fallback immediately if enabled (and if we haven't tried that for this candidate)
                if (useTerrainFallback)
                {
                    if (TryGetTerrainPosition(candidateXZ, out Vector3 terrainPos))
                    {
                        foundAnyHit = true;
                        float score = ComputeCandidateScore(terrainPos);
                        if (score > bestCandidateScore)
                        {
                            bestCandidateScore = score;
                            bestCandidate = terrainPos;
                        }

                        float nearestDistance = NearestSpawnDistance(terrainPos);
                        if (nearestDistance >= minDistanceBetween)
                        {
                            CreateDust(terrainPos, Vector3.up);
                            Debug.Log($"[DustSpawner_Full] Placed dust #{index} on Terrain fallback at {terrainPos} (attempt {attempt + 1}).");
                            return true;
                        }
                        else
                        {
                            Debug.Log($"[DustSpawner_Full] Attempt {attempt + 1} for dust #{index}: Terrain pos too close ({nearestDistance:F2}m).");
                        }
                    }
                    else
                    {
                        Debug.Log($"[DustSpawner_Full] Attempt {attempt + 1} for dust #{index}: Terrain fallback found nothing under XZ={candidateXZ.x:F2},{candidateXZ.z:F2}.");
                    }
                }
            }
        } // attempts loop

        // Fallback placement: if we at least had any hit candidate, place at bestCandidate even if a bit close
        if (foundAnyHit && bestCandidateScore > -Mathf.Infinity)
        {
            CreateDust(bestCandidate, Vector3.up);
            Debug.LogWarning($"[DustSpawner_Full] Fallback: placed dust at best candidate {bestCandidate} after {maxAttemptsPerSpot} attempts.");
            return true;
        }

        // Nothing found at all
        Debug.LogError($"[DustSpawner_Full] Could NOT find any valid placement for dust after {maxAttemptsPerSpot} attempts. Check floorLayer, collider presence, or ray distances.");
        return false;
    }

    // Try a Physics.Raycast downward; returns true if hit floorLayer
    bool TryRaycastFloor(Vector3 rayOrigin, out Vector3 hitPoint, out Vector3 hitNormal, out Collider hitCollider)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        hitCollider = null;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDownDistance, floorLayer))
        {
            hitPoint = hit.point;
            hitNormal = hit.normal;
            hitCollider = hit.collider;
            return true;
        }
        return false;
    }

    // Try to sample terrain(s) under the world XZ point; returns true + world-position
    bool TryGetTerrainPosition(Vector3 worldXZPoint, out Vector3 outPos)
    {
        outPos = Vector3.zero;
        Terrain[] terrains = Terrain.activeTerrains;
        if (terrains == null || terrains.Length == 0) return false;

        foreach (var t in terrains)
        {
            if (t == null) continue;
            Vector3 tPos = t.transform.position;
            Vector3 size = t.terrainData.size;

            if (worldXZPoint.x >= tPos.x && worldXZPoint.x <= tPos.x + size.x &&
                worldXZPoint.z >= tPos.z && worldXZPoint.z <= tPos.z + size.z)
            {
                float height = t.SampleHeight(worldXZPoint);
                float worldY = tPos.y + height + terrainSamplePadding;
                outPos = new Vector3(worldXZPoint.x, worldY, worldXZPoint.z);
                return true;
            }
        }
        return false;
    }

    #endregion

    #region Helpers

    // Create dust GameObject and add to spawned list (and try to register with containerChecker later)
    void CreateDust(Vector3 pos, Vector3 normal)
    {
        Transform parent = dustParent ? dustParent : this.transform;
        GameObject go = Instantiate(dustPrefab, pos, Quaternion.identity, parent);

        // random Y rotation
        if (randomRotationY)
        {
            float y = Random.Range(0f, 360f);
            go.transform.rotation = Quaternion.Euler(0f, y, 0f);
        }

        float scale = Random.Range(randomScaleRange.x, randomScaleRange.y);
        go.transform.localScale = Vector3.one * scale;

        spawned.Add(go.transform);
    }

    // Compute a simple score for candidate: sum of distances to all existing dusts (prefer larger)
    float ComputeCandidateScore(Vector3 candidate)
    {
        float sum = 0f;
        foreach (var t in spawned)
        {
            if (t == null) continue;
            sum += Vector3.Distance(t.position, candidate);
        }
        return sum;
    }

    // Nearest spawn distance
    float NearestSpawnDistance(Vector3 pos)
    {
        float min = float.MaxValue;
        foreach (var t in spawned)
        {
            if (t == null) continue;
            float d = Vector3.Distance(t.position, pos);
            if (d < min) min = d;
        }
        if (min == float.MaxValue) return Mathf.Infinity;
        return min;
    }

    // Random point in BoxCollider area (XZ). Returns world point with collider's Y.
    Vector3 RandomPointInBounds(BoxCollider bc)
    {
        Vector3 localCenter = bc.center;
        Vector3 extents = bc.size * 0.5f;

        Vector3 localRandom = new Vector3(
            Random.Range(-extents.x, extents.x),
            0f,
            Random.Range(-extents.z, extents.z)
        );

        Vector3 worldPoint = bc.transform.TransformPoint(localCenter + localRandom);
        // use the collider transform's y as base (so ray origin uses raycastHeight above this)
        return new Vector3(worldPoint.x, worldPoint.y, worldPoint.z);
    }

    // Validate inspector setup before running
    bool ValidateSetup()
    {
        if (!dustPrefab)
        {
            Debug.LogError("[DustSpawner_Full] dustPrefab is not assigned.");
            return false;
        }
        if (!roomBounds)
        {
            Debug.LogError("[DustSpawner_Full] roomBounds (BoxCollider) is not assigned.");
            return false;
        }
        // quick hint if floorLayer might be empty (common mistake)
        if (floorLayer == 0)
        {
            Debug.LogWarning("[DustSpawner_Full] floorLayer mask is empty. Make sure to include the layer of your floor (cube, terrain, etc.).");
        }
        return true;
    }

    #endregion

    #region Registration with DustContainerCheckerAuto

    void EnsureContainerChecker()
    {
        if (containerChecker != null) return;

        // try parent or dustParent first
        if (dustParent != null)
        {
            containerChecker = dustParent.GetComponentInChildren(typeof(Component), true);
        }

        // fallback: try to find a component named "DustContainerCheckerAuto" anywhere upward or on this GameObject
        if (containerChecker == null)
        {
            var found = GetComponentInParent(typeof(object)); // placeholder - we'll search by name below
            // search all MonoBehaviours in parents for type name
            Transform t = this.transform;
            while (t != null)
            {
                var comps = t.GetComponents<MonoBehaviour>();
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    var tn = c.GetType().Name;
                    if (tn == "DustContainerCheckerAuto" || tn == "DustContainerChecker")
                    {
                        containerChecker = c;
                        break;
                    }
                }
                if (containerChecker != null) break;
                t = t.parent;
            }

            // final fallback: FindObjectOfType by name
            if (containerChecker == null)
            {
                var all = FindObjectsOfType<MonoBehaviour>();
                foreach (var mb in all)
                {
                    if (mb == null) continue;
                    var tn = mb.GetType().Name;
                    if (tn == "DustContainerCheckerAuto" || tn == "DustContainerChecker")
                    {
                        containerChecker = mb;
                        break;
                    }
                }
            }
        }
    }

    IEnumerator RegisterAfterFrame()
    {
        // wait a couple frames to let spawned objects Awake/OnEnable run (so DustAuto can register itself)
        yield return null;
        yield return null;
        RegisterSpawnedImmediately();
    }

    void RegisterSpawnedImmediately()
    {
        if (containerChecker == null)
        {
            EnsureContainerChecker();
        }

        int countRegistered = 0;
        foreach (var tr in spawned)
        {
            if (tr == null) continue;
            GameObject go = tr.gameObject;

            bool registered = false;

            // Preferred: containerChecker has RegisterDust(GameObject)
            if (containerChecker != null)
            {
                var mi = containerChecker.GetType().GetMethod("RegisterDust", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi != null)
                {
                    try
                    {
                        mi.Invoke(containerChecker, new object[] { go });
                        registered = true;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning("[DustSpawner] RegisterDust invoke threw: " + ex);
                    }
                }
            }

            // Fallback: add to containerChecker's tracked list if field exists (best effort)
            if (!registered && containerChecker != null)
            {
                var f = containerChecker.GetType().GetField("trackedDustObjects", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null)
                {
                    try
                    {
                        var listObj = f.GetValue(containerChecker) as System.Collections.IList;
                        if (listObj != null)
                        {
                            listObj.Add(go);
                            registered = true;
                        }
                    }
                    catch { }
                }
            }

            if (!registered)
            {
                // Optionally, if DustAuto exists on the dust itself it will self-register in Awake/OnEnable/OnDestroy.
                // So if nothing registered, that's not necessarily an error.
            }
            else countRegistered++;
        }

        Debug.Log($"[DustSpawner] Registered {countRegistered}/{spawned.Count} spawned dust pieces with containerChecker.");
    }

    void TryDeregisterSpawned()
    {
        if (containerChecker == null) return;

        int countRemoved = 0;
        foreach (var tr in spawned)
        {
            if (tr == null) continue;
            GameObject go = tr.gameObject;

            var mi = containerChecker.GetType().GetMethod("NotifyDustRemoved", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi != null)
            {
                try
                {
                    mi.Invoke(containerChecker, new object[] { go });
                    countRemoved++;
                }
                catch { }
            }
        }

        if (countRemoved > 0) Debug.Log($"[DustSpawner] Deregistered {countRemoved}/{spawned.Count} before clearing.");
    }

    #endregion

    #region Editor Gizmos
    void OnDrawGizmosSelected()
    {
        if (roomBounds)
        {
            Gizmos.color = new Color(0.1f, 0.6f, 0.1f, 0.15f);
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(roomBounds.transform.position, roomBounds.transform.rotation, roomBounds.transform.lossyScale);
            Gizmos.DrawCube(roomBounds.center, roomBounds.size);
            Gizmos.matrix = old;
        }

        // draw spawned points
        Gizmos.color = Color.yellow;
        foreach (var t in spawned)
            if (t) Gizmos.DrawSphere(t.position + Vector3.up * 0.02f, 0.04f);
    }
    #endregion
}
