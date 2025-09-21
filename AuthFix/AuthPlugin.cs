using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using HarmonyLib;
using Il2CppSystem;
using System.Collections.Generic;

namespace AuthFix;

[BepInAutoPlugin("dev.xtracube.authfix")]
// ReSharper disable once ClassNeverInstantiated.Global
public partial class AuthPlugin : BasePlugin
{
    private static Dictionary<string, string> Translations = [];

    public static string GetStarlightString(string key)
    {
        if (Translations.TryGetValue(key, out var value))
        {
            return value;
        }
        return "STRMISS";
    }

    public override void Load()
    {
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), Id);

        var translationsField = typeof(IL2CPPChainloader).GetField("Translations", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (translationsField != null)
        {
            if (translationsField.GetValue(null) is Dictionary<string, string> translations)
            {
                Translations = translations;
                foreach (var kvp in translations)
                {
                    Log.LogInfo($"Key: {kvp.Key}, Value: {kvp.Value}");
                }
            }
        }
        else
        {
            Log.LogInfo("Translations field not found");
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
            loginOptions.Credentials = new Nullable<Credentials>(credentials);
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
            purchasePopUp.titleText.text = GetStarlightString("starlight_iap_not_supported_title");
            purchasePopUp.infoText.text = GetStarlightString("starlight_iap_not_supported_desc");
            purchasePopUp.infoText.gameObject.SetActive(true);
            purchasePopUp.controllerFocusHolder.gameObject.SetActive(true);
            purchasePopUp.closeButton.gameObject.SetActive(true);
            return false;
        }
    }
}