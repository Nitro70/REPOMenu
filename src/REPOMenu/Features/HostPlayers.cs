using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using REPOMenu.Core;
using REPOMenu.UI;
using UnityEngine;

namespace REPOMenu.Features
{
    // Doing things to other players. The game only accepts these from the host (its RPC checks), so
    // the cards lock themselves when you are a guest. Friends need no mod: everything here goes
    // through the game's own messages.
    internal static class HostPlayers
    {
        public static bool HealEveryone, NoAggroEveryone;

        private static string selectedId;
        private static float healAt, aggroAt;
        private static readonly HashSet<string> addedNoVision = new();

        private static string NeedWorld() => Game.InWorld ? null : "Load into a level first";

        public static void Register()
        {
            var tab = Registry.Tab("Players", "☺", "Heal, revive, move and upgrade anyone in your lobby. Host only; your friends don't need the mod.");

            tab.Panel("pl.pick", "Player", "Pick who the cards below act on.", Scope.Host, PickBody);
            tab.Panel("pl.actions", "Actions", "Heal, revive, kill or move the player you picked.", Scope.Host, ActionsBody).When(NeedWorld);
            tab.Panel("pl.upgrades", "Upgrades", "Every upgrade the game has (read from the save), for the player you picked. Applies at once, like buying one, and is saved with the run.", Scope.Host, UpgradesBody).When(NeedWorld);
            tab.Panel("pl.all", "Everyone at once", "The same actions for the whole lobby.", Scope.Host, EveryoneBody).When(NeedWorld);
            tab.Switch("pl.healall", "Keep everyone healed", "Heals every living player to full once a second.", Scope.Host, () => HealEveryone, v => HealEveryone = v);
            tab.Switch("pl.noaggro", "Enemies ignore everyone", "Enemies stop seeing any player (the game's own no-aggro switch, run on the host where enemies think).", Scope.Host, () => NoAggroEveryone, SetNoAggroEveryone);
        }

        public static PlayerAvatar Selected
        {
            get
            {
                var list = Game.Players;
                var p = list.FirstOrDefault(x => x && x.steamID == selectedId);
                return p ? p : Game.Local;
            }
        }

        private static void PickBody()
        {
            var players = Game.Players.Where(p => p).ToList();

            if (players.Count == 0)
            {
                GUILayout.Label("No players yet.", Theme.LabelSubtle);
                return;
            }

            var sel = Selected;
            var names = players.Select(p => Game.PlayerName(p) + (p.isLocal ? " (you)" : "") + (p.deadSet ? " ☠" : "")).ToList();
            int i = Ui.ButtonGrid(names, players.IndexOf(sel), 160, 600);

            if (i >= 0)
            {
                selectedId = players[i].steamID;
            }

            if (sel && sel.playerHealth)
            {
                GUILayout.Label($"{Game.PlayerName(sel)}: {sel.playerHealth.health}/{sel.playerHealth.maxHealth} HP{(sel.deadSet ? ", dead" : "")}", Theme.LabelSubtle);
            }
        }

