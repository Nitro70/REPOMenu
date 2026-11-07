using System.Collections.Generic;
using REPOMenu.Core;
using REPOMenu.UI;
using UnityEngine;

namespace REPOMenu.Features
{
    // Things done to your own character. R.E.P.O. lets each player own their health, stamina and
    // movement (the game only accepts those from the owner), so all of this works as a guest too.
    internal static class SelfMods
    {
        public static bool God, NoDeath, Stamina, NoTumble, AutoHeal, Speed, Jump, ExtraJumps, Fly, Reach, Strength, Throw;

        private static bool godApplied, tumbleApplied, jumpsApplied;
        private static float healAt;

        private static readonly Scaled jumpForce = new(), grabRange = new(), throwStrength = new();

        public static float SpeedMult => Settings.Float("self.speed", 2f);
        public static float JumpMult => Settings.Float("self.jump", 1.8f);
        public static int JumpCount => Mathf.RoundToInt(Settings.Float("self.jumps", 3f));
        public static float FlySpeed => Settings.Float("self.fly", 9f);
        public static float ReachMult => Settings.Float("self.reach", 3f);
        public static float StrengthMult => Settings.Float("self.strength", 4f);
        public static float ThrowMult => Settings.Float("self.throw", 3f);

        private static string NeedWorld() => Game.InWorld ? null : "Load into a level, the shop or the truck first";

        public static void Register()
        {
            var self = Registry.Tab("Self", "♥", "Your own health and stamina. The game lets every player control these for themselves, so they work in any lobby.");

            self.Switch("self.god", "God mode", "You take no damage at all, from anything.", Scope.Client, () => God, v => God = v);
            self.Switch("self.nodeath", "Can't die", "You still get hurt, but the last hit always leaves you on 1 HP.", Scope.Client, () => NoDeath, v => NoDeath = v)
                .When(() => Patches.HurtPatched ? null : "The game's damage code changed; this one is off for now");
            self.Switch("self.autoheal", "Auto heal", "Tops your health back up to full twice a second.", Scope.Client, () => AutoHeal, v => AutoHeal = v);
            self.Action("self.heal", "Heal to full", "Fills your health bar once, the game's own way (others see the heal).", Scope.Client, "Heal", HealSelf).When(NeedWorld);
            self.Panel("self.maxhp", "Max health", "Sets your maximum health right now, without a new round. The game sends it to everyone. Goes back to normal next level.", Scope.Client, MaxHealthBody).When(NeedWorld);
            self.Switch("self.stamina", "Infinite stamina", "Sprint forever: your energy never drains.", Scope.Client, () => Stamina, v => Stamina = v);
            self.Switch("self.notumble", "No ragdoll", "Hits, explosions and enemies can't knock you over.", Scope.Client, () => NoTumble, v => NoTumble = v);
            self.Action("self.kill", "Kill yourself", "Dies on the spot (for a quick respawn as a head, or a laugh).", Scope.Client, "Die", () =>
            {
                if (Game.Local && !Game.Local.deadSet)
                {
                    Patches.AllowOwnDeath = true;

                    try
                    {
                        Game.Local.PlayerDeath(-1);
                    }
                    finally
                    {
                        Patches.AllowOwnDeath = false;
                    }
                }
            }).When(NeedWorld);

            var move = Registry.Tab("Movement", "»", "How you move. Your movement is yours in R.E.P.O., so all of this works as a guest too.");

            move.Switch("move.speed", "Speed", "Walk, sprint and crouch faster.", Scope.Client, () => Speed, v => Speed = v)
                .WithBody(() => Settings.SetFloat("self.speed", Ui.Slider("Multiplier", SpeedMult, 1f, 8f, "0.0", "x")));
            move.Switch("move.jump", "Jump height", "Jump higher.", Scope.Client, () => Jump, v => Jump = v)
                .WithBody(() => Settings.SetFloat("self.jump", Ui.Slider("Multiplier", JumpMult, 1f, 5f, "0.0", "x")));
            move.Switch("move.jumps", "Extra air jumps", "Jump again in mid-air, on top of any Extra Jump upgrades you own.", Scope.Client, () => ExtraJumps, v => ExtraJumps = v)
                .WithBody(() => Settings.SetFloat("self.jumps", Ui.IntSlider("Extra jumps", JumpCount, 1, 20)));
            move.Switch("move.fly", "Fly / noclip", "Fly through walls. WASD moves where you look, Jump goes up, Crouch goes down, Sprint is faster.", Scope.Client, () => Fly, v => Fly = v)
                .WithBody(() => Settings.SetFloat("self.fly", Ui.Slider("Fly speed", FlySpeed, 2f, 40f, "0")));
            move.Switch("move.reach", "Long reach", "Grab things from further away.", Scope.Client, () => Reach, v => Reach = v)
                .WithBody(() => Settings.SetFloat("self.reach", Ui.Slider("Multiplier", ReachMult, 1f, 10f, "0.0", "x")));
            move.Switch("move.strength", "Strong grab", "Hold heavy things easily. As a guest the host's game does the physics, so this is strongest when you host.", Scope.Client, () => Strength, v => Strength = v)
                .WithBody(() => Settings.SetFloat("self.strength", Ui.Slider("Multiplier", StrengthMult, 1f, 20f, "0.0", "x")));
            move.Switch("move.throw", "Strong throw", "Throw things harder.", Scope.Client, () => Throw, v => Throw = v)
                .WithBody(() => Settings.SetFloat("self.throw", Ui.Slider("Multiplier", ThrowMult, 1f, 10f, "0.0", "x")));
        }

        private static int maxHpValue = 300;

        private static void MaxHealthBody()
        {
            var h = Game.Local ? Game.Local.playerHealth : null;

            GUILayout.BeginHorizontal();
            GUILayout.Label(h ? $"Now: {h.health} / {h.maxHealth}" : "Now: -", Theme.LabelSubtle, GUILayout.Width(140));
            maxHpValue = Ui.IntSlider("New max", maxHpValue, 50, 2000);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            foreach (var preset in new[] { 100, 200, 500, 1000 })
            {
                if (Ui.Small(preset.ToString(), 60))
                {
                    maxHpValue = preset;
                }
            }

            GUILayout.FlexibleSpace();

            if (Ui.Button("Set and heal", 120, Theme.ButtonAccent))
            {
                SetMaxHealth(maxHpValue);
            }

            GUILayout.EndHorizontal();
        }

        public static void HealSelf()
        {
            var h = Game.Local ? Game.Local.playerHealth : null;

            if (h && h.health < h.maxHealth)
            {
                h.Heal(h.maxHealth - h.health);
            }
        }

        private static void SetMaxHealth(int value)
        {
            var h = Game.Local ? Game.Local.playerHealth : null;

            if (!h)
            {
                return;
            }

            h.maxHealth = Mathf.Max(1, value);

            if (h.health > h.maxHealth)
            {
                h.health = h.maxHealth;
            }

            // Heal sends our new maximum to everyone (UpdateHealthRPC carries it)
            if (h.health < h.maxHealth)
            {
                h.Heal(h.maxHealth - h.health, false);
            }
            else
            {
                h.Heal(1, false);
            }

            Toasts.Show($"Max health {h.maxHealth}");
        }

        public static void Tick()
        {
            var pc = PlayerController.instance;
            var av = PlayerAvatar.instance;

            if (!Game.InWorld || !pc || !av)
            {
                return;
            }

            Safe.Run("self.god", () =>
            {
                if (God)
                {
                    av.playerHealth.godMode = true;
                    godApplied = true;
                }
                else if (godApplied)
                {
                    av.playerHealth.godMode = false;
                    godApplied = false;
                }
            });

            if (Stamina)
            {
                Safe.Run("self.stamina", () => pc.EnergyCurrent = pc.EnergyStart);
            }

            Safe.Run("self.notumble", () =>
            {
                if (NoTumble)
                {
                    pc.DebugNoTumble = true;
                    tumbleApplied = true;
                }
                else if (tumbleApplied)
                {
                    pc.DebugNoTumble = false;
                    tumbleApplied = false;
                }
            });

            if (AutoHeal && Time.unscaledTime >= healAt)
            {
                healAt = Time.unscaledTime + 0.5f;
                Safe.Run("self.autoheal", () =>
                {
                    if (!av.deadSet)
                    {
                        HealSelf();
                    }
                });
            }

            if (Speed)
            {
                Safe.Run("move.speed", () => pc.OverrideSpeed(SpeedMult, 0.25f));
            }

            Safe.Run("move.jump", () => pc.JumpForce = jumpForce.Apply(pc, pc.JumpForce, Jump, JumpMult));

            Safe.Run("move.jumps", () =>
            {
                // the game sets JumpExtra from your upgrades; ours goes on top
                int owned = StatsManager.instance ? StatsManager.instance.playerUpgradeExtraJump.GetValueOrDefault(pc.playerSteamID ?? "", 0) : 0;

                if (ExtraJumps)
                {
                    pc.JumpExtra = owned + JumpCount;
                    jumpsApplied = true;
                }
                else if (jumpsApplied)
                {
                    pc.JumpExtra = owned;
                    jumpsApplied = false;
                }
            });

            var grabber = av.physGrabber;

            if (grabber)
            {
                Safe.Run("move.reach", () => grabber.grabRange = grabRange.Apply(grabber, grabber.grabRange, Reach, ReachMult));
                // the game's own temporary grab-strength override (used by items), refreshed each frame
                if (Strength)
                {
                    Safe.Run("move.strength", () => grabber.OverrideGrabStrength(Mathf.Max(grabber.grabStrength, 1f) * StrengthMult, 0.25f));
                }
                Safe.Run("move.throw", () => grabber.throwStrength = throwStrength.Apply(grabber, grabber.throwStrength, Throw, ThrowMult, 1f));
            }

            if (Fly)
            {
                Safe.Run("move.fly", () => FlyTick(pc));
            }
        }

        private static void FlyTick(PlayerController pc)
        {
            if (Game.Local.deadSet || MenuWindow.Open)
            {
                if (!Game.Local.deadSet)
                {
                    pc.Kinematic(0.2f);
                }

                return;
            }

            // a kinematic body skips the controller's physics entirely (its FixedUpdate returns early)
            pc.Kinematic(0.2f);

            var im = InputManager.instance;
            var cam = Game.Camera;

            if (!im || !cam)
            {
                return;
            }

            var look = cam.transform;
            var dir = look.forward * im.GetMovementY() + look.right * im.GetMovementX();

            if (im.KeyHold(InputKey.Jump))
            {
                dir += Vector3.up;
            }

            if (im.KeyHold(InputKey.Crouch))
            {
                dir += Vector3.down;
            }

            float speed = FlySpeed * (im.KeyHold(InputKey.Sprint) ? 2.5f : 1f);
            var pos = pc.transform.position + dir.normalized * speed * Time.deltaTime;
            pc.transform.position = pos;
            pc.rb.position = pos;
            pc.rb.linearVelocity = Vector3.zero;
        }

        // Applies a multiplier to a game value without losing the real one: if the game changes the
        // value while we are on (an upgrade, a new level), the change is kept and multiplied too.
        private sealed class Scaled
        {
            private Object owner;
            private float baseValue, lastSet;
            private bool active;

            public float Apply(Object who, float current, bool on, float mult, float minBase = 0f)
            {
                if (owner != who)
                {
                    owner = who;
                    baseValue = current;
                    lastSet = current;
                    active = false;
                }
                else if (!Mathf.Approximately(current, lastSet))
                {
                    // something else changed it: carry that change into the base
                    baseValue += current - lastSet;
                }

                if (!on && !active)
                {
                    baseValue = current;
                    lastSet = current;
                    return current;
                }

                active = on;
                float target = on ? Mathf.Max(baseValue, minBase) * mult : baseValue;
                lastSet = target;
                return target;
            }
        }

        public static IEnumerable<string> ActiveNames()
        {
            if (God) yield return "God";
            if (Fly) yield return "Fly";
            if (Speed) yield return "Speed";
        }
    }
}
