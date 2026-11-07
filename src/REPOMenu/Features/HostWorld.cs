using System.Linq;
using Photon.Pun;
using REPOMenu.Core;
using REPOMenu.UI;
using UnityEngine;

namespace REPOMenu.Features
{
    // The run itself: money, extraction goals, valuables, batteries, which level you are on.
    // Host only (the game keeps the run on the host), except the battery switch.
    internal static class HostWorld
    {
        public static bool Unbreakable, InfiniteBattery, LowHaul;

        private static int moneyValue = 100;
        private static int goalValue = 1000;
        private static int setAllValue = 5000;
        private static int levelNumber = 5;
        private static float unbreakAt;
        private static bool lowHaulApplied, batteryApplied;
        private static readonly Ui.Picker<Level> levelPicker = new();

        private static string NeedWorld() => Game.InWorld ? null : "Load into a level, the shop or the truck first";

        private static string NeedRunLevel() => Game.InRunLevel ? null : "Only on a level (not the shop or truck)";

        private static string NeedRun()
        {
            if (!Game.InWorld)
            {
                return "Start or join a run first";
            }

            return null;
        }

        public static void Register()
        {
            var tab = Registry.Tab("World", "⚑", "Money, extraction goals, valuables and levels. Host only: the run lives on the host's game.");

            tab.Panel("w.money", "Money", "The run's cash, the number you spend in the shop. Shown in thousands like the game does.", Scope.Host, MoneyBody).When(NeedRun);
            tab.Panel("w.goal", "Extraction", "Unlock every point, finish the active one now, or change how much the active one wants.", Scope.Host, GoalBody).When(NeedRunLevel);
            tab.Switch("w.lowhaul", "Low extraction goals", "The game's own low-goal switch: the next level you load asks for very little.", Scope.Host, () => LowHaul, v => LowHaul = v);
            tab.Panel("w.values", "Valuable prices", "Change what every valuable on the level is worth. Everyone sees the new prices.", Scope.Host, ValuesBody).When(NeedRunLevel);
            tab.Switch("w.unbreak", "Unbreakable valuables", "Drops and crashes no longer damage or break valuables.", Scope.Host, () => Unbreakable, v => Unbreakable = v);
            tab.Switch("w.battery", "Infinite batteries", "Guns, drones and tools never run out (the game's own developer switch). Strongest when you host.", Scope.Client, () => InfiniteBattery, v => InfiniteBattery = v);
            tab.Panel("w.level", "Change level", "Skip, restart, go to the shop or the truck, or pick any level the game has. Uses the game's own developer level commands.", Scope.Host, LevelBody).When(NeedWorld);
        }

        // ---------------------------------------------------------------- money

        private static void MoneyBody()
        {
            int now = SemiFunc.StatGetRunCurrency();
            GUILayout.Label($"Now: ${now}K", Theme.LabelSubtle);

            GUILayout.BeginHorizontal();
            moneyValue = Ui.IntField("Set to ($K)", moneyValue, 90);

            if (Ui.Button("Set", 60, Theme.ButtonAccent))
            {
                SetMoney(moneyValue);
            }

            GUILayout.FlexibleSpace();

            foreach (var add in new[] { 10, 50, 100, 1000 })
            {
                if (Ui.Small("+" + add, 56))
                {
                    SetMoney(now + add);
                }
            }

            GUILayout.EndHorizontal();
        }

        public static void SetMoney(int value)
        {
            value = Mathf.Clamp(value, 0, 9999999);
            SemiFunc.StatSetRunCurrency(value);

            if (RoundDirector.instance && RoundDirector.instance.extractionPointCurrent)
            {
                RoundDirector.instance.extractionPointCurrent.UpdateHaulTextShop();
            }

            if (CurrencyUI.instance)
            {
                CurrencyUI.instance.FetchCurrency();
            }

            Toasts.Show($"Money: ${value}K");
        }

        // ---------------------------------------------------------------- extraction goal

