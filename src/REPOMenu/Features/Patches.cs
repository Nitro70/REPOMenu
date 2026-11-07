using System;
using HarmonyLib;

namespace REPOMenu.Features
{
    // The only Harmony patches the menu needs. Each is installed on its own and a missing method
    // (after a game update) only turns off that one feature instead of stopping the plugin.
    internal static class Patches
    {
        public static bool HurtPatched;

        public static void Install(Harmony h)
        {
            HurtPatched = Prefix(h, typeof(PlayerHealth), nameof(PlayerHealth.Hurt), nameof(HurtPrefix));
            Prefix(h, typeof(PlayerAvatar), nameof(PlayerAvatar.PlayerDeath), nameof(DeathPrefix));
        }

        private static bool Prefix(Harmony h, Type type, string method, string prefix)
        {
            try
            {
                var target = AccessTools.DeclaredMethod(type, method);

                if (target == null)
                {
                    Plugin.Log.LogWarning($"{type.Name}.{method} is gone in this game version; the feature that needs it is off.");
                    return false;
                }

                h.Patch(target, prefix: new HarmonyMethod(typeof(Patches), prefix));
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not patch {type.Name}.{method}: {e.Message}");
                return false;
            }
        }

        // set while the menu kills you on purpose ("Kill yourself")
        public static bool AllowOwnDeath;

        // God mode and "Can't die" also stop deaths your own game starts that skip the damage code
        // (the same guard repo-host-tools uses). Deaths the host orders arrive as an RPC and are
        // left alone, so everyone's games keep agreeing on who is alive.
        private static bool DeathPrefix(PlayerAvatar __instance)
        {
            try
            {
                return AllowOwnDeath || !__instance.isLocal || !(SelfMods.God || SelfMods.NoDeath);
            }
            catch
            {
                return true;
            }
        }

        // "Can't die": damage to the local player stops one point short of death.
        private static bool HurtPrefix(PlayerHealth __instance, ref int damage)
        {
            try
            {
                if (!SelfMods.NoDeath || !__instance.playerAvatar || !__instance.playerAvatar.isLocal)
                {
                    return true;
                }

                if (damage >= __instance.health)
                {
                    damage = __instance.health - 1;
                }

                return damage > 0;
            }
            catch
            {
                return true;
            }
        }
    }
}
