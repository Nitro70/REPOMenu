using System;
using System.Collections.Generic;
using System.Linq;
using REPOMenu.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace REPOMenu.UI
{
    // The menu window, laid out like WobblyMenu:
    //
    //   [ REPOMenu  R.E.P.O. · by Nitro70 ]   [ search every card....... ] [ Favourites ] [ All off ] [ Close ]
    //   [ sidebar: tabs with icons and   ]   [ the picked tab's cards; while searching or showing        ]
    //   [ how many cards each one has     ]   [ favourites, every matching card from every tab, by tab    ]
    //   [ status: host / guest / alone ......................... F2 opens and closes · right-click: bind ]
    internal static class MenuWindow
    {
        private const int WindowId = 0x52504D31;
        private const float HeaderH = 48f;
        private const float StatusH = 26f;
        private const float SidebarW = 182f;

        public static bool Open;
        public static Card BindTarget;
        public static bool BindingMenuKey;

        private static Rect rect;
        private static bool rectLoaded;
        private static bool resizing;
        private static int selectedTab;
        private static string search = "";
        private static bool favouritesMode;
        private static Vector2 sidebarScroll;
        private static readonly Dictionary<string, Vector2> scrolls = new();
        private static readonly Dictionary<string, float> muted = new();
        private static List<Card> shownCards = new();
        private static string shownKey;

        public static float Scale => Mathf.Clamp(Settings.UiScale.Value, 0.75f, 1.6f);

        public static void Draw()
        {
            LoadRect();

            var oldMatrix = GUI.matrix;
            float s = Scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));

            float sw = Screen.width / s, sh = Screen.height / s;
            rect.width = Mathf.Clamp(rect.width, 780, Mathf.Max(780, sw - 20));
            rect.height = Mathf.Clamp(rect.height, 420, Mathf.Max(420, sh - 20));
            rect.x = Mathf.Clamp(rect.x, -rect.width + 120, sw - 120);
            rect.y = Mathf.Clamp(rect.y, 0, sh - 60);

            var before = rect;
            rect = GUI.Window(WindowId, rect, DrawWindow, GUIContent.none, Theme.Window);

            if (rect != before)
            {
                SaveRect();
            }

            GUI.matrix = oldMatrix;
        }

        private static void LoadRect()
        {
            if (rectLoaded)
            {
                return;
            }

            rectLoaded = true;
            var bits = (Settings.Window.Value ?? "").Split(',');

            if (bits.Length == 4 && float.TryParse(bits[0], out var x) && float.TryParse(bits[1], out var y) &&
                float.TryParse(bits[2], out var w) && float.TryParse(bits[3], out var h))
            {
                rect = new Rect(x, y, w, h);
            }
            else
            {
                ResetRect();
            }

            var last = Settings.String("ui.tab", "");
            int idx = Registry.Tabs.FindIndex(t => t.Name == last);
            selectedTab = idx >= 0 ? idx : 0;
        }

        public static void ResetRect()
        {
            float s = Scale;
            float w = 900, h = 600;
            rect = new Rect((Screen.width / s - w) / 2f, (Screen.height / s - h) / 2f, w, h);
            SaveRect();
        }

        private static void SaveRect()
        {
            Settings.Window.Value = $"{rect.x:0},{rect.y:0},{rect.width:0},{rect.height:0}";
            Settings.MarkDirty();
        }

        private static void DrawWindow(int id)
        {
            float w = rect.width, h = rect.height;

            DrawHeader(w);
            DrawSidebar(new Rect(8, HeaderH + 6, SidebarW, h - HeaderH - StatusH - 16));
            DrawContent(new Rect(SidebarW + 16, HeaderH + 6, w - SidebarW - 24, h - HeaderH - StatusH - 16));
            DrawStatus(new Rect(8, h - StatusH - 6, w - 16, StatusH));
            DrawResizeGrip(w, h);

            if (BindTarget != null || BindingMenuKey)
            {
                DrawBindOverlay(w, h);
            }

            // a click on empty space takes the keyboard away from the search box
            if (Event.current.type == EventType.MouseDown)
            {
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = 0;
            }

            GUI.DragWindow(new Rect(0, 0, w, HeaderH));
        }

        private static void DrawHeader(float w)
        {
            var header = new Rect(0, 0, w, HeaderH);

            if (Event.current.type == EventType.Repaint)
            {
                Theme.Header.Draw(header, false, false, false, false);
            }

            GUI.Label(new Rect(16, 9, 360, 30), Brand.TitleRich, Theme.TitleBig);

            float x = w - 16;
            x -= 64;
            if (GUI.Button(new Rect(x, 11, 64, 26), "Close", Theme.Button))
            {
                Open = false;
            }

            x -= 78;
            if (GUI.Button(new Rect(x, 11, 74, 26), "All off", Theme.ButtonDanger))
            {
                int n = Registry.AllOff();
                Toasts.Show(n == 0 ? "Nothing was on" : $"Turned off {n} card(s)");
            }

            x -= 112;
            if (GUI.Button(new Rect(x, 11, 108, 26), favouritesMode ? "★ Favourites" : "☆ Favourites", favouritesMode ? Theme.ButtonAccent : Theme.Button))
            {
                favouritesMode = !favouritesMode;
            }

            // the search box fills the gap between the title and the buttons
            float fieldW = Mathf.Clamp(x - 4 - 372, 90, 300);
            var field = new Rect(x - 4 - fieldW, 11, fieldW, 26);
            GUI.SetNextControlName("repomenu.search");
            search = GUI.TextField(field, search ?? "", Theme.Field1);

            if (string.IsNullOrEmpty(search) && GUI.GetNameOfFocusedControl() != "repomenu.search")
            {
                GUI.Label(new Rect(field.x + 9, field.y, field.width - 10, field.height), "Search every card...", Theme.LabelSubtle);
            }
        }

        private static void DrawSidebar(Rect area)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Theme.Sidebar.Draw(area, false, false, false, false);
            }

            GUILayout.BeginArea(new Rect(area.x, area.y + 8, area.width, area.height - 16));
            sidebarScroll = GUILayout.BeginScrollView(sidebarScroll);

            for (int i = 0; i < Registry.Tabs.Count; i++)
            {
                var tab = Registry.Tabs[i];
                bool sel = i == selectedTab && !Searching && !favouritesMode;
                int on = tab.Cards.Count(c => c.HasSwitch && c.IsOn);
                string count = tab.Page != null ? "" : tab.Cards.Count.ToString();
                string label = $"{tab.Icon}  {tab.Name}";

                var r = GUILayoutUtility.GetRect(new GUIContent(label), Theme.Tab, GUILayout.ExpandWidth(true));

                if (GUI.Button(r, label, sel ? Theme.TabSel : Theme.Tab))
                {
                    selectedTab = i;
                    favouritesMode = false;
                    search = "";
                    GUI.FocusControl(null);
                    Settings.SetString("ui.tab", tab.Name);
                }

                if (Event.current.type == EventType.Repaint)
                {
                    var rr = new Rect(r.xMax - 46, r.y, 38, r.height);
                    var st = new GUIStyle(Theme.LabelSmall) { alignment = TextAnchor.MiddleRight };
                    GUI.Label(rr, on > 0 ? $"<color=#7FD1A0>{on} on</color>" : count, st);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static bool Searching => !string.IsNullOrEmpty(search?.Trim());

        private static void DrawContent(Rect area)
        {
            GUILayout.BeginArea(area);

            string key;
            TabDef tab = Registry.Tabs.Count > 0 ? Registry.Tabs[Mathf.Clamp(selectedTab, 0, Registry.Tabs.Count - 1)] : null;

            if (Searching)
            {
                key = "search";
                GUILayout.Label($"Search: <b>{search.Trim()}</b>", Theme.TitleBig);
            }
            else if (favouritesMode)
            {
                key = "favourites";
                GUILayout.Label("★ Favourites", Theme.TitleBig);
                GUILayout.Label("Cards you starred, from every tab. Click the star on any card to add it here.", Theme.Desc);
            }
            else if (tab != null)
            {
                key = "tab:" + tab.Name;
                GUILayout.Label($"{tab.Icon}  {tab.Name}", Theme.TitleBig);

                if (!string.IsNullOrEmpty(tab.Blurb))
                {
                    GUILayout.Label(tab.Blurb, Theme.Desc);
                }
            }
            else
            {
                GUILayout.EndArea();
                return;
            }

            GUILayout.Space(4);
            scrolls.TryGetValue(key, out var scroll);
            scroll = GUILayout.BeginScrollView(scroll);

            if (!Searching && !favouritesMode && tab?.Page != null)
            {
                RunBody("page." + tab.Name, tab.Page);
            }
            else
            {
                // which cards to show is decided on the layout pass only (see Ui.Picker)
                if (Event.current.type == EventType.Layout || shownKey != key + search)
                {
                    shownKey = key + search;
                    shownCards = Pick(tab);
                }

                string lastTab = null;
                bool grouped = Searching || favouritesMode;

                foreach (var card in shownCards)
                {
                    if (grouped && card.Tab.Name != lastTab)
                    {
                        lastTab = card.Tab.Name;
                        Ui.Section($"{card.Tab.Icon}  {card.Tab.Name}");
                    }

                    DrawCard(card);
                }

                if (shownCards.Count == 0)
                {
                    GUILayout.Label(Searching ? "No card matches that." : "No favourites yet: click the star on a card.", Theme.Desc);
                }
            }

            GUILayout.Space(10);
            GUILayout.EndScrollView();
            scrolls[key] = scroll;
            GUILayout.EndArea();
        }

        private static List<Card> Pick(TabDef tab)
        {
            if (Searching)
            {
                var q = search.Trim().ToLowerInvariant();
                return Registry.Tabs.SelectMany(t => t.Cards).Where(c =>
                    (c.Title ?? "").ToLowerInvariant().Contains(q) ||
                    (c.Desc ?? "").ToLowerInvariant().Contains(q) ||
                    c.Tab.Name.ToLowerInvariant().Contains(q)).ToList();
            }

            if (favouritesMode)
            {
                return Registry.Tabs.SelectMany(t => t.Cards).Where(c => Settings.Favourites.Contains(c.Id)).ToList();
            }

            return tab?.Cards.ToList() ?? new List<Card>();
        }

        private static void DrawCard(Card c)
        {
            bool fav = Settings.Favourites.Contains(c.Id);
            bool broken = Safe.IsBroken(c.Id);
            string blocked = c.BlockReason();
            bool usable = blocked == null && !broken;

            GUILayout.BeginVertical(fav ? Theme.CardFav : Theme.Card);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button(fav ? "★" : "☆", fav ? Theme.StarOn : Theme.Star))
            {
                Settings.ToggleFavourite(c.Id);
            }

            GUILayout.Label(c.Title, Theme.Title);
            DrawBadge(c.Scope);

            if (Settings.Binds.TryGetValue(c.Id, out var key))
            {
                GUILayout.Label($"<color=#8FB8DE>[{KeyName(key)}]</color>", Theme.LabelSmall);
            }

            GUILayout.FlexibleSpace();

            if (c.HasSwitch)
            {
                bool on = c.IsOn;
                bool now = Ui.Switch(on, usable);

                if (now != on)
                {
                    Safe.Run(c.Id, () => c.Set(now));
                }
            }
            else if (c.Run != null)
            {
                GUI.enabled = usable;

                if (GUILayout.Button(c.RunLabel, Theme.ButtonAccent, GUILayout.MinWidth(70)))
                {
                    Safe.Run(c.Id, c.Run);
                }

                GUI.enabled = true;
            }

            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(c.Desc))
            {
                GUILayout.Label(c.Desc, Theme.Desc);
            }

            if (broken)
            {
                GUILayout.Label("<color=#E08080>Turned off after an error, probably a game update changed it:</color> " + Safe.Broken[c.Id], Theme.Desc);

                if (GUILayout.Button("Try again", Theme.ButtonSmall, GUILayout.Width(90)))
                {
                    Safe.Reset(c.Id);
                    Safe.Reset(c.Id + ".body");
                }
            }
            else if (blocked != null)
            {
                GUILayout.Label("<color=#E8A860>" + blocked + "</color>", Theme.Desc);
            }

            if (c.Body != null && !broken)
            {
                GUI.enabled = usable;
                RunBody(c.Id + ".body", c.Body);
                GUI.enabled = true;
            }

            GUILayout.EndVertical();

            var r = GUILayoutUtility.GetLastRect();
            var ev = Event.current;

            if (c.Bindable && ev.type == EventType.MouseDown && ev.button == 1 && r.Contains(ev.mousePosition))
            {
                BindTarget = c;
                ev.Use();
            }
        }

        // A card body that throws is skipped for a few seconds instead of breaking the whole window.
        private static void RunBody(string id, Action body)
        {
            if (muted.TryGetValue(id, out var until) && Time.unscaledTime < until)
            {
                GUILayout.Label("<color=#E08080>This part is waiting for the game (it hit an error).</color>", Theme.Desc);
                return;
            }

            try
            {
                body();
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception e)
            {
                muted[id] = Time.unscaledTime + 3f;
                Plugin.Log.LogWarning($"[{id}] {e}");
            }
        }

        private static void DrawBadge(Scope scope)
        {
            if (scope == Scope.Menu)
            {
                return;
            }

            var old = GUI.backgroundColor;
            GUI.backgroundColor = scope == Scope.Host ? Theme.HostBadge : Theme.ClientBadge;
            GUILayout.Label(scope == Scope.Host ? "HOST" : "CLIENT", Theme.Badge, GUILayout.Width(scope == Scope.Host ? 42 : 52));
            GUI.backgroundColor = old;
        }

        private static void DrawStatus(Rect r)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Theme.StatusBar.Draw(r, false, false, false, false);
            }

            string version = Game.Version;
            bool match = version.StartsWith(Brand.BuiltForGame);
            string left = $"{Game.Status}   ·   game {version}" + (match || version == "?" ? "" : $" <color=#E8A860>(menu built for {Brand.BuiltForGame}.x)</color>");
            GUI.Label(new Rect(r.x + 10, r.y, r.width * 0.62f, r.height), left, Theme.LabelSmall);

            var right = new GUIStyle(Theme.LabelSmall) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(r.x + r.width * 0.5f, r.y, r.width * 0.5f - 26, r.height),
                $"{KeyName(Settings.MenuKey.Value)} opens and closes   ·   right-click a card to bind a key", right);
        }

        private static void DrawResizeGrip(float w, float h)
        {
            var grip = new Rect(w - 18, h - 18, 16, 16);
            var ev = Event.current;

            if (ev.type == EventType.Repaint)
            {
                var old = GUI.color;
                GUI.color = new Color(0.4f, 0.5f, 0.6f, 0.8f);

                for (int i = 0; i < 3; i++)
                {
                    GUI.DrawTexture(new Rect(grip.xMax - 4 - i * 4, grip.yMax - 4, 2, 2), Theme.White);
                    GUI.DrawTexture(new Rect(grip.xMax - 4, grip.yMax - 4 - i * 4, 2, 2), Theme.White);
                }

                GUI.color = old;
            }

            if (ev.type == EventType.MouseDown && ev.button == 0 && grip.Contains(ev.mousePosition))
            {
                resizing = true;
                ev.Use();
            }
            else if (resizing && ev.type == EventType.MouseDrag)
            {
                rect.width = Mathf.Max(780, rect.width + ev.delta.x);
                rect.height = Mathf.Max(420, rect.height + ev.delta.y);
                ev.Use();
            }
            else if (resizing && (ev.type == EventType.MouseUp || ev.rawType == EventType.MouseUp))
            {
                resizing = false;
                SaveRect();
            }
        }

        private static void DrawBindOverlay(float w, float h)
        {
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Theme.White);
            GUI.color = old;

            var box = new Rect((w - 420) / 2f, (h - 140) / 2f, 420, 140);
            GUI.Box(box, GUIContent.none, Theme.Card);
            string what = BindingMenuKey ? "opening the menu" : $"<b>{BindTarget.Title}</b>";
            GUI.Label(new Rect(box.x + 16, box.y + 14, box.width - 32, 24), "Press a key for " + what, Theme.Title);
            GUI.Label(new Rect(box.x + 16, box.y + 44, box.width - 32, 40),
                BindingMenuKey ? "Escape cancels." : "Escape cancels. Backspace removes the bind." +
                (BindTarget.HasSwitch ? " The key flips the switch." : " The key presses the button."), Theme.Desc);

            if (GUI.Button(new Rect(box.xMax - 96, box.yMax - 38, 80, 26), "Cancel", Theme.Button))
            {
                BindTarget = null;
                BindingMenuKey = false;
            }

            // swallow clicks behind the overlay
            if (Event.current.type == EventType.MouseDown)
            {
                Event.current.Use();
            }
        }

        // called from Update: the Input System sees keys IMGUI does not get while the game has focus
        public static void CaptureBind()
        {
            var kb = Keyboard.current;

            if (kb == null)
            {
                return;
            }

            foreach (var k in kb.allKeys)
            {
                if (k == null || !k.wasPressedThisFrame)
                {
                    continue;
                }

                var key = k.keyCode;

                if (key == Key.Escape)
                {
                    BindTarget = null;
                    BindingMenuKey = false;
                    return;
                }

                if (BindingMenuKey)
                {
                    if (key != Key.Backspace)
                    {
                        Settings.MenuKey.Value = key;
                        Settings.MarkDirty();
                        Toasts.Show("Menu key: " + KeyName(key));
                    }

                    BindingMenuKey = false;
                    return;
                }

                if (key == Key.Backspace)
                {
                    Settings.SetBind(BindTarget.Id, Key.None);
                    Toasts.Show("Removed the key from " + BindTarget.Title);
                }
                else if (key == Settings.MenuKey.Value)
                {
                    Toasts.Show(KeyName(key) + " already opens the menu");
                }
                else
                {
                    Settings.SetBind(BindTarget.Id, key);
                    Toasts.Show($"{KeyName(key)} now triggers {BindTarget.Title}");
                }

                BindTarget = null;
                return;
            }
        }

        public static string KeyName(Key key)
        {
            var name = key.ToString();
            return name.StartsWith("Digit") ? name.Substring(5) : name;
        }
    }
}
