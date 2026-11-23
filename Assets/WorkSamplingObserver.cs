using System.Collections.Generic;
using UnityEngine;
using VehiclePhysics;

/// <summary>The four mutually exclusive work states of the driver.</summary>
public enum WorkState
{
    NormalCruising,   // productive: steady, smooth driving within the limit
    CautiousControl,  // braking / decelerating / creeping at low speed
    IdlingDelay,      // (almost) stopped
    UnsafeViolation   // above the applicable speed limit
}

/// <summary>
/// Work Sampling Observer - an automated "stopwatch-free" observer. While a run is active it takes an
/// instantaneous snapshot of the driver at random intervals (default 2 - 4.5 s) and classifies the
/// driver into exactly one WorkState.
///
/// Classification priority (first match wins):
///   1. IdlingDelay     speed &lt; idleSpeedKmh
///   2. UnsafeViolation speed &gt; applicable limit (global limit, or the zone's limit while inside a zone)
///   3. CautiousControl brake pressed, OR decelerating harder than decelThreshold, OR speed &lt; cautiousSpeedKmh
///   4. NormalCruising  everything else
/// </summary>
public class WorkSamplingObserver : MonoBehaviour
{
    [Header("Random Sampling (seconds)")]
    public float minSampleInterval = 2f;
    public float maxSampleInterval = 4.5f;

    [Header("State Classification (km/h)")]
    public float idleSpeedKmh = 3f;
    public float cautiousSpeedKmh = 20f;
    public float globalSpeedLimitKmh = 50f;
    public float brakeInputThreshold = 0.15f;   // 0..1 (Vehicle Physics Pro brake input)
    public float decelThresholdMs2 = 1.2f;      // m/s^2 of smoothed deceleration

    [Header("References (auto-filled by ExperimentManager)")]
    public Rigidbody carRigidbody;
    public VehicleBase vehicle;
    public SignalZone currentZone;

    [System.Serializable]
    public class SampleData
    {
        public string condition;
        public int sampleNo;
        public float timeInRun;
        public float speedKmh;
        public float accelMs2;
        public float brake;
        public float throttle;
        public WorkState state;
        public float limitKmh;
        public bool inZone;
        public string zoneName;
    }

    readonly List<SampleData> samples = new List<SampleData>();
    readonly Queue<Vector2> speedHistory = new Queue<Vector2>(); // (time, m/s)
    float nextSampleTime;
    float runStartTime;
    bool sampling;
    string condition = "Clear";
    int sampleCounter;

    public bool IsSampling { get { return sampling; } }
    public int CountFor(string cond) { int n = 0; foreach (var s in samples) if (s.condition == cond) n++; return n; }
    public int TotalCount { get { return samples.Count; } }

    public void BeginRun(string cond)
    {
        condition = cond;
        runStartTime = Time.time;
        sampleCounter = 0;
        speedHistory.Clear();
        sampling = true;
        ScheduleNextSample();
    }

    public void EndRun() { sampling = false; }

    public void ClearCondition(string cond) { samples.RemoveAll(s => s.condition == cond); }
    public void ClearAll() { samples.Clear(); }
    public List<SampleData> GetSamples() { return new List<SampleData>(samples); }

    public void EnterZone(SignalZone z) { currentZone = z; }
    public void ExitZone(SignalZone z) { if (currentZone == z) currentZone = null; }

    void Update()
    {
        if (!sampling || carRigidbody == null) return;

        // keep ~1.2 s of speed history for a smoothed acceleration estimate
        float now = Time.time;
        speedHistory.Enqueue(new Vector2(now, carRigidbody.linearVelocity.magnitude));
        while (speedHistory.Count > 2 && now - speedHistory.Peek().x > 1.2f) speedHistory.Dequeue();

        if (now >= nextSampleTime)
        {
            TakeSample();
            ScheduleNextSample();
        }
    }

    void ScheduleNextSample()
    {
        nextSampleTime = Time.time + Random.Range(minSampleInterval, maxSampleInterval);
    }

    float SmoothedAcceleration()
    {
        if (speedHistory.Count < 2) return 0f;
        Vector2 oldest = speedHistory.Peek();
        Vector2 newest = Vector2.zero;
        foreach (var v in speedHistory) newest = v;
        float dt = newest.x - oldest.x;
        return dt > 0.3f ? (newest.y - oldest.y) / dt : 0f;
    }

    void TakeSample()
    {
        float speedKmh = carRigidbody.linearVelocity.magnitude * 3.6f;
        float accel = SmoothedAcceleration();

        float brake = 0f, throttle = 0f;
        if (vehicle != null)
        {
            int[] input = vehicle.data.Get(Channel.Input);
            brake = input[InputData.Brake] / 10000.0f;
            throttle = input[InputData.Throttle] / 10000.0f;
        }

        bool inZone = currentZone != null && currentZone.IsInside;
        float limit = inZone ? currentZone.speedLimitKmh : globalSpeedLimitKmh;
        WorkState state = Classify(speedKmh, accel, brake, limit);

        sampleCounter++;
        var s = new SampleData
        {
            condition = condition,
            sampleNo = sampleCounter,
            timeInRun = Time.time - runStartTime,
            speedKmh = speedKmh,
            accelMs2 = accel,
            brake = brake,
            throttle = throttle,
            state = state,
            limitKmh = limit,
            inZone = inZone,
            zoneName = inZone ? currentZone.zoneName : "None"
        };
        samples.Add(s);

        Debug.Log("[Sample] " + condition + " #" + s.sampleNo + " t=" + s.timeInRun.ToString("F1") +
                  "s speed=" + speedKmh.ToString("F1") + " km/h accel=" + accel.ToString("F2") +
                  " brake=" + brake.ToString("F2") + " -> " + state);
    }

    public WorkState Classify(float speedKmh, float accelMs2, float brake, float limitKmh)
    {
        if (speedKmh < idleSpeedKmh) return WorkState.IdlingDelay;
        if (speedKmh > limitKmh) return WorkState.UnsafeViolation;
        if (brake > brakeInputThreshold || accelMs2 < -decelThresholdMs2 || speedKmh < cautiousSpeedKmh)
            return WorkState.CautiousControl;
        return WorkState.NormalCruising;
    }
}
