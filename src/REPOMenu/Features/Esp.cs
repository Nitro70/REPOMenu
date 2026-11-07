using System.Collections.Generic;
using REPOMenu.Core;
using REPOMenu.UI;
using UnityEngine;

namespace REPOMenu.Features
{
    // See things through walls. Reads only what your own game already knows, so it works in any
    // lobby. R.E.P.O. renders through a small render texture, so positions are projected through the
    // camera's viewport (0..1) and scaled to the real screen, never through pixel coordinates.
    internal static class Esp
    {
        public static bool On;

        private static bool Opt(string id, bool def = true) => Settings.Bool("esp." + id, def);
        private static float MaxDistance => Settings.Float("esp.distance", 120f);
        private static float MinValue => Settings.Float("esp.minvalue", 0f);

        private static readonly Dictionary<int, Renderer[]> renderers = new();
        private static GUIStyle label;
        private static float rendererCacheAt;

        private static readonly Color EnemyColor = new(1f, 0.32f, 0.3f);
        private static readonly Color PlayerColor = new(0.35f, 0.75f, 1f);
        private static readonly Color DeadColor = new(0.65f, 0.65f, 0.7f);
        private static readonly Color ItemColor = new(0.45f, 0.95f, 0.95f);
        private static readonly Color ExtractColor = new(0.5f, 1f, 0.45f);
        private static readonly Color CartColor = new(0.85f, 0.7f, 1f);

        public static void Register()
        {
            var tab = Registry.Tab("ESP", "◎", "See enemies, loot and players through walls. Only reads what your game already knows, so it works in any lobby and nobody else sees it.");

            tab.Switch("esp.on", "ESP", "Master switch for everything below.", Scope.Client, () => On, v => On = v);

            tab.Panel("esp.what", "What to show", "Pick the kinds of things to mark.", Scope.Client, () =>
            {
                Row("enemies", "Enemies (name, health)");
                Row("valuables", "Valuables (dollar value)");
                Row("items", "Items and weapons");
                Row("players", "Other players (and dead players' heads)");
                Row("extractions", "Extraction points");
                Row("carts", "Carts");
            });

            tab.Action("esp.reveal", "Put every valuable on the map", "Marks every valuable as found, the same as if someone had looked at it: it shows on the map for the whole lobby.", Scope.Client, "Reveal", () =>
            {
                int n = 0;

                foreach (var v in WorldCache.Valuables)
                {
                    if (v && !v.discovered && v.Discover(ValuableDiscoverGraphic.State.Discover))
                    {
                        n++;
                    }
                }

                Toasts.Show($"Revealed {n} valuables");
            }).When(() => Game.InRunLevel ? null : "Only on a level");

            tab.Panel("esp.style", "How it looks", "Boxes, lines and limits.", Scope.Client, () =>
            {
                Row("boxes", "Boxes around things");
                Row("tracers", "Lines from the bottom of the screen", false);
                Row("distance", "Show distance");
                Settings.SetFloat("esp.distance", Ui.Slider("Max distance", MaxDistance, 10f, 500f, "0", " m"));
                Settings.SetFloat("esp.minvalue", Ui.Slider("Valuables worth at least", MinValue, 0f, 20000f, "0", " $"));
            });
        }

        private static void Row(string id, string text, bool def = true)
        {
            Settings.SetBool("esp." + id, Ui.SwitchRow(text, Opt(id, def)));
        }

        public static void Draw()
        {
            if (!On || Event.current.type != EventType.Repaint || !Game.InWorld)
            {
                return;
            }

            var cam = Game.Camera;

            if (!cam)
            {
                return;
            }

            label ??= new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.UpperCenter, richText = true, wordWrap = false };

            if (Time.unscaledTime > rendererCacheAt)
            {
                rendererCacheAt = Time.unscaledTime + 10f;
                renderers.Clear();
            }

            var me = Game.LocalPosition;
            float max = MaxDistance;
            bool boxes = Opt("boxes"), tracers = Opt("tracers", false), dist = Opt("distance");

