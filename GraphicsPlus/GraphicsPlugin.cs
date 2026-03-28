using System;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GraphicsPlus;

[BepInAutoPlugin("dev.xtracube.graphicsplus")]
public partial class GraphicsPlugin : BasePlugin
{
    private static GraphicsPlugin Instance { get; set; } = null!;

    private ConfigEntry<int> TargetFrameRate { get; set; }

    private ConfigEntry<bool> FullResolution { get; set; }

    private static CustomResolutionManager? _customResolutionManager;
    
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
        ClassInjector.RegisterTypeInIl2Cpp<CustomResolutionManager>();

        SceneManager.add_sceneLoaded((Action<Scene, LoadSceneMode>) ((scene, _) =>
        {
            _customResolutionManager?.SetNativeResolution();
        }));
        
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
                _customResolutionManager = Instance.AddComponent<CustomResolutionManager>();
            }
        }
    }

    public class CustomResolutionManager : MonoBehaviour
    {
        public CustomResolutionManager(nint ptr) : base(ptr) { }
        
        public CustomResolutionManager() : base(ClassInjector.DerivedConstructorPointer<CustomResolutionManager>())
        {
            ClassInjector.DerivedConstructorBody(this);
        }

        private record struct ScreenSize(int Width, int Height);

        private ScreenSize _lastSize = new (0, 0);
        private ScreenOrientation _lastOrientation = ScreenOrientation.Unknown;

        private Coroutine? _activeCoroutine;

        public void Start()
        {
            _activeCoroutine = StartCoroutine(CoSetNativeResolution().WrapToIl2Cpp());
        }

        public void SetNativeResolution()
        {
            _activeCoroutine ??= StartCoroutine(CoSetNativeResolution().WrapToIl2Cpp());
        }

        public void Update()
        {
            var screenSize = new ScreenSize(Display.main.renderingWidth, Display.main.renderingHeight);
            var orientation = Screen.orientation;
            if (_lastSize != screenSize ||  _lastOrientation != orientation)
            {
                _lastSize = screenSize;
                _lastOrientation = orientation;
                _activeCoroutine ??= StartCoroutine(CoSetNativeResolution().WrapToIl2Cpp());
            }
        }

        public IEnumerator CoSetNativeResolution()
        {
            ScalableBufferManager.ResizeBuffers(1f, 1f);
            var width = get_width();
            var height = get_height();
            var aspectRatio = width / (float)height;

            Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);
            Instance.Log.LogInfo($"Set resolution to {width}x{height} (Orientation: {Screen.orientation})");

            yield return null;
            ResolutionManager.ResolutionChanged.Invoke(aspectRatio, width, height, true);

            _activeCoroutine = null;
        }
    }
}