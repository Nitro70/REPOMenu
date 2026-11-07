using System.Collections.Generic;
using REPOMenu.Core;
using UnityEngine;

namespace REPOMenu.UI
{
    // While the menu is open: free the mouse and keep the game from reacting to clicks, mouse look,
    // movement and keys. Uses the game's own short "disable" timers, refreshed every frame, so the
    // moment the menu closes they run out by themselves and nothing has to be put back.
    internal static class InputBlock
    {
        private static readonly List<InputKey> noKeys = new();
        private static bool wasOpen;

        public static void Tick(bool open)
        {
            if (open)
            {
                Safe.Run("input.block", () =>
                {
                    var im = InputManager.instance;

                    if (im)
                    {
                        im.disableMovementTimer = 0.2f;
                        im.disableAimingTimer = 0.2f;
                        im.DisableControlsExcept(0.2f, noKeys);
                    }

                    if (PlayerController.instance)
                    {
                        PlayerController.instance.InputDisable(0.2f);
                    }

                    // the game draws its own cursor mesh; ours is the OS cursor while the menu is up
                    if (MenuCursor.instance)
                    {
                        MenuCursor.instance.OverrideHide(0.2f);
                    }

                    if (PlayerAvatar.instance && PlayerAvatar.instance.physGrabber)
                    {
                        PlayerAvatar.instance.physGrabber.OverrideGrabDisable(0.2f);
                    }
                });

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                wasOpen = true;
            }
            else if (wasOpen)
            {
                wasOpen = false;

                // hand the cursor back: the game re-locks it in gameplay and keeps it free in its menus
                Safe.Run("input.release", () =>
                {
                    Cursor.visible = false;

                    if (CursorManager.instance)
                    {
                        CursorManager.instance.unlockTimer = 0f;
                    }
                });
            }
        }
    }
}
