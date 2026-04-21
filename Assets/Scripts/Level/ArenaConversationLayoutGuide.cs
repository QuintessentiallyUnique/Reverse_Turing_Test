using UnityEngine;

public class ArenaConversationLayoutGuide : MonoBehaviour
{
    [Header("Anchors")]
    [SerializeField] private Transform center;
    [SerializeField] private Transform playerSpawn;
    [SerializeField] private Transform[] npcAnchors;

    [Header("Layout")]
    [SerializeField] private float npcRadius = 4.25f;
    [SerializeField] private float playerDistance = 3.5f;
    [SerializeField] private float anchorHeight = 0f;
    [SerializeField] private bool faceCenter = true;

    [Header("Gizmos")]
    [SerializeField] private bool showGuides = true;
    [SerializeField] private Color npcColor = new Color(0.2f, 0.8f, 1f, 1f);
    [SerializeField] private Color playerColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color ringColor = new Color(1f, 1f, 1f, 0.22f);

    [ContextMenu("Apply Circular Layout")]
    public void ApplyCircularLayout()
    {
        if (center == null)
        {
            center = transform;
        }

        Vector3 centerPos = center.position;
        int validCount = 0;
        for (int i = 0; i < npcAnchors.Length; i++)
        {
            if (npcAnchors[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return;
        }

        int placed = 0;
        for (int i = 0; i < npcAnchors.Length; i++)
        {
            Transform anchor = npcAnchors[i];
            if (anchor == null)
            {
                continue;
            }

            float angle = (Mathf.PI * 2f * placed) / validCount;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            anchor.position = centerPos + dir * npcRadius + Vector3.up * anchorHeight;

            if (faceCenter)
            {
                Vector3 lookDir = centerPos - anchor.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    anchor.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                }
            }

            placed++;
        }

        if (playerSpawn != null)
        {
            Vector3 forward = center.forward;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            playerSpawn.position = centerPos - forward.normalized * playerDistance + Vector3.up * anchorHeight;
            if (faceCenter)
            {
                Vector3 lookDir = centerPos - playerSpawn.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    playerSpawn.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGuides)
        {
            return;
        }

        Transform centerTransform = center == null ? transform : center;
        Vector3 centerPos = centerTransform.position;

        Gizmos.color = ringColor;
        DrawRing(centerPos, npcRadius, 36);

        if (playerSpawn != null)
        {
            Gizmos.color = playerColor;
            Gizmos.DrawLine(centerPos, playerSpawn.position);
            Gizmos.DrawSphere(playerSpawn.position, 0.12f);
        }

        Gizmos.color = npcColor;
        for (int i = 0; i < npcAnchors.Length; i++)
        {
            if (npcAnchors[i] == null)
            {
                continue;
            }

            Gizmos.DrawLine(centerPos, npcAnchors[i].position);
            Gizmos.DrawSphere(npcAnchors[i].position, 0.12f);
        }
    }

    private static void DrawRing(Vector3 centerPos, float radius, int segments)
    {
        Vector3 previous = centerPos + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = t * Mathf.PI * 2f;
            Vector3 next = centerPos + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
