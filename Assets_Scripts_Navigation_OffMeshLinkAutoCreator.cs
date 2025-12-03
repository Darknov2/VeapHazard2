using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Unity.AI.Navigation;

/// <summary>
/// Automatically creates NavMeshLink components between nearby NavMeshSurface boundaries.
/// This allows NPCs to traverse small gaps, jump between islands, or move between terrain and caves.
/// </summary>
public class OffMeshLinkAutoCreator : MonoBehaviour
{
    [Header("Link Creation Settings")]
    [Tooltip("Maximum distance between surfaces to create a link")]
    public float maxLinkDistance = 4f;

    [Tooltip("Number of sample points to test per surface edge")]
    public int samplesPerSurface = 20;

    [Tooltip("Minimum height difference to consider creating a link")]
    public float minHeightDifference = 0.5f;

    [Tooltip("Maximum height difference for a traversable link")]
    public float maxHeightDifference = 5f;

    [Header("Link Properties")]
    [Tooltip("Should links be bidirectional?")]
    public bool bidirectional = true;

    [Tooltip("Area type for created links")]
    public int areaType = 0;

    [Tooltip("Parent object to organize created links")]
    public Transform linksParent;

    [Header("Debug")]
    public bool debugVisualization = true;
    public Color linkColor = Color.cyan;

    private const int edgeSamplesPerSide = 4; // Number of edge sides to distribute samples
    private List<NavMeshLink> createdLinks = new List<NavMeshLink>();

    private void Start()
    {
        if (linksParent == null)
        {
            GameObject parent = new GameObject("OffMeshLinks");
            linksParent = parent.transform;
        }
    }

    /// <summary>
    /// Create all off-mesh links between surfaces.
    /// </summary>
    public void CreateLinks()
    {
        ClearExistingLinks();

        NavMeshSurface[] surfaces = NavMeshSurfaceUtilities.FindAllSurfaces();
        if (surfaces.Length < 2)
        {
            Debug.LogWarning("OffMeshLinkAutoCreator: Need at least 2 NavMeshSurfaces to create links.");
            return;
        }

        Debug.Log($"OffMeshLinkAutoCreator: Creating links between {surfaces.Length} surfaces...");

        int linksCreated = 0;

        // Compare each pair of surfaces
        for (int i = 0; i < surfaces.Length; i++)
        {
            for (int j = i + 1; j < surfaces.Length; j++)
            {
                linksCreated += CreateLinksBetweenSurfaces(surfaces[i], surfaces[j]);
            }
        }

        Debug.Log($"OffMeshLinkAutoCreator: Created {linksCreated} off-mesh links.");
    }

    /// <summary>
    /// Create links between two specific surfaces.
    /// </summary>
    private int CreateLinksBetweenSurfaces(NavMeshSurface surfaceA, NavMeshSurface surfaceB)
    {
        if (surfaceA == null || surfaceB == null) return 0;
        if (surfaceA.navMeshData == null || surfaceB.navMeshData == null) return 0;

        Bounds boundsA = NavMeshSurfaceUtilities.GetSurfaceBounds(surfaceA);
        Bounds boundsB = NavMeshSurfaceUtilities.GetSurfaceBounds(surfaceB);

        // Early out if bounds are too far apart
        float boundsDistance = Vector3.Distance(boundsA.center, boundsB.center);
        if (boundsDistance > boundsA.extents.magnitude + boundsB.extents.magnitude + maxLinkDistance)
        {
            return 0;
        }

        int linksCreated = 0;
        List<Vector3> samplesA = GetSurfaceSamplePoints(surfaceA, boundsA);
        List<Vector3> samplesB = GetSurfaceSamplePoints(surfaceB, boundsB);

        // Find pairs within link distance
        foreach (Vector3 posA in samplesA)
        {
            foreach (Vector3 posB in samplesB)
            {
                float distance = Vector3.Distance(posA, posB);
                if (distance > maxLinkDistance) continue;

                float heightDiff = Mathf.Abs(posA.y - posB.y);
                if (heightDiff < minHeightDifference || heightDiff > maxHeightDifference) continue;

                // Verify both positions are on NavMesh
                if (!VerifyNavMeshPosition(posA) || !VerifyNavMeshPosition(posB)) continue;

                // Create the link
                CreateLink(posA, posB);
                linksCreated++;
            }
        }

        return linksCreated;
    }

    /// <summary>
    /// Get sample points around a surface's bounds.
    /// </summary>
    private List<Vector3> GetSurfaceSamplePoints(NavMeshSurface surface, Bounds bounds)
    {
        List<Vector3> samples = new List<Vector3>();

        // Sample points around the perimeter and center
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3 center = bounds.center;

        // Add corner samples
        samples.Add(new Vector3(min.x, center.y, min.z));
        samples.Add(new Vector3(max.x, center.y, min.z));
        samples.Add(new Vector3(min.x, center.y, max.z));
        samples.Add(new Vector3(max.x, center.y, max.z));

        // Add edge samples
        int edgeSamples = Mathf.Max(1, samplesPerSurface / edgeSamplesPerSide);
        for (int i = 0; i < edgeSamples; i++)
        {
            float t = (float)i / edgeSamples;

            // Front and back edges
            samples.Add(Vector3.Lerp(new Vector3(min.x, center.y, min.z), new Vector3(max.x, center.y, min.z), t));
            samples.Add(Vector3.Lerp(new Vector3(min.x, center.y, max.z), new Vector3(max.x, center.y, max.z), t));

            // Left and right edges
            samples.Add(Vector3.Lerp(new Vector3(min.x, center.y, min.z), new Vector3(min.x, center.y, max.z), t));
            samples.Add(Vector3.Lerp(new Vector3(max.x, center.y, min.z), new Vector3(max.x, center.y, max.z), t));
        }

        // Project samples onto NavMesh
        List<Vector3> validSamples = new List<Vector3>();
        foreach (Vector3 sample in samples)
        {
            if (NavMeshSurfaceUtilities.GetNearestWalkablePosition(sample, out Vector3 navPos, maxLinkDistance))
            {
                validSamples.Add(navPos);
            }
        }

        return validSamples;
    }

    /// <summary>
    /// Verify a position is on the NavMesh.
    /// </summary>
    private bool VerifyNavMeshPosition(Vector3 position)
    {
        NavMeshHit hit;
        return NavMesh.SamplePosition(position, out hit, 1f, NavMesh.AllAreas);
    }

    /// <summary>
    /// Create a single NavMeshLink between two positions.
    /// </summary>
    private void CreateLink(Vector3 start, Vector3 end)
    {
        GameObject linkObj = new GameObject($"Link_{createdLinks.Count}");
        linkObj.transform.SetParent(linksParent);
        linkObj.transform.position = start;

        NavMeshLink link = linkObj.AddComponent<NavMeshLink>();
        link.startPoint = Vector3.zero;
        link.endPoint = linkObj.transform.InverseTransformPoint(end);
        link.bidirectional = bidirectional;
        link.area = areaType;
        link.autoUpdatePositions = false;

        createdLinks.Add(link);
    }

    /// <summary>
    /// Clear all previously created links.
    /// </summary>
    public void ClearExistingLinks()
    {
        foreach (var link in createdLinks)
        {
            if (link != null && link.gameObject != null)
            {
                Destroy(link.gameObject);
            }
        }
        createdLinks.Clear();
    }

    private void OnDrawGizmos()
    {
        if (!debugVisualization) return;

        Gizmos.color = linkColor;
        foreach (var link in createdLinks)
        {
            if (link != null)
            {
                Vector3 start = link.transform.TransformPoint(link.startPoint);
                Vector3 end = link.transform.TransformPoint(link.endPoint);
                Gizmos.DrawLine(start, end);
                Gizmos.DrawWireSphere(start, 0.3f);
                Gizmos.DrawWireSphere(end, 0.3f);
            }
        }
    }
}
