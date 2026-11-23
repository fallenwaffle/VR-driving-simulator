using UnityEngine;

/// <summary>
/// SignalZone - trigger box on the road (signal / hazard zone).
/// Compliance rule: the car must ENTER the zone at or below speedLimitKmh (+ toleranceKmh).
/// One result is recorded per zone per run, when the car leaves the zone (or the run ends inside it).
/// Warning signs (red/yellow diamonds + orange stripe) are built at the zone entry so the zone is
/// visible from far away in clear weather and hidden by fog.
/// </summary>
public class SignalZone : MonoBehaviour
{
    [Header("Zone Settings")]
    public string zoneName = "Zone";
    public int zoneIndex = 1;
    public string signalType = "Hazard Zone";
    public float speedLimitKmh = 30f;
    public float toleranceKmh = 2f;

    [Header("Debug")]
    public bool showGizmos = true;

    int contacts;
    bool inside;
    bool counted;
    bool passedThisRun;
    float entryTime, entrySpeed, maxSpeed;

    public bool IsInside { get { return inside; } }
    public bool PassedThisRun { get { return passedThisRun; } }

    public void ResetForRun()
    {
        passedThisRun = false;
        counted = false;
    }

    bool IsCar(Collider other)
    {
        var mgr = ExperimentManager.Instance;
        if (mgr == null || mgr.carRigidbody == null) return false;
        var rb = other.GetComponent<Rigidbody>();
        return rb != null && rb == mgr.carRigidbody;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsCar(other)) return;
        contacts++;
        if (contacts == 1) BeginPass();
    }

    void OnTriggerStay(Collider other)
    {
        if (!inside || !counted || !IsCar(other)) return;
        var rb = other.GetComponent<Rigidbody>();
        float v = rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f;
        if (v > maxSpeed) maxSpeed = v;
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsCar(other)) return;
        contacts = Mathf.Max(0, contacts - 1);
        if (contacts == 0) EndPass();
    }

    void BeginPass()
    {
        inside = true;
        var mgr = ExperimentManager.Instance;
        if (mgr == null || !mgr.IsRunning || passedThisRun) { counted = false; return; }

        counted = true;
        entryTime = mgr.RunTime;
        entrySpeed = mgr.carRigidbody.linearVelocity.magnitude * 3.6f;
        maxSpeed = entrySpeed;
        if (mgr.observer != null) mgr.observer.EnterZone(this);
    }

    void EndPass()
    {
        inside = false;
        var mgr = ExperimentManager.Instance;
        if (mgr != null && mgr.observer != null) mgr.observer.ExitZone(this);
        if (!counted || mgr == null) return;
        counted = false;
        passedThisRun = true;

        mgr.ReportZone(new ZoneResult
        {
            condition = mgr.CurrentCondition,
            zoneName = zoneName,
            zoneIndex = zoneIndex,
            entryTime = entryTime,
            entrySpeedKmh = entrySpeed,
            maxSpeedKmh = maxSpeed,
            limitKmh = speedLimitKmh,
            compliant = entrySpeed <= speedLimitKmh + toleranceKmh
        });
    }

    /// <summary>Called by the manager when the run ends while the car is still inside the zone.</summary>
    public void FinalizePass()
    {
        if (inside && counted) EndPass();
    }

    // ---------------------------------------------------------------- visuals
    public static void BuildVisuals(Transform zone, float width, float depth)
    {
        Color red = new Color(0.85f, 0.08f, 0.08f);
        Color yellow = new Color(1f, 0.85f, 0.1f);
        Color grey = new Color(0.35f, 0.35f, 0.38f);
        Color orange = new Color(1f, 0.5f, 0.05f);

        // road stripes at entry (orange) and exit (white)
        Piece(zone, "EntryStripe", new Vector3(0, 0.05f, -depth * 0.5f), new Vector3(width, 0.04f, 1.0f), Quaternion.identity, orange);
        Piece(zone, "ExitStripe", new Vector3(0, 0.05f, depth * 0.5f), new Vector3(width, 0.04f, 0.5f), Quaternion.identity, Color.white);

        // tall warning signs on both sides of the road, just before the entry
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            float x = side * (width * 0.5f + 1.5f);
            float z = -depth * 0.5f - 1f;
            Piece(zone, "Pole" + i, new Vector3(x, 2.5f, z), new Vector3(0.2f, 5f, 0.2f), Quaternion.identity, grey);
            Quaternion diamond = Quaternion.Euler(0, 0, 45f);
            Piece(zone, "SignRed" + i, new Vector3(x, 5.6f, z), new Vector3(2.0f, 2.0f, 0.15f), diamond, red);
            Piece(zone, "SignYellow" + i, new Vector3(x, 5.6f, z - 0.02f), new Vector3(1.35f, 1.35f, 0.15f), diamond, yellow);
        }
    }

    static void Piece(Transform parent, string name, Vector3 localPos, Vector3 scale, Quaternion localRot, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = MakeMaterial(color);
    }

    static Material MakeMaterial(Color c)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        if (s == null) s = Shader.Find("Unlit/Color");
        var m = new Material(s);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        return m;
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;
        var box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