            if (Opt("extractions"))
            {
                foreach (var ep in WorldCache.Extractions)
                {
                    if (ep)
                    {
                        Mark(cam, ep.gameObject, ep.transform.position + Vector3.up, "Extraction  " + ep.currentState, ExtractColor, me, max, false, tracers, dist);
                    }
                }
            }

            if (Opt("carts"))
            {
                foreach (var cart in WorldCache.Carts)
                {
                    if (cart && cart.gameObject.activeInHierarchy)
                    {
                        Mark(cam, cart.gameObject, cart.transform.position + Vector3.up * 0.5f, "Cart", CartColor, me, max, boxes, tracers, dist);
                    }
                }
            }

            if (Opt("valuables"))
            {
                float min = MinValue;

                foreach (var v in WorldCache.Valuables)
                {
                    if (!v || !v.gameObject.activeInHierarchy || v.dollarValueCurrent < min)
                    {
                        continue;
                    }

                    var pos = v.physGrabObject ? v.physGrabObject.centerPoint : v.transform.position;
                    Mark(cam, v.gameObject, pos, "$" + Mathf.RoundToInt(v.dollarValueCurrent).ToString("N0"), ValueColor(v.dollarValueCurrent), me, max, boxes, tracers, dist);
                }
            }

            if (Opt("items"))
            {
                foreach (var it in WorldCache.Items)
                {
                    if (!it || !it.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    var name = string.IsNullOrEmpty(it.itemName) ? (it.item ? Catalog.ItemName(it.item) : "Item") : it.itemName;
                    Mark(cam, it.gameObject, it.transform.position, name, ItemColor, me, max, boxes, tracers, dist);
                }
            }

            if (Opt("players"))
            {
                foreach (var p in Game.Players)
                {
                    if (!p || p.isLocal)
                    {
                        continue;
                    }

                    if (p.deadSet)
                    {
                        if (p.playerDeathHead && p.playerDeathHead.physGrabObject)
                        {
                            Mark(cam, null, p.playerDeathHead.physGrabObject.centerPoint, Game.PlayerName(p) + " (dead)", DeadColor, me, max, false, tracers, dist);
                        }

                        continue;
                    }

                    var hp = p.playerHealth ? $"  <color=#9FE8A0>{p.playerHealth.health} HP</color>" : "";
                    var go = p.playerAvatarVisuals ? p.playerAvatarVisuals.gameObject : p.gameObject;
                    Mark(cam, go, p.transform.position + Vector3.up * 1.9f, Game.PlayerName(p) + hp, PlayerColor, me, max, boxes, tracers, dist);
                }
            }

            if (Opt("enemies"))
            {
                foreach (var e in WorldCache.Enemies)
                {
                    if (!WorldCache.EnemyActive(e))
                    {
                        continue;
                    }

                    var body = WorldCache.EnemyBody(e);
                    // only the host's game counts enemy health; a guest's copy never changes
                    var hp = Game.IsHost && e.Enemy.HasHealth && e.Enemy.Health ? $"  <color=#FFB0A0>{e.Enemy.Health.healthCurrent} HP</color>" : "";
                    Mark(cam, e.Enemy.gameObject, body.position + Vector3.up * 0.6f, EnemyName(e) + hp, EnemyColor, me, max, boxes, tracers, dist);
                }
            }
        }

        public static string EnemyName(EnemyParent e)
        {
            if (!e)
            {
                return "?";
            }

            if (!string.IsNullOrEmpty(e.enemyName))
            {
                return e.enemyName;
            }

            return e.name.Replace("(Clone)", "").Trim();
        }

        private static Color ValueColor(float v)
        {
            if (v >= 10000f) return new Color(1f, 0.55f, 0.95f);
            if (v >= 5000f) return new Color(1f, 0.8f, 0.25f);
            if (v >= 1000f) return new Color(0.6f, 1f, 0.45f);
            return new Color(0.75f, 0.85f, 0.75f);
        }

