using System.Collections.Generic;
using UnityEngine;

namespace REPOMenu.UI
{
    // The look of the menu: the same dark blue palette as WobblyMenu, rounded panels, slim scrollbars.
    // Built from textures drawn in code, so the plugin is a single DLL with no asset bundle.
    internal static class Theme
    {
        public static readonly Color WindowBg = new(0.055f, 0.07f, 0.095f, 0.975f);
        public static readonly Color HeaderBg = new(0.07f, 0.09f, 0.12f, 1f);
        public static readonly Color SidebarBg = new(0.08f, 0.11f, 0.15f, 1f);
        public static readonly Color TabNormal = new(0.11f, 0.15f, 0.2f, 1f);
        public static readonly Color TabHover = new(0.13f, 0.19f, 0.26f, 1f);
        public static readonly Color TabSelected = new(0.15f, 0.38f, 0.55f, 1f);
        public static readonly Color CardBg = new(0.095f, 0.125f, 0.17f, 1f);
        public static readonly Color CardBorder = new(0.16f, 0.21f, 0.28f, 1f);
        public static readonly Color Field = new(0.06f, 0.08f, 0.11f, 1f);
        public static readonly Color Accent = new(0.27f, 0.62f, 0.88f, 1f);
        public static readonly Color AccentDim = new(0.17f, 0.42f, 0.6f, 1f);
        public static readonly Color Off = new(0.2f, 0.25f, 0.32f, 1f);
        public static readonly Color Subtle = new(0.62f, 0.70f, 0.78f, 1f);
        public static readonly Color HostBadge = new(0.93f, 0.58f, 0.2f, 1f);
        public static readonly Color ClientBadge = new(0.32f, 0.78f, 0.5f, 1f);
        public static readonly Color Danger = new(0.85f, 0.3f, 0.3f, 1f);
        public static readonly Color Gold = new(1f, 0.82f, 0.25f, 1f);

        public static GUISkin Skin;
        public static GUIStyle Window, Header, Sidebar, StatusBar, Card, CardFav;
        public static GUIStyle Title, TitleBig, Desc, Label, LabelSubtle, LabelSmall, SectionHeader;
        public static GUIStyle Tab, TabSel, Button, ButtonAccent, ButtonDanger, ButtonSmall, ButtonFlat, Star, StarOn;
        public static GUIStyle Field1, Badge, ListItem, ListItemSel, Toast;
        public static Texture2D White, Pill, Circle, Line;

        private static readonly Dictionary<string, Texture2D> cache = new();

        public static bool Ensure()
        {
            if (Skin && White && Pill && Circle)
            {
                return true;
            }

            cache.Clear();
            Build();
            return Skin;
        }

