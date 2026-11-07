using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace REPOMenu.Core
{
    // Everything the menu can pick from (items, valuables, enemies, levels, upgrades, cosmetics,
    // colours) is found at runtime, never typed into the menu. Each list is gathered from every
    // place the game keeps that kind of thing: its registries, every field of the director objects
    // that holds one (found by type, not by name, so a renamed or new list is still read), and every
    // loaded asset of that type. New game content and content added by other mods show up by
    // themselves.
    internal static class Catalog
    {
        private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public sealed class ValuableEntry
        {
            public PrefabRef Ref;
            public string Name;
            public string Size;
            public string Level;
        }

        private static List<Item> items = new();
        private static List<EnemySetup> enemies = new();
        private static List<Level> levels = new();
        private static List<ValuableEntry> valuables = new();
        private static List<string> upgrades = new();
        private static float itemsAt, enemiesAt, levelsAt, valuablesAt, upgradesAt;

        private const float Refresh = 8f;

        // ------------------------------------------------------------------ items

        public static List<Item> Items
        {
            get
            {
                if (Time.unscaledTime < itemsAt)
                {
                    return items;
                }

                itemsAt = Time.unscaledTime + Refresh;
                items = Safe.Get("catalog.items", () =>
                {
                    var found = new List<Item>();

                    if (StatsManager.instance && StatsManager.instance.itemDictionary != null)
                    {
                        // early on the dictionary can still be empty: fill it the game's own way
                        if (StatsManager.instance.itemDictionary.Count == 0)
                        {
                            Safe.Run("catalog.loaditems", StatsManager.instance.LoadItemsFromFolder);
                        }

                        found.AddRange(StatsManager.instance.itemDictionary.Values);
                    }

                    if (StatsManager.instance)
                    {
                        found.AddRange(ObjectsInFields<Item>(StatsManager.instance));
                    }

                    if (ShopManager.instance)
                    {
                        found.AddRange(ObjectsInFields<Item>(ShopManager.instance));
                    }

                    found.AddRange(Resources.FindObjectsOfTypeAll<Item>());

                    return found.Where(i => i && i.prefab != null && i.prefab.IsValid())
                        .Distinct()
                        .GroupBy(i => i.prefab.ResourcePath)
                        .Select(g => g.First())
                        .OrderBy(ItemName, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }, items);

                return items;
            }
        }

        public static string ItemName(Item i)
        {
            if (!i)
            {
                return "?";
            }

            if (!string.IsNullOrEmpty(i.itemName) && i.itemName != "N/A")
            {
                return i.itemName;
            }

            return Regex.Replace(i.name, "^Item ", "");
        }

        // ------------------------------------------------------------------ enemies

        public static List<EnemySetup> Enemies
        {
            get
            {
                if (Time.unscaledTime < enemiesAt)
                {
                    return enemies;
                }

                enemiesAt = Time.unscaledTime + Refresh;
                enemies = Safe.Get("catalog.enemies", () =>
                {
                    var found = new List<EnemySetup>();

                    if (EnemyDirector.instance)
                    {
                        found.AddRange(ObjectsInFields<EnemySetup>(EnemyDirector.instance));
                    }

                    found.AddRange(Resources.FindObjectsOfTypeAll<EnemySetup>());

                    return found.Where(e => e && e.spawnObjects != null && e.spawnObjects.Any(s => s != null && s.IsValid()))
                        .Distinct()
                        .GroupBy(EnemyName)
                        .Select(g => g.First())
                        .OrderBy(EnemyName, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }, enemies);

                return enemies;
            }
        }

        public static string EnemyName(EnemySetup e)
        {
            if (!e)
            {
                return "?";
            }

            var n = Regex.Replace(e.name, "^Enemy (- )?", "");
            return string.IsNullOrEmpty(n) ? e.name : n;
        }

        // ------------------------------------------------------------------ levels

        public static List<Level> Levels
        {
            get
            {
                if (Time.unscaledTime < levelsAt)
                {
                    return levels;
                }

                levelsAt = Time.unscaledTime + Refresh;
                levels = Safe.Get("catalog.levels", () =>
                {
                    var found = new List<Level>();

                    if (RunManager.instance)
                    {
                        found.AddRange(ObjectsInFields<Level>(RunManager.instance));
                    }

                    found.AddRange(Resources.FindObjectsOfTypeAll<Level>());

                    return found.Where(l => l)
                        .Distinct()
                        .OrderBy(LevelName, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }, levels);

                return levels;
            }
        }

        public static string LevelName(Level l)
        {
            if (!l)
            {
                return "?";
            }

            var n = Regex.Replace(l.name, "^Level - ", "");
            return string.IsNullOrEmpty(l.NarrativeName) || l.NarrativeName == n ? n : $"{n} ({l.NarrativeName})";
        }

        // the levels a run can be sent to: normal levels first, then shop, arena, lobby
        public static List<Level> PlayableLevels
        {
            get
            {
                var rm = RunManager.instance;

                if (!rm)
                {
                    return new List<Level>();
                }

                var skip = new HashSet<Level> { rm.levelMainMenu, rm.levelLobbyMenu, rm.levelSplashScreen, rm.levelRecording, rm.levelTutorial };
                return Levels.Where(l => !skip.Contains(l)).ToList();
            }
        }

        // ------------------------------------------------------------------ valuables

        public static List<ValuableEntry> Valuables
        {
            get
            {
                if (Time.unscaledTime < valuablesAt)
                {
                    return valuables;
                }

                valuablesAt = Time.unscaledTime + Refresh;
                valuables = Safe.Get("catalog.valuables", () =>
                {
                    var byPath = new Dictionary<string, ValuableEntry>();

                    foreach (var level in Levels)
                    {
                        if (level.ValuablePresets == null)
                        {
                            continue;
                        }

                        foreach (var preset in level.ValuablePresets)
                        {
                            if (!preset)
                            {
                                continue;
                            }

                            // every list of prefabs on the preset, whatever the size categories are called
                            foreach (var field in preset.GetType().GetFields(AllInstance))
                            {
                                if (!typeof(IEnumerable).IsAssignableFrom(field.FieldType) || field.FieldType == typeof(string))
                                {
                                    continue;
                                }

                                if (field.GetValue(preset) is not IEnumerable list)
                                {
                                    continue;
                                }

                                foreach (var o in list)
                                {
                                    if (o is PrefabRef r && r.IsValid() && !string.IsNullOrEmpty(r.ResourcePath) && !byPath.ContainsKey(r.ResourcePath))
                                    {
                                        byPath[r.ResourcePath] = new ValuableEntry
                                        {
                                            Ref = r,
                                            Name = Regex.Replace(r.PrefabName ?? "?", "^Valuable ", ""),
                                            Size = field.Name,
                                            Level = LevelName(level)
                                        };
                                    }
                                }
                            }
                        }
                    }

                    return byPath.Values.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
                }, valuables);

                return valuables;
            }
        }

        // ------------------------------------------------------------------ upgrades

        // every player upgrade the save knows about ("Health", "Speed", ... and any new ones)
        public static List<string> Upgrades
        {
            get
            {
                if (Time.unscaledTime < upgradesAt && upgrades.Count > 0)
                {
                    return upgrades;
                }

                upgradesAt = Time.unscaledTime + Refresh;
                upgrades = Safe.Get("catalog.upgrades", () =>
                {
                    if (!StatsManager.instance || StatsManager.instance.dictionaryOfDictionaries == null)
                    {
                        return new List<string>();
                    }

                    return StatsManager.instance.dictionaryOfDictionaries.Keys
                        .Where(k => k.StartsWith("playerUpgrade"))
                        .Select(k => k.Substring("playerUpgrade".Length))
                        .Where(k => k.Length > 0)
                        .OrderBy(k => k)
                        .ToList();
                }, upgrades);

                return upgrades;
            }
        }

        public static int UpgradeLevel(string upgrade, string steamId)
        {
            if (!StatsManager.instance || string.IsNullOrEmpty(steamId))
            {
                return 0;
            }

            return StatsManager.instance.dictionaryOfDictionaries.TryGetValue("playerUpgrade" + upgrade, out var d) && d.TryGetValue(steamId, out var v) ? v : 0;
        }

        // ------------------------------------------------------------------ cosmetics

        // The game's own master list; a cosmetic's id is its index in it.
        public static List<CosmeticAsset> Cosmetics => MetaManager.instance && MetaManager.instance.cosmeticAssets != null ? MetaManager.instance.cosmeticAssets : new List<CosmeticAsset>();

        public static string CosmeticName(CosmeticAsset c)
        {
            if (!c)
            {
                return "?";
            }

            if (!string.IsNullOrEmpty(c.assetName))
            {
                return c.assetName;
            }

            return Regex.Replace(c.name, "^Cosmetic - ", "");
        }

        public static List<SemiColor> Colors => MetaManager.instance && MetaManager.instance.colors != null ? MetaManager.instance.colors : new List<SemiColor>();

        public static void Invalidate()
        {
            itemsAt = enemiesAt = levelsAt = valuablesAt = upgradesAt = 0f;
        }

        // ------------------------------------------------------------------ helpers

        // Every T held by an object's fields: single references, lists and arrays, public or not.
        public static IEnumerable<T> ObjectsInFields<T>(object owner) where T : UnityEngine.Object
        {
            if (owner == null)
            {
                yield break;
            }

            foreach (var field in owner.GetType().GetFields(AllInstance))
            {
                object value;

                try
                {
                    value = field.GetValue(owner);
                }
                catch
                {
                    continue;
                }

                if (value is T single)
                {
                    if (single)
                    {
                        yield return single;
                    }

                    continue;
                }

                if (value is IEnumerable list && value is not string && ElementIs<T>(field.FieldType))
                {
                    foreach (var o in list)
                    {
                        if (o is T t && t)
                        {
                            yield return t;
                        }
                    }
                }
            }
        }

        private static bool ElementIs<T>(Type type)
        {
            if (type.IsArray)
            {
                return typeof(T).IsAssignableFrom(type.GetElementType());
            }

            if (type.IsGenericType)
            {
                var args = type.GetGenericArguments();
                return args.Any(a => typeof(T).IsAssignableFrom(a));
            }

            return false;
        }
    }
}
