using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using VehiclePhysics;
using CommonUsages = UnityEngine.XR.CommonUsages;
using XRDevice = UnityEngine.XR.InputDevice;

/// <summary>Maps Quest Touch Plus controls to Vehicle Physics Pro external inputs.</summary>
public class Quest3DrivingInput : MonoBehaviour
{
    VehicleBase vehicle;
    Component vppInput;
    Transform driverCameraSeatOrigin;
    FieldInfo throttle, brake, handbrake, steer, clutch, reverse;
    static FieldInfo cameraMode;
    static bool driverCameraAvailable;
    bool vrCameraInitialized;
    float aPressedAt;
    bool aWasPressed;

    void Start()
    {
        cameraComponent = null;
        cameraMode = null;
        foreach (var component in FindObjectsByType<MonoBehaviour>())
        {
            if (component == null) continue;
            var type = component.GetType();
            var changeKey = type.GetField("changeCameraKey");
            var modeField = type.GetField("mode");
            if (changeKey != null && modeField != null)
            {
                cameraComponent = component;
                cameraMode = modeField;
                break;
            }
        }
        vehicle = FindAnyObjectByType<VehicleBase>();
        if (vehicle != null)
        {
            foreach (var component in vehicle.GetComponents<MonoBehaviour>())
            {
                var type = component.GetType();
                var t = type.GetField("externalThrottle");
                var s = type.GetField("externalSteer");
                if (t == null || s == null) continue;
                vppInput = component;
                throttle = t; steer = s;
                brake = type.GetField("externalBrake");
                handbrake = type.GetField("externalHandbrake");
                clutch = type.GetField("externalClutch");
                reverse = type.GetField("reverse");
                break;
            }
        }
        else Debug.LogWarning("[Quest 3] No VPP vehicle found in this scene.");
        if (vehicle != null && vppInput == null)
            Debug.LogWarning("[Quest 3] Could not find VPP standard input fields on the active vehicle. Quest driving axes are inactive.");
        if (cameraComponent == null)
            Debug.LogWarning("[Quest 3] Could not find a VPP camera controller. Quest camera switching is inactive.");
        ConfigureDriverCamera();
        ConfigureHeadTracking();
        EnsureVrDriverCamera();
    }

