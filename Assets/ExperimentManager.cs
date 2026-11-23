using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using VehiclePhysics;
using UnityEngine.XR;

[Serializable]
public class ZoneResult
{
    public string condition;
    public string zoneName;
    public int zoneIndex;
    public float entryTime;
    public float entrySpeedKmh;
    public float maxSpeedKmh;
    public float limitKmh;
    public bool compliant;
}

[Serializable]
public class ZoneRecord
{
    public string name;
    public float x, y, z, yaw;
    public float width, depth, limitKmh;
}

[Serializable]
class ZoneFile { public List<ZoneRecord> zones = new List<ZoneRecord>(); }

/// <summary>
/// ExperimentManager - runs the two-stage work sampling experiment (Clear vs Fog) on the same route.
///
///   1 = start Run 1 (Clear)     2 = start Run 2 (Fog)     3 = stop/abort run
///   F = preview fog (only when no run is active)
///   Z = drop a zone at the car (route setup, auto-saved)   Backspace = undo last dropped zone
///   F9 = export CSVs now
///
/// It is created automatically in any scene that contains a Vehicle Physics Pro vehicle
/// (SUV, Sports Car) - no manual scene wiring needed.
/// </summary>
public class ExperimentManager : MonoBehaviour
{
    static readonly Dictionary<string, bool> questButtonStates = new Dictionary<string, bool>();
    public static ExperimentManager Instance { get; private set; }

    [Header("Experiment")]
    public string participantId = "P01";
    public float runDurationSeconds = 120f;
    public bool resetCarOnRunStart = true;
    public bool autoExportOnRunEnd = true;

    [Header("Fog (Run 2)")]
    public float fogDensity = 0.035f;                       // exp-squared; ~50 m visibility is near zero
    public Color fogColor = new Color(0.70f, 0.73f, 0.76f);

    [Header("Zones")]
    public int defaultZoneCount = 6;                        // used only if no zones were recorded
    public float defaultFirstZoneDistance = 100f;
    public float defaultZoneSpacing = 130f;
    public float zoneWidth = 14f;
    public float zoneDepth = 20f;
    public float zoneSpeedLimitKmh = 30f;

    [Header("References (auto)")]
    public Rigidbody carRigidbody;
    public VehicleBase vehicle;
    public WorkSamplingObserver observer;
    public CSVExporter exporter;

    public bool IsRunning { get; private set; }
    public string CurrentCondition { get; private set; }
    public float RunTime { get { return IsRunning ? Time.time - runStartTime : 0f; } }
    public readonly List<ZoneResult> ZoneResults = new List<ZoneResult>();
    public int TotalZones { get { return zones.Count; } }

    readonly List<SignalZone> zones = new List<SignalZone>();
    readonly List<ZoneRecord> records = new List<ZoneRecord>();
    Transform zonesRoot;
    float runStartTime;
    Vector3 startPos;
    Quaternion startRot;
    string message = "";
    float messageUntil;
    bool dirty;

    // original fog so we can restore it
    bool origFog; FogMode origMode; float origDensity; Color origColor;

    // ------------------------------------------------------------ lifecycle
    void Awake()
    {
        Instance = this;
        CurrentCondition = "Clear";
    }

