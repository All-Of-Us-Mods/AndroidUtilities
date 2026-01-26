using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace GraphicsPlus;

[BepInAutoPlugin("dev.xtracube.graphicsplus")]
public partial class GraphicsPlugin : BasePlugin
{
    private static GraphicsPlugin Instance { get; set; }
    
    private ConfigEntry<int> TargetFrameRate { get; set; }

    private ConfigEntry<bool> FullResolution { get; set; }
    
    public GraphicsPlugin()
    {
        Instance = this;
        TargetFrameRate = Config.Bind("General", "Target Frame Rate", 60, "The target frame rate of the game");
        FullResolution = Config.Bind("General", "Increase Resolution", false, "Set the game to use the display resolution.");
    }

    public override void Load()
    {
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), Id);
        Log.LogInfo("GraphicsPlus loaded!");
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.Awake))]
    public static class FrameRatePatch
    {
        public static void Postfix()
        { 
            Instance.Log.LogInfo($"Setting target frame rate to {Instance.TargetFrameRate.Value}");
            Application.targetFrameRate = Instance.TargetFrameRate.Value;

            if (!Instance.FullResolution.Value) return;

            Instance.Log.LogInfo($"Setting fullscreen resolution to {Display.main.systemWidth}x{Display.main.systemHeight}");
            
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;

            Screen.orientation = ScreenOrientation.Portrait;

            Screen.SetResolution(
                Display.main.systemWidth,
                Display.main.systemHeight,
                true
            );
        }
    }
}