using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace AuthFix;

[BepInAutoPlugin("dev.xtracube.authfix")]
// ReSharper disable once ClassNeverInstantiated.Global
public partial class AuthPlugin : BasePlugin
{
    [DllImport("libstarlight.so", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern string get_string(string key);

    public override void Load()
    {
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), Id);
        ServerManager.DefaultRegions = new Il2CppReferenceArray<IRegionInfo>(0);
        
        SceneManager.add_sceneLoaded((System.Action<Scene, LoadSceneMode>) ((scene, _) =>
        {
            if (scene.name == "MainMenu")
            {
                ModManager.Instance.ShowModStamp();
            }
        }));
    }
    
    [HarmonyPatch(typeof(ServerDropdown), nameof(ServerDropdown.FillServerOptions))]
    public static class ServerDropdownPatch
    {
        private static bool RegionEquals(IRegionInfo region, IRegionInfo other)
        {
            return region.Name == other.Name &&
                   region.TranslateName == other.TranslateName &&
                   region.PingServer == other.PingServer &&
                   region.TargetServer == other.TargetServer &&
                   region.Servers.All(s => other.Servers.Any(x => x.Equals(s)));
        }

        public static bool Prefix(ServerDropdown __instance)
        {
            var num = 0;
            __instance.background.size = new Vector2(8.4f, 4.8f);

            foreach (var regionInfo in ServerManager.Instance.AvailableRegions)
            {
                var findingGame = SceneManager.GetActiveScene().name is "FindAGame";

                if (RegionEquals(ServerManager.Instance.CurrentRegion, regionInfo))
                {
                    __instance.defaultButtonSelected = __instance.firstOption;
                    __instance.firstOption.ChangeButtonText(
                        TranslationController.Instance.GetStringWithDefault(
                            regionInfo.TranslateName,
                            regionInfo.Name));
                }
                else
                {
                    var region = regionInfo;
                    var serverListButton = __instance.ButtonPool.Get<ServerListButton>();
                    var x = num % 2 == 0 ? -2 : 2;
                    if (findingGame)
                    {
                        x += 2;
                    }

                    var y = -0.55f * (num / 2f);
                    serverListButton.transform.localPosition = new Vector3(x, __instance.y_posButton + y, -1f);
                    serverListButton.transform.localScale = Vector3.one;
                    serverListButton.Text.text =
                        TranslationController.Instance.GetStringWithDefault(
                            regionInfo.TranslateName,
                            regionInfo.Name);
                    serverListButton.Text.ForceMeshUpdate();
                    serverListButton.Button.OnClick.RemoveAllListeners();
                    serverListButton.Button.OnClick.AddListener(
                        (UnityAction)(() => { __instance.ChooseOption(region); }));
                    __instance.controllerSelectable.Add(serverListButton.Button);
                    __instance.background.transform.localPosition = new Vector3(
                        findingGame ? 2f : 0f,
                        __instance.initialYPos + -0.3f * (num / 2f),
                        0f);
                    __instance.background.size = new Vector2(__instance.background.size.x, 1.2f + 0.6f * (num / 2f));
                    num++;
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(EOSManager), nameof(EOSManager.LoginWithCorrectPlatformImpl))]
    public static class AuthPatch
    {
        // ReSharper disable once InconsistentNaming
        public static bool Prefix(EOSManager __instance, OnLoginCallback successCallbackIn)
        {
            var loginOptions = new LoginOptions();
            var credentials = new Credentials();
            credentials.Token = new Utf8String("DUMMY");
            credentials.Type = ExternalCredentialType.ItchioKey;
            loginOptions.Credentials = new Il2CppSystem.Nullable<Credentials>(credentials);
            var loginOptions2 = loginOptions;
            __instance.PlatformInterface.GetConnectInterface().Login(ref loginOptions2, null, successCallbackIn);
            __instance.stopTimeOutCheck = true;

            return false;
        }
    }

    [HarmonyPatch(typeof(StoreManager), nameof(StoreManager.InitiateStorePurchaseStar))]
    public static class DisableStarBuyPatch
    {
        // ReSharper disable once InconsistentNaming
        public static bool Prefix()
        {
            var purchasePopUp = StoreMenu.Instance.plsWaitModal;
            purchasePopUp.waitingText.gameObject.SetActive(false);
            purchasePopUp.titleText.text = get_string("starlight_iap_not_supported_title");
            purchasePopUp.infoText.text = get_string("starlight_iap_not_supported_desc");
            purchasePopUp.infoText.gameObject.SetActive(true);
            purchasePopUp.controllerFocusHolder.gameObject.SetActive(true);
            purchasePopUp.closeButton.gameObject.SetActive(true);
            return false;
        }
    }
}