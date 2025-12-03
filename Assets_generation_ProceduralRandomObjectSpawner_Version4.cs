using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Random object spawner adapted to ProceduralTerrainGenerator.
/// - Adds three spawn categories: vegetation (surface), underground (below surface), floating islands (above surface).
/// - Uses the same StructureSpawnSettings type you already have; separate lists per category let you tune each group in the inspector.
/// - For underground/floating spawns there are global min/max parameters (per-category depth/height).
/// </summary>
public class ProceduralRandomObjectSpawner : MonoBehaviour
{
    [Header("References")]
    public ProceduralTerrainGenerator terrain; // Procedural terrain reference
    [Tooltip("Optional component with a public BuildNavMesh() method (e.g., NavMeshSurface).")]
    public Component navMeshSurface;

    [Header("Grid Settings")]
    [Tooltip("Spacing of the spawning grid in world units. Typically equals your voxelScale or a multiple of it.")]
    public float cellSize = 1f;
    [Tooltip("Offset applied after snapping to grid.")]
    public Vector3 snapOffset = Vector3.zero;

    [Header("Structure Spawn Settings (surface)")]
    [Tooltip("Structures/objects that spawn on the ground surface.")]
    public List<StructureSpawnSettings> structureSpawnSettings;

    [Header("Vegetation (surface)")]
    [Tooltip("Vegetation prefabs that spawn on the surface (uses same StructureSpawnSettings structure).")]
    public List<StructureSpawnSettings> vegetationSpawnSettings;

    [Header("Underground (below surface)")]
    [Tooltip("Structures that should attempt to spawn underground (in cavities).")]
    public List<StructureSpawnSettings> undergroundSpawnSettings;
    [Tooltip("Min depth below surface when attempting underground spawn (world units).")]
    public float undergroundMinDepth = 2f;
    [Tooltip("Max depth below surface when attempting underground spawn (world units).")]
    public float undergroundMaxDepth = 8f;
    [Tooltip("If true, underground spawns will only be placed if the target position has no overlapping colliders (i.e. open cavity).")]
    public bool undergroundRequireEmptySpace = true;

    [Header("Floating Islands (above surface)")]
    [Tooltip("Structures that should attempt to spawn on floating islands (above surface).")]
    public List<StructureSpawnSettings> floatingIslandSpawnSettings;
    [Tooltip("Min height above surface for floating island spawns.")]
    public float floatingMinHeight = 5f;
    [Tooltip("Max height above surface for floating island spawns.")]
    public float floatingMaxHeight = 30f;
    [Tooltip("If true, floating spawn will only succeed if the chosen position has no overlapping colliders.")]
    public bool floatingRequireEmptySpace = true;

    [Header("Spawn Timing")]
    [Tooltip("Extra delay after terrain reports ready (seconds).")]
    public float spawnDelaySeconds = 0.1f;

    private bool hasSpawned = false;
    private int gridCellsX = 10;
    private int gridCellsY = 2;
    private int gridCellsZ = 10;

    // Track placed colliders and their origin settings
    private readonly List<(Collider col, StructureSpawnSettings setting)> placedColliders = new List<(Collider, StructureSpawnSettings)>();

    private ProceduralTerrainConfig Cfg => terrain != null ? terrain.config : null;
    private Vector3 GridOrigin => Cfg != null ? Cfg.worldOffset : Vector3.zero;

    private void OnEnable()
    {
        // Subscribe to "ready" event if terrain is assigned
        if (terrain != null)
            terrain.OnInitialTerrainReady += HandleTerrainReady;
    }

    private void OnDisable()
    {
        if (terrain != null)
            terrain.OnInitialTerrainReady -= HandleTerrainReady;
    }

    private void Start()
    {
        if (terrain == null)
        {
            terrain = FindObjectOfType<ProceduralTerrainGenerator>();
            if (terrain == null)
                Debug.LogWarning("ProceduralRandomObjectSpawner: No ProceduralTerrainGenerator found in scene.");
            else
                terrain.OnInitialTerrainReady += HandleTerrainReady;
        }

        if (Cfg != null)
        {
            if (cellSize <= 0f) cellSize = Mathf.Max(0.1f, Cfg.voxelScale);

            // Compute initial grid bounds from static config.
            float sizeX = Cfg.chunksX * Cfg.chunkSizeXZ * Cfg.voxelScale;
            float sizeY = Mathf.Max(1, Cfg.chunksY) * Cfg.chunkSizeY * Cfg.voxelScale;
            float sizeZ = Cfg.chunksZ * Cfg.chunkSizeXZ * Cfg.voxelScale;

            gridCellsX = Mathf.Max(1, Mathf.CeilToInt(sizeX / cellSize));
            gridCellsY = Mathf.Max(1, Mathf.CeilToInt(sizeY / cellSize));
            gridCellsZ = Mathf.Max(1, Mathf.CeilToInt(sizeZ / cellSize));
        }
        else
        {
            Debug.LogWarning("ProceduralRandomObjectSpawner: Terrain config not found, using default grid settings.");
        }

        // If terrain is already ready, spawn; otherwise, wait via event/polling
        if (terrain != null && terrain.IsInitialTerrainReady)
            StartCoroutine(SpawnAfterDelay());
        else
            StartCoroutine(SpawnWhenReady());
    }

