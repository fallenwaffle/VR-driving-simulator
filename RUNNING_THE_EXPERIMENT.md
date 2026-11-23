# Running the experiment

1. Copy the `Assets/` and `UNITY6_UPGRADE_CHECKLIST.md` files over your project (overwrite when asked). Put `analysis/` next to `Assets/`.
2. In Unity: `Tools > Setup Experiment Scripts` (once). It adds the observer to the car and a `[Experiment_Manager]` (CSVExporter + VisibilityController) to the SUV and Sports Car scenes.
3. Place real SignalZones at intersections / sharp turns: move the Scene view camera there and use `Tools > Work Sampling > Add Signal Zone`, then resize the box. Delete or move `[Sample_SignalZone]` (placed at 0,0,100 as a demo).
4. Press Play. HUD shows speed, state, samples and signals followed/total.
5. Run 1: F1 (Clear). Drive the full lap. Run 2: F2 (Fog) — switching auto-exports Run 1 and resets. Drive the same lap, same driver, same input device. F10 exports the current run (also exported on quit).
6. CSVs are in `Application.persistentDataPath/Exports` (path printed in the Console).
7. `python analysis/compare_runs.py summary_Clear_*.csv summary_Fog_*.csv`