    void ConfigureDriverCamera()
    {
        driverCameraAvailable = false;
        if (vehicle == null || cameraComponent == null) return;

        Transform driverAnchor = FindDriverAnchor();
        if (driverAnchor == null)
        {
            Debug.LogWarning("[Quest 3] No DriverHead/Driver View anchor found on the vehicle; first-person camera is unavailable.");
            return;
        }

        FieldInfo driverField = cameraComponent.GetType().GetField("driver", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (driverField == null)
        {
            Debug.LogWarning("[Quest 3] VPP camera controller has no driver settings field; first-person camera is unavailable.");
            return;
        }

        object driverSettings = driverField.GetValue(cameraComponent);
        if (driverSettings == null) return;

        FieldInfo attachTarget = driverSettings.GetType().GetField("attachTarget", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (attachTarget == null || !attachTarget.FieldType.IsAssignableFrom(typeof(Transform)))
        {
            Debug.LogWarning("[Quest 3] VPP driver settings have no Transform attachTarget; first-person camera is unavailable.");
            return;
        }

        Vector3 seatLocalPosition = vehicle.transform.InverseTransformPoint(driverAnchor.position);
        seatLocalPosition.y = 0f; // The XR pose supplies eye height above the vehicle floor.
        driverCameraSeatOrigin = vehicle.transform.Find("Quest3 Driver Seat Origin");
        if (driverCameraSeatOrigin == null)
        {
            var seatOriginObject = new GameObject("Quest3 Driver Seat Origin");
            driverCameraSeatOrigin = seatOriginObject.transform;
            driverCameraSeatOrigin.SetParent(vehicle.transform, false);
        }
        driverCameraSeatOrigin.localPosition = seatLocalPosition;
        driverCameraSeatOrigin.localRotation = Quaternion.Inverse(vehicle.transform.rotation) * driverAnchor.rotation;
        driverCameraSeatOrigin.localScale = Vector3.one;

        attachTarget.SetValue(driverSettings, driverCameraSeatOrigin);
        if (driverField.FieldType.IsValueType) driverField.SetValue(cameraComponent, driverSettings);
        driverCameraAvailable = true;
        Debug.Log("[Quest 3] Driver camera attached to floor-level seat origin from '" + driverAnchor.name + "'.");
    }

    Transform FindDriverAnchor()
    {
        Transform[] children = vehicle.GetComponentsInChildren<Transform>(true);
        string[] preferredNames = { "DriverHead", "Driver View", "DriverCamera", "Driver Camera", "DriverSeat", "Driver Seat" };
        foreach (string preferredName in preferredNames)
            foreach (Transform child in children)
                if (string.Equals(child.name, preferredName, StringComparison.OrdinalIgnoreCase)) return child;

        return null;
    }

    void ConfigureHeadTracking()
    {
        Camera xrCamera = cameraComponent != null
            ? cameraComponent.GetComponentInChildren<Camera>(true)
            : Camera.main;
        if (xrCamera == null)
        {
            Debug.LogError("[Quest 3] No Main Camera found; headset tracking cannot be applied.");
            return;
        }

        var poseDriver = xrCamera.GetComponent<TrackedPoseDriver>();
        if (poseDriver != null) return;

        poseDriver = xrCamera.gameObject.AddComponent<TrackedPoseDriver>();
        poseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
        poseDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
        poseDriver.ignoreTrackingState = true;
        poseDriver.positionInput = new InputActionProperty(new InputAction(
            "Quest Head Position", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
        poseDriver.rotationInput = new InputActionProperty(new InputAction(
            "Quest Head Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
        Debug.Log("[Quest 3] Added Input System head pose tracking to '" + xrCamera.name + "'.");
    }

    void Update()
    {
        EnsureVrDriverCamera();
        XRDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        XRDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!left.isValid || !right.isValid)
        {
            Set(steer, 0f); Set(throttle, 0f); Set(brake, 0f);
            Set(handbrake, 0f); Set(clutch, 0f); Set(reverse, false);
            if (aWasPressed && vehicle != null) vehicle.data.Set(Channel.Input, InputData.Key, 0);
            aWasPressed = false;
            rightFlickHeld = false;
            return;
        }
        bool modifier = Read(left, CommonUsages.primary2DAxisClick);
        if (vppInput != null)
        {
            Vector2 stick; left.TryGetFeatureValue(CommonUsages.primary2DAxis, out stick);
            Vector2 rightStick; right.TryGetFeatureValue(CommonUsages.primary2DAxis, out rightStick);
            Set(steer, stick.x);
            Set(throttle, Axis(right, CommonUsages.trigger));
            Set(brake, Axis(left, CommonUsages.trigger));
            Set(handbrake, Axis(right, CommonUsages.grip));
            Set(clutch, Axis(left, CommonUsages.grip));
            Set(reverse, !modifier && Read(right, CommonUsages.secondaryButton));
            if (!modifier) HandleIgnition(right);
            else if (aWasPressed && vehicle != null)
            {
                vehicle.data.Set(Channel.Input, InputData.Key, 0);
                aWasPressed = false;
            }
            if (!modifier && rightStick.y > 0.75f && !rightFlickHeld) ShiftGear(1);
            if (!modifier && rightStick.y < -0.75f && !rightFlickHeld) ShiftGear(-1);
            rightFlickHeld = Mathf.Abs(rightStick.y) > 0.75f;
        }
        if (ReadDown(right, CommonUsages.primary2DAxisClick)) TryRecenter();
    }

    void EnsureVrDriverCamera()
    {
        if (vrCameraInitialized || !XRSettings.isDeviceActive || cameraMode == null || cameraComponent == null) return;
        // Prefer the in-car view when its vehicle anchor is available.
        SetCameraMode(driverCameraAvailable ? 0 : 1);
        vrCameraInitialized = true;
        Camera xrCamera = cameraComponent.GetComponentInChildren<Camera>(true);
        Vector3 carPosition = vehicle != null ? vehicle.transform.position : Vector3.positiveInfinity;
        string cameraPosition = xrCamera != null ? xrCamera.transform.position.ToString("F2") : "missing";
        string cameraRotation = xrCamera != null ? xrCamera.transform.eulerAngles.ToString("F1") : "missing";
        string cameraLocalPosition = xrCamera != null ? xrCamera.transform.localPosition.ToString("F2") : "missing";
        Debug.Log("[Quest 3] VR " + (driverCameraAvailable ? "driver" : "follow") + " camera active. Rig=" + cameraComponent.transform.position.ToString("F2") +
            ", camera=" + cameraPosition + ", local=" + cameraLocalPosition + ", rotation=" + cameraRotation +
            ", vehicle=" + carPosition.ToString("F2") +
            (xrCamera != null ? ", cullingMask=" + xrCamera.cullingMask + ", targetEye=" + xrCamera.stereoTargetEye : ""));
    }

    static void SetCameraMode(int mode)
    {
        Type type = cameraMode.FieldType;
        if (type.IsEnum) cameraMode.SetValue(cameraComponent, Enum.ToObject(type, mode));
        else if (type == typeof(int)) cameraMode.SetValue(cameraComponent, mode);
    }

    static Component cameraComponent;
    bool rightFlickHeld;

    public static void ToggleCamera()
    {
        if (cameraMode == null || cameraComponent == null) return;
        Type modeType = cameraMode.FieldType;
        if (modeType.IsEnum)
        {
            Array modes = Enum.GetValues(modeType);
            int current = Array.IndexOf(modes, cameraMode.GetValue(cameraComponent));
            for (int i = 1; i <= modes.Length; i++)
            {
                int next = (current + i) % modes.Length;
                if (!driverCameraAvailable && Convert.ToInt32(modes.GetValue(next)) == 0) continue;
                cameraMode.SetValue(cameraComponent, modes.GetValue(next));
                return;
            }
        }
        else if (modeType == typeof(int))
        {
            int next = ((int)cameraMode.GetValue(cameraComponent) + 1) % 5;
            if (!driverCameraAvailable && next == 0) next = 1;
            cameraMode.SetValue(cameraComponent, next);
        }
    }

    void ShiftGear(int increment)
    {
        int pending = vehicle.data.Get(Channel.Input, InputData.GearShift);
        vehicle.data.Set(Channel.Input, InputData.GearShift, pending + increment);
    }

    void HandleIgnition(XRDevice right)
    {
        bool pressed = Read(right, CommonUsages.primaryButton);
        if (pressed && !aWasPressed) aPressedAt = Time.time;
        if (!pressed && aWasPressed)
        {
            if (vehicle != null) vehicle.data.Set(Channel.Input, InputData.Key, Time.time - aPressedAt >= 0.65f ? 1 : 0);
        }
        aWasPressed = pressed;
        if (pressed && Time.time - aPressedAt >= 0.65f && vehicle != null)
            vehicle.data.Set(Channel.Input, InputData.Key, 1);
        if (!pressed && vehicle != null && vehicle.data.Get(Channel.Input, InputData.Key) == 1)
            vehicle.data.Set(Channel.Input, InputData.Key, 0);
    }

    static void TryRecenter()
    {
        var subsystems = new System.Collections.Generic.List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        foreach (var subsystem in subsystems)
            if (subsystem.running && subsystem.TryRecenter()) break;
    }

    static float Axis(XRDevice d, InputFeatureUsage<float> usage) { float v; return d.TryGetFeatureValue(usage, out v) ? Mathf.Clamp01(v) : 0f; }
    static bool Read(XRDevice d, InputFeatureUsage<bool> usage) { bool v; return d.TryGetFeatureValue(usage, out v) && v; }
    static readonly System.Collections.Generic.HashSet<string> held = new System.Collections.Generic.HashSet<string>();
    static bool ReadDown(XRDevice d, InputFeatureUsage<bool> usage)
    {
        string key = d.name + ":" + usage.name;
        bool value = Read(d, usage);
        bool was = held.Contains(key);
        if (value) held.Add(key); else held.Remove(key);
        return value && !was;
    }
    void Set(FieldInfo field, float value)
    {
        if (field == null || vppInput == null) return;
        if (field.FieldType == typeof(bool)) field.SetValue(vppInput, value >= 0.5f);
        else if (field.FieldType == typeof(float)) field.SetValue(vppInput, value);
        else if (field.FieldType == typeof(int)) field.SetValue(vppInput, Mathf.RoundToInt(value));
    }
    void Set(FieldInfo field, bool value)
    {
        if (field == null || vppInput == null) return;
        if (field.FieldType == typeof(bool)) field.SetValue(vppInput, value);
        else if (field.FieldType == typeof(float)) field.SetValue(vppInput, value ? 1f : 0f);
        else if (field.FieldType == typeof(int)) field.SetValue(vppInput, value ? 1 : 0);
    }
}
