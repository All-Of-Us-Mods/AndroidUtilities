using AmongUs.Data;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace AuthFix;

[BepInAutoPlugin("dev.xtracube.authfix")]
// ReSharper disable once ClassNeverInstantiated.Global
public partial class AuthPlugin : BasePlugin
{
    [LibraryImport("libstarlight.so", EntryPoint = "get_string", StringMarshalling = StringMarshalling.Utf8)]
    private static unsafe partial string get_string(string key);

    [LibraryImport("libstarlight.so", EntryPoint = "quit_app")]
    private static unsafe partial void quit_app();

    public override void Load()
    {
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), Id);
        ServerManager.DefaultRegions = new Il2CppReferenceArray<IRegionInfo>([]);
        
        SceneManager.add_sceneLoaded((System.Action<Scene, LoadSceneMode>) ((scene, _) =>
        {
            if (scene.name == "MainMenu")
            {
                ModManager.Instance.ShowModStamp();

                GameObject exitGameButton = FindInactiveByName("ExitGameButton");

                if (exitGameButton != null)
                {
                    exitGameButton.GetComponent<ConditionalHide>().enabled = false;
                    exitGameButton.SetActive(true);
                }

                GameObject adsButton = FindInactiveByName("AdsButton");

                // No Standalone script to manage it, it's managed somewhere else.
                if (adsButton != null)
                {
                    adsButton.transform.localScale = Vector3.zero;
                    adsButton.GetComponent<PassiveButton>().enabled = false;
                    adsButton.SetActive(false);
                }
            }
        }));
    }

    public static GameObject FindInactiveByName(string name)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        Il2CppSystem.Collections.Generic.List<GameObject> rootObjects = new Il2CppSystem.Collections.Generic.List<GameObject>();
        activeScene.GetRootGameObjects(rootObjects);

        foreach (GameObject root in rootObjects)
        {
            // GetComponentsInChildren<Transform>(true) finds all children, active or not
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.name == name) return child.gameObject;
            }
        }
        return null;
    }

    [HarmonyPatch(typeof(SceneChanger), nameof(SceneChanger.ExitGame))]
    public static class ExitPatch
    {
        public static bool Prefix()
        {
            quit_app();
            return false;
        }
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
            if (ServerManager.Instance.AvailableRegions.Count <= 3)
            {
                // Don't adjust for small region lists
                return true;
            }
            
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
        public static bool Prefix(EOSManager __instance, [HarmonyArgument(0)] OnLoginCallback successCallbackIn)
        {
            var loginOptions = new LoginOptions();
            var credentials = new Credentials();
            credentials.Token = new Utf8String("DUMMY");
            credentials.Type = ExternalCredentialType.ItchioKey;
            loginOptions.Credentials = new Il2CppSystem.Nullable<Credentials>(credentials);

            // Callback must be pinned to avoid GC collecting it
            GCHandle.Alloc(successCallbackIn, GCHandleType.Pinned);
            __instance.PlatformInterface.GetConnectInterface().Login(ref loginOptions, null, successCallbackIn);
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

    [HarmonyPatch(typeof(EOSManager), nameof(EOSManager.ContinueInOfflineMode))]
    public static class SignInFailPatch
    {
        public static bool SignInFailed;

        // ReSharper disable once InconsistentNaming
        public static bool Prefix()
        {
            SignInFailed = true;
            DataManager.Player.Account.LoginStatus = EOSManager.AccountLoginStatus.LoggedIn;
            DataManager.Settings.Multiplayer.ChatMode = QuickChatModes.FreeChatOrQuickChat;
            DataManager.Player.Save();
            EOSManager.Instance.IsAllowedOnline(true);
            return true;
        }
    }

    [HarmonyPatch(typeof(HttpMatchmakerManager), nameof(HttpMatchmakerManager.TryReadCachedToken))]
    public static class CoGetOrRefreshTokenPatch
    {
        public static bool Prefix(ref bool __result, ref string matchmakerToken)
        {
            if (!SignInFailPatch.SignInFailed)
            {
                return true;
            }

            __result = true;
            matchmakerToken = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new
            {
                Content = new
                {
                    Puid = "RemoveAccounts",
                    ClientVersion = Constants.GetBroadcastVersion(),
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                },
                Hash = "impostor_was_here",
            }));
            return false;
        }
    }

    [HarmonyPatch(typeof(EOSManager), nameof(EOSManager.IsAllowedOnline))]
    public static class IsAllowedOnlineOverride
    {
        public static bool Prefix([HarmonyArgument(0)] ref bool canOnline)
        {
            if (SignInFailPatch.SignInFailed)
            {
                canOnline = true;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(AccountManager), nameof(AccountManager.CanPlayOnline))]
    public static class CanPlayOnlineOverride
    {
        public static void Postfix([HarmonyArgument(0)] ref bool __result)
        {
            if (SignInFailPatch.SignInFailed)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(EOSManager), nameof(EOSManager.IsFreechatAllowed))]
    public static class IsFreechatAllowedOverride
    {
        public static void Postfix(ref bool __result)
        {
            if (SignInFailPatch.SignInFailed)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(EOSManager), nameof(EOSManager.UpdatePermissionKeys))]
    public static class UpdatePermissionKeysOverride
    {
        public static bool Prefix([HarmonyArgument(0)] Il2CppSystem.Action callback)
        {
            if (!SignInFailPatch.SignInFailed)
            {
                return true;
            }

            DestroyableSingleton<FriendsListManager>.Instance.Ui.Close(false);
            DataManager.Player.Account.LoginStatus = EOSManager.AccountLoginStatus.LoggedIn;
            DataManager.Player.Save();
            callback.Invoke();
            return false;
        }
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.Update))]
    public static class AmongUsClientUpdate
    {
        public static void Postfix()
        {
            if (SignInFailPatch.SignInFailed && EOSManager.Instance.loginFlowFinished)
            {
                DataManager.Player.Account.LoginStatus = EOSManager.AccountLoginStatus.LoggedIn;
            }
        }
    }


    [HarmonyPatch(typeof(EOSManager), nameof(EOSManager.ProductUserId), MethodType.Getter)]
    public static class ProductUserIdOverride
    {
        public static bool Prefix(ref string __result)
        {
            if (!SignInFailPatch.SignInFailed)
            {
                return true;
            }

            __result = ".";
            return false;
        }
    }

    [HarmonyPatch(typeof(EOSManager), nameof(EOSManager.UserIDToken), MethodType.Getter)]
    public static class UserIDTokenOverride
    {
        public static bool Prefix(ref string __result)
        {
            if (!SignInFailPatch.SignInFailed)
            {
                return true;
            }

            __result = ".";
            return false;
        }
    }
}