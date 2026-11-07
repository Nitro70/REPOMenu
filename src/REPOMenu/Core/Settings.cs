using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace REPOMenu.Core
{
    // Everything the menu remembers between launches, in BepInEx\config\com.nitro70.repomenu.cfg.
    // Cheats always start switched off; only settings, slider values, favourites and binds persist.
    internal static class Settings
    {
        private static ConfigFile cfg;
        private static bool dirty;
        private static float saveAt;

        public static ConfigEntry<Key> MenuKey;
        public static ConfigEntry<float> UiScale;
        public static ConfigEntry<string> Window;
        public static ConfigEntry<string> FavouriteList;
        public static ConfigEntry<string> BindList;
        public static ConfigEntry<bool> ShowToasts;

        public static readonly HashSet<string> Favourites = new();
        public static readonly Dictionary<string, Key> Binds = new();

        private static readonly Dictionary<string, ConfigEntry<float>> floats = new();
        private static readonly Dictionary<string, ConfigEntry<bool>> bools = new();
        private static readonly Dictionary<string, ConfigEntry<string>> strings = new();

        public static void Init(ConfigFile config)
        {
            cfg = config;
            cfg.SaveOnConfigSet = false;

            MenuKey = cfg.Bind("Menu", "OpenKey", Key.F2, "Key that opens and closes the menu.");
            UiScale = cfg.Bind("Menu", "Scale", 1f, "Size of the menu window (0.75 to 1.6).");
            Window = cfg.Bind("Menu", "Window", "", "Last window position and size.");
            ShowToasts = cfg.Bind("Menu", "Toasts", true, "Show short messages in the corner when something happens.");
            FavouriteList = cfg.Bind("Menu", "Favourites", "", "Cards marked with the star.");
            BindList = cfg.Bind("Menu", "Binds", "", "Keys bound to cards (card=Key;...).");

            foreach (var id in FavouriteList.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Favourites.Add(id.Trim());
            }

            foreach (var pair in BindList.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var bits = pair.Split('=');

                if (bits.Length == 2 && Enum.TryParse(bits[1], out Key key) && key != Key.None)
                {
                    Binds[bits[0]] = key;
                }
            }
        }

        public static void ToggleFavourite(string id)
        {
            if (!Favourites.Remove(id))
            {
                Favourites.Add(id);
            }

            FavouriteList.Value = string.Join(",", Favourites);
            MarkDirty();
        }

        public static void SetBind(string id, Key key)
        {
            if (key == Key.None)
            {
                Binds.Remove(id);
            }
            else
            {
                // one card per key, so a key never does two things by surprise
                foreach (var other in Binds.Where(b => b.Value == key && b.Key != id).Select(b => b.Key).ToList())
                {
                    Binds.Remove(other);
                }

                Binds[id] = key;
            }

            BindList.Value = string.Join(";", Binds.Select(b => b.Key + "=" + b.Value));
            MarkDirty();
        }

        public static float Float(string id, float def)
        {
            if (!floats.TryGetValue(id, out var e))
            {
                e = cfg.Bind("Values", id, def);
                floats[id] = e;
            }

            return e.Value;
        }

        public static void SetFloat(string id, float value)
        {
            Float(id, value);

            if (!Mathf.Approximately(floats[id].Value, value))
            {
                floats[id].Value = value;
                MarkDirty();
            }
        }

        public static bool Bool(string id, bool def)
        {
            if (!bools.TryGetValue(id, out var e))
            {
                e = cfg.Bind("Options", id, def);
                bools[id] = e;
            }

            return e.Value;
        }

        public static void SetBool(string id, bool value)
        {
            Bool(id, value);

            if (bools[id].Value != value)
            {
                bools[id].Value = value;
                MarkDirty();
            }
        }

        public static string String(string id, string def)
        {
            if (!strings.TryGetValue(id, out var e))
            {
                e = cfg.Bind("Text", id, def);
                strings[id] = e;
            }

            return e.Value;
        }

        public static void SetString(string id, string value)
        {
            String(id, value);

            if (strings[id].Value != value)
            {
                strings[id].Value = value;
                MarkDirty();
            }
        }

        public static void MarkDirty()
        {
            if (!dirty)
            {
                dirty = true;
                saveAt = Time.unscaledTime + 2f;
            }
        }

        // written at most every two seconds, so dragging a slider does not hammer the disk
        public static void Tick()
        {
            if (dirty && Time.unscaledTime >= saveAt)
            {
                SaveNow();
            }
        }

        public static void SaveNow()
        {
            dirty = false;

            try
            {
                cfg.Save();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not save settings: " + e.Message);
            }
        }
    }
}
