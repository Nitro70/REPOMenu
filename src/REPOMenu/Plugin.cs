using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using REPOMenu.Core;
using REPOMenu.Features;
using UnityEngine;

namespace REPOMenu
{
    [BepInPlugin(Brand.Guid, Brand.Name, Brand.Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static Harmony Harmony;

        private void Awake()
        {
            Log = Logger;
            Settings.Init(Config);

            Harmony = new Harmony(Brand.Guid);
            Features.Patches.Install(Harmony);

            Hub.RegisterAll();

            // our own object, kept across scenes and out of the scene lists, so the game's scene
            // cleanup never takes the menu with it
            var go = new GameObject("REPOMenu.Runner");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<Runner>();

            Log.LogInfo($"{Brand.Name} {Brand.Version} by {Brand.Author} loaded. Press {Settings.MenuKey.Value} to open it.");
        }
    }
}