    void Start()
    {
        FindCar();
        if (carRigidbody == null)
        {
            Debug.LogError("[Experiment] No car found in this scene - experiment disabled.");
            enabled = false;
            return;
        }

        observer = gameObject.GetComponent<WorkSamplingObserver>();
        if (observer == null) observer = gameObject.AddComponent<WorkSamplingObserver>();
        observer.carRigidbody = carRigidbody;
        observer.vehicle = vehicle;

        exporter = gameObject.GetComponent<CSVExporter>();
        if (exporter == null) exporter = gameObject.AddComponent<CSVExporter>();
        exporter.manager = this;
        exporter.observer = observer;

        origFog = RenderSettings.fog; origMode = RenderSettings.fogMode;
        origDensity = RenderSettings.fogDensity; origColor = RenderSettings.fogColor;
        SetFog(false);

        startPos = carRigidbody.position;
        startRot = carRigidbody.rotation;

        LoadOrCreateZones();
        Flash("Ready. Press 1 for Run 1 (Clear), 2 for Run 2 (Fog).", 8f);
        Debug.Log("[Experiment] Ready on scene '" + SceneManager.GetActiveScene().name + "'. Export folder: " + CSVExporter.ExportRoot());
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            if (exporter != null && observer != null && dirty && observer.TotalCount > 0) exporter.ExportAll("AutoSave");
            RestoreFog();
            Instance = null;
        }
    }

    void FindCar()
    {
        vehicle = FindAnyObjectByType<VehicleBase>();
        if (vehicle != null) carRigidbody = vehicle.cachedRigidbody;
        if (carRigidbody == null)
        {
            float best = 0f;
            foreach (var rb in FindObjectsByType<Rigidbody>())
                if (!rb.isKinematic && rb.mass > best) { best = rb.mass; carRigidbody = rb; }
        }
    }

    // ------------------------------------------------------------ update / input
    void Update()
    {
        // Touch Plus experiment actions use the left stick click as a modifier.
        // Read XR features directly so this also works with the legacy Input Manager.
        InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool modifier = Feature(left, CommonUsages.primary2DAxisClick);
        // Update edge state every frame so pressing a button before the modifier is
        // held cannot accidentally fire an experiment action when the modifier comes down.
        bool clearPressed = FeatureDown(right, CommonUsages.primaryButton);
        bool fogPressed = FeatureDown(right, CommonUsages.secondaryButton);
        bool exportPressed = FeatureDown(right, CommonUsages.primary2DAxisClick);
        bool undoPressed = RightStickFlickDown(right);
        bool xPressed = FeatureDown(left, CommonUsages.primaryButton);
        bool yPressed = FeatureDown(left, CommonUsages.secondaryButton);
        if (modifier)
        {
            if (clearPressed) StartRun("Clear", false);       // A
            if (fogPressed) StartRun("Fog", true);             // B
            if (exportPressed && exporter != null) exporter.ExportAll("Manual");
            if (undoPressed) UndoLastZone();
            if (yPressed) DropZoneAtCar();                      // Y
            if (xPressed && IsRunning) EndRun(false);            // X
        }
        else
        {
            if (yPressed) ResetCar();
            if (xPressed) Quest3DrivingInput.ToggleCamera();
        }
        if (Pressed(KeyCode.Alpha1)) StartRun("Clear", false);
        else if (Pressed(KeyCode.Alpha2)) StartRun("Fog", true);
        else if (Pressed(KeyCode.Alpha3)) { if (IsRunning) EndRun(false); }
        else if (Pressed(KeyCode.F)) TogglePreviewFog();
        else if (Pressed(KeyCode.Z)) DropZoneAtCar();
        else if (Pressed(KeyCode.Backspace)) UndoLastZone();
        else if (Pressed(KeyCode.F9)) { if (exporter != null) exporter.ExportAll("Manual"); }

        if (IsRunning && RunTime >= runDurationSeconds) EndRun(true);
    }

    static bool Feature(InputDevice device, InputFeatureUsage<bool> usage)
    { bool value; return device.isValid && device.TryGetFeatureValue(usage, out value) && value; }

    static bool FeatureDown(InputDevice device, InputFeatureUsage<bool> usage)
    {
        bool now = Feature(device, usage);
        string key = device.name + "_" + usage.name;
        bool was; questButtonStates.TryGetValue(key, out was);
        questButtonStates[key] = now;
        return now && !was;
    }

    static bool RightStickFlickDown(InputDevice device)
    {
        Vector2 axis;
        if (!device.isValid || !device.TryGetFeatureValue(CommonUsages.primary2DAxis, out axis)) return false;
        bool down = axis.y < -0.75f;
        string key = device.name + "_RightStickDown";
        bool was; questButtonStates.TryGetValue(key, out was);
        questButtonStates[key] = down;
        return down && !was;
    }

    static bool Pressed(KeyCode key)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(key);
