using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// CSVExporter - Exports WorkSamplingObserver data to CSV files
/// Supports separate exports for clear and fog conditions.
/// </summary>
public class CSVExporter : MonoBehaviour
{
    [Header("Export Settings")]
    public string fileNamePrefix = "DrivingData";
    public string exportFolder = "Exports";

    [Header("References")]
    public WorkSamplingObserver observer;

    private void Start()
    {
        if (observer == null)
        {
            // Unity 6 compatibility: use FindFirstObjectByType instead of FindObjectOfType
            observer = FindFirstObjectByType<WorkSamplingObserver>();
        }

        // Create export folder if it doesn't exist
        if (!Directory.Exists(exportFolder))
        {
            Directory.CreateDirectory(exportFolder);
        }
    }

    /// <summary>
    /// Export all samples to CSV file
    /// </summary>
    public void ExportToCSV(string condition = "Clear")
    {
        if (observer == null)
        {
            Debug.LogError("[CSVExporter] No WorkSamplingObserver reference!");
            return;
        }

        List<WorkSamplingObserver.SampleData> samples = observer.GetSamples();

        if (samples.Count == 0)
        {
            Debug.LogWarning("[CSVExporter] No samples to export!");
            return;
        }

        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{fileNamePrefix}_{condition}_{timestamp}.csv";
        string filePath = Path.Combine(exportFolder, fileName);

        StringBuilder csv = new StringBuilder();

        // Header row
        csv.AppendLine("Timestamp,Speed_Kmh,State,InSignalZone,SignalCompliant,SignalType");

        // Data rows
        foreach (var sample in samples)
        {
            csv.AppendLine($"{sample.timestamp:F2},{sample.speedKmh:F2},{sample.state},{sample.inSignalZone},{sample.signalCompliant},{sample.signalType}");
        }

        File.WriteAllText(filePath, csv.ToString());

        Debug.Log($"[CSVExporter] Exported {samples.Count} samples to: {filePath}");
        Debug.Log($"[CSVExporter] Full path: {Path.GetFullPath(filePath)}");
    }

    /// <summary>
    /// Export clear condition data
    /// </summary>
    public void ExportClearRun()
    {
        ExportToCSV("Clear");
    }

    /// <summary>
    /// Export fog condition data
    /// </summary>
    public void ExportFogRun()
    {
        ExportToCSV("Fog");
    }

    /// <summary>
    /// Export with custom condition name
    /// </summary>
    public void ExportCustomRun(string conditionName)
    {
        ExportToCSV(conditionName);
    }

    /// <summary>
    /// Clear samples from observer after export
    /// </summary>
    public void ExportAndClear(string condition = "Clear")
    {
        ExportToCSV(condition);
        observer.ClearSamples();
        Debug.Log("[CSVExporter] Samples cleared after export.");
    }

    /// <summary>
    /// Export summary statistics
    /// </summary>
    public void ExportSummary(string condition = "Clear")
    {
        if (observer == null)
        {
            Debug.LogError("[CSVExporter] No WorkSamplingObserver reference!");
            return;
        }

        List<WorkSamplingObserver.SampleData> samples = observer.GetSamples();

        if (samples.Count == 0)
        {
            Debug.LogWarning("[CSVExporter] No samples to summarize!");
            return;
        }

        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{fileNamePrefix}_Summary_{condition}_{timestamp}.csv";
        string filePath = Path.Combine(exportFolder, fileName);

        StringBuilder csv = new StringBuilder();

        // Calculate statistics
        int totalSamples = samples.Count;
        int inZoneCount = 0;
        int compliantCount = 0;
        int stoppedCount = 0;
        int lowSpeedCount = 0;
        int mediumSpeedCount = 0;
        int highSpeedCount = 0;
        float totalSpeed = 0f;
        float avgSpeed = 0f;

        foreach (var sample in samples)
        {
            totalSpeed += sample.speedKmh;

            if (sample.inSignalZone) inZoneCount++;
            if (sample.signalCompliant) compliantCount++;

            switch (sample.state)
            {
                case WorkSamplingObserver.DriverState.Stopped:
                    stoppedCount++;
                    break;
                case WorkSamplingObserver.DriverState.LowSpeed:
                    lowSpeedCount++;
                    break;
                case WorkSamplingObserver.DriverState.MediumSpeed:
                    mediumSpeedCount++;
                    break;
                case WorkSamplingObserver.DriverState.HighSpeed:
                    highSpeedCount++;
                    break;
            }
        }

        avgSpeed = totalSpeed / totalSamples;
        float complianceRate = inZoneCount > 0 ? (float)compliantCount / inZoneCount * 100f : 0f;

        // Header
        csv.AppendLine("Metric,Value");
        csv.AppendLine($"Condition,{condition}");
        csv.AppendLine($"Total Samples,{totalSamples}");
        csv.AppendLine($"Average Speed (km/h),{avgSpeed:F2}");
        csv.AppendLine($"Samples in Signal Zones,{inZoneCount}");
        csv.AppendLine($"Compliant Samples,{compliantCount}");
        csv.AppendLine($"Compliance Rate (%),{complianceRate:F2}");
        csv.AppendLine($"Stopped Samples (<25 km/h),{stoppedCount}");
        csv.AppendLine($"Low Speed Samples (25-30 km/h),{lowSpeedCount}");
        csv.AppendLine($"Medium Speed Samples (30-60 km/h),{mediumSpeedCount}");
        csv.AppendLine($"High Speed Samples (>60 km/h),{highSpeedCount}");

        File.WriteAllText(filePath, csv.ToString());

        Debug.Log($"[CSVExporter] Exported summary to: {filePath}");
    }

    /// <summary>
    /// Get the full path to the export folder
    /// </summary>
    public string GetExportFolderPath()
    {
        return Path.GetFullPath(exportFolder);
    }
}