        private static void ActionsBody()
        {
            var p = Selected;

            if (!p)
            {
                return;
            }

            GUILayout.BeginHorizontal();

            if (Ui.Button("Heal", 80))
            {
                Heal(p);
            }

            if (Ui.Button("Revive", 80))
            {
                Revive(p);
            }

            if (Ui.Button("Kill", 70, Theme.ButtonDanger))
            {
                if (!p.deadSet)
                {
                    p.PlayerDeath(-1);
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (Ui.Button("Bring to me", 110))
            {
                Move(p, Game.LocalPosition + Game.Local.transform.forward * 1.2f + Vector3.up * 0.3f);
            }

            if (Ui.Button("Go to them", 110))
            {
                Teleport.To(Teleport.PlayerSpot(p));
            }

            if (Ui.Button("To the truck", 110))
            {
                if (TruckSafetySpawnPoint.instance)
                {
                    Move(p, TruckSafetySpawnPoint.instance.transform.position + Vector3.up * 0.5f);
                }
            }

            if (Ui.Button("To where I look", 140))
            {
                Move(p, Game.AimPoint());
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            bool ignored = EnemyDirector.instance && EnemyDirector.instance.debugNoVision.Contains(p.steamID);
            bool now = Ui.SwitchRow("Enemies ignore this player", ignored);

            if (now != ignored)
            {
                SetNoVision(p.steamID, now);
            }
        }

        private static void UpgradesBody()
        {
            var p = Selected;

            if (!p)
            {
                return;
            }

            var ups = Catalog.Upgrades;

            if (ups.Count == 0)
            {
                GUILayout.Label("The game has not loaded its upgrade list yet.", Theme.LabelSubtle);
                return;
            }

            foreach (var up in ups)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Pretty(up), Theme.Label, GUILayout.Width(170));
                GUILayout.Label(Catalog.UpgradeLevel(up, p.steamID).ToString(), Theme.LabelSubtle, GUILayout.Width(40));

                foreach (var step in new[] { -1, 1, 5, 10 })
                {
                    if (Ui.Small(step > 0 ? "+" + step : step.ToString(), 44))
                    {
                        Upgrade(p, up, step);
                    }
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
        }

        private static void EveryoneBody()
        {
            GUILayout.BeginHorizontal();

            if (Ui.Button("Heal all", 90))
            {
                ForAll(Heal);
            }

            if (Ui.Button("Revive all", 100))
            {
                ForAll(Revive);
            }

            if (Ui.Button("Bring all to me", 130))
            {
                var at = Game.LocalPosition;
                int i = 0;

                ForAll(p =>
                {
                    if (!p.isLocal)
                    {
                        float a = i++ * 1.1f;
                        Move(p, at + new Vector3(Mathf.Cos(a), 0.3f, Mathf.Sin(a)) * 1.5f);
                    }
                });
            }

            if (Ui.Button("All to the truck", 130) && TruckSafetySpawnPoint.instance)
            {
                var at = TruckSafetySpawnPoint.instance.transform.position + Vector3.up * 0.5f;
                ForAll(p => Move(p, at));
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Every upgrade, for everyone:", Theme.Label);

            foreach (var step in new[] { 1, 5, 10 })
            {
                if (Ui.Small("+" + step, 44))
                {
                    foreach (var up in Catalog.Upgrades)
                    {
                        ForAll(p => Upgrade(p, up, step));
                    }

                    Toasts.Show($"+{step} on every upgrade for everyone");
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static void ForAll(System.Action<PlayerAvatar> act)
        {
            foreach (var p in Game.Players.ToList())
            {
                if (p)
                {
                    Safe.Run("pl.all", () => act(p));
                }
            }
        }

        public static void Heal(PlayerAvatar p)
        {
            var h = p.playerHealth;

            if (h && !p.deadSet && h.health < h.maxHealth)
            {
                h.HealOther(h.maxHealth - h.health, true);
            }
        }

        // the game's own /revive steps
        public static void Revive(PlayerAvatar p)
        {
            if (!p.deadSet || !p.playerDeathHead)
            {
                return;
            }

            p.playerDeathHead.inExtractionPoint = true;
            p.playerDeathHead.Revive();
        }

        public static void Move(PlayerAvatar p, Vector3 pos)
        {
            if (p.isLocal)
            {
                Teleport.To(pos);
                return;
            }

            if (p.deadSet && p.playerDeathHead && p.playerDeathHead.physGrabObject)
            {
                p.playerDeathHead.physGrabObject.Teleport(pos, Quaternion.identity);
                return;
            }

            if (p.isTumbling && p.tumble && p.tumble.physGrabObject)
            {
                p.tumble.physGrabObject.Teleport(pos, p.transform.rotation);
                return;
            }

            // SpawnRPC: the game's host-only "put this player here" message
            p.Spawn(pos, p.transform.rotation);
        }

        // the game's tester upgrade message: the host sends it, every game applies it its own way
        public static void Upgrade(PlayerAvatar p, string upgrade, int amount)
        {
            int now = Catalog.UpgradeLevel(upgrade, p.steamID);
            int delta = Mathf.Max(0, now + amount) - now;

            if (delta == 0 || !PunManager.instance)
            {
                return;
            }

            if (Game.Multiplayer)
            {
                PunManager.instance.photonView.RPC("TesterUpgradeCommandRPC", RpcTarget.All, p.steamID, upgrade, delta);
            }
            else
            {
                PunManager.instance.TesterUpgradeCommandRPC(p.steamID, upgrade, delta);
            }
        }

        private static string Pretty(string s)
        {
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1]))
                {
                    sb.Append(' ');
                }

                sb.Append(s[i]);
            }

            return sb.ToString();
        }

        public static void SetNoVision(string steamId, bool on)
        {
            if (string.IsNullOrEmpty(steamId))
            {
                return;
            }

            // the same two lists the game's own /noaggro message edits, on this (the host's) game,
            // where enemies do their thinking. Called directly: a local call of the RPC method would
            // fail its own "sent by the host" check in multiplayer.
            if (EnemyDirector.instance)
            {
                var list = EnemyDirector.instance.debugNoVision;

                if (on && !list.Contains(steamId))
                {
                    list.Add(steamId);
                }
                else if (!on)
                {
                    list.Remove(steamId);
                }
            }

            if (DebugCommandHandler.instance)
            {
                var list = DebugCommandHandler.instance.enemyNoVision;

                if (on && !list.Contains(steamId))
                {
                    list.Add(steamId);
                }
                else if (!on)
                {
                    list.Remove(steamId);
                }
            }
        }

        private static void SetNoAggroEveryone(bool on)
        {
            NoAggroEveryone = on;

            if (!on)
            {
                foreach (var id in addedNoVision.ToList())
                {
                    SetNoVision(id, false);
                }

                addedNoVision.Clear();
            }
        }

        public static void Tick()
        {
            if (!Game.InWorld || !Game.IsHost)
            {
                return;
            }

            if (HealEveryone && Time.unscaledTime >= healAt)
            {
                healAt = Time.unscaledTime + 1f;
                ForAll(Heal);
            }

            if (NoAggroEveryone && Time.unscaledTime >= aggroAt)
            {
                aggroAt = Time.unscaledTime + 1f;

                Safe.Run("pl.noaggro", () =>
                {
                    foreach (var p in Game.Players)
                    {
                        if (p && EnemyDirector.instance && !EnemyDirector.instance.debugNoVision.Contains(p.steamID))
                        {
                            SetNoVision(p.steamID, true);
                            addedNoVision.Add(p.steamID);
                        }
                    }
                });
            }
        }
    }
}
