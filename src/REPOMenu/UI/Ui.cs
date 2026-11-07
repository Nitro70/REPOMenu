using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace REPOMenu.UI
{
    // The controls cards are built from: switches, sliders, buttons, pickers. All GUILayout, drawn
    // with the theme, and safe to call every frame.
    internal static class Ui
    {
        private static int dragSlider;

        public static bool Switch(bool on, bool enabled = true)
        {
            var rect = GUILayoutUtility.GetRect(40, 20, GUILayout.Width(40), GUILayout.Height(20));
            rect.y += 2;
            bool hover = rect.Contains(Event.current.mousePosition);

            if (Event.current.type == EventType.Repaint)
            {
                var old = GUI.color;
                GUI.color = !enabled ? new Color(0.2f, 0.22f, 0.25f) : on ? (hover ? Theme.Accent * 1.1f : Theme.Accent) : (hover ? Theme.TabHover * 1.3f : Theme.Off);
                GUI.DrawTexture(rect, Theme.Pill);
                GUI.color = enabled ? Color.white : new Color(0.55f, 0.55f, 0.55f);
                var knob = new Rect(on ? rect.xMax - 18 : rect.x + 2, rect.y + 2, 16, 16);
                GUI.DrawTexture(knob, Theme.Circle);
                GUI.color = old;
            }

            if (enabled && Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return !on;
            }

            return on;
        }

        // a labelled switch on its own row
        public static bool SwitchRow(string label, bool on, string hint = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Theme.Label);

            if (!string.IsNullOrEmpty(hint))
            {
                GUILayout.Label(hint, Theme.LabelSmall);
            }

            GUILayout.FlexibleSpace();
            on = Switch(on);
            GUILayout.EndHorizontal();
            return on;
        }

        public static float Slider(string label, float value, float min, float max, string format = "0.0", string suffix = "")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Theme.Label, GUILayout.Width(150));

            var rect = GUILayoutUtility.GetRect(100, 20, GUILayout.ExpandWidth(true), GUILayout.Height(20));
            int id = GUIUtility.GetControlID(FocusType.Passive);
            var track = new Rect(rect.x + 7, rect.y + 8, rect.width - 14, 5);
            var ev = Event.current;

            switch (ev.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (ev.button == 0 && rect.Contains(ev.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        dragSlider = id;
                        value = Mathf.Lerp(min, max, Mathf.InverseLerp(track.x, track.xMax, ev.mousePosition.x));
                        GUI.changed = true;
                        ev.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        value = Mathf.Lerp(min, max, Mathf.InverseLerp(track.x, track.xMax, ev.mousePosition.x));
                        GUI.changed = true;
                        ev.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        dragSlider = 0;
                        ev.Use();
                    }
                    break;
                case EventType.Repaint:
                {
                    var old = GUI.color;
                    GUI.color = Theme.Field;
                    GUI.DrawTexture(track, Theme.Pill);
                    float t = Mathf.InverseLerp(min, max, value);
                    GUI.color = Theme.AccentDim;
                    GUI.DrawTexture(new Rect(track.x, track.y, Mathf.Max(5, track.width * t), track.height), Theme.Pill);
                    GUI.color = GUIUtility.hotControl == id ? Color.white : new Color(0.85f, 0.9f, 0.95f);
                    GUI.DrawTexture(new Rect(track.x + track.width * t - 7, rect.y + 3, 14, 14), Theme.Circle);
                    GUI.color = old;
                    break;
                }
            }

            GUILayout.Label(value.ToString(format) + suffix, Theme.LabelSubtle, GUILayout.Width(62));
            GUILayout.EndHorizontal();
            return value;
        }

        public static int IntSlider(string label, int value, int min, int max, string suffix = "")
        {
            return Mathf.RoundToInt(Slider(label, value, min, max, "0", suffix));
        }

        public static bool Button(string text, float width = -1, GUIStyle style = null)
        {
            style ??= Theme.Button;
            return width > 0 ? GUILayout.Button(text, style, GUILayout.Width(width)) : GUILayout.Button(text, style);
        }

        public static bool Small(string text, float width = -1)
        {
            return width > 0 ? GUILayout.Button(text, Theme.ButtonSmall, GUILayout.Width(width)) : GUILayout.Button(text, Theme.ButtonSmall);
        }

        public static void Section(string text) => GUILayout.Label(text, Theme.SectionHeader);

        public static void Hint(string text) => GUILayout.Label(text, Theme.Desc);

        public static string Field(string value, float width = -1)
        {
            return width > 0 ? GUILayout.TextField(value ?? "", Theme.Field1, GUILayout.Width(width)) : GUILayout.TextField(value ?? "", Theme.Field1);
        }

        public static int IntField(string label, int value, float width = 90)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Theme.Label);
            var text = GUILayout.TextField(value.ToString(), Theme.Field1, GUILayout.Width(width));
            GUILayout.EndHorizontal();
            return int.TryParse(text, out var v) ? v : (string.IsNullOrEmpty(text) ? 0 : value);
        }

        // Buttons laid out in rows that wrap to the card's width.
        public static int ButtonGrid(IList<string> labels, int selected, float cellWidth, float available, GUIStyle normal = null, GUIStyle sel = null)
        {
            normal ??= Theme.ButtonSmall;
            sel ??= Theme.ListItemSel;
            int perRow = Mathf.Max(1, Mathf.FloorToInt(available / (cellWidth + 4)));
            int clicked = -1;

            for (int i = 0; i < labels.Count; i += perRow)
            {
                GUILayout.BeginHorizontal();

                for (int j = i; j < Math.Min(labels.Count, i + perRow); j++)
                {
                    if (GUILayout.Button(labels[j], j == selected ? sel : normal, GUILayout.Width(cellWidth), GUILayout.Height(22)))
                    {
                        clicked = j;
                    }
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            return clicked;
        }

        // A search box over a scrolling list. Returns the clicked entry or null.
        public sealed class Picker<T> where T : class
        {
            public string Search = "";
            public Vector2 Scroll;
            public T Selected;
            private List<T> shown;

            public T Draw(IEnumerable<T> source, Func<T, string> name, float height = 180f, Func<T, string> right = null, Func<T, bool> highlight = null)
            {
                T clicked = null;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Search", Theme.LabelSubtle, GUILayout.Width(50));
                Search = GUILayout.TextField(Search ?? "", Theme.Field1);

                if (!string.IsNullOrEmpty(Search) && GUILayout.Button("x", Theme.ButtonSmall, GUILayout.Width(24)))
                {
                    Search = "";
                }

                GUILayout.EndHorizontal();

                // the list is rebuilt only on the layout pass, so every pass of one frame draws the
                // same rows (IMGUI throws if the control count changes between layout and repaint)
                if (Event.current.type == EventType.Layout || shown == null)
                {
                    shown = source.Where(x => x != null).ToList();

                    if (!string.IsNullOrEmpty(Search))
                    {
                        var q = Search.Trim().ToLowerInvariant();
                        shown = shown.Where(x => (name(x) ?? "").ToLowerInvariant().Contains(q)).ToList();
                    }
                }

                var list = shown;

                GUILayout.Label($"{list.Count} found", Theme.LabelSmall);
                Scroll = GUILayout.BeginScrollView(Scroll, GUILayout.Height(height));

                // only the rows in view are drawn; the rest is empty space of the same height, so a
                // list of hundreds (every cosmetic) costs the same as a list of ten
                const float rowH = 24f;
                int first = Mathf.Clamp(Mathf.FloorToInt(Scroll.y / rowH) - 1, 0, list.Count);
                int last = Mathf.Clamp(first + Mathf.CeilToInt(height / rowH) + 3, 0, list.Count);

                if (first > 0)
                {
                    GUILayout.Space(first * rowH);
                }

                for (int index = first; index < last; index++)
                {
                    var item = list[index];
                    bool isSel = ReferenceEquals(item, Selected);
                    bool hl = highlight != null && highlight(item);
                    var label = name(item);

                    if (hl)
                    {
                        label = "<color=#7FD1A0>" + label + "</color>";
                    }

                    GUILayout.BeginHorizontal(GUILayout.Height(rowH));

                    if (GUILayout.Button(label, isSel ? Theme.ListItemSel : Theme.ListItem))
                    {
                        Selected = item;
                        clicked = item;
                    }

                    if (right != null)
                    {
                        GUILayout.Label(right(item), Theme.LabelSmall, GUILayout.Width(110));
                    }

                    GUILayout.EndHorizontal();
                }

                if (last < list.Count)
                {
                    GUILayout.Space((list.Count - last) * rowH);
                }

                GUILayout.EndScrollView();
                return clicked;
            }
        }
    }
}
