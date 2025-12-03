using UnityEngine;
using UnityEngine.AI;
using System.Collections;
#if UNITY_EDITOR
using Unity.AI.Navigation;
#else
using Unity.AI.Navigation;
#endif

/// <summary>
/// Main NPC navigation controller that wraps NavMeshAgent.
/// Handles destination setting, off-mesh link traversal, surface transitions (caves/terrain),
/// and provides fallback movement when no NavMesh path is available.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NPCNavigator : MonoBehaviour
{
    [Header("Navigation Settings")]
    [Tooltip("The target destination for this NPC")]
    public Transform targetDestination;

    [Tooltip("Update destination every frame if target is moving")]
    public bool continuouslyUpdateDestination = true;

    [Tooltip("Distance threshold to consider destination reached")]
    public float destinationThreshold = 0.5f;

    [Header("Surface Switching")]
    [Tooltip("Current active NavMeshSurface (null means use any)")]
    public NavMeshSurface currentSurface;

    [Tooltip("Should area mask be updated when entering caves?")]
    public bool useAreaMaskSwitching = false;

    [Header("Off-Mesh Link Traversal")]
    [Tooltip("Speed multiplier when traversing off-mesh links")]
    public float offMeshLinkSpeed = 1.5f;

    [Tooltip("Curve for off-mesh link movement (parabolic jump)")]
    public AnimationCurve offMeshLinkCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Fallback Movement")]
    [Tooltip("Enable simple fallback movement when NavMesh is unavailable")]
    public bool useFallbackMovement = true;

    [Tooltip("Speed for fallback direct movement")]
    public float fallbackMoveSpeed = 3.5f;

    [Tooltip("Max distance to use fallback movement before giving up")]
    public float fallbackMaxDistance = 20f;

    [Header("Debug")]
    public bool debugLogs = false;
    public bool drawDebugPath = true;
    public Color pathColor = Color.green;

    private NavMeshAgent agent;
    private bool isTraversingLink = false;
    private Vector3 lastValidPosition;
    private bool isFallbackMode = false;
    private Vector3 fallbackTarget;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        lastValidPosition = transform.position;
    }

    private void Start()
    {
        if (agent == null)
        {
            Debug.LogError("NPCNavigator: No NavMeshAgent component found!");
            enabled = false;
            return;
        }

        // Try to find nearest NavMesh position at start
        if (!agent.isOnNavMesh)
        {
            if (NavMeshSurfaceUtilities.GetNearestWalkablePosition(transform.position, out Vector3 navPos, 10f))
            {
                transform.position = navPos;
                if (debugLogs) Debug.Log($"NPCNavigator: Warped '{gameObject.name}' to nearest NavMesh position");
            }
        }
    }

    private void Update()
    {
        if (agent == null || !agent.enabled) return;

        // Handle off-mesh link traversal
        if (agent.isOnOffMeshLink && !isTraversingLink)
        {
            StartCoroutine(TraverseOffMeshLink());
            return;
        }

        // Update destination if target is set
        if (targetDestination != null && continuouslyUpdateDestination && !isTraversingLink)
        {
            SetDestination(targetDestination.position);
        }

        // Check for path issues and potentially switch to fallback
        if (useFallbackMovement && !isTraversingLink)
        {
            CheckPathStatus();
        }

        // Handle fallback movement
        if (isFallbackMode)
        {
            UpdateFallbackMovement();
        }

        // Store last valid position when on NavMesh
        if (agent.isOnNavMesh && !isFallbackMode)
        {
            lastValidPosition = transform.position;
        }
    }

    /// <summary>
    /// Set the destination for the NPC.
    /// </summary>
    public bool SetDestination(Vector3 destination)
    {
        if (agent == null || !agent.enabled) return false;

        // If in fallback mode, try to return to NavMesh first
        if (isFallbackMode)
        {
            fallbackTarget = destination;
            return true;
        }

        // Ensure we're on NavMesh
        if (!agent.isOnNavMesh)
        {
            if (debugLogs) Debug.LogWarning("NPCNavigator: Not on NavMesh, attempting to warp");
            TryReturnToNavMesh();
            return false;
        }

        // Set the destination
        bool success = agent.SetDestination(destination);

        if (!success && debugLogs)
        {
            Debug.LogWarning($"NPCNavigator: Failed to set destination to {destination}");
        }

        return success;
    }

    /// <summary>
    /// Check the current path status and handle failures.
    /// </summary>
    private void CheckPathStatus()
    {
        if (!agent.hasPath) return;

        NavMeshPathStatus status = agent.pathStatus;

        if (status == NavMeshPathStatus.PathInvalid)
        {
            if (debugLogs) Debug.Log("NPCNavigator: Path invalid, switching to fallback mode");
            EnterFallbackMode();
        }
        else if (status == NavMeshPathStatus.PathPartial)
        {
            // Partial path is okay for now, but monitor it
            if (debugLogs && Time.frameCount % 60 == 0)
            {
                Debug.Log("NPCNavigator: Path partial, NPC may not reach final destination");
            }
        }
    }

    /// <summary>
    /// Enter fallback movement mode (direct steering toward target).
    /// </summary>
    private void EnterFallbackMode()
    {
        if (isFallbackMode) return;

        isFallbackMode = true;
        fallbackTarget = targetDestination != null ? targetDestination.position : agent.destination;

        if (debugLogs) Debug.Log("NPCNavigator: Entered fallback mode");

        // Stop the agent to prevent conflicts
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
    }

    /// <summary>
    /// Update fallback movement (simple direct steering).
    /// </summary>
    private void UpdateFallbackMovement()
    {
        Vector3 toTarget = fallbackTarget - transform.position;
        float distance = toTarget.magnitude;

        // Check if we can return to NavMesh
        if (agent.isOnNavMesh && Time.frameCount % 30 == 0)
        {
            ExitFallbackMode();
            return;
        }

        // Give up if too far
        if (distance > fallbackMaxDistance)
        {
            if (debugLogs) Debug.LogWarning("NPCNavigator: Fallback target too far, giving up");
            ExitFallbackMode();
            return;
        }

        // Check if we reached the target
        if (distance < destinationThreshold)
        {
            if (debugLogs) Debug.Log("NPCNavigator: Reached fallback target");
            ExitFallbackMode();
            return;
        }

        // Move toward target
        Vector3 moveDirection = toTarget.normalized;
        transform.position += moveDirection * fallbackMoveSpeed * Time.deltaTime;
        transform.forward = moveDirection;

        // Try to sample NavMesh at current position
        if (NavMeshSurfaceUtilities.GetNearestWalkablePosition(transform.position, out Vector3 navPos, 2f))
        {
            transform.position = navPos;
            ExitFallbackMode();
        }
    }

    /// <summary>
    /// Exit fallback mode and resume NavMesh navigation.
    /// </summary>
    private void ExitFallbackMode()
    {
        if (!isFallbackMode) return;

        isFallbackMode = false;

        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            if (targetDestination != null)
            {
                SetDestination(targetDestination.position);
            }
        }

        if (debugLogs) Debug.Log("NPCNavigator: Exited fallback mode");
    }

    /// <summary>
    /// Traverse an off-mesh link smoothly (with parabolic arc).
    /// </summary>
    private IEnumerator TraverseOffMeshLink()
    {
        isTraversingLink = true;

        OffMeshLinkData linkData = agent.currentOffMeshLinkData;
        Vector3 startPos = agent.transform.position;
        Vector3 endPos = linkData.endPos;

        float duration = Vector3.Distance(startPos, endPos) / (agent.speed * offMeshLinkSpeed);
        float elapsed = 0f;

        // Disable automatic link traversal
        agent.updatePosition = false;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Apply curve for parabolic motion
            float curveT = offMeshLinkCurve.Evaluate(t);

            // Interpolate position with height arc
            Vector3 position = Vector3.Lerp(startPos, endPos, t);
            position.y += Mathf.Sin(curveT * Mathf.PI) * 1.5f; // Add jump arc

            agent.transform.position = position;

            yield return null;
        }

        // Re-enable automatic position updates
        agent.updatePosition = true;

        // Complete the link
        agent.CompleteOffMeshLink();

        isTraversingLink = false;

        if (debugLogs) Debug.Log("NPCNavigator: Completed off-mesh link traversal");
    }

    /// <summary>
    /// Try to return to the nearest NavMesh position.
    /// </summary>
    private void TryReturnToNavMesh()
    {
        if (NavMeshSurfaceUtilities.GetNearestWalkablePosition(transform.position, out Vector3 navPos, 15f))
        {
            transform.position = navPos;
            if (debugLogs) Debug.Log("NPCNavigator: Warped back to NavMesh");
        }
        else if (NavMeshSurfaceUtilities.GetNearestWalkablePosition(lastValidPosition, out navPos, 5f))
        {
            transform.position = navPos;
            if (debugLogs) Debug.Log("NPCNavigator: Warped to last valid position");
        }
    }

    /// <summary>
    /// Called when the NPC enters a cave trigger.
    /// </summary>
    public void OnEnterCave(CaveTrigger trigger)
    {
        if (trigger == null) return;

        NavMeshSurface caveSurface = trigger.GetCaveSurface();
        
        if (caveSurface != null)
        {
            currentSurface = caveSurface;
            if (debugLogs) Debug.Log($"NPCNavigator: Switched to cave surface '{caveSurface.gameObject.name}'");
        }

        // Optionally update area mask
        if (useAreaMaskSwitching)
        {
            int areaMask = trigger.GetCaveAreaMask();
            if (areaMask >= 0)
            {
                agent.areaMask = areaMask;
                if (debugLogs) Debug.Log($"NPCNavigator: Updated area mask to {areaMask}");
            }
        }
    }

    /// <summary>
    /// Called when the NPC exits a cave trigger.
    /// </summary>
    public void OnExitCave(CaveTrigger trigger)
    {
        if (trigger == null) return;

        NavMeshSurface exteriorSurface = trigger.GetExteriorSurface();
        
        if (exteriorSurface != null)
        {
            currentSurface = exteriorSurface;
            if (debugLogs) Debug.Log($"NPCNavigator: Switched to exterior surface '{exteriorSurface.gameObject.name}'");
        }
        else
        {
            currentSurface = null;
            if (debugLogs) Debug.Log("NPCNavigator: Switched to any surface");
        }

        // Reset area mask to all areas
        if (useAreaMaskSwitching)
        {
            agent.areaMask = NavMesh.AllAreas;
            if (debugLogs) Debug.Log("NPCNavigator: Reset area mask to all areas");
        }
    }

    /// <summary>
    /// Check if the NPC has reached its destination.
    /// </summary>
    public bool HasReachedDestination()
    {
        if (!agent.hasPath) return false;
        if (agent.pathPending) return false;

        return agent.remainingDistance <= destinationThreshold;
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugPath || agent == null || !agent.hasPath) return;

        Gizmos.color = pathColor;
        Vector3[] corners = agent.path.corners;

        for (int i = 0; i < corners.Length - 1; i++)
        {
            Gizmos.DrawLine(corners[i], corners[i + 1]);
        }

        // Draw destination
        if (targetDestination != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(targetDestination.position, 0.5f);
        }
    }
}
