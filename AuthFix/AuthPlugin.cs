using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using HarmonyLib;
using Il2CppSystem;

namespace AuthFix;

[BepInAutoPlugin("dev.xtracube.authplugin")]
// ReSharper disable once ClassNeverInstantiated.Global
public partial class AuthPlugin : BasePlugin
{
    public override void Load()
    {
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), Id);
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
            loginOptions.Credentials = new Nullable<Credentials>(credentials);
            var loginOptions2 = loginOptions;
            __instance.PlatformInterface.GetConnectInterface().Login(ref loginOptions2, null, successCallbackIn);
            __instance.stopTimeOutCheck = true;

            return false;
        }
    }

    [HarmonyPatch(typeof(EOSManager), nameof(EOSManager.EOSConnectPlatformLoginCallback))]
    public static class AuthCallbackPatch
    {
        // ReSharper disable once InconsistentNaming
        public static bool Prefix(EOSManager __instance, LoginCallbackInfo loginCallbackInfo)
        {
            if (loginCallbackInfo.ResultCode == Result.Success)
            {
                return true;
            }

            __instance.ContinueInOfflineMode();
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
            purchasePopUp.titleText.text = "NOT SUPPORTED";
            purchasePopUp.infoText.text = "Platform Purchases are not supported in Starlight.\nBuy in the vanilla client instead.";
            purchasePopUp.infoText.gameObject.SetActive(true);
            purchasePopUp.controllerFocusHolder.gameObject.SetActive(true);
            purchasePopUp.closeButton.gameObject.SetActive(true);
            return false;
        }
    }
}