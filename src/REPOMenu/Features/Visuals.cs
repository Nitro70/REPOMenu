using REPOMenu.Core;
using REPOMenu.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace REPOMenu.Features
{
    // What your own screen shows. Nobody else is affected, so all of it works in any lobby.
    internal static class Visuals
    {
        public static bool Bright, NoFog, Fov, Freecam;

        private static bool brightApplied, fogApplied;
        private static AmbientMode oldMode;
        private static Color oldAmbient;
        private static float oldIntensity;
        private static bool oldFog;

        private static CameraZoom fovOwner;
        private static float fovOriginal;

        private static float Brightness => Settings.Float("vis.bright", 0.7f);
        private static float FovValue => Settings.Float("vis.fov", 90f);
        private static float FreecamSpeed => Settings.Float("vis.freecam", 12f);

        public static void Register()
        {
            var tab = Registry.Tab("Visuals", "☼", "Change what your own screen shows. Only you see any of this.");

            tab.Switch("vis.bright", "Fullbright", "Lights up the dark levels so you can see everything.", Scope.Client, () => Bright, v => Bright = v)
                .WithBody(() => Settings.SetFloat("vis.bright", Ui.Slider("Brightness", Brightness, 0.2f, 1.5f, "0.00")));
            tab.Switch("vis.nofog", "No fog", "Removes the fog so you can see far.", Scope.Client, () => NoFog, v => NoFog = v);
            tab.Switch("vis.fov", "Field of view", "Wider (or narrower) view. Sprinting still zooms a little, like normal.", Scope.Client, () => Fov, v => Fov = v)
                .WithBody(() => Settings.SetFloat("vis.fov", Ui.Slider("FOV", FovValue, 40f, 140f, "0")));
            tab.Switch("vis.freecam", "Free camera", "The game's own developer camera: fly the view around with WASD, Jump and Crouch, scroll for speed. Press the Map key to move yourself to the camera.", Scope.Client, () => Freecam, SetFreecam)
                .When(() => Game.InWorld ? null : "Load into a level first")
                .WithBody(() => Settings.SetFloat("vis.freecam", Ui.Slider("Start speed", FreecamSpeed, 1f, 60f, "0")));
            tab.Action("vis.hud", "Hide the HUD", "The game's cinematic mode: hides the HUD for clean screenshots and clips. Press again to bring it back.", Scope.Client, "Toggle", () =>
            {
                if (GameDirector.instance)
                {
                    GameDirector.instance.CommandRecordingDirectorToggle();
                }
            }).When(() => Game.InWorld ? null : "Load into a level first");
        }

        private static void SetFreecam(bool on)
        {
            var d = DebugCommandHandler.instance;

            if (!d)
            {
                Toasts.Show("The game's debug camera is not loaded yet");
                Freecam = false;
                return;
            }

            if (on && Game.Camera)
            {
                d.freecamPosition = Game.Camera.transform.position;
                d.freecamSmoothedPosition = d.freecamPosition;
                d.freecamVelocity = Vector3.zero;
                d.freecamSpeed = FreecamSpeed;
            }

            d.debugFreeCam = on;
            Freecam = on;
        }

        public static void LateTick()
        {
            Safe.Run("vis.bright", () =>
            {
                if (Bright && Game.InWorld)
                {
                    if (!brightApplied)
                    {
                        oldMode = RenderSettings.ambientMode;
                        oldAmbient = RenderSettings.ambientLight;
                        oldIntensity = RenderSettings.ambientIntensity;
                        brightApplied = true;
                    }

                    float b = Brightness;
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = new Color(b, b, b, 1f);
                    RenderSettings.ambientIntensity = 1f + b;
                }
                else if (brightApplied)
                {
                    RenderSettings.ambientMode = oldMode;
                    RenderSettings.ambientLight = oldAmbient;
                    RenderSettings.ambientIntensity = oldIntensity;
                    brightApplied = false;
                }
            });

            Safe.Run("vis.nofog", () =>
            {
                if (NoFog && Game.InWorld)
                {
                    if (!fogApplied)
                    {
                        oldFog = RenderSettings.fog;
                        fogApplied = true;
                    }

                    RenderSettings.fog = false;
                }
                else if (fogApplied)
                {
                    RenderSettings.fog = oldFog;
                    fogApplied = false;
                }
            });

            Safe.Run("vis.fov", () =>
            {
                var zoom = CameraZoom.Instance;

                if (!zoom)
                {
                    fovOwner = null;
                    return;
                }

                if (fovOwner != zoom)
                {
                    fovOwner = zoom;
                    fovOriginal = zoom.playerZoomDefault;
                }

                zoom.playerZoomDefault = Fov ? FovValue : fovOriginal;
            });

            // the game turns its freecam off when a level loads; keep our switch honest
            Safe.Run("vis.freecam", () =>
            {
                if (Freecam && DebugCommandHandler.instance && !DebugCommandHandler.instance.debugFreeCam)
                {
                    Freecam = false;
                }
            });
        }
    }
}
