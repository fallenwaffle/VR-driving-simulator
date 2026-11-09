# Unity 6.3 Upgrade Verification Checklist

## Pre-Opening Checks
- [ ] Unity 6.3.0f1 installed via Unity Hub with Android Build Support, OpenJDK, SDK/NDK
- [ ] Backup of original project created (git branch `unity6-upgrade` created)

## After Opening Project in Unity 6.3

### 1. Compilation
- [ ] 0 compile errors in Console
- [ ] No obsolete warnings (except expected VPP deprecations that we fixed)
- [ ] All custom scripts compile: trial.cs, last_script.cs, WorkSamplingObserver.cs, SignalZone.cs, CSVExporter.cs

### 2. Scene Loading
- [ ] Main Menu.unity opens without missing scripts
- [ ] SUV.unity opens without missing scripts
- [ ] Sports Car.unity opens without missing scripts
- [ ] No pink materials (missing shaders) in any scene

### 3. Vehicle Physics
- [ ] Car drives forward with acceleration input
- [ ] Speedometer/speed readout displays correct speed
- [ ] Steering wheel input works (NITTO Drive 1 Pro in Xbox mode)
- [ ] Reverse gear works correctly
- [ ] Braking works correctly
- [ ] Vehicle physics feel the same as Unity 2022 (no unexpected behavior)

### 4. VR Setup
- [ ] Meta Quest 2 headset connects via Air Link
- [ ] VR camera rig displays correctly
- [ ] Head tracking works (look around inside car)
- [ ] Quest Touch controllers work for interactions
- [ ] Single-Pass Instanced Rendering is enabled (XR Plug-in Management > Android)

### 5. XR Configuration
- [ ] OpenXR enabled for Android platform
- [ ] Oculus Touch controller profile enabled
- [ ] Meta Quest feature group enabled
- [ ] XR Interaction Toolkit components load correctly

### 6. Experimental Scripts (NEW)
- [ ] WorkSamplingObserver component can be added to car
- [ ] SignalZone triggers work (car entering/exiting zones)
- [ ] Random sampling occurs at 2-4.5 second intervals
- [ ] 4-state classification works (Stopped <25, Low 25-30, Medium 30-60, High >60 km/h)
- [ ] CSVExporter can export data to CSV files
- [ ] CSV columns: Timestamp, Speed_Kmh, State, InSignalZone, SignalCompliant, SignalType

### 7. Fog and Lighting
- [ ] Fog settings preserved (RenderSettings: fog mode, density, color)
- [ ] Daylight lighting looks the same as before
- [ ] Skybox displays correctly
- [ ] No unexpected light changes
- [ ] **Note:** Lightmaps may need rebaking if Unity prompts for it

### 8. Built-in Render Pipeline
- [ ] Built-in pipeline still active (Graphics Settings)
- [ ] **Note:** Built-in pipeline is deprecated in Unity 6.5+ but still supported through 6.7 LTS
- [ ] No migration to URP needed yet (can stay on Built-in for now)

### 9. Vehicle Physics Pro
- [ ] VPP SDK compiles without errors
- [ ] VPP vehicles (JPickup, Sport Coupe) load correctly
- [ ] Wheel colliders work
- [ ] **Critical:** If VPP is older than v9.7, update from Asset Store manually with your license
- [ ] After VPP update, verify our fixes to PauseVehicle.cs still work

### 10. Package Versions
Current versions (may auto-update when opening in Unity 6.3):
- XR Interaction Toolkit: 2.6.3 (kept for compatibility - can upgrade to 3.x later if needed)
- OpenXR: 1.13.2 (will likely auto-update to 1.16.1 for Unity 6.3)
- Input System: 1.11.2 (will likely auto-update)
- Cinemachine: 2.10.3 (kept 2.x to avoid breaking changes)
- TextMeshPro: 3.0.7 (may auto-update or be integrated into UGUI)
- Timeline: 1.7.6
- Visual Scripting: 1.9.4

## Manual Editor Steps Required

### 1. First Time Opening
1. Open Unity Hub
2. Add project: "The_Car_Simulator_-VR--main"
3. Unity will detect version mismatch and prompt to upgrade
4. Confirm the upgrade to Unity 6.3.0f1
5. Wait for Unity to reimport assets (may take several minutes)

### 2. After First Open
1. Check Console for errors
2. If lightmaps are invalid, rebake them:
   - Window > Rendering > Lighting Settings
   - Click "Generate Lighting"
3. Verify XR Plug-in Management:
   - Edit > Project Settings > XR Plug-in Management
   - Android tab: Ensure OpenXR is checked
   - OpenXR > Meta Quest feature group should be enabled

### 3. Test VR Build
1. Build Settings > Android
2. Ensure texture compression is ASTC
3. Ensure Graphics APIs include Vulkan/OpenGLES3
4. Build and test on Quest 2

## Potential Issues and Solutions

### Issue: VPP Compile Errors
**Solution:** Update Vehicle Physics Pro to v9.7+ from Asset Store (requires your license)

### Issue: XR Interaction Toolkit Breaking Changes
**Solution:** We kept XRI 2.6.3 for now. To upgrade to 3.x later:
- Remove old XRI samples before upgrading
- Use Unity's automatic namespace migration tool
- Rebuild XR rig from Starter Assets if needed

### Issue: Fog/Lighting Looks Different
**Solution:** Rebake lightmaps (Window > Rendering > Lighting Settings > Generate Lighting)

### Issue: TextMeshPro Missing
**Solution:** TextMeshPro is now integrated into UGUI in Unity 6. Unity may auto-merge it.

## CSV Export Test
To test the experimental scripts:
1. Add WorkSamplingObserver to car GameObject
2. Add CSVExporter to scene
3. Add SignalZone trigger zones (colliders with isTrigger = true)
4. Play scene
5. Drive through signal zones
6. Call CSVExporter.ExportClearRun() or ExportFogRun() from code or inspector button
7. Check Assets/Exports folder for CSV files

## Git Commit Summary
After verification, commit the upgrade with:
```
git add .
git commit -m "Upgrade to Unity 6.3.0f1

- Updated ProjectVersion.txt to 6000.3.0f1
- Removed deprecated com.unity.collab-proxy from manifest
- Removed com.unity.textmeshpro (merged into UGUI in Unity 6)
- Fixed Rigidbody.velocity -> linearVelocity for Unity 6 compatibility
- Fixed FindObjectOfType -> FindFirstObjectByType for Unity 6 compatibility
- Fixed VPP PauseVehicle.cs velocity/angularVelocity -> linearVelocity/angularVelocity
- Created experimental scripts: WorkSamplingObserver, SignalZone, CSVExporter
- Built-in Render Pipeline: Still supported through Unity 6.7 LTS
- XR Setup: OpenXR maintained, XRI 2.6.3 kept for compatibility

Generated with [Devin](https://devin.ai)

Co-Authored-By: Devin <158243242+devin-ai-integration[bot]@users.noreply.github.com>"
```