        private static void GoalBody()
        {
            var ep = RoundDirector.instance ? RoundDirector.instance.extractionPointCurrent : null;

            GUILayout.BeginHorizontal();

            if (Ui.Button("Unlock every extraction point", 220))
            {
                // the game's own call that opens all points once the first is done
                RoundDirector.instance.ExtractionPointsUnlock();
                Toasts.Show("Extraction points unlocked");
            }

            var was = GUI.enabled;
            GUI.enabled = was && ep && ep.currentState == ExtractionPoint.State.Active;

            if (Ui.Button("Extract the active one now", 210, Theme.ButtonAccent) && ep)
            {
                // what the game does when a point is finished early: mark it done, start extracting
                ep.isCompletedRightAway = true;
                ep.StateSet(ExtractionPoint.State.Extracting);
                Toasts.Show("Extracting");
            }

            GUI.enabled = was;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (!ep)
            {
                GUILayout.Label("No extraction point is active yet.", Theme.LabelSubtle);
                return;
            }

            GUILayout.Label($"Active goal: ${ep.haulGoal:N0}   ·   in it now: ${ep.haulCurrent:N0}   ·   state: {ep.currentState}", Theme.LabelSubtle);
            GUILayout.BeginHorizontal();
            goalValue = Ui.IntField("New goal ($)", goalValue, 100);

            if (Ui.Button("Set goal", 90, Theme.ButtonAccent))
            {
                ep.HaulGoalSet(Mathf.Max(0, goalValue));
                Toasts.Show($"Extraction goal ${goalValue:N0}");
            }

            if (Ui.Button("Goal = what's in it", 150))
            {
                ep.HaulGoalSet(Mathf.Max(0, ep.haulCurrent));
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- valuables

        private static void ValuesBody()
        {
            var list = WorldCache.Valuables.Where(v => v).ToList();
            float total = list.Sum(v => v.dollarValueCurrent);
            GUILayout.Label($"{list.Count} valuables, worth ${total:N0} together", Theme.LabelSubtle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Multiply every one:", Theme.Label);

            foreach (var mult in new[] { 2, 5, 10 })
            {
                if (Ui.Small("x" + mult, 50))
                {
                    foreach (var v in list)
                    {
                        SetValue(v, v.dollarValueCurrent * mult);
                    }

                    Toasts.Show($"Every valuable x{mult}");
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (Ui.Button("Bring every valuable to me", 210))
            {
                var at = Game.AimPoint(4f, 0.6f);
                int i = 0;

                foreach (var v in list)
                {
                    if (v.physGrabObject)
                    {
                        // a loose pile above the spot, so they do not spawn inside each other
                        float a = i * 2.39996f;
                        float r = 0.35f * Mathf.Sqrt(i);
                        v.physGrabObject.Teleport(at + new Vector3(Mathf.Cos(a) * r, 0.5f + (i % 6) * 0.35f, Mathf.Sin(a) * r), Quaternion.identity);
                        i++;
                    }
                }

                Toasts.Show($"Brought {i} valuables");
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            setAllValue = Ui.IntField("Set every one to ($)", setAllValue, 100);

            if (Ui.Button("Set", 60, Theme.ButtonAccent))
            {
                foreach (var v in list)
                {
                    SetValue(v, setAllValue);
                }

                Toasts.Show($"Every valuable ${setAllValue:N0}");
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        // the game's host-only "this valuable is worth X" message
        public static void SetValue(ValuableObject v, float value)
        {
            value = Mathf.Clamp(Mathf.Round(value), 1f, 10000000f);

            if (Game.Multiplayer && v.photonView)
            {
                v.photonView.RPC("DollarValueSetRPC", RpcTarget.All, value);
            }
            else
            {
                v.DollarValueSetRPC(value);
            }
        }

        // ---------------------------------------------------------------- levels

        private static void LevelBody()
        {
            var rm = RunManager.instance;
            GUILayout.Label($"Now: {Catalog.LevelName(rm.levelCurrent)}   ·   levels completed: {rm.levelsCompleted}", Theme.LabelSubtle);

            GUILayout.BeginHorizontal();

            if (Ui.Button("Next level", 100, Theme.ButtonAccent))
            {
                rm.ChangeLevel(true, SemiFunc.RunIsArena());
            }

            if (Ui.Button("Restart this level", 140))
            {
                ResetProgress();
                rm.RestartScene();

                if (!SemiFunc.RunIsTutorial())
                {
                    SemiFunc.OnSceneSwitch(rm.gameOver, false);
                }
            }

            if (Ui.Button("Shop", 70))
            {
                if (!SemiFunc.RunIsLevel())
                {
                    ResetProgress();
                }

                rm.ChangeLevel(false, false, RunManager.ChangeLevelType.Shop);
            }

            if (Ui.Button("Truck lobby", 100))
            {
                ResetProgress();
                rm.debugLevel = rm.levelLobby;
                rm.ChangeLevel(false, false);
                rm.debugLevel = null;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            levelPicker.Draw(Catalog.PlayableLevels, Catalog.LevelName, 130);

            GUILayout.BeginHorizontal();
            var was = GUI.enabled;
            GUI.enabled = was && levelPicker.Selected;

            if (Ui.Button(levelPicker.Selected ? "Go to " + Catalog.LevelName(levelPicker.Selected) : "Pick a level above", 280, Theme.ButtonAccent) && levelPicker.Selected)
            {
                ResetProgress();
                rm.debugLevel = levelPicker.Selected;
                rm.ChangeLevel(false, false);
                rm.debugLevel = null;
            }

            GUI.enabled = was;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            levelNumber = Ui.IntField("Level number", levelNumber, 70);

            if (Ui.Button("Set and reload", 130))
            {
                // the game's own /levelnum steps
                rm.levelsCompleted = Mathf.Max(1, levelNumber) - 1;
                SemiFunc.StatSetRunLevel(rm.levelsCompleted);

                if (SemiFunc.RunIsShop())
                {
                    rm.ChangeLevel(false, false, RunManager.ChangeLevelType.Shop);
                }
                else if (SemiFunc.RunIsLevel())
                {
                    rm.ChangeLevel(false, false);
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        // what the game's level commands do before they jump: reload the run from its save so a
        // half-played level does not leave the save in a strange state
        private static void ResetProgress()
        {
            if (DebugCommandHandler.instance)
            {
                DebugCommandHandler.instance.ResetLevelProgress();
            }
        }

        // ---------------------------------------------------------------- ticks

        public static void Tick()
        {
            Safe.Run("w.battery", () =>
            {
                if (!RoundDirector.instance)
                {
                    return;
                }

                if (InfiniteBattery)
                {
                    RoundDirector.instance.debugInfiniteBattery = true;
                    batteryApplied = true;
                }
                else if (batteryApplied)
                {
                    RoundDirector.instance.debugInfiniteBattery = false;
                    batteryApplied = false;
                }
            });

            if (!Game.IsHost)
            {
                return;
            }

            Safe.Run("w.lowhaul", () =>
            {
                if (!RoundDirector.instance)
                {
                    return;
                }

                if (LowHaul)
                {
                    RoundDirector.instance.debugLowHaul = true;
                    lowHaulApplied = true;
                }
                else if (lowHaulApplied)
                {
                    RoundDirector.instance.debugLowHaul = false;
                    lowHaulApplied = false;
                }
            });

            if (Unbreakable && Game.InWorld && Time.unscaledTime >= unbreakAt)
            {
                unbreakAt = Time.unscaledTime + 0.2f;

                Safe.Run("w.unbreak", () =>
                {
                    foreach (var v in WorldCache.Valuables)
                    {
                        if (v && v.physGrabObject)
                        {
                            v.physGrabObject.OverrideIndestructible(0.5f);
                        }
                    }
                });
            }
        }
    }
}