        private static void Build()
        {
            White = Solid(Color.white);
            Line = White;
            Pill = Rounded(Color.white, 10, 40, 20);
            Circle = Rounded(Color.white, 10, 20, 20);

            Skin = Object.Instantiate(GUI.skin);
            Skin.hideFlags = HideFlags.HideAndDontSave;
            Skin.name = "REPOMenuSkin";

            var font = GUI.skin.font;

            Window = Panel(WindowBg, 10, CardBorder, 1);
            Window.padding = new RectOffset(0, 0, 0, 0);

            Header = Panel(HeaderBg, 10, default, 0);
            Sidebar = Panel(SidebarBg, 8, default, 0);
            StatusBar = Panel(HeaderBg, 8, default, 0);
            StatusBar.padding = new RectOffset(10, 10, 3, 3);

            Card = Panel(CardBg, 8, CardBorder, 1);
            Card.padding = new RectOffset(12, 12, 9, 10);
            Card.margin = new RectOffset(0, 0, 0, 8);

            CardFav = Panel(CardBg, 8, new Color(0.55f, 0.47f, 0.18f, 1f), 1);
            CardFav.padding = Card.padding;
            CardFav.margin = Card.margin;

            Title = Text(14, Color.white, FontStyle.Bold);
            TitleBig = Text(17, Color.white, FontStyle.Bold);
            Desc = Text(12, Subtle, FontStyle.Normal);
            Desc.wordWrap = true;
            Label = Text(13, Color.white, FontStyle.Normal);
            LabelSubtle = Text(12, Subtle, FontStyle.Normal);
            LabelSmall = Text(11, Subtle, FontStyle.Normal);
            SectionHeader = Text(13, Accent, FontStyle.Bold);
            SectionHeader.margin = new RectOffset(2, 2, 6, 4);

            Tab = ButtonStyle(TabNormal, TabHover, TabSelected, 6, 13);
            Tab.alignment = TextAnchor.MiddleLeft;
            Tab.padding = new RectOffset(12, 10, 0, 0);
            Tab.fixedHeight = 32;
            Tab.margin = new RectOffset(8, 8, 2, 2);

            TabSel = ButtonStyle(TabSelected, TabSelected, TabSelected, 6, 13);
            TabSel.alignment = Tab.alignment;
            TabSel.padding = Tab.padding;
            TabSel.fixedHeight = Tab.fixedHeight;
            TabSel.margin = Tab.margin;
            TabSel.fontStyle = FontStyle.Bold;

            Button = ButtonStyle(new Color(0.15f, 0.2f, 0.27f), new Color(0.19f, 0.27f, 0.36f), AccentDim, 6, 12);
            ButtonAccent = ButtonStyle(AccentDim, Accent, new Color(0.12f, 0.32f, 0.48f), 6, 12);
            ButtonDanger = ButtonStyle(new Color(0.4f, 0.16f, 0.16f), new Color(0.55f, 0.2f, 0.2f), new Color(0.3f, 0.12f, 0.12f), 6, 12);
            ButtonSmall = ButtonStyle(new Color(0.15f, 0.2f, 0.27f), new Color(0.19f, 0.27f, 0.36f), AccentDim, 5, 11);
            ButtonSmall.fixedHeight = 22;
            ButtonSmall.padding = new RectOffset(8, 8, 2, 2);

            ButtonFlat = new GUIStyle(Label) { alignment = TextAnchor.MiddleCenter };
            ButtonFlat.hover.textColor = Accent;

            Star = new GUIStyle(Label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, fixedWidth = 22, fixedHeight = 20 };
            Star.normal.textColor = new Color(0.4f, 0.47f, 0.55f);
            Star.hover.textColor = Gold;
            StarOn = new GUIStyle(Star);
            StarOn.normal.textColor = Gold;

            Field1 = Panel(Field, 6, CardBorder, 1);
            Field1.font = font;
            Field1.fontSize = 13;
            Field1.normal.textColor = Color.white;
            Field1.focused.background = Rounded(Field, 6, 64, 64, Accent, 1);
            Field1.focused.textColor = Color.white;
            Field1.hover.background = Field1.normal.background;
            Field1.hover.textColor = Color.white;
            Field1.padding = new RectOffset(8, 8, 5, 5);
            Field1.alignment = TextAnchor.MiddleLeft;
            Field1.clipping = TextClipping.Clip;
            Field1.fixedHeight = 26;

            Badge = Text(10, Color.white, FontStyle.Bold);
            Badge.alignment = TextAnchor.MiddleCenter;
            Badge.normal.background = Rounded(Color.white, 5, 24, 16);
            Badge.border = new RectOffset(6, 6, 6, 6);
            Badge.padding = new RectOffset(6, 6, 1, 1);
            Badge.fixedHeight = 16;

            ListItem = ButtonStyle(new Color(0, 0, 0, 0), new Color(0.15f, 0.21f, 0.29f), AccentDim, 4, 12);
            ListItem.alignment = TextAnchor.MiddleLeft;
            ListItem.fixedHeight = 22;
            ListItem.padding = new RectOffset(8, 6, 0, 0);
            ListItem.margin = new RectOffset(0, 0, 1, 1);
            ListItemSel = ButtonStyle(AccentDim, Accent, AccentDim, 4, 12);
            ListItemSel.alignment = ListItem.alignment;
            ListItemSel.fixedHeight = ListItem.fixedHeight;
            ListItemSel.padding = ListItem.padding;
            ListItemSel.margin = ListItem.margin;

            Toast = Panel(new Color(0.08f, 0.11f, 0.15f, 0.95f), 8, Accent, 1);
            Toast.font = font;
            Toast.fontSize = 13;
            Toast.normal.textColor = Color.white;
            Toast.padding = new RectOffset(12, 12, 8, 8);
            Toast.wordWrap = true;
            Toast.richText = true;

            // slim scrollbars
            var track = new GUIStyle(GUI.skin.verticalScrollbar)
            {
                fixedWidth = 8,
                margin = new RectOffset(4, 2, 2, 2),
                padding = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(4, 4, 4, 4)
            };
            track.normal.background = Rounded(new Color(0.06f, 0.08f, 0.1f), 4, 8, 16);
            Skin.verticalScrollbar = track;

            var thumb = new GUIStyle(GUI.skin.verticalScrollbarThumb)
            {
                fixedWidth = 8,
                border = new RectOffset(4, 4, 4, 4)
            };
            thumb.normal.background = Rounded(new Color(0.24f, 0.33f, 0.43f), 4, 8, 16);
            thumb.hover.background = Rounded(Accent, 4, 8, 16);
            thumb.active.background = thumb.hover.background;
            Skin.verticalScrollbarThumb = thumb;
            Skin.verticalScrollbarUpButton = new GUIStyle { fixedHeight = 0, fixedWidth = 0 };
            Skin.verticalScrollbarDownButton = new GUIStyle { fixedHeight = 0, fixedWidth = 0 };
            Skin.horizontalScrollbar = new GUIStyle { fixedHeight = 0 };
            Skin.horizontalScrollbarThumb = new GUIStyle { fixedHeight = 0 };
            Skin.scrollView = new GUIStyle();
            Skin.textField = Field1;
            Skin.label = Label;
            Skin.button = Button;
        }