    private void HandleTerrainReady()
    {
        // Terrain signaled ready; trigger spawn if not already started
        if (!hasSpawned) StartCoroutine(SpawnAfterDelay());
    }

    private IEnumerator SpawnWhenReady()
    {
        // Fallback polling in case event was missed (e.g., enabled late)
        while (terrain == null || Cfg == null || !terrain.IsInitialTerrainReady)
            yield return null;

        yield return SpawnAfterDelay();
    }

    private IEnumerator SpawnAfterDelay()
    {
        if (spawnDelaySeconds > 0f) yield return new WaitForSeconds(spawnDelaySeconds);
        // One more frame to ensure colliders/meshes are fully in scene
        yield return null;

        SpawnObjectsRandomlyOnGrid();
    }

    private void SpawnObjectsRandomlyOnGrid()
    {
        if (hasSpawned) return;
        hasSpawned = true;

        if ((structureSpawnSettings == null || structureSpawnSettings.Count == 0) &&
            (vegetationSpawnSettings == null || vegetationSpawnSettings.Count == 0) &&
            (undergroundSpawnSettings == null || undergroundSpawnSettings.Count == 0) &&
            (floatingIslandSpawnSettings == null || floatingIslandSpawnSettings.Count == 0))
        {
            Debug.LogWarning("ProceduralRandomObjectSpawner: No spawn settings assigned.");
            return;
        }

        // Surface structures (legacy)
        if (structureSpawnSettings != null && structureSpawnSettings.Count > 0)
            SpawnCategory(structureSpawnSettings, TryFindSurfaceGridPosition);

        // Vegetation (surface)
        if (vegetationSpawnSettings != null && vegetationSpawnSettings.Count > 0)
            SpawnCategory(vegetationSpawnSettings, TryFindSurfaceGridPosition);

        // Underground
        if (undergroundSpawnSettings != null && undergroundSpawnSettings.Count > 0)
            SpawnCategory(undergroundSpawnSettings, TryFindUndergroundGridPosition);

        // Floating islands
        if (floatingIslandSpawnSettings != null && floatingIslandSpawnSettings.Count > 0)
            SpawnCategory(floatingIslandSpawnSettings, TryFindFloatingGridPosition);

        TryBuildNavMesh();
    }

    // Generic category spawner that takes a position-finder delegate
    private delegate bool TryFindPosDelegate(Vector3Int cell, out Vector3 pos);

    private void SpawnCategory(List<StructureSpawnSettings> settingsList, TryFindPosDelegate tryFindPos)
    {
        foreach (var setting in settingsList)
        {
            if (setting == null || setting.prefab == null) continue;

            var availableCells = new List<Vector3Int>();
            for (int gx = 0; gx < gridCellsX; gx++)
            for (int gz = 0; gz < gridCellsZ; gz++)
                availableCells.Add(new Vector3Int(gx, 0, gz)); // Y resolved later

            int objectsToSpawn = Mathf.Min(setting.objectsToSpawn, availableCells.Count);
            var placedCells = new HashSet<Vector3Int>();
            int attempts = 0;
            int maxAttempts = objectsToSpawn * 10;

            while (placedCells.Count < objectsToSpawn && availableCells.Count > 0 && attempts < maxAttempts)
            {
                attempts++;
                int idx = Random.Range(0, availableCells.Count);
                Vector3Int cell = availableCells[idx];
                availableCells.RemoveAt(idx);

                if (placedCells.Contains(cell))
                    continue;

                if (!tryFindPos(cell, out Vector3 placePosition))
                    continue;

                Quaternion rot = Quaternion.Euler(0, GetRandomCardinalAngle(), 0);

                var obj = Instantiate(setting.prefab, placePosition, rot, this.transform);
                // keep tagging behavior as before
                obj.tag = "PlacedObject";

                // Overlap filtering for children (same logic as before)
                List<Collider> childrenToKeep = new List<Collider>();
                Collider[] allChildColliders = obj.GetComponentsInChildren<Collider>();
                foreach (var childCol in allChildColliders)
                {
                    if (!childCol.enabled) continue;
                    bool overlaps = false;

                    if (setting.childrenAffectedByOverlap)
                    {
                        Bounds childBounds = childCol.bounds;
                        childBounds.Expand(-setting.overlapMargin);

                        foreach (var (placedCol, placedSetting) in placedColliders)
                        {
                            if (!placedSetting.childrenAffectOthers) continue;
                            if (placedCol == null) continue;

                            Bounds placedBounds = placedCol.bounds;
                            placedBounds.Expand(-placedSetting.overlapMargin);

                            if (childBounds.Intersects(placedBounds))
                            {
                                overlaps = true;
                                break;
                            }
                        }
                    }

                    if (overlaps)
                        Destroy(childCol.gameObject);
                    else
                        childrenToKeep.Add(childCol);
                }

                if (childrenToKeep.Count == 0)
                {
                    Destroy(obj);
                    continue;
                }
                else if (setting.childrenAffectOthers)
                {
                    foreach (var c in childrenToKeep)
                        placedColliders.Add((c, setting));
                }

                placedCells.Add(cell);
            }
        }
    }

    // Surface position finder (same behavior as before)
    private bool TryFindSurfaceGridPosition(Vector3Int cell, out Vector3 result)
    {
        result = Vector3.zero;
        Vector3 origin = GridOrigin;
        float x = origin.x + cell.x * cellSize;
        float z = origin.z + cell.z * cellSize;

        float startY;
        if (Cfg != null)
        {
            float byGrid = origin.y + Mathf.Max(1, Cfg.chunksY) * Cfg.chunkSizeY * Cfg.voxelScale + 10f;
            float byNoise = Cfg.surfaceBaseHeight + Mathf.Abs(Cfg.surfaceNoiseAmplitude) * 3f + 50f;
            startY = Mathf.Max(byGrid, byNoise);
        }
        else startY = origin.y + 200f;

        Vector3 rayOrigin = new Vector3(x, startY, z);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 5000f))
        {
            int gridY = Mathf.FloorToInt((hit.point.y - origin.y) / cellSize);
            float y = origin.y + gridY * cellSize;
            result = new Vector3(x, y, z) + snapOffset;
            return true;
        }
        return false;
    }

    // Underground finder: raycast to surface, then step down between min/max depth and require empty space
    private bool TryFindUndergroundGridPosition(Vector3Int cell, out Vector3 result)
    {
        result = Vector3.zero;
        if (!TryFindSurfaceGridPosition(cell, out Vector3 surfacePos))
            return false;

        float surfaceY = surfacePos.y;
        float depth = Random.Range(undergroundMinDepth, undergroundMaxDepth);
        Vector3 candidate = new Vector3(surfacePos.x, surfaceY - depth, surfacePos.z) + snapOffset;

        // Check bounds: do not go below terrain origin too far
        float minY = GridOrigin.y - (Cfg != null ? 5f * cellSize : 50f);
        if (candidate.y < minY) candidate.y = minY;

        if (undergroundRequireEmptySpace)
        {
            // small radius check to ensure the target point is in open space (a cave)
            float r = Mathf.Max(0.25f, cellSize * 0.3f);
            var cols = Physics.OverlapSphere(candidate, r);
            if (cols != null && cols.Length > 0) return false;
        }

        result = candidate;
        return true;
    }

    // Floating island finder: raycast to surface, then pick a height above surface and ensure empty space
    private bool TryFindFloatingGridPosition(Vector3Int cell, out Vector3 result)
    {
        result = Vector3.zero;
        if (!TryFindSurfaceGridPosition(cell, out Vector3 surfacePos))
            return false;

        float height = Random.Range(floatingMinHeight, floatingMaxHeight);
        Vector3 candidate = new Vector3(surfacePos.x, surfacePos.y + height, surfacePos.z) + snapOffset;

        if (floatingRequireEmptySpace)
        {
            float r = Mathf.Max(0.25f, cellSize * 0.4f);
            var cols = Physics.OverlapSphere(candidate, r);
            if (cols != null && cols.Length > 0) return false;
        }

        result = candidate;
        return true;
    }

    private void TryBuildNavMesh()
    {
        if (navMeshSurface == null) return;
        var m = navMeshSurface.GetType().GetMethod("BuildNavMesh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (m != null) m.Invoke(navMeshSurface, null);
    }

    public float GetRandomCardinalAngle()
    {
        float[] angles = { 0f, 90f, 180f, 270f };
        return angles[Random.Range(0, angles.Length)];
    }
}