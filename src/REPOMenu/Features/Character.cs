using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using REPOMenu.Core;
using REPOMenu.UI;
using UnityEngine;

namespace REPOMenu.Features
{
    // Cosmetics and colours, changed live: the game's own equip code runs and its own sync sends the
    // new look to everyone (a buffered RPC, so people joining later see it too). No new round needed.
    //
    // Every cosmetic, slot type, rarity and colour listed here comes from the game at runtime
    // (MetaManager's lists and enums), so new cosmetics appear without a menu update.
    // R.E.P.O. cosmetics are earned in game and saved in MetaSave.es3 on this PC; none are sold.
    internal static class Character
    {
        public static bool Rainbow, UnlockOnEquip = true;

        private static readonly Ui.Picker<CosmeticAsset> picker = new();
        private static string typeFilter = "All";
        private static int colorSlot = -1;
        private static Vector2 swatchScroll;
        private static int[] rainbowSaved;
        private static int rainbowIndex;
        private static float rainbowAt;

        private static float RainbowDelay => Settings.Float("char.rainbow", 0.6f);

        // a cosmetic's id is its index in the game's list; looked up once per frame, not per row
        private static readonly Dictionary<CosmeticAsset, int> ids = new();
        private static int idsFrame = -1;

        private static int Id(CosmeticAsset c)
        {
            if (idsFrame != Time.frameCount)
            {
                idsFrame = Time.frameCount;
                ids.Clear();
                var all = Catalog.Cosmetics;

                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] && !ids.ContainsKey(all[i]))
                    {
                        ids[all[i]] = i;
                    }
                }
            }

            return c && ids.TryGetValue(c, out var id) ? id : -1;
        }

        private static string NeedMeta()
        {
            if (!MetaManager.instance || !MetaManager.instance.saveReady)
            {
                return "The cosmetics save is still loading";
            }

            return null;
        }

        public static void Register()
        {
            var tab = Registry.Tab("Character", "✦", "Cosmetics and colours, changed live in the menu or mid-round. Everyone sees the new look straight away.");

            tab.Panel("char.wardrobe", "Wardrobe", "Every cosmetic in the game. Click one to put it on or take it off. Green = you have it on.", Scope.Client, WardrobeBody).When(NeedMeta);
            tab.Panel("char.colors", "Colours", "Pick a slot, then a colour. Changes show at once.", Scope.Client, ColorsBody).When(NeedMeta);
            tab.Switch("char.rainbow", "Rainbow", "Cycles every colour slot through all the game's colours. Your saved colours come back when you turn it off.", Scope.Client, () => Rainbow, SetRainbow)
                .When(NeedMeta)
                .WithBody(() => Settings.SetFloat("char.rainbow", Ui.Slider("Seconds per colour", RainbowDelay, 0.3f, 3f, "0.0", " s")));
            tab.Action("char.unlockall", "Unlock every cosmetic", "Uses the game's own unlock-all (its developer cheat). Your cosmetics save is backed up first; \"Restore cosmetics backup\" puts it back.", Scope.Client, "Unlock all", UnlockAll).When(NeedMeta);
            tab.Action("char.restore", "Restore cosmetics backup", "Puts back your cosmetics save files (MetaSave*.es3) as they were before the last unlock from this menu. Backups are in the save folder under REPOMenu-backups.", Scope.Client, "Restore", RestoreBackup).When(NeedMeta);
            tab.Panel("char.outfit", "Copy / paste outfit", "Copies your outfit to the clipboard in the game's own /cosmetic import format, or puts one on from the clipboard.", Scope.Client, OutfitBody).When(NeedMeta);
            tab.Panel("char.tokens", "Cosmetic tokens", "Adds tokens for the cosmetic machine, by rarity.", Scope.Client, TokensBody).When(NeedMeta);
        }

        // ---------------------------------------------------------------- wardrobe

        private static void WardrobeBody()
        {
            var mm = MetaManager.instance;
            var all = Catalog.Cosmetics;
            var types = new List<string> { "All", "On me", "Locked" };
            types.AddRange(all.Where(c => c).Select(c => c.type.ToString()).Distinct().OrderBy(t => t));

            GUILayout.Label($"{mm.cosmeticUnlocks.Count} of {all.Count(c => c)} unlocked, {mm.cosmeticEquipped.Count} worn", Theme.LabelSubtle);

            int clickedType = Ui.ButtonGrid(types, types.IndexOf(typeFilter), 112, 600);

            if (clickedType >= 0)
            {
                typeFilter = types[clickedType];
            }

            GUILayout.BeginHorizontal();
            UnlockOnEquip = Ui.SwitchRow("Unlock a locked one when I put it on (saved)", UnlockOnEquip);
            GUILayout.EndHorizontal();

            IEnumerable<CosmeticAsset> source = all.Where(c => c);

            if (typeFilter == "On me")
            {
                source = source.Where(c => mm.cosmeticEquipped.Contains(Id(c)));
            }
            else if (typeFilter == "Locked")
            {
                source = source.Where(c => !mm.cosmeticUnlocks.Contains(Id(c)));
            }
            else if (typeFilter != "All")
            {
                source = source.Where(c => c.type.ToString() == typeFilter);
            }

            var clicked = picker.Draw(source, c => Catalog.CosmeticName(c), 230,
                c => $"{c.type}  ·  {(mm.cosmeticUnlocks.Contains(Id(c)) ? c.rarity.ToString() : "<color=#E8A860>locked</color>")}",
                c => mm.cosmeticEquipped.Contains(Id(c)));

            if (clicked)
            {
                ToggleCosmetic(clicked);
            }

            GUILayout.BeginHorizontal();

            if (Ui.Button("Take everything off", 160))
            {
                foreach (var id in mm.cosmeticEquipped.ToList())
                {
                    if (id >= 0 && id < all.Count && all[id])
                    {
                        mm.CosmeticUnequip(all[id], false);
                    }
                }

                ApplyLive();
            }

            if (Ui.Button("Random outfit", 130))
            {
                RandomOutfit();
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static void ToggleCosmetic(CosmeticAsset c)
        {
            var mm = MetaManager.instance;
            int id = Catalog.Cosmetics.IndexOf(c);

            if (id < 0)
            {
                return;
            }

            if (mm.cosmeticEquipped.Contains(id))
            {
                mm.CosmeticUnequip(c, false);
                ApplyLive();
                return;
            }

            if (!mm.cosmeticUnlocks.Contains(id))
            {
                if (!UnlockOnEquip)
                {
                    Toasts.Show(Catalog.CosmeticName(c) + " is locked (turn on \"unlock when I put it on\")");
                    return;
                }

                BackupOnce();
                mm.CosmeticUnlock(c);
            }

            if (mm.CosmeticEquip(c, false))
            {
                ApplyLive();
                Toasts.Show("Wearing " + Catalog.CosmeticName(c));
            }
        }

        private static void RandomOutfit()
        {
            var mm = MetaManager.instance;
            var all = Catalog.Cosmetics;
            var owned = mm.cosmeticUnlocks.Where(i => i >= 0 && i < all.Count && all[i]).Select(i => all[i]).ToList();

            foreach (var id in mm.cosmeticEquipped.ToList())
            {
                if (id >= 0 && id < all.Count && all[id])
                {
                    mm.CosmeticUnequip(all[id], false);
                }
            }

            // one random owned cosmetic per slot type; the game itself removes clashing ones
            foreach (var group in owned.GroupBy(c => c.type))
            {
                var list = group.ToList();
                mm.CosmeticEquip(list[UnityEngine.Random.Range(0, list.Count)], false);
            }

            var colors = Catalog.Colors;

            for (int i = 0; i < mm.colorsEquipped.Length && colors.Count > 0; i++)
            {
                mm.CosmeticColorSet(i, UnityEngine.Random.Range(0, colors.Count));
            }

            ApplyLive();
            Toasts.Show("Random outfit");
        }

        // the game's own steps after an equip (same as its /cosmetic import command)
        public static void ApplyLive(bool save = true)
        {
            var mm = MetaManager.instance;

            if (save)
            {
                mm.Save();
            }

            mm.CosmeticPreviewSet(false);
            mm.CosmeticPlayerUpdateLocal(true);
        }

        // ---------------------------------------------------------------- colours

        private static void ColorsBody()
        {
            var mm = MetaManager.instance;
            var colors = Catalog.Colors;
            var slots = Enum.GetNames(typeof(SemiFunc.CosmeticType)).Take(mm.colorsEquipped.Length).ToList();
            slots.Insert(0, "Every slot");

            int clicked = Ui.ButtonGrid(slots, colorSlot + 1, 118, 600);

            if (clicked >= 0)
            {
                colorSlot = clicked - 1;
            }

            swatchScroll = GUILayout.BeginScrollView(swatchScroll, GUILayout.Height(96));
            int perRow = 16;

            for (int i = 0; i < colors.Count; i += perRow)
            {
                GUILayout.BeginHorizontal();

                for (int j = i; j < Mathf.Min(colors.Count, i + perRow); j++)
                {
                    var c = colors[j];
                    var r = GUILayoutUtility.GetRect(26, 26, GUILayout.Width(26), GUILayout.Height(26));
                    bool current = colorSlot >= 0 && colorSlot < mm.colorsEquipped.Length && mm.colorsEquipped[colorSlot] == j;

                    if (Event.current.type == EventType.Repaint)
                    {
                        var old = GUI.color;
                        GUI.color = current ? Color.white : new Color(0.1f, 0.12f, 0.15f);
                        GUI.DrawTexture(r, Theme.Circle);
                        GUI.color = c ? c.color : Color.gray;
                        GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), Theme.Circle);
                        GUI.color = old;
                    }

                    if (GUI.Button(r, new GUIContent("", c ? c.colorName : ""), GUIStyle.none))
                    {
                        SetColor(j);
                    }
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private static void SetColor(int colorIndex)
        {
            var mm = MetaManager.instance;

            if (Rainbow)
            {
                SetRainbow(false);
            }

            for (int i = 0; i < mm.colorsEquipped.Length; i++)
            {
                if (colorSlot < 0 || colorSlot == i)
                {
                    mm.CosmeticColorSet(i, colorIndex);
                }
            }

            ApplyLive();
        }

        private static void SetRainbow(bool on)
        {
            var mm = MetaManager.instance;

            if (!mm)
            {
                Rainbow = false;
                return;
            }

            if (on && !Rainbow)
            {
                rainbowSaved = mm.colorsEquipped.ToArray();
            }
            else if (!on && Rainbow && rainbowSaved != null)
            {
                for (int i = 0; i < rainbowSaved.Length && i < mm.colorsEquipped.Length; i++)
                {
                    mm.CosmeticColorSet(i, rainbowSaved[i]);
                }

                rainbowSaved = null;
                ApplyLive(false);
            }

            Rainbow = on;
        }

        public static void Tick()
        {
            if (!Rainbow || Time.unscaledTime < rainbowAt)
            {
                return;
            }

            rainbowAt = Time.unscaledTime + Mathf.Max(0.3f, RainbowDelay);

            Safe.Run("char.rainbow", () =>
            {
                var mm = MetaManager.instance;
                var colors = Catalog.Colors;

                if (!mm || colors.Count == 0)
                {
                    return;
                }

                rainbowIndex = (rainbowIndex + 1) % colors.Count;

                for (int i = 0; i < mm.colorsEquipped.Length; i++)
                {
                    mm.colorsEquipped[i] = rainbowIndex;
                }

                // colours only, not saved to disk: the real ones come back when it is switched off
                var local = PlayerAvatar.instance;

                if (local && local.playerCosmetics && !local.playerCosmetics.firstSetup)
                {
                    local.playerCosmetics.SetupColors(true);
                }
                else
                {
                    mm.CosmeticPlayerUpdateLocal(false, true);
                }
            });
        }

        // ---------------------------------------------------------------- unlock + backup

        private static string backupMadeThisSession;

        private static string SaveDir => Path.GetDirectoryName(MetaManager.instance.SavePathFull);

        private static string BackupRoot => Path.Combine(SaveDir, "REPOMenu-backups");

        // every cosmetics save file: the game's MetaSave.es3 (+ .bak) and REPOLib's MetaSaveModded.es3
        private static string[] MetaFiles() => Directory.GetFiles(SaveDir, "MetaSave*");

        private static void BackupOnce()
        {
            if (backupMadeThisSession != null)
            {
                return;
            }

            try
            {
                var dir = Path.Combine(BackupRoot, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                Directory.CreateDirectory(dir);

                foreach (var f in MetaFiles())
                {
                    File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true);
                }

                backupMadeThisSession = dir;
                Plugin.Log.LogInfo("Cosmetics save backed up to " + dir);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not back up the cosmetics save: " + e.Message);
            }
        }

        private static void UnlockAll()
        {
            BackupOnce();
            bool changed = MetaManager.instance.CosmeticUnlockAll();
            ApplyLive();
            Toasts.Show(changed ? $"Unlocked all {Catalog.Cosmetics.Count(c => c)} cosmetics" : "You already had every cosmetic");
        }

        private static void RestoreBackup()
        {
            try
            {
                var latest = Directory.Exists(BackupRoot) ? Directory.GetDirectories(BackupRoot).OrderByDescending(d => d).FirstOrDefault() : null;

                if (latest == null)
                {
                    Toasts.Show("No backup from this menu yet");
                    return;
                }

                foreach (var f in Directory.GetFiles(latest))
                {
                    File.Copy(f, Path.Combine(SaveDir, Path.GetFileName(f)), true);
                }

                // the game reads the save again (other mods hooked into Load, like REPOLib, run too)
                MetaManager.instance.Load();
                ApplyLive(false);
                Toasts.Show("Cosmetics restored from " + Path.GetFileName(latest));
            }
            catch (Exception e)
            {
                Toasts.Show("Restore failed: " + e.Message);
            }
        }

        // ---------------------------------------------------------------- outfit + tokens

        private static void OutfitBody()
        {
            var mm = MetaManager.instance;
            GUILayout.BeginHorizontal();

            if (Ui.Button("Copy my outfit", 140))
            {
                GUIUtility.systemCopyBuffer = "/cosmetic import " + string.Join(",", mm.cosmeticEquipped) + "|" + string.Join(",", mm.colorsEquipped);
                Toasts.Show("Outfit copied");
            }

            if (Ui.Button("Wear outfit from clipboard", 200, Theme.ButtonAccent))
            {
                PasteOutfit(GUIUtility.systemCopyBuffer);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static void PasteOutfit(string text)
        {
            var mm = MetaManager.instance;
            var all = Catalog.Cosmetics;
            text = (text ?? "").Trim();

            if (text.StartsWith("/cosmetic import"))
            {
                text = text.Substring("/cosmetic import".Length).Trim();
            }

            var parts = text.Split('|');

            if (parts.Length != 2)
            {
                Toasts.Show("The clipboard does not hold an outfit");
                return;
            }

            var ids = parts[0].Split(',').Select(s => int.TryParse(s, out var v) ? v : -1).Where(v => v >= 0 && v < all.Count && all[v]).ToList();
            var cols = parts[1].Split(',').Select(s => int.TryParse(s, out var v) ? v : -1).ToList();

            foreach (var id in mm.cosmeticEquipped.ToList())
            {
                if (id >= 0 && id < all.Count && all[id])
                {
                    mm.CosmeticUnequip(all[id], false);
                }
            }

            int worn = 0;

            foreach (var id in ids)
            {
                if (!mm.cosmeticUnlocks.Contains(id))
                {
                    if (!UnlockOnEquip)
                    {
                        continue;
                    }

                    BackupOnce();
                    mm.CosmeticUnlock(all[id]);
                }

                if (mm.CosmeticEquip(all[id], false))
                {
                    worn++;
                }
            }

            for (int i = 0; i < cols.Count && i < mm.colorsEquipped.Length; i++)
            {
                if (cols[i] >= 0 && cols[i] < Catalog.Colors.Count)
                {
                    mm.CosmeticColorSet(i, cols[i]);
                }
            }

            ApplyLive();
            Toasts.Show($"Wearing {worn} of {ids.Count} cosmetics");
        }

        private static void TokensBody()
        {
            var mm = MetaManager.instance;
            var counts = Enum.GetValues(typeof(SemiFunc.Rarity)).Cast<SemiFunc.Rarity>()
                .Select(r => $"{r}: {mm.cosmeticTokens.Count(t => t == (int)r)}");
            GUILayout.Label("You have  " + string.Join("   ", counts), Theme.LabelSubtle);

            GUILayout.BeginHorizontal();

            foreach (SemiFunc.Rarity r in Enum.GetValues(typeof(SemiFunc.Rarity)))
            {
                if (Ui.Small("+1 " + r, 110))
                {
                    AddToken(r);
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static void AddToken(SemiFunc.Rarity rarity)
        {
            var mm = MetaManager.instance;

            if (CosmeticTokenUI.instance)
            {
                mm.CosmeticTokenAdd(rarity);
            }
            else
            {
                // CosmeticTokenAdd refreshes a UI that only exists in some scenes; do its other two steps
                mm.cosmeticTokens.Add((int)rarity);
                mm.Save();
            }

            Toasts.Show("+1 " + rarity + " token");
        }
    }
}
