using System.Linq;
using REPOMenu.Core;
using REPOMenu.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace REPOMenu.Features
{
    // Binds, Settings and About: pages about the menu itself.
    internal static class MenuPages
    {
        public static void Register()
        {
            var binds = Registry.Tab("Binds", "⌨", "Keys you bound to cards. Right-click any card with a switch or a button to give it a key.");
            binds.Page = BindsPage;

            var settings = Registry.Tab("Settings", "⚙", "How the menu itself behaves.");
            settings.Page = SettingsPage;

            var about = Registry.Tab("About", "ⓘ", null);
            about.Page = AboutPage;
        }

        private static void BindsPage()
        {
            if (Settings.Binds.Count == 0)
            {
                GUILayout.Label("No binds yet. Right-click a card (for example God mode) and press a key.", Theme.Desc);
                return;
            }

            GUILayout.BeginVertical(Theme.Card);

            foreach (var bind in Settings.Binds.ToList().OrderBy(b => b.Key))
            {
                Registry.ById.TryGetValue(bind.Key, out var card);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{MenuWindow.KeyName(bind.Value)}</b>", Theme.Label, GUILayout.Width(110));
                GUILayout.Label(card != null ? $"{card.Title}  <color=#9FB2C6>({card.Tab.Name})</color>" : bind.Key + "  <color=#E08080>(card no longer exists)</color>", Theme.Label);
                GUILayout.FlexibleSpace();

                if (card != null && Ui.Small("Change", 70))
                {
                    MenuWindow.BindTarget = card;
                }

                if (Ui.Small("Remove", 70))
                {
                    Settings.SetBind(bind.Key, Key.None);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        private static void SettingsPage()
        {
            GUILayout.BeginVertical(Theme.Card);
            GUILayout.Label("Menu key", Theme.Title);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Opens and closes the menu: <b>{MenuWindow.KeyName(Settings.MenuKey.Value)}</b>", Theme.Label);
            GUILayout.FlexibleSpace();

            if (Ui.Button("Change", 90))
            {
                MenuWindow.BindingMenuKey = true;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(Theme.Card);
            GUILayout.Label("Window", Theme.Title);
            float scale = Ui.Slider("Size", Settings.UiScale.Value, 0.75f, 1.6f, "0.00", "x");

            if (!Mathf.Approximately(scale, Settings.UiScale.Value))
            {
                Settings.UiScale.Value = scale;
                Settings.MarkDirty();
            }

            GUILayout.BeginHorizontal();

            if (Ui.Button("Put the window back in the middle", 260))
            {
                MenuWindow.ResetRect();
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            bool toasts = Ui.SwitchRow("Show messages in the corner", Settings.ShowToasts.Value);

            if (toasts != Settings.ShowToasts.Value)
            {
                Settings.ShowToasts.Value = toasts;
                Settings.MarkDirty();
            }

            GUILayout.EndVertical();

            GUILayout.BeginVertical(Theme.Card);
            GUILayout.Label("Game lists", Theme.Title);
            GUILayout.Label($"Found in the game right now: {Catalog.Items.Count} items, {Catalog.Valuables.Count} valuables, {Catalog.Enemies.Count} enemies, {Catalog.Levels.Count} levels, {Catalog.Upgrades.Count} upgrades, {Catalog.Cosmetics.Count(c => c)} cosmetics, {Catalog.Colors.Count} colours.", Theme.Desc);

            if (Ui.Button("Look again now", 140))
            {
                Catalog.Invalidate();
                WorldCache.ForceRefresh();
            }

            GUILayout.EndVertical();

            if (Safe.Broken.Count > 0)
            {
                GUILayout.BeginVertical(Theme.Card);
                GUILayout.Label("Turned off after errors", Theme.Title);

                foreach (var b in Safe.Broken.ToList())
                {
                    GUILayout.Label($"<b>{b.Key}</b>: {b.Value}", Theme.Desc);
                }

                if (Ui.Button("Try them all again", 160))
                {
                    foreach (var id in Safe.Broken.Keys.ToList())
                    {
                        Safe.Reset(id);
                    }
                }

                GUILayout.EndVertical();
            }
        }

        private static void AboutPage()
        {
            GUILayout.BeginVertical(Theme.Card);
            GUILayout.Label($"<size=22><b>{Brand.Name}</b></size>", Theme.Label);
            GUILayout.Label($"by <b>{Brand.Author}</b>   ·   version {Brand.Version}", Theme.Label);
            GUILayout.Space(6);
            GUILayout.Label($"A mod menu for R.E.P.O. Game version here: <b>{Game.Version}</b> (written against {Brand.BuiltForGame}.x).", Theme.Desc);
            GUILayout.Space(6);
            GUILayout.Label("<b>CLIENT</b> cards change only you or your own screen. The game lets every player own their health, stamina, movement and view, so these work in any lobby.", Theme.Desc);
            GUILayout.Label("<b>HOST</b> cards change the world or other players. The game only accepts those from the host, so they lock when you join someone else. Friends don't need the mod.", Theme.Desc);
            GUILayout.Space(6);
            GUILayout.Label("Lists of items, valuables, enemies, levels, upgrades, cosmetics and colours are read from the game while it runs, never typed into the menu, so new updates and other mods' content show up by themselves. If a game update changes something a card uses, that card turns itself off and says so; the rest keeps working.", Theme.Desc);
            GUILayout.Space(6);
            GUILayout.Label($"Settings, favourites and binds: BepInEx\\config\\{Brand.Guid}.cfg. Switches always start off.", Theme.Desc);
            GUILayout.EndVertical();
        }
    }
}
