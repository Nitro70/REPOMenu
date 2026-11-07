using System.Collections.Generic;
using REPOMenu.Core;
using UnityEngine;

namespace REPOMenu.UI
{
    // Short messages in the bottom-right corner ("Spawned 3 items", "Host only").
    internal static class Toasts
    {
        private sealed class Entry
        {
            public string Text;
            public float Until;
        }

        private static readonly List<Entry> entries = new();

        public static void Show(string text, float seconds = 2.5f)
        {
            Plugin.Log.LogInfo(StripTags(text));

            if (!Settings.ShowToasts.Value)
            {
                return;
            }

            entries.Add(new Entry { Text = text, Until = Time.unscaledTime + seconds });

            while (entries.Count > 5)
            {
                entries.RemoveAt(0);
            }
        }

        public static void Draw()
        {
            entries.RemoveAll(e => Time.unscaledTime > e.Until);

            if (entries.Count == 0 || Event.current.type != EventType.Repaint)
            {
                return;
            }

            float y = Screen.height - 20;

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var content = new GUIContent(entries[i].Text);
                float w = Mathf.Min(380, Theme.Toast.CalcSize(content).x + 4);
                float h = Theme.Toast.CalcHeight(content, w);
                y -= h + 6;
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01((entries[i].Until - Time.unscaledTime) * 3f));
                Theme.Toast.Draw(new Rect(Screen.width - w - 20, y, w, h), content, false, false, false, false);
                GUI.color = old;
            }
        }

        private static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "");
    }
}
