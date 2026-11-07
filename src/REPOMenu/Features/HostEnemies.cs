using System.Linq;
using Photon.Pun;
using REPOMenu.Core;
using REPOMenu.UI;
using UnityEngine;

namespace REPOMenu.Features
{
    // Enemies think on the host's game, so these are host only. Spawning and removing use the same
    // calls as the game's own /spawn, /despawnenemies and /noenemies developer commands.
    internal static class HostEnemies
    {
        public static bool FreezeAll, OneHit;

        private static float freezeAt;
        private static readonly Ui.Picker<EnemySetup> spawnPicker = new();
        private static Vector2 listScroll;
        private static int spawnCount = 1;
        private static bool atAim;

        private static string NeedLevel()
        {
            if (!Game.InWorld)
            {
                return "Load into a level first";
            }

            return Game.Ready && SemiFunc.RunIsLobbyMenu() ? "Not in the lobby menu" : null;
        }

        public static void Register()
        {
            var tab = Registry.Tab("Enemies", "☠", "Kill, freeze, remove and spawn monsters. Host only: enemies run on the host's game.");

            tab.Panel("en.list", "Enemies on this level", "Every monster the level has spawned, with its health.", Scope.Host, ListBody).When(NeedLevel);
            tab.Action("en.killall", "Kill every enemy", "Deals each one its whole health in damage, so they die the normal way (and drop loot where the game does).", Scope.Host, "Kill all", () =>
            {
                int n = 0;

                foreach (var e in WorldCache.Enemies.ToList())
                {
                    if (Kill(e))
                    {
                        n++;
                    }
                }

                Toasts.Show($"Killed {n} enemies");
            }).When(NeedLevel);
            tab.Action("en.despawn", "Send every enemy away", "The game's /despawnenemies: they leave for a while and come back later.", Scope.Host, "Despawn", () =>
            {
                int n = 0;

                foreach (var e in WorldCache.Enemies.ToList())
                {
                    if (e && e.Spawned)
                    {
                        e.SpawnedTimerSet(0f);
                        e.DespawnedTimerSet(10f);
                        n++;
                    }
                }

                Toasts.Show($"Sent away {n} enemies");
            }).When(NeedLevel);
            tab.Action("en.destroy", "Remove every enemy", "The game's /noenemies: deletes them for the rest of the level.", Scope.Host, "Remove", () =>
            {
                int n = 0;

                foreach (var e in EnemyDirector.instance.enemiesSpawned.ToList())
                {
                    if (!e)
                    {
                        continue;
                    }

                    EnemyDirector.instance.enemiesSpawned.Remove(e);

                    if (Game.Multiplayer)
                    {
                        PhotonNetwork.Destroy(e.gameObject);
                    }
                    else
                    {
                        Object.Destroy(e.gameObject);
                    }

                    n++;
                }

                WorldCache.ForceRefresh();
                Toasts.Show($"Removed {n} enemies");
            }).When(NeedLevel);
            tab.Switch("en.freeze", "Freeze every enemy", "Stops all enemies where they stand until you turn it off.", Scope.Host, () => FreezeAll, v => FreezeAll = v);
            tab.Switch("en.onehit", "Enemies die in one hit", "Any damage an enemy takes kills it.", Scope.Host, () => OneHit, v => OneHit = v);
            tab.Panel("en.spawn", "Spawn an enemy", "Every enemy the game has, found at runtime. Spawns at the level point nearest you (where the game puts them), or where you look.", Scope.Host, SpawnBody).When(NeedLevel);
        }

        private static void ListBody()
        {
            var list = WorldCache.Enemies.Where(e => e).ToList();

            if (list.Count == 0)
            {
                GUILayout.Label("No enemies right now.", Theme.LabelSubtle);
                return;
            }

            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Height(Mathf.Min(220, 30 * list.Count + 10)));