        private static bool Project(Camera cam, Vector3 world, out Vector2 screen)
        {
            var vp = cam.WorldToViewportPoint(world);
            screen = new Vector2(vp.x * Screen.width, (1f - vp.y) * Screen.height);
            return vp.z > 0.05f && vp.x > -0.2f && vp.x < 1.2f && vp.y > -0.2f && vp.y < 1.2f;
        }

        private static void Mark(Camera cam, GameObject go, Vector3 anchor, string text, Color color, Vector3 me, float max, bool box, bool tracer, bool showDist)
        {
            float d = Vector3.Distance(me, anchor);

            if (d > max || !Project(cam, anchor, out var at))
            {
                return;
            }

            if (box && go && TryScreenBox(cam, go, out var rect))
            {
                DrawBox(rect, color);
                at = new Vector2(rect.center.x, rect.yMin - 16);
            }

            if (tracer)
            {
                DrawLine(new Vector2(Screen.width / 2f, Screen.height), at, new Color(color.r, color.g, color.b, 0.55f), 1.2f);
            }

            var content = showDist ? $"{text}  <color=#C8D2DC>{d:0}m</color>" : text;
            var r = new Rect(at.x - 120, at.y - 2, 240, 20);
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.85f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), StripColors(content), label);
            GUI.color = Color.white;
            GUI.Label(r, "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + content + "</color>", label);
            GUI.color = old;
        }

        private static readonly Vector3[] corners = new Vector3[8];

        private static bool TryScreenBox(Camera cam, GameObject go, out Rect rect)
        {
            rect = default;
            int id = go.GetInstanceID();

            if (!renderers.TryGetValue(id, out var list))
            {
                list = go.GetComponentsInChildren<Renderer>(false);
                renderers[id] = list;
            }

            bool any = false;
            var b = new Bounds();

            foreach (var r in list)
            {
                if (!r || !r.enabled || r is ParticleSystemRenderer)
                {
                    continue;
                }

                if (!any)
                {
                    b = r.bounds;
                    any = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            if (!any || b.size.sqrMagnitude > 400f)
            {
                return false;
            }

            var c = b.center;
            var e = b.extents;
            corners[0] = c + new Vector3(e.x, e.y, e.z);
            corners[1] = c + new Vector3(e.x, e.y, -e.z);
            corners[2] = c + new Vector3(e.x, -e.y, e.z);
            corners[3] = c + new Vector3(e.x, -e.y, -e.z);
            corners[4] = c + new Vector3(-e.x, e.y, e.z);
            corners[5] = c + new Vector3(-e.x, e.y, -e.z);
            corners[6] = c + new Vector3(-e.x, -e.y, e.z);
            corners[7] = c + new Vector3(-e.x, -e.y, -e.z);

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;

            foreach (var corner in corners)
            {
                var vp = cam.WorldToViewportPoint(corner);

                if (vp.z <= 0.05f)
                {
                    return false;
                }

                float x = vp.x * Screen.width, y = (1f - vp.y) * Screen.height;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }

            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return rect.width > 2 && rect.height > 2 && rect.width < Screen.width * 1.5f;
        }

        private static void DrawBox(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.6f);
            Outline(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), 3);
            GUI.color = c;
            Outline(r, 1);
            GUI.color = old;
        }

        private static void Outline(Rect r, float t)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Theme.White);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Theme.White);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Theme.White);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Theme.White);
        }

        private static void DrawLine(Vector2 a, Vector2 b, Color c, float width)
        {
            var old = GUI.color;
            var matrix = GUI.matrix;
            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            float len = (b - a).magnitude;
            GUI.color = c;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width / 2f, len, width), Theme.White);
            GUI.matrix = matrix;
            GUI.color = old;
        }

        private static string StripColors(string s) => System.Text.RegularExpressions.Regex.Replace(s, "</?color[^>]*>", "");
    }
}
