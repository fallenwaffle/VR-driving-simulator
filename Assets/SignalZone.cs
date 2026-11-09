using UnityEngine;

/// <summary>
/// SignalZone - Trigger zone for traffic signals (stop signs, traffic lights, etc.)
/// Detects when the car enters/exits the zone and evaluates signal compliance.
/// </summary>
public class SignalZone : MonoBehaviour
{
    [Header("Signal Settings")]
    public string signalType = "Stop Sign"; // Stop Sign, Traffic Light, etc.
    public float requiredStopDuration = 3f; // Seconds to stop for compliance
    public float stopSpeedThreshold = 5f; // km/h - speed below which counts as stopped

    [Header("Debug Visualization")]
    public Color gizmoColor = Color.yellow;
    public bool showGizmos = true;

    private WorkSamplingObserver observer;
    private bool carInZone = false;
    private float zoneEnterTime;
    private float timeStopped = 0f;
    private bool hasComplied = false;

    private void Start()
    {
        // Find the WorkSamplingObserver in the scene
        // Unity 6 compatibility: use FindFirstObjectByType instead of FindObjectOfType
        observer = FindFirstObjectByType<WorkSamplingObserver>();

        if (observer == null)
        {
            Debug.LogWarning("[SignalZone] No WorkSamplingObserver found in scene. Signal tracking may not work.", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponent<Rigidbody>() != null)
        {
            if (!carInZone)
            {
                carInZone = true;
                zoneEnterTime = Time.time;
                timeStopped = 0f;
                hasComplied = false;

                Debug.Log($"[SignalZone] Car entered {signalType} zone at {Time.time:F2}s");

                // Notify observer
                if (observer != null)
                {
                    observer.SetCurrentSignalZone(this);
                }
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (carInZone && (other.CompareTag("Player") || other.GetComponent<Rigidbody>() != null))
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Unity 6 compatibility: use linearVelocity
                float speedKmh = rb.linearVelocity.magnitude * 3.6f;

                if (speedKmh < stopSpeedThreshold)
                {
                    timeStopped += Time.deltaTime;

                    if (timeStopped >= requiredStopDuration && !hasComplied)
                    {
                        hasComplied = true;
                        Debug.Log($"[SignalZone] Car complied with {signalType} (stopped for {timeStopped:F1}s)");
                    }
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (carInZone && (other.CompareTag("Player") || other.GetComponent<Rigidbody>() != null))
        {
            carInZone = false;

            Debug.Log($"[SignalZone] Car exited {signalType} zone. Total stopped: {timeStopped:F1}s, Complied: {hasComplied}");

            // Clear from observer
            if (observer != null)
            {
                observer.SetCurrentSignalZone(null);
            }
        }
    }

    public bool IsCarInZone(GameObject car)
    {
        return carInZone;
    }

    public bool CheckCompliance(float speedKmh, WorkSamplingObserver.DriverState state)
    {
        // Compliance formula: must stop for required duration when in zone
        // Special case: if not stopped, cannot be compliant
        if (speedKmh > stopSpeedThreshold)
        {
            return false;
        }

        return hasComplied;
    }

    public string GetSignalType()
    {
        return signalType;
    }

    public float GetTimeStopped()
    {
        return timeStopped;
    }

    public bool HasComplied()
    {
        return hasComplied;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = gizmoColor;
            if (col is BoxCollider)
            {
                BoxCollider box = col as BoxCollider;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider)
            {
                SphereCollider sphere = col as SphereCollider;
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = Color.green;
            if (col is BoxCollider)
            {
                BoxCollider box = col as BoxCollider;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider)
            {
                SphereCollider sphere = col as SphereCollider;
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
            }
        }
    }
}
