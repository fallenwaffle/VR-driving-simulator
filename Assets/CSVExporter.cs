using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// CSVExporter - writes the experiment data. Each export rewrites four files (same session id):
///   *_samples.csv     every random snapshot (both runs, with the Condition column)
///   *_zones.csv       one row per zone pass (entry speed, compliant yes/no)
///   *_summary.csv     state proportions, 95% confidence intervals, compliance rate per condition
///   *_comparison.md   slide-ready Clear vs Fog comparison table with z-test p-values
/// </summary>
public class CSVExporter : MonoBehaviour
{
    public string fileNamePrefix = "WorkSampling";
    public ExperimentManager manager;
    public WorkSamplingObserver observer;
    public string LastFolder { get; private set; }

    static readonly WorkState[] States =
    {
        WorkState.NormalCruising, WorkState.CautiousControl, WorkState.IdlingDelay, WorkState.UnsafeViolation
    };

    static readonly string[] StateLabels =
    {
        "1. Normal Cruising (Productive)", "2. Cautious Slowing / Control",
        "3. Idling / Stopped (Delay)", "4. Speeding / Violation"
    };

    string session = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

    public static string ExportRoot()
    {
#if UNITY_EDITOR
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Exports");
#else
        return Path.Combine(Application.persistentDataPath, "Exports");
#endif
    }

    static string I(double v, string fmt = "F2") { return v.ToString(fmt, CultureInfo.InvariantCulture); }

    public string ExportAll(string reason)
    {
        if (manager == null) manager = ExperimentManager.Instance;
        if (observer == null && manager != null) observer = manager.observer;
        if (observer == null || observer.TotalCount == 0)
        {
            Debug.LogWarning("[CSVExporter] No samples to export yet.");
            return null;
        }

        string folder = ExportRoot();
        Directory.CreateDirectory(folder);
        LastFolder = folder;
        string pid = manager != null ? manager.participantId : "P00";
        string stem = Path.Combine(folder, fileNamePrefix + "_" + pid + "_" + session);

        var samples = observer.GetSamples();
        var zones = manager != null ? manager.ZoneResults : new List<ZoneResult>();

        File.WriteAllText(stem + "_samples.csv", BuildSamplesCsv(pid, samples), Encoding.UTF8);
        File.WriteAllText(stem + "_zones.csv", BuildZonesCsv(pid, zones), Encoding.UTF8);
        File.WriteAllText(stem + "_summary.csv", BuildSummaryCsv(pid, samples, zones), Encoding.UTF8);
        File.WriteAllText(stem + "_comparison.md", BuildComparisonMarkdown(pid, samples, zones, manager != null ? manager.TotalZones : 0), Encoding.UTF8);

        Debug.Log("[CSVExporter] (" + reason + ") exported " + samples.Count + " samples + " + zones.Count + " zone results to " + folder);
        return stem;
    }

