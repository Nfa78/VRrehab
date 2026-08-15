using UnityEngine;

[DisallowMultipleComponent]
public class WaterSpillTrigger : MonoBehaviour
{
    [SerializeField] private bool logTriggerContacts = true;
    [SerializeField] private float stayLogInterval = 0.5f;
    [SerializeField] private bool logOverlapProbe = true;
    [SerializeField] private float overlapProbeInterval = 0.5f;
    [SerializeField] private bool includeSelfInOverlapProbe;
    [SerializeField] private bool ignoreOvrRigInLogs = true;
    [SerializeField] private bool drawDebugGizmo = true;
    [SerializeField] private Color debugGizmoColor = new Color(0.2f, 0.8f, 1f, 0.3f);

    private float nextStayLogTime;
    private float nextOverlapProbeTime;
    private readonly Collider[] overlapResults = new Collider[32];

    private void OnTriggerEnter(Collider other)
    {
        if (ShouldIgnoreCollider(other))
        {
            return;
        }

        if (!logTriggerContacts)
        {
            NotifyFlowerPot(other);
            return;
        }

        Debug.Log($"[WaterSpillTrigger] Enter with {DescribeCollider(other)} on {name}.", this);
        NotifyFlowerPot(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (ShouldIgnoreCollider(other))
        {
            return;
        }

        NotifyFlowerPot(other);

        if (!logTriggerContacts || Time.time < nextStayLogTime)
        {
            return;
        }

        nextStayLogTime = Time.time + stayLogInterval;
        Debug.Log($"[WaterSpillTrigger] Stay with {DescribeCollider(other)} on {name}.", this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (ShouldIgnoreCollider(other))
        {
            return;
        }

        if (!logTriggerContacts)
        {
            return;
        }

        Debug.Log($"[WaterSpillTrigger] Exit with {DescribeCollider(other)} on {name}.", this);
    }

    public void ProbeCurrentOverlaps(bool forceLog = false)
    {
        if (!logOverlapProbe && !forceLog)
        {
            return;
        }

        if (!forceLog && Time.time < nextOverlapProbeTime)
        {
            return;
        }

        nextOverlapProbeTime = Time.time + overlapProbeInterval;

        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            Debug.LogWarning("[WaterSpillTrigger] Probe requested, but no CapsuleCollider is attached.", this);
            return;
        }

        int count = Physics.OverlapCapsuleNonAlloc(
            GetCapsulePoint(capsule, false),
            GetCapsulePoint(capsule, true),
            GetCapsuleRadius(capsule),
            overlapResults,
            ~0,
            QueryTriggerInteraction.Collide);

        if (count == 0)
        {
            Debug.Log($"[WaterSpillTrigger] Probe found no overlaps for {name}.", this);
            return;
        }

        int ignoredSelfCount = 0;
        int ignoredOvrCount = 0;
        int reportedCount = 0;

        for (int i = 0; i < count; i++)
        {
            Collider other = overlapResults[i];
            if (other == null)
            {
                continue;
            }

            if (IsSelfCollider(other))
            {
                if (!includeSelfInOverlapProbe)
                {
                    ignoredSelfCount++;
                    continue;
                }
            }

            if (IsOvrRigCollider(other))
            {
                if (ignoreOvrRigInLogs)
                {
                    ignoredOvrCount++;
                    continue;
                }
            }

            if (ShouldIgnoreCollider(other))
            {
                continue;
            }

            reportedCount++;
            Debug.Log($"[WaterSpillTrigger] Probe overlap with {DescribeCollider(other)}.", other);
        }

        if (reportedCount == 0)
        {
            Debug.Log(
                $"[WaterSpillTrigger] Probe found {count} raw overlap(s) for {name}, but all were filtered. ignoredSelf={ignoredSelfCount}, ignoredOvr={ignoredOvrCount}.",
                this);
        }
    }

    private void NotifyFlowerPot(Collider other)
    {
        if (other == null)
        {
            return;
        }

        FlowerPot flowerPot = other.GetComponent<FlowerPot>();
        if (flowerPot == null)
        {
            flowerPot = other.GetComponentInParent<FlowerPot>();
        }

        if (flowerPot != null)
        {
            flowerPot.HandleWaterTrigger(this, other);
        }
    }

    private bool IsSelfCollider(Collider other)
    {
        return other != null && other.transform.root == transform.root;
    }

    private bool IsOvrRigCollider(Collider other)
    {
        return other != null &&
               other.transform.root != null &&
               other.transform.root.name == "OVRCameraRig";
    }

    private bool ShouldIgnoreCollider(Collider other)
    {
        if (other == null)
        {
            return true;
        }

        if (!includeSelfInOverlapProbe && IsSelfCollider(other))
        {
            return true;
        }

        if (ignoreOvrRigInLogs && IsOvrRigCollider(other))
        {
            return true;
        }

        return false;
    }

    private Vector3 GetCapsulePoint(CapsuleCollider capsule, bool upperPoint)
    {
        Vector3 center = transform.TransformPoint(capsule.center);
        float radius = GetCapsuleRadius(capsule);
        float cylinderHalfHeight = Mathf.Max(0f, capsule.height * 0.5f - radius);
        Vector3 axis = GetCapsuleAxis(capsule);
        return center + axis * (upperPoint ? cylinderHalfHeight : -cylinderHalfHeight);
    }

    private float GetCapsuleRadius(CapsuleCollider capsule)
    {
        Vector3 lossyScale = transform.lossyScale;
        switch (capsule.direction)
        {
            case 0:
                return capsule.radius * Mathf.Max(Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
            case 1:
                return capsule.radius * Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z));
            default:
                return capsule.radius * Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y));
        }
    }

    private Vector3 GetCapsuleAxis(CapsuleCollider capsule)
    {
        switch (capsule.direction)
        {
            case 0:
                return transform.right;
            case 1:
                return transform.up;
            default:
                return transform.forward;
        }
    }

    private static string DescribeCollider(Collider other)
    {
        string rootName = other.transform.root != null ? other.transform.root.name : "<no root>";
        string flowerPotName = other.GetComponentInParent<FlowerPot>() != null ? other.GetComponentInParent<FlowerPot>().name : "-";
        return
            $"{other.name} ({other.GetType().Name}) root={rootName} tag={other.tag} layer={LayerMask.LayerToName(other.gameObject.layer)} flowerPot={flowerPotName}";
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugGizmo)
        {
            return;
        }

        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            return;
        }

        Gizmos.color = debugGizmoColor;
        Vector3 lower = GetCapsulePoint(capsule, false);
        Vector3 upper = GetCapsulePoint(capsule, true);
        float radius = GetCapsuleRadius(capsule);

        Gizmos.DrawWireSphere(lower, radius);
        Gizmos.DrawWireSphere(upper, radius);
        Gizmos.DrawLine(lower + transform.right * radius, upper + transform.right * radius);
        Gizmos.DrawLine(lower - transform.right * radius, upper - transform.right * radius);
        Gizmos.DrawLine(lower + transform.up * radius, upper + transform.up * radius);
        Gizmos.DrawLine(lower - transform.up * radius, upper - transform.up * radius);
        Gizmos.DrawLine(lower + transform.forward * radius, upper + transform.forward * radius);
        Gizmos.DrawLine(lower - transform.forward * radius, upper - transform.forward * radius);
    }
}