#elif ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return false;
        switch (key)
        {
            case KeyCode.Alpha1: return kb.digit1Key.wasPressedThisFrame;
            case KeyCode.Alpha2: return kb.digit2Key.wasPressedThisFrame;
            case KeyCode.Alpha3: return kb.digit3Key.wasPressedThisFrame;
            case KeyCode.F: return kb.fKey.wasPressedThisFrame;
            case KeyCode.Z: return kb.zKey.wasPressedThisFrame;
            case KeyCode.Backspace: return kb.backspaceKey.wasPressedThisFrame;
            case KeyCode.F9: return kb.f9Key.wasPressedThisFrame;
        }
        return false;
#else
        return false;
#endif
    }

    // ------------------------------------------------------------ run control
    public void StartRun(string condition, bool fog)
    {
        if (carRigidbody == null) return;
        if (IsRunning) EndRun(false);

        CurrentCondition = condition;
        ZoneResults.RemoveAll(z => z.condition == condition);
        observer.ClearCondition(condition);

        if (resetCarOnRunStart) ResetCar();
        foreach (var z in zones) if (z != null) z.ResetForRun();
        SetFog(fog);

        runStartTime = Time.time;
        IsRunning = true;
        dirty = true;
        observer.BeginRun(condition);
        Flash("RUN STARTED: " + condition + " (" + runDurationSeconds.ToString("F0") + " s)", 4f);
        Debug.Log("[Experiment] Run started: " + condition + ", fog=" + fog);
    }

    public void EndRun(bool completed)
    {
        if (!IsRunning) return;
        foreach (var z in zones) if (z != null) z.FinalizePass();
        IsRunning = false;
        observer.EndRun();
        Flash((completed ? "RUN COMPLETE: " : "RUN STOPPED: ") + CurrentCondition +
              " - " + observer.CountFor(CurrentCondition) + " samples", 6f);
        Debug.Log("[Experiment] Run ended: " + CurrentCondition + " completed=" + completed);
        if (autoExportOnRunEnd && exporter != null) exporter.ExportAll("RunEnd_" + CurrentCondition);
    }

    public void ResetCar()
    {
        var rb = carRigidbody;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.MovePosition(startPos);
        rb.MoveRotation(startRot);
        Physics.SyncTransforms();
    }

    public void ReportZone(ZoneResult r)
    {
        ZoneResults.Add(r);
        Flash(r.zoneName + ": entered at " + r.entrySpeedKmh.ToString("F0") + " km/h (limit " +
              r.limitKmh.ToString("F0") + ") - " + (r.compliant ? "COMPLIANT" : "VIOLATION"), 3f);
        Debug.Log("[Zone] " + r.condition + " " + r.zoneName + " entry=" + r.entrySpeedKmh.ToString("F1") +
                  " km/h compliant=" + r.compliant);
    }

    // ------------------------------------------------------------ fog
    public void SetFog(bool on)
    {
        RenderSettings.fog = on;
        if (on)
        {
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = fogColor;
        }
    }

    void TogglePreviewFog()
    {
        if (IsRunning) { Flash("Fog is locked while a run is active.", 2f); return; }
        SetFog(!RenderSettings.fog);
        Flash("Fog preview: " + (RenderSettings.fog ? "ON" : "OFF"), 2f);
    }

    void RestoreFog()
    {
        RenderSettings.fog = origFog; RenderSettings.fogMode = origMode;
        RenderSettings.fogDensity = origDensity; RenderSettings.fogColor = origColor;
    }

    // ------------------------------------------------------------ zones / route
    static string ZoneFilePath(bool forWrite)
    {
        string file = "zones_" + SceneManager.GetActiveScene().name.Replace(" ", "_") + ".json";
        string stream = Path.Combine(Application.streamingAssetsPath, "ExperimentZones");
        string persistent = Path.Combine(Application.persistentDataPath, "ExperimentZones");
#if UNITY_EDITOR
        if (forWrite) return Path.Combine(stream, file);   // lives in the project -> commit it to git
#else
        if (forWrite) return Path.Combine(persistent, file);
#endif
        string p1 = Path.Combine(persistent, file);
        if (File.Exists(p1)) return p1;
        return Path.Combine(stream, file);
    }

    void LoadOrCreateZones()
    {
        zonesRoot = new GameObject("[Experiment_Zones]").transform;

        // 1. zones the team placed by hand in the scene
        foreach (var z in FindObjectsByType<SignalZone>()) zones.Add(z);
        zones.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        if (zones.Count > 0) { Renumber(); Debug.Log("[Experiment] Using " + zones.Count + " zones from the scene."); return; }

        // 2. zones recorded earlier with the Z key
        string path = ZoneFilePath(false);
        if (File.Exists(path))
        {
            try
            {
                var f = JsonUtility.FromJson<ZoneFile>(File.ReadAllText(path));
                if (f != null && f.zones != null)
                    foreach (var r in f.zones) { records.Add(r); zones.Add(SpawnZone(r)); }
                Debug.Log("[Experiment] Loaded " + zones.Count + " zones from " + path);
            }
            catch (Exception e) { Debug.LogWarning("[Experiment] Could not read zone file: " + e.Message); }
        }
        if (zones.Count > 0) { Renumber(); return; }

        // 3. fallback: straight line ahead of the start position
        Vector3 fwd = startRot * Vector3.forward; fwd.y = 0; fwd.Normalize();
        float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        for (int i = 0; i < defaultZoneCount; i++)
        {
            Vector3 p = startPos + fwd * (defaultFirstZoneDistance + i * defaultZoneSpacing);
            p.y = GroundY(p, startPos.y - 0.5f);
            var r = new ZoneRecord { name = "Zone_" + (i + 1), x = p.x, y = p.y, z = p.z, yaw = yaw,
                                     width = zoneWidth, depth = zoneDepth, limitKmh = zoneSpeedLimitKmh };
            zones.Add(SpawnZone(r));
        }
        Renumber();
        Debug.LogWarning("[Experiment] No recorded route found - placed " + zones.Count +
                         " DEFAULT zones in a straight line. If the road bends, drive the route once and press Z at each hazard to record zones.");
    }

    SignalZone SpawnZone(ZoneRecord r)
    {
        var go = new GameObject(r.name);
        go.transform.SetParent(zonesRoot, false);
        go.transform.SetPositionAndRotation(new Vector3(r.x, r.y, r.z), Quaternion.Euler(0f, r.yaw, 0f));
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, 3f, 0f);
        box.size = new Vector3(r.width, 6f, r.depth);
        var z = go.AddComponent<SignalZone>();
        z.zoneName = r.name;
        z.speedLimitKmh = r.limitKmh;
        SignalZone.BuildVisuals(go.transform, r.width, r.depth);
        return z;
    }

    void Renumber()
    {
        for (int i = 0; i < zones.Count; i++) { zones[i].zoneIndex = i + 1; }
    }

    float GroundY(Vector3 p, float fallback)
    {
        var hits = Physics.RaycastAll(p + Vector3.up * 100f, Vector3.down, 300f, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var h in hits)
        {
            var rb = h.collider.GetComponent<Rigidbody>();
            if (rb != null && rb == carRigidbody) continue;
            return h.point.y;
        }
        return fallback;
    }

    void DropZoneAtCar()
    {
        if (IsRunning) { Flash("Cannot edit zones during a run.", 2f); return; }
        Vector3 fwd = carRigidbody.transform.forward; fwd.y = 0; fwd.Normalize();
        Vector3 p = carRigidbody.position;
        p.y = GroundY(p, p.y - 0.5f);
        var r = new ZoneRecord { name = "Zone_" + (zones.Count + 1), x = p.x, y = p.y, z = p.z,
                                 yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg,
                                 width = zoneWidth, depth = zoneDepth, limitKmh = zoneSpeedLimitKmh };
        records.Add(r);
        zones.Add(SpawnZone(r));
        Renumber();
        SaveZoneFile();
        Flash("Zone " + zones.Count + " recorded at car position.", 3f);
    }

    void UndoLastZone()
    {
        if (IsRunning || records.Count == 0 || zones.Count == 0) return;
        var last = zones[zones.Count - 1];
        zones.RemoveAt(zones.Count - 1);
        records.RemoveAt(records.Count - 1);
        if (last != null) Destroy(last.gameObject);
        SaveZoneFile();
        Flash("Last zone removed. " + zones.Count + " zones left.", 3f);
    }

    void SaveZoneFile()
    {
        try
        {
            string path = ZoneFilePath(true);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(new ZoneFile { zones = records }, true));
            Debug.Log("[Experiment] Zones saved to " + path);
        }
        catch (Exception e) { Debug.LogWarning("[Experiment] Could not save zones: " + e.Message); }
    }

    // ------------------------------------------------------------ HUD
    void Flash(string msg, float seconds) { message = msg; messageUntil = Time.time + seconds; }

    void OnGUI()
    {
        int fs = Mathf.Max(14, Screen.height / 45);
        var style = new GUIStyle(GUI.skin.label) { fontSize = fs, richText = true };
        style.normal.textColor = Color.white;

        if (carRigidbody == null)
        {
            string errorText = "<b>WORK SAMPLING</b> - NOT INITIALIZED (no car found)\n" +
                "Press Play and check Console for errors.";
            GUI.Box(new Rect(10, 10, Screen.width * 0.5f, fs * 2f), GUIContent.none);
            GUI.Label(new Rect(20, 14, Screen.width * 0.48f, fs * 1.8f), errorText, style);
            return;
        }

        int reached = 0, ok = 0;
        foreach (var r in ZoneResults)
            if (r.condition == CurrentCondition) { reached++; if (r.compliant) ok++; }

        string runState = IsRunning
            ? "<color=#ffd24d>RUNNING " + Mathf.Max(0f, runDurationSeconds - RunTime).ToString("F0") + " s left</color>"
            : "IDLE";
        float speed = carRigidbody.linearVelocity.magnitude * 3.6f;

        string text =
            "<b>WORK SAMPLING</b>   participant " + participantId + "   [" + SceneManager.GetActiveScene().name + "]\n" +
            "Condition: <b>" + CurrentCondition + "</b>   Fog: " + (RenderSettings.fog ? "ON" : "off") + "   " + runState + "\n" +
            "Samples (this cond.): " + observer.CountFor(CurrentCondition) +
            "   Zones: " + reached + "/" + zones.Count + " reached, " + ok + " compliant\n" +
            "Speed: " + speed.ToString("F0") + " km/h\n" +
            "<size=" + (fs - 3) + ">1 = Clear run   2 = Fog run   3 = stop   F = fog preview   Z = add zone   Bksp = undo zone   F9 = export</size>";
        if (Time.time < messageUntil) text += "\n<color=#7dffb0>" + message + "</color>";

        GUI.Box(new Rect(10, 10, Screen.width * 0.62f, fs * 8.2f), GUIContent.none);
        GUI.Label(new Rect(20, 14, Screen.width * 0.6f, fs * 8f), text, style);
    }
}

/// <summary>Creates the ExperimentManager automatically in every scene that contains a vehicle.</summary>
public static class ExperimentBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        Ensure();
    }

    static void OnLoaded(Scene s, LoadSceneMode m) { Ensure(); }

    static void Ensure()
    {
        var existingManager = UnityEngine.Object.FindAnyObjectByType<ExperimentManager>();
        if (existingManager != null)
        {
            if (existingManager.GetComponent<Quest3DrivingInput>() == null)
                existingManager.gameObject.AddComponent<Quest3DrivingInput>();
            return;
        }
        var vehicle = UnityEngine.Object.FindAnyObjectByType<VehicleBase>();
        var rb = UnityEngine.Object.FindAnyObjectByType<Rigidbody>();
        // Create manager if we have either a VehicleBase OR any Rigidbody (fallback for different vehicle setups)
        if (vehicle == null && rb == null) return;
        var managerObject = new GameObject("[Experiment_Manager]");
        managerObject.AddComponent<ExperimentManager>();
        managerObject.AddComponent<Quest3DrivingInput>();
    }
}