            foreach (var e in list)
            {
                bool active = WorldCache.EnemyActive(e);
                var body = WorldCache.EnemyBody(e);
                string hp = e.Enemy && e.Enemy.HasHealth && e.Enemy.Health ? $"{e.Enemy.Health.healthCurrent}/{e.Enemy.Health.health} HP" : "";
                string where = active && body ? $"{Vector3.Distance(Game.LocalPosition, body.position):0} m" : "away";

                GUILayout.BeginHorizontal();
                GUILayout.Label(Esp.EnemyName(e), Theme.Label, GUILayout.Width(150));
                GUILayout.Label(hp, Theme.LabelSubtle, GUILayout.Width(90));
                GUILayout.Label(where, Theme.LabelSubtle, GUILayout.Width(60));

                var was = GUI.enabled;
                GUI.enabled = was && active;

                if (Ui.Small("Kill", 46))
                {
                    Kill(e);
                }

                if (Ui.Small("Freeze 15s", 80))
                {
                    e.Enemy.Freeze(15f);
                }

                if (Ui.Small("Go to", 52) && body)
                {
                    Teleport.To(body.position + Vector3.up * 1.5f - body.forward * 2f);
                }

                GUI.enabled = was;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        public static bool Kill(EnemyParent e)
        {
            if (!WorldCache.EnemyActive(e) || !e.Enemy.HasHealth || !e.Enemy.Health || e.Enemy.Health.dead)
            {
                return false;
            }

            // armour (some enemies resist damage for a while) would swallow the hit
            e.Enemy.Health.damageResistance = 0f;
            e.Enemy.Health.Hurt(Mathf.Max(1, e.Enemy.Health.healthCurrent) + 9999, Vector3.zero);
            return true;
        }

        private static void SpawnBody()
        {
            var clicked = spawnPicker.Draw(Catalog.Enemies, Catalog.EnemyName, 170);

            GUILayout.BeginHorizontal();
            spawnCount = Ui.IntSlider("How many", spawnCount, 1, 10);
            GUILayout.EndHorizontal();
            atAim = Ui.SwitchRow("Spawn where I look (instead of the nearest level point)", atAim);

            var was = GUI.enabled;
            GUI.enabled = was && spawnPicker.Selected;

            if (Ui.Button(spawnPicker.Selected ? "Spawn " + Catalog.EnemyName(spawnPicker.Selected) : "Pick an enemy above", 260, Theme.ButtonAccent) && spawnPicker.Selected)
            {
                for (int i = 0; i < spawnCount; i++)
                {
                    Spawn(spawnPicker.Selected);
                }

                WorldCache.ForceRefresh();
            }

            GUI.enabled = was;
        }

        // the game's own /spawn enemy steps
        public static void Spawn(EnemySetup setup)
        {
            Vector3 pos;

            if (atAim)
            {
                pos = Game.AimPoint(40f, 0.3f);
            }
            else
            {
                var lp = SemiFunc.LevelPointsGetClosestToLocalPlayer();

                if (!lp)
                {
                    Toasts.Show("No level point near you");
                    return;
                }

                pos = lp.transform.position;
            }

            foreach (var obj in setup.spawnObjects)
            {
                if (obj == null || !obj.IsValid())
                {
                    continue;
                }

                GameObject go = Game.Multiplayer
                    ? PhotonNetwork.InstantiateRoomObject(obj.ResourcePath, pos, Quaternion.identity, 0, EnemyFloaterHalloweenState.SpawnData(obj))
                    : Object.Instantiate(obj.Prefab, pos, Quaternion.identity);

                var parent = go ? go.GetComponent<EnemyParent>() : null;

                if (parent)
                {
                    parent.SetupDone = true;
                    var enemy = go.GetComponentInChildren<Enemy>();

                    if (enemy)
                    {
                        enemy.EnemyTeleported(pos);
                    }

                    parent.firstSpawnPointUsed = true;
                }
            }

            Toasts.Show("Spawned " + Catalog.EnemyName(setup));
        }

        public static void Tick()
        {
            if (!Game.InWorld || !Game.IsHost)
            {
                return;
            }

            if (FreezeAll && Time.unscaledTime >= freezeAt)
            {
                freezeAt = Time.unscaledTime + 0.5f;

                Safe.Run("en.freeze", () =>
                {
                    foreach (var e in WorldCache.Enemies)
                    {
                        if (WorldCache.EnemyActive(e))
                        {
                            e.Enemy.Freeze(1f);
                        }
                    }
                });
            }

            if (OneHit)
            {
                Safe.Run("en.onehit", () =>
                {
                    foreach (var e in WorldCache.Enemies)
                    {
                        if (WorldCache.EnemyActive(e) && e.Enemy.HasHealth && e.Enemy.Health && !e.Enemy.Health.dead && e.Enemy.Health.healthCurrent > 1)
                        {
                            e.Enemy.Health.healthCurrent = 1;
                        }
                    }
                });
            }
        }
    }
}
