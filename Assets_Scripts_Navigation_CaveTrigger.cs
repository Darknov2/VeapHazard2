using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using Unity.AI.Navigation;
#else
using Unity.AI.Navigation;
#endif

/// <summary>
/// Attach this component to cave entrance trigger colliders.
/// When an NPC enters, it notifies the NPC to switch to the cave NavMeshSurface.
/// When the NPC leaves, it switches back to the main terrain surface.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CaveTrigger : MonoBehaviour
{
    [Header("Cave Settings")]
    [Tooltip("The NavMeshSurface representing the cave interior")]
    public NavMeshSurface caveSurface;

    [Tooltip("The NavMeshSurface representing the exterior/terrain (optional - will auto-detect if not set)")]
    public NavMeshSurface exteriorSurface;

    [Tooltip("Alternative: specify NavMesh area mask for cave (if not using surface switching)")]
    public int caveAreaMask = -1; // -1 means use all areas

    [Header("Trigger Settings")]
    [Tooltip("Tag to identify NPCs (leave empty to trigger on any object with NPCNavigator)")]
    public string npcTag = "NPC";

    [Header("Debug")]
    public bool debugLogs = false;

    private Collider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (!triggerCollider.isTrigger)
        {
            Debug.LogWarning($"CaveTrigger on '{gameObject.name}': Collider is not set as trigger. Setting it now.");
            triggerCollider.isTrigger = true;
        }

        // Try to auto-find cave surface if not set
        if (caveSurface == null)
        {
            caveSurface = GetComponentInChildren<NavMeshSurface>();
            if (caveSurface == null)
            {
                caveSurface = GetComponentInParent<NavMeshSurface>();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if this is an NPC
        if (!IsNPC(other)) return;

        NPCNavigator navigator = other.GetComponent<NPCNavigator>();
        if (navigator == null)
        {
            navigator = other.GetComponentInParent<NPCNavigator>();
        }

        if (navigator != null)
        {
            if (debugLogs)
            {
                Debug.Log($"CaveTrigger: NPC '{other.gameObject.name}' entering cave");
            }

            navigator.OnEnterCave(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Check if this is an NPC
        if (!IsNPC(other)) return;

        NPCNavigator navigator = other.GetComponent<NPCNavigator>();
        if (navigator == null)
        {
            navigator = other.GetComponentInParent<NPCNavigator>();
        }

        if (navigator != null)
        {
            if (debugLogs)
            {
                Debug.Log($"CaveTrigger: NPC '{other.gameObject.name}' exiting cave");
            }

            navigator.OnExitCave(this);
        }
    }

    private bool IsNPC(Collider other)
    {
        // If tag is specified, check tag
        if (!string.IsNullOrEmpty(npcTag))
        {
            return other.CompareTag(npcTag);
        }

        // Otherwise, check if it has NPCNavigator component
        return other.GetComponent<NPCNavigator>() != null || other.GetComponentInParent<NPCNavigator>() != null;
    }

    /// <summary>
    /// Get the NavMeshSurface for this cave.
    /// </summary>
    public NavMeshSurface GetCaveSurface()
    {
        return caveSurface;
    }

    /// <summary>
    /// Get the exterior NavMeshSurface.
    /// </summary>
    public NavMeshSurface GetExteriorSurface()
    {
        if (exteriorSurface != null) return exteriorSurface;

        // Try to auto-find exterior surface (the first surface that's not this cave)
        NavMeshSurface[] surfaces = FindObjectsOfType<NavMeshSurface>();
        foreach (var surface in surfaces)
        {
            if (surface != caveSurface)
            {
                return surface;
            }
        }

        return null;
    }

    /// <summary>
    /// Get the area mask for the cave (if using area-based switching).
    /// </summary>
    public int GetCaveAreaMask()
    {
        return caveAreaMask;
    }

    private void OnDrawGizmos()
    {
        if (triggerCollider == null) triggerCollider = GetComponent<Collider>();

        Gizmos.color = new Color(0.5f, 0.2f, 0.8f, 0.3f);
        if (triggerCollider is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
        else if (triggerCollider is SphereCollider sphere)
        {
            Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
        }
    }
}
