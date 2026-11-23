# VR Driving Work Sampling Experiment - Run Guide

**Question:** does heavy fog change how a driver allocates time across four work states, and how many
signal/hazard zones they pass at a safe speed?
**IV:** visibility (Run 1 Clear, Run 2 Fog). **DVs:** state proportions p_i, signal compliance rate.

## How it works (no scene setup needed)
`ExperimentManager` starts automatically in any scene that contains a Vehicle Physics Pro car
(SUV and Sports Car). Optional: *Tools > Work Sampling > Add Experiment Manager To SUV + Sports Car Scenes*
saves it into both scenes so you can edit participant id, fog density and run length in the Inspector.

| State | Rule (first match wins) |
|---|---|
| Idling / Delay | speed < 3 km/h |
| Unsafe Violation | speed > limit (50 km/h globally, 30 km/h inside a zone) |
| Cautious Control | brake > 0.15, or decelerating > 1.2 m/s^2, or speed < 20 km/h |
| Normal Cruising | everything else |

Random snapshots every `Random.Range(2.0, 4.5)` s -> about 37 samples per 120 s run.
**Compliance:** a zone counts as compliant if the car *enters* it at <= 30 km/h (+2 tolerance).
Zones are marked by tall red/yellow diamond signs and an orange road stripe, visible from far away in
clear weather and hidden by fog.

## One-time route setup (per scene, ~2 minutes)
1. Press Play, drive the route once (fog is off).
2. Press **Z** at each of 5-8 hazard locations. Zones are saved to
   `Assets/StreamingAssets/ExperimentZones/zones_<scene>.json` (commit these files to git).
   **Backspace** undoes the last zone.
3. Without a recorded file, 6 default zones are placed in a straight line ahead of the start - fine only if the road is straight.

## Running a participant
Brief them: *"Red/yellow diamond signs mean slow to 30 km/h or below before passing. Drive normally otherwise."*
Give 1-2 minutes of free practice first, then:
1. **1** = Run 1 (Clear, 120 s). The car is reset to the start automatically.
2. **2** = Run 2 (Fog, 120 s) on the same route.
3. CSVs export automatically after each run (**F9** = manual). **3** aborts a run.

Counterbalance order across participants (half Clear-first, half Fog-first) to cancel learning effects.
Set `participantId` in the Inspector before each participant.

## Output (`Exports/` in the project folder; persistentDataPath in builds)
`*_samples.csv`, `*_zones.csv`, `*_summary.csv`, `*_comparison.md` (instant table in Unity), then for the report:

    pip install pandas numpy scipy matplotlib
    python Analysis/analyze_work_sampling.py            # or pass the Exports folder path

This gives the Clear-vs-Fog table with 95% CIs (E = 1.96*sqrt(p(1-p)/N)), z-tests, a chi-square test,
Fisher's exact test for compliance, and `analysis_chart.png`.

## Limitations to state in the report
- N of about 37 per run gives roughly +/-15 pp CIs; pool participants (or lengthen runs) toward N = 385 for +/-5 pp.
- Eight zones per run is a small denominator for compliance, so use the exact test and Wilson CI.
- Speed/brake thresholds are operational definitions chosen by the team.
- Pooled samples from one participant are not fully independent (autocorrelation).
