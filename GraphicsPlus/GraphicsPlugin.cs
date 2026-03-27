using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace GraphicsPlus;

[BepInAutoPlugin("dev.xtracube.graphicsplus")]
public partial class GraphicsPlugin : BasePlugin
{
    private static GraphicsPlugin Instance { get; set; }

    private ConfigEntry<int> TargetFrameRate { get; set; }

    private ConfigEntry<bool> FullResolution { get; set; }

    private static ResolutionManager _resolutionManager;
    
    [LibraryImport("libstarlight.so")]
    private static unsafe partial int get_width();

    [LibraryImport("libstarlight.so")]
    private static unsafe partial int get_height();

    public GraphicsPlugin()
    {
        Instance = this;
        TargetFrameRate = Config.Bind("General", "Target Frame Rate", 60, "The target frame rate of the game");
        FullResolution = Config.Bind("General", "Increase Resolution", false,
            "Set the game to use the display resolution.");
    }

    public override void Load()
    {
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), Id);
        ClassInjector.RegisterTypeInIl2Cpp<ResolutionManager>();
        Log.LogInfo("GraphicsPlus loaded!");
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.Awake))]
    public static class FrameRatePatch
    {
        public static void Postfix()
        {
            Instance.Log.LogInfo($"Setting target frame rate to {Instance.TargetFrameRate.Value}");
            Application.targetFrameRate = Instance.TargetFrameRate.Value;

            if (Instance.FullResolution.Value)
            {
                _resolutionManager = Instance.AddComponent<ResolutionManager>();
            }
        }
    }

    public class ResolutionManager : MonoBehaviour
    {
        public ResolutionManager(nint ptr) : base(ptr) { }
        
        public ResolutionManager() : base(ClassInjector.DerivedConstructorPointer<ResolutionManager>())
        {
            ClassInjector.DerivedConstructorBody(this);
        }

        private ScreenOrientation _lastOrientation = ScreenOrientation.Landscape;

        public void Start()
        {
            SetNativeResolution();
        }

        public void Update()
        {
            if (_lastOrientation != Screen.orientation)
            {
                SetNativeResolution();
            }
        }

        public void SetNativeResolution()
        {
            ScalableBufferManager.ResizeBuffers(1f, 1f);
            var width = get_width();
            var height = get_height();
            if (Screen.orientation is ScreenOrientation.Landscape or ScreenOrientation.LandscapeRight)
            {
                Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);
                Instance.Log.LogInfo($"Set resolution to {width}x{height}");
            }
            _lastOrientation = Screen.orientation;
        }
    }
}