using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;

/// <summary>
/// Singleton manager for NavMesh surfaces. Handles runtime baking of all NavMeshSurface components
/// in the scene and can rebuild specific surfaces or all surfaces.
/// </summary>
public class NavMeshManager : MonoBehaviour
{
    private static NavMeshManager instance;
    public static NavMeshManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<NavMeshManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("NavMeshManager");
                    instance = go.AddComponent<NavMeshManager>();
                }
            }
            return instance;
        }
    }

    [Header("Baking Settings")]
    [Tooltip("Automatically bake all NavMeshSurfaces on start")]
    public bool bakeOnStart = true;

    [Tooltip("Use async baking (won't block the main thread as much)")]
    public bool asyncBaking = true;

    [Tooltip("Delay before initial baking starts (in seconds)")]
    public float initialBakeDelay = 0.5f;

    [Header("Status")]
    public bool isBaking = false;

    [Header("References")]
    [Tooltip("Optional reference to OffMeshLinkAutoCreator (will auto-find if not set)")]
    public OffMeshLinkAutoCreator linkCreator;

    private List<NavMeshSurface> surfaces = new List<NavMeshSurface>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void Start()
    {
        // Cache reference to link creator if not set
        if (linkCreator == null)
        {
            linkCreator = FindObjectOfType<OffMeshLinkAutoCreator>();
        }

        if (bakeOnStart)
        {
            StartCoroutine(InitialBakeCoroutine());
        }
    }

    private IEnumerator InitialBakeCoroutine()
    {
        yield return new WaitForSeconds(initialBakeDelay);
        BakeAllSurfaces();
    }

    /// <summary>
    /// Find all NavMeshSurface components in the scene and bake them.
    /// </summary>
    public void BakeAllSurfaces()
    {
        if (isBaking)
        {
            Debug.LogWarning("NavMeshManager: Already baking, please wait.");
            return;
        }

        surfaces.Clear();
        surfaces.AddRange(NavMeshSurfaceUtilities.FindAllSurfaces());

        if (surfaces.Count == 0)
        {
            Debug.LogWarning("NavMeshManager: No NavMeshSurface components found in scene.");
            return;
        }

        Debug.Log($"NavMeshManager: Found {surfaces.Count} NavMeshSurface(s), starting bake...");

        if (asyncBaking)
        {
            StartCoroutine(BakeAllAsync());
        }
        else
        {
            BakeAllSync();
        }
    }

    /// <summary>
    /// Rebuild a specific NavMeshSurface.
    /// </summary>
    public void RebuildSurface(NavMeshSurface surface)
    {
        if (surface == null)
        {
            Debug.LogWarning("NavMeshManager: Cannot rebuild null surface.");
            return;
        }

        Debug.Log($"NavMeshManager: Rebuilding NavMeshSurface on '{surface.gameObject.name}'");
        surface.RemoveData();
        surface.BuildNavMesh();
    }

    /// <summary>
    /// Bake all surfaces synchronously (may cause frame drops).
    /// </summary>
    private void BakeAllSync()
    {
        isBaking = true;

        foreach (var surface in surfaces)
        {
            if (surface != null)
            {
                Debug.Log($"NavMeshManager: Baking '{surface.gameObject.name}'");
                surface.RemoveData();
                surface.BuildNavMesh();
            }
        }

        isBaking = false;
        OnBakingComplete();
    }

    /// <summary>
    /// Bake all surfaces asynchronously (one per frame to reduce hitching).
    /// </summary>
    private IEnumerator BakeAllAsync()
    {
        isBaking = true;

        foreach (var surface in surfaces)
        {
            if (surface != null)
            {
                Debug.Log($"NavMeshManager: Baking '{surface.gameObject.name}'");
                surface.RemoveData();
                surface.BuildNavMesh();

                // Yield to allow other frame work
                yield return null;
            }
        }

        isBaking = false;
        OnBakingComplete();
    }

    /// <summary>
    /// Called when all surfaces have been baked.
    /// </summary>
    private void OnBakingComplete()
    {
        Debug.Log("NavMeshManager: Baking complete.");

        // Refresh the surfaces cache since baking may have changed them
        NavMeshSurfaceUtilities.RefreshSurfacesCache();

        // Automatically create off-mesh links if the component exists
        if (linkCreator != null)
        {
            linkCreator.CreateLinks();
        }
    }

    /// <summary>
    /// Get list of all managed surfaces.
    /// </summary>
    public List<NavMeshSurface> GetSurfaces()
    {
        if (surfaces.Count == 0)
        {
            surfaces.AddRange(NavMeshSurfaceUtilities.FindAllSurfaces());
        }
        return surfaces;
    }
}
