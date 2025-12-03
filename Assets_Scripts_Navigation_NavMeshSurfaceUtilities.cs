using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>
/// Helper utilities for working with NavMesh surfaces and finding nearest walkable positions.
/// </summary>
public static class NavMeshSurfaceUtilities
{
    /// <summary>
    /// Find the nearest NavMeshSurface component to the given world position.
    /// </summary>
    public static NavMeshSurface GetNearestSurface(Vector3 position)
    {
        NavMeshSurface[] surfaces = Object.FindObjectsOfType<NavMeshSurface>();
        NavMeshSurface nearest = null;
        float minDistance = float.MaxValue;

        foreach (var surface in surfaces)
        {
            if (surface.navMeshData == null) continue;

            float distance = Vector3.Distance(position, surface.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = surface;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Find the nearest walkable position on any NavMesh from the given position.
    /// </summary>
    public static bool GetNearestWalkablePosition(Vector3 position, out Vector3 result, float maxDistance = 10f)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(position, out hit, maxDistance, NavMesh.AllAreas))
        {
            result = hit.position;
            return true;
        }

        result = position;
        return false;
    }

    /// <summary>
    /// Check if a position is on a specific NavMeshSurface.
    /// </summary>
    public static bool IsOnSurface(Vector3 position, NavMeshSurface surface, float maxDistance = 2f)
    {
        if (surface == null || surface.navMeshData == null) return false;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(position, out hit, maxDistance, NavMesh.AllAreas))
        {
            // Try to determine if this hit belongs to our surface
            // This is a heuristic based on proximity to the surface's bounds
            Bounds surfaceBounds = GetSurfaceBounds(surface);
            return surfaceBounds.Contains(hit.position);
        }

        return false;
    }

    /// <summary>
    /// Get approximate bounds of a NavMeshSurface based on its colliders or size.
    /// </summary>
    public static Bounds GetSurfaceBounds(NavMeshSurface surface)
    {
        if (surface == null) return new Bounds();

        // Try to get bounds from colliders first
        Collider[] colliders = surface.GetComponentsInChildren<Collider>();
        if (colliders.Length > 0)
        {
            Bounds bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                bounds.Encapsulate(colliders[i].bounds);
            }
            return bounds;
        }

        // Fallback to a reasonable default around the surface position
        Vector3 size = new Vector3(100, 50, 100);
        if (surface.size != Vector3.zero)
        {
            size = surface.size;
        }

        return new Bounds(surface.transform.position + surface.center, size);
    }

    /// <summary>
    /// Find all NavMeshSurface components in the scene.
    /// </summary>
    public static NavMeshSurface[] FindAllSurfaces()
    {
        return Object.FindObjectsOfType<NavMeshSurface>();
    }
}