    // ------------------------------------------------------------------ raw data
    string BuildSamplesCsv(string pid, List<WorkSamplingObserver.SampleData> samples)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Participant,Condition,SampleNo,TimeInRun_s,Speed_kmh,Accel_ms2,Brake,Throttle,State,SpeedLimit_kmh,InZone,ZoneName");
        foreach (var s in samples)
            sb.AppendLine(pid + "," + s.condition + "," + s.sampleNo + "," + I(s.timeInRun) + "," + I(s.speedKmh) + "," +
                          I(s.accelMs2) + "," + I(s.brake) + "," + I(s.throttle) + "," + s.state + "," +
                          I(s.limitKmh, "F0") + "," + s.inZone + "," + s.zoneName);
        return sb.ToString();
    }

    string BuildZonesCsv(string pid, List<ZoneResult> zones)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Participant,Condition,ZoneName,ZoneIndex,EntryTime_s,EntrySpeed_kmh,MaxSpeedInZone_kmh,SpeedLimit_kmh,Compliant");
        foreach (var z in zones)
            sb.AppendLine(pid + "," + z.condition + "," + z.zoneName + "," + z.zoneIndex + "," + I(z.entryTime) + "," +
                          I(z.entrySpeedKmh) + "," + I(z.maxSpeedKmh) + "," + I(z.limitKmh, "F0") + "," + z.compliant);
        return sb.ToString();
    }

    // ------------------------------------------------------------------ statistics
    static int[] Counts(List<WorkSamplingObserver.SampleData> all, string cond, out int n)
    {
        var c = new int[States.Length];
        n = 0;
        foreach (var s in all)
        {
            if (s.condition != cond) continue;
            n++;
            c[Array.IndexOf(States, s.state)]++;
        }
        return c;
    }

    static void Compliance(List<ZoneResult> zones, string cond, out int ok, out int total)
    {
        ok = 0; total = 0;
        foreach (var z in zones) if (z.condition == cond) { total++; if (z.compliant) ok++; }
    }

    string BuildSummaryCsv(string pid, List<WorkSamplingObserver.SampleData> samples, List<ZoneResult> zones)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Participant,Condition,Metric,Count,N,Proportion,CI95_HalfWidth_E,CI95_Lower,CI95_Upper");
        foreach (string cond in new[] { "Clear", "Fog" })
        {
            int n; var c = Counts(samples, cond, out n);
            if (n > 0)
                for (int i = 0; i < States.Length; i++)
                {
                    double p = WorkSamplingStats.Proportion(c[i], n);
                    double e = WorkSamplingStats.HalfWidth(p, n);
                    sb.AppendLine(pid + "," + cond + "," + States[i] + "," + c[i] + "," + n + "," + I(p, "F4") + "," +
                                  I(e, "F4") + "," + I(Math.Max(0, p - e), "F4") + "," + I(Math.Min(1, p + e), "F4"));
                }
            int ok, tot; Compliance(zones, cond, out ok, out tot);
            if (tot > 0)
            {
                double lo, hi; WorkSamplingStats.Wilson(ok, tot, out lo, out hi);
                double p = WorkSamplingStats.Proportion(ok, tot);
                sb.AppendLine(pid + "," + cond + ",SignalCompliance(Wilson)," + ok + "," + tot + "," + I(p, "F4") + "," +
                              I(WorkSamplingStats.HalfWidth(p, tot), "F4") + "," + I(lo, "F4") + "," + I(hi, "F4"));
            }
        }
        return sb.ToString();
    }

    string BuildComparisonMarkdown(string pid, List<WorkSamplingObserver.SampleData> samples, List<ZoneResult> zones, int totalZones)
    {
        int nC, nF;
        var cC = Counts(samples, "Clear", out nC);
        var cF = Counts(samples, "Fog", out nF);
        var sb = new StringBuilder();
        sb.AppendLine("# Work Sampling Results - participant " + pid);
        sb.AppendLine();
        sb.AppendLine("Independent variable: visibility (Clear vs Heavy Fog). Random sampling interval 2-4.5 s. 95% CI: E = 1.96 * sqrt(p(1-p)/N).");
        sb.AppendLine();
        sb.AppendLine("| Work state / metric | Run 1: Clear (N = " + nC + ") | Run 2: Fog (N = " + nF + ") | Change (pp) | z-test p |");
        sb.AppendLine("|---|---|---|---|---|");
        for (int i = 0; i < States.Length; i++)
        {
            double pC = WorkSamplingStats.Proportion(cC[i], nC), pF = WorkSamplingStats.Proportion(cF[i], nF);
            string a = nC > 0 ? I(pC * 100, "F1") + "% (" + cC[i] + ") +/-" + I(WorkSamplingStats.HalfWidth(pC, nC) * 100, "F1") : "-";
            string b = nF > 0 ? I(pF * 100, "F1") + "% (" + cF[i] + ") +/-" + I(WorkSamplingStats.HalfWidth(pF, nF) * 100, "F1") : "-";
            string d = (nC > 0 && nF > 0) ? I((pF - pC) * 100, "F1") : "-";
            string pv = (nC > 0 && nF > 0) ? I(WorkSamplingStats.TwoProportionPValue(cF[i], nF, cC[i], nC), "F3") : "-";
            sb.AppendLine("| " + StateLabels[i] + " | " + a + " | " + b + " | " + d + " | " + pv + " |");
        }
        int okC, tC, okF, tF;
        Compliance(zones, "Clear", out okC, out tC);
        Compliance(zones, "Fog", out okF, out tF);
        string compC = tC > 0 ? okC + "/" + tC + " (" + I(100.0 * okC / tC, "F1") + "%)" : "-";
        string compF = tF > 0 ? okF + "/" + tF + " (" + I(100.0 * okF / tF, "F1") + "%)" : "-";
        string compD = (tC > 0 && tF > 0) ? I(100.0 * okF / tF - 100.0 * okC / tC, "F1") : "-";
        string compP = (tC > 0 && tF > 0) ? I(WorkSamplingStats.TwoProportionPValue(okF, tF, okC, tC), "F3") : "-";
        sb.AppendLine("| **Signal compliance** (zones passed at/below limit) | " + compC + " | " + compF + " | " + compD + " | " + compP + " |");
        sb.AppendLine();
        sb.AppendLine("Zones on route: " + totalZones + ". With only a handful of zones the compliance z-test is approximate - use Fisher's exact test (Analysis/analyze_work_sampling.py).");
        sb.AppendLine();
        sb.AppendLine("Samples needed for +/-5% at 95% confidence (worst case p = 0.5): N = " + Math.Ceiling(WorkSamplingStats.RequiredSamples(0.5, 0.05)) +
                      ". Pool several participants to approach this.");
        return sb.ToString();
    }
}