        private static GUIStyle Text(int size, Color color, FontStyle style)
        {
            var s = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = style,
                richText = true,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(2, 2, 1, 1),
                alignment = TextAnchor.MiddleLeft
            };
            s.normal.textColor = color;
            s.hover.textColor = color;
            return s;
        }

        private static GUIStyle Panel(Color fill, int radius, Color border, int borderWidth)
        {
            var s = new GUIStyle();
            s.normal.background = Rounded(fill, radius, radius * 2 + 8, radius * 2 + 8, border, borderWidth);
            s.border = new RectOffset(radius + 2, radius + 2, radius + 2, radius + 2);
            s.padding = new RectOffset(8, 8, 8, 8);
            s.richText = true;
            return s;
        }

        private static GUIStyle ButtonStyle(Color normal, Color hover, Color active, int radius, int fontSize)
        {
            var s = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(radius + 2, radius + 2, radius + 2, radius + 2),
                padding = new RectOffset(10, 10, 4, 4),
                margin = new RectOffset(2, 2, 2, 2),
                fixedHeight = 26
            };
            int size = radius * 2 + 8;
            s.normal.background = Rounded(normal, radius, size, size);
            s.hover.background = Rounded(hover, radius, size, size);
            s.active.background = Rounded(active, radius, size, size);
            s.focused.background = s.normal.background;
            s.onNormal.background = s.active.background;
            s.onHover.background = s.active.background;
            s.normal.textColor = Color.white;
            s.hover.textColor = Color.white;
            s.active.textColor = Color.white;
            s.focused.textColor = Color.white;
            s.onNormal.textColor = Color.white;
            s.onHover.textColor = Color.white;
            return s;
        }

        public static Texture2D Solid(Color c)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels(new[] { c, c, c, c });
            t.Apply();
            return t;
        }

        // A rounded rectangle drawn with a signed distance field, so the corners are smooth.
        public static Texture2D Rounded(Color fill, int radius, int width, int height, Color border = default, int borderWidth = 0)
        {
            string key = $"{fill}|{radius}|{width}|{height}|{border}|{borderWidth}";

            if (cache.TryGetValue(key, out var cached) && cached)
            {
                return cached;
            }

            var t = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var px = new Color[width * height];
            float hw = width / 2f, hh = height / 2f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - hw) - (hw - radius);
                    float qy = Mathf.Abs(y + 0.5f - hh) - (hh - radius);
                    float outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude;
                    float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;

                    float cover = Mathf.Clamp01(0.5f - d);
                    Color c = fill;

                    if (borderWidth > 0)
                    {
                        float inner = Mathf.Clamp01(0.5f - (d + borderWidth));
                        c = Color.Lerp(border, fill, inner);
                    }

                    c.a *= cover;
                    px[y * width + x] = c;
                }
            }

            t.SetPixels(px);
            t.Apply();
            cache[key] = t;
            return t;
        }
    }
}
