using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Rewired;
using UnityEngine;

namespace AuthFix;

internal static class AndroidKeyboardGuard
{
    private const int SourceKeyboard = 0x00000101;
    private const int SourceGamepad = 0x00000401;
    private const int SourceJoystick = 0x01000010;
    private const int KeyboardTypeAlphabetic = 2;

    private static float nextDeviceScanTime;
    private static bool cachedHasHardwareKeyboard;
    private static bool cachedHasRealGamepad;
    private static readonly HashSet<string> cachedKeyboardNames = [];

    public static bool IsKeyboardController()
    {
        RefreshAndroidInputDevices();

        return cachedHasHardwareKeyboard && !cachedHasRealGamepad;
    }

    public static bool ShouldUseKeyboardControls()
    {
        if (Application.platform != RuntimePlatform.Android)
            return false;

        RefreshAndroidInputDevices();

        if (!cachedHasHardwareKeyboard)
            return false;

        if (!cachedHasRealGamepad)
            return true;

        return IsKeyboardController();
    }

    public static bool ShouldBlockControllerInput()
    {
        return ActiveInputManager.currentControlType == ActiveInputManager.InputType.Keyboard &&
               ShouldUseKeyboardControls();
    }

    public static void ForceKeyboardMode()
    {
        ActiveInputManager.InputType oldType = ActiveInputManager.currentControlType;
        ActiveInputManager.currentControlType = ActiveInputManager.InputType.Keyboard;

        if (DestroyableSingleton<HudManager>.InstanceExists)
        {
            IVirtualJoystick joystick = DestroyableSingleton<HudManager>.Instance.joystick;
            if (joystick == null || joystick.TryCast<KeyboardJoystick>() == null)
                DestroyableSingleton<HudManager>.Instance.SetTouchType(ControlTypes.Keyboard);
        }

        if (oldType != ActiveInputManager.InputType.Keyboard)
            ActiveInputManager.CurrentInputSourceChanged?.Invoke();
    }

    public static void ForceKeyboardMode(ref Rewired.Controller lastUsedController)
    {
        lastUsedController = null;
        ForceKeyboardMode();
    }

    private static bool ControllerNameLooksLikeKeyboard(Rewired.Controller controller)
    {
        var joystick = controller as Joystick;
        string controllerName = (controller.name ?? string.Empty).ToLowerInvariant();
        string hardwareId = joystick != null
            ? (joystick.hardwareIdentifier ?? string.Empty).ToLowerInvariant()
            : string.Empty;

        string combined = controllerName + " " + hardwareId;

        if (combined.Contains("keyboard") ||
            combined.Contains("qwerty") ||
            combined.Contains("keypad") ||
            combined.Contains("gpio-keys"))
        {
            return true;
        }

        foreach (string keyboardName in cachedKeyboardNames)
        {
            if (string.IsNullOrEmpty(keyboardName))
                continue;

            string name = keyboardName.ToLowerInvariant();

            if (controllerName.Contains(name) ||
                hardwareId.Contains(name) ||
                name.Contains(controllerName))
            {
                return true;
            }
        }

        return false;
    }

    private static void RefreshAndroidInputDevices()
    {
        if (Time.unscaledTime < nextDeviceScanTime)
            return;

        nextDeviceScanTime = Time.unscaledTime + 1f;
        cachedHasHardwareKeyboard = false;
        cachedHasRealGamepad = false;
        cachedKeyboardNames.Clear();

        try
        {
            using var inputDevice = new AndroidJavaObjectSafe("android.view.InputDevice");

            var idsObj = inputDevice.CallStaticReturn("getDeviceIds");
            if (idsObj == null)
                return;

            var deviceIds = idsObj.Cast<Il2CppStructArray<int>>();

            foreach (var id in deviceIds)
            {
                var deviceRaw = inputDevice.CallStaticReturn("getDevice", AndroidJavaObjectSafe.Args(id));
                if (deviceRaw == null)
                    continue;

                using var device = new AndroidJavaObjectSafe(deviceRaw.Cast<AndroidJavaObject>());

                var sourcesObj = device.CallReturn("getSources");
                if (sourcesObj == null)
                    continue;

                int sources = sourcesObj.Unbox<int>();

                var keyboardTypeObj = device.CallReturn("getKeyboardType");
                int keyboardType = keyboardTypeObj != null ? keyboardTypeObj.Unbox<int>() : 0;

                bool isKeyboard =
                    (sources & SourceKeyboard) == SourceKeyboard &&
                    keyboardType == KeyboardTypeAlphabetic;

                bool isGamepad =
                    (sources & SourceGamepad) == SourceGamepad ||
                    (sources & SourceJoystick) == SourceJoystick;

                if (isKeyboard)
                {
                    cachedHasHardwareKeyboard = true;

                    var nameObj = device.CallReturn("getName");
                    if (nameObj != null)
                    {
                        string name = nameObj.ToString();
                        if (!string.IsNullOrEmpty(name))
                            cachedKeyboardNames.Add(name);
                    }
                }

                // A few keyboards expose arrow keys through joystick-like
                // sources. Do not let that defeat the single-keyboard fallback.
                if (isGamepad && !isKeyboard)
                    cachedHasRealGamepad = true;
            }
        }
        catch
        {
            cachedHasHardwareKeyboard = false;
            cachedHasRealGamepad = false;
            cachedKeyboardNames.Clear();
        }
    }
}
