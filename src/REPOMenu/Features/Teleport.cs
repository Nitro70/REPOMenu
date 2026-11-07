using System.Linq;
using REPOMenu.Core;
using REPOMenu.UI;
using UnityEngine;

namespace REPOMenu.Features
{
    // Moving yourself. Your position is yours in R.E.P.O. (the game's own freecam moves you the same
    // way), so this works as a guest too. Moving other players is in the Players tab (host).
    internal static class Teleport
    {
        private static string NeedWorld() => Game.InWorld ? null : "Load into a level, the shop or the truck first";

        public static void Register()
        {
            var tab = Registry.Tab("Teleport", "➜", "Move yourself anywhere. Works in any lobby.");

            tab.Action("tp.aim", "To where you look", "Jumps to the spot your crosshair points at.", Scope.Client, "Go", () => To(Game.AimPoint())).When(NeedWorld);
            tab.Action("tp.truck", "To the truck", "Back to the truck, safe and sound.", Scope.Client, "Go", () =>
            {
                if (TruckSafetySpawnPoint.instance)
                {
                    To(TruckSafetySpawnPoint.instance.transform.position + Vector3.up * 0.5f);
                }
                else
                {
                    Toasts.Show("This level has no truck");
                }
            }).When(NeedWorld);
            tab.Action("tp.extract", "To the extraction point", "The active one, or else the nearest one.", Scope.Client, "Go", () =>
            {
                ExtractionPoint ep = RoundDirector.instance ? RoundDirector.instance.extractionPointCurrent : null;

                if (!ep)
                {
                    ep = WorldCache.Extractions.Where(e => e).OrderBy(e => Vector3.Distance(e.transform.position, Game.LocalPosition)).FirstOrDefault();
                }

                if (ep)
                {
                    To(ep.transform.position + Vector3.up * 1.2f + ep.transform.forward * 1.5f);
                }
                else
                {
                    Toasts.Show("No extraction point found");
                }
            }).When(NeedWorld);
            tab.Action("tp.valuable", "To the best valuable", "Goes to the most valuable thing on the level.", Scope.Client, "Go", () =>
            {
                var best = WorldCache.Valuables.Where(v => v && v.gameObject.activeInHierarchy).OrderByDescending(v => v.dollarValueCurrent).FirstOrDefault();

                if (best)
                {
                    To(best.transform.position + Vector3.up * 1.2f);
                    Toasts.Show("$" + Mathf.RoundToInt(best.dollarValueCurrent).ToString("N0"));
                }
            }).When(NeedWorld);
            tab.Panel("tp.players", "To a player", "Click a name to go to them (or to their head, if they died).", Scope.Client, PlayersBody).When(NeedWorld);
            tab.Panel("tp.saved", "Saved spots", "Save where you stand and come back later in the same level.", Scope.Client, SavedBody).When(NeedWorld);
        }

        private static void PlayersBody()
        {
            var others = Game.Players.Where(p => p && !p.isLocal).ToList();

            if (others.Count == 0)
            {
                GUILayout.Label("Nobody else here.", Theme.LabelSubtle);
                return;
            }

            int i = Ui.ButtonGrid(others.Select(p => Game.PlayerName(p) + (p.deadSet ? " (dead)" : "")).ToList(), -1, 150, 520);

            if (i >= 0)
            {
                To(PlayerSpot(others[i]));
            }
        }

        private static readonly Vector3?[] saved = new Vector3?[4];

        private static void SavedBody()
        {
            for (int i = 0; i < saved.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Spot {i + 1}", Theme.Label, GUILayout.Width(60));
                GUILayout.Label(saved[i].HasValue ? saved[i].Value.ToString("0") : "empty", Theme.LabelSubtle, GUILayout.Width(170));

                if (Ui.Small("Save here", 80))
                {
                    saved[i] = Game.LocalPosition + Vector3.up * 0.3f;
                    Toasts.Show($"Saved spot {i + 1}");
                }

                var was = GUI.enabled;
                GUI.enabled = was && saved[i].HasValue;

                if (Ui.Small("Go", 50) && saved[i].HasValue)
                {
                    To(saved[i].Value);
                }

                GUI.enabled = was;
                GUILayout.EndHorizontal();
            }
        }

        public static Vector3 PlayerSpot(PlayerAvatar p)
        {
            if (p.deadSet && p.playerDeathHead && p.playerDeathHead.physGrabObject)
            {
                return p.playerDeathHead.physGrabObject.centerPoint + Vector3.up * 0.8f;
            }

            return p.transform.position + Vector3.up * 0.3f;
        }

        // the same steps the game's own freecam uses to put you somewhere
        public static void To(Vector3 position)
        {
            var av = PlayerAvatar.instance;
            var pc = PlayerController.instance;

            if (!av || !pc)
            {
                return;
            }

            if (av.deadSet)
            {
                Toasts.Show("You're dead: a teleport needs a body");
                return;
            }

            if (av.isTumbling && av.tumble && av.tumble.physGrabObject)
            {
                av.tumble.physGrabObject.Teleport(position, av.transform.rotation);
                return;
            }

            pc.transform.position = position;

            if (pc.rb)
            {
                pc.rb.position = position;
                pc.rb.linearVelocity = Vector3.zero;
            }
        }
    }
}
