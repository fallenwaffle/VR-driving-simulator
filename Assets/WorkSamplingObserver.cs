using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Work Sampling Observer - Randomly samples driver state at 2-4.5 second intervals
/// for experimental data collection. Implements 4-state classification based on speed.
/// </summary>
public class WorkSamplingObserver : MonoBehaviour
{
    [Header("Speed Thresholds (km/h)")]
    public float speedThresholdHigh = 60f;   // 60 km/h
    public float speedThresholdMedium = 30f; // 30 km/h
    public float speedThresholdLow = 25f;    // 25 km/h

    [Header("Sampling Settings")]
    public float minSampleInterval = 2f;     // 2 seconds
    public float maxSampleInterval = 4.5f;   // 4.5 seconds

    [Header("References")]
    public Rigidbody carRigidbody;
    public SignalZone currentSignalZone;

    // State classification
    public enum DriverState
    {
        Stopped,           // Speed < 25 km/h
        LowSpeed,          // 25-30 km/h
        MediumSpeed,       // 30-60 km/h
        HighSpeed          // > 60 km/h
    }

    private float nextSampleTime;
    private List<SampleData> samples = new List<SampleData>();
    private bool isSampling = true;

    [System.Serializable]
    public class SampleData
    {
        public float timestamp;
        public float speedKmh;
        public DriverState state;
        public bool inSignalZone;
        public bool signalCompliant;
        public string signalType;
    }

    private void Start()
    {
        if (carRigidbody == null)
        {
            carRigidbody = GetComponent<Rigidbody>();
        }

        ScheduleNextSample();
    }

    private void Update()
    {
        if (!isSampling) return;

        if (Time.time >= nextSampleTime)
        {
            TakeSample();
            ScheduleNextSample();
        }
    }

    private void ScheduleNextSample()
    {
        float randomInterval = Random.Range(minSampleInterval, maxSampleInterval);
        nextSampleTime = Time.time + randomInterval;
    }

    private void TakeSample()
    {
        if (carRigidbody == null) return;

        // Unity 6 compatibility: use linearVelocity instead of velocity
        Vector3 velocity = carRigidbody.linearVelocity;
        float speedMs = velocity.magnitude;
        float speedKmh = speedMs * 3.6f; // Convert m/s to km/h

        DriverState state = ClassifyState(speedKmh);

        bool inZone = currentSignalZone != null && currentSignalZone.IsCarInZone(gameObject);
        bool compliant = false;
        string signalType = "None";

        if (inZone && currentSignalZone != null)
        {
            compliant = currentSignalZone.CheckCompliance(speedKmh, state);
            signalType = currentSignalZone.GetSignalType();
        }

        SampleData sample = new SampleData
        {
            timestamp = Time.time,
            speedKmh = speedKmh,
            state = state,
            inSignalZone = inZone,
            signalCompliant = compliant,
            signalType = signalType
        };

        samples.Add(sample);

        Debug.Log($"[Sample] T={sample.timestamp:F2}s | Speed={speedKmh:F1} km/h | State={state} | InZone={inZone} | Compliant={compliant}");
    }

    private DriverState ClassifyState(float speedKmh)
    {
        if (speedKmh < speedThresholdLow)
            return DriverState.Stopped;
        else if (speedKmh < speedThresholdMedium)
            return DriverState.LowSpeed;
        else if (speedKmh < speedThresholdHigh)
            return DriverState.MediumSpeed;
        else
            return DriverState.HighSpeed;
    }

    public List<SampleData> GetSamples()
    {
        return new List<SampleData>(samples);
    }

    public void ClearSamples()
    {
        samples.Clear();
    }

    public void SetSampling(bool active)
    {
        isSampling = active;
    }

    public void SetCurrentSignalZone(SignalZone zone)
    {
        currentSignalZone = zone;
    }
}
