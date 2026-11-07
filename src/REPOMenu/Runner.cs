using REPOMenu.Core;
using REPOMenu.Features;
using REPOMenu.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace REPOMenu
{
    // The one MonoBehaviour: keys, feature ticks, input blocking and drawing.
    internal sealed class Runner : MonoBehaviour
    {
        private bool typingInMenu;

        private void Update()
        {
            Settings.Tick();
            WorldCache.Tick();
            Safe.Run("keys", Keys);
            Hub.Tick();
        }

        private void LateUpdate()
        {
            InputBlock.Tick(MenuWindow.Open);
            Hub.LateTick();
        }

        private void Keys()
        {
            var kb = Keyboard.current;

            if (kb == null)
            {
                return;
            }

            if (MenuWindow.BindTarget != null || MenuWindow.BindingMenuKey)
            {
                MenuWindow.CaptureBind();
                return;
            }

            // the game's own chat or name boxes have the keyboard: keys are text, not commands
            bool gameTyping = Game.Ready && !Safe.Get("game.notext", SemiFunc.NoTextInputsActive, true);

            if (gameTyping || (MenuWindow.Open && typingInMenu))
            {
                return;
            }

            var menuKey = kb[Settings.MenuKey.Value];

            if (menuKey != null && menuKey.wasPressedThisFrame)
            {
                MenuWindow.Open = !MenuWindow.Open;

                if (!MenuWindow.Open)
                {
                    Settings.SaveNow();
                }

                return;
            }

            foreach (var bind in Settings.Binds)
            {
                var control = kb[bind.Value];

                if (control != null && control.wasPressedThisFrame && Registry.ById.TryGetValue(bind.Key, out var card))
                {
                    card.Trigger();
                    break;
                }
            }
        }

        private void OnGUI()
        {
            if (!Theme.Ensure())
            {
                return;
            }

            GUI.depth = -1000;
            var oldSkin = GUI.skin;
            GUI.skin = Theme.Skin;

            Safe.Run("esp.draw", Esp.Draw);

            if (MenuWindow.Open)
            {
                MenuWindow.Draw();
                typingInMenu = GUIUtility.keyboardControl != 0;
            }
            else
            {
                typingInMenu = false;
            }

            Toasts.Draw();
            GUI.skin = oldSkin;
        }

        private void OnApplicationQuit()
        {
            Settings.SaveNow();
        }
    }
}
