using System.Linq;
using Photon.Pun;
using REPOMenu.Core;
using REPOMenu.UI;
using UnityEngine;

namespace REPOMenu.Features
{
    // Spawning items and valuables, the way the game's own /spawn command does it. Host only: the
    // game only lets the host create shared objects. Both lists are found at runtime (every item the
    // game has registered or loaded, every valuable in every level's presets), so new content and
    // content from other mods shows up by itself.
    internal static class Spawner
    {
        private static readonly Ui.Picker<Item> itemPicker = new();
        private static readonly Ui.Picker<Catalog.ValuableEntry> valuablePicker = new();
        private static int itemCount = 1, valuableCount = 1;
        private static int valuablePrice;

        private static string NeedLevel()
        {
            if (!Game.InWorld)
            {
                return "Load into a level, the shop or the truck first";
            }

            return null;
        }

        public static void Register()
        {
            var tab = Registry.Tab("Spawner", "✚", "Spawn any item or valuable in front of you. Host only. Lists come from the game itself, so new and modded things appear here too.");

            tab.Panel("sp.items", "Items and weapons", "Every item the game knows about. Pick one, choose how many, spawn.", Scope.Host, ItemsBody).When(NeedLevel);
            tab.Panel("sp.valuables", "Valuables", "Every valuable from every level. They get a value the game's normal way, or one you set.", Scope.Host, ValuablesBody).When(NeedLevel);
            tab.Action("sp.allitems", "Every item, laid out around you", "The game's own /itemsAll: puts one of each item on the floor around you.", Scope.Host, "Spawn all", () =>
            {
                int n = DebugCommandHandler.instance ? DebugCommandHandler.instance.ItemFloorSpawn(PlayerAvatar.instance) : 0;
                Toasts.Show(n == 0 ? "No room here (or already busy)" : $"Spawned {n} items");
            }).When(NeedLevel);
        }

        private static void ItemsBody()
        {
            itemPicker.Draw(Catalog.Items, Catalog.ItemName, 200, i => i.itemType.ToString());
            itemCount = Ui.IntSlider("How many", itemCount, 1, 20);

            var was = GUI.enabled;
            GUI.enabled = was && itemPicker.Selected;

            if (Ui.Button(itemPicker.Selected ? "Spawn " + Catalog.ItemName(itemPicker.Selected) : "Pick an item above", 260, Theme.ButtonAccent) && itemPicker.Selected)
            {
                for (int i = 0; i < itemCount; i++)
                {
                    SpawnItem(itemPicker.Selected, i);
                }

                Toasts.Show($"Spawned {itemCount} x {Catalog.ItemName(itemPicker.Selected)}");
            }

            GUI.enabled = was;
        }

        private static void ValuablesBody()
        {
            valuablePicker.Draw(Catalog.Valuables, v => v.Name, 200, v => v.Size);
            valuableCount = Ui.IntSlider("How many", valuableCount, 1, 20);
            valuablePrice = Ui.IntField("Value in $ (0 = the game decides)", valuablePrice, 110);

            var was = GUI.enabled;
            GUI.enabled = was && valuablePicker.Selected != null;

            if (Ui.Button(valuablePicker.Selected != null ? "Spawn " + valuablePicker.Selected.Name : "Pick a valuable above", 260, Theme.ButtonAccent) && valuablePicker.Selected != null)
            {
                for (int i = 0; i < valuableCount; i++)
                {
                    SpawnValuable(valuablePicker.Selected, i, valuablePrice);
                }

                Toasts.Show($"Spawned {valuableCount} x {valuablePicker.Selected.Name}");
            }

            GUI.enabled = was;
        }

        // in front of you, spread out a little so a stack does not explode
        private static Vector3 SpotFor(int index)
        {
            var cam = Game.Camera;
            var basePos = Game.AimPoint(3.5f, 0.5f);

            if (index == 0 || !cam)
            {
                return basePos + Vector3.up * 0.4f;
            }

            float a = index * 2.4f;
            return basePos + new Vector3(Mathf.Cos(a), 0.4f + index * 0.15f, Mathf.Sin(a)) * 0.6f;
        }

        public static GameObject SpawnItem(Item item, int index = 0)
        {
            var pos = SpotFor(index);
            var rot = Quaternion.identity;

            return Game.Multiplayer
                ? PhotonNetwork.InstantiateRoomObject(item.prefab.ResourcePath, pos, rot, 0)
                : Object.Instantiate(item.prefab.Prefab, pos, rot);
        }

        public static void SpawnValuable(Catalog.ValuableEntry entry, int index = 0, int price = 0)
        {
            var pos = SpotFor(index);
            var go = Game.Multiplayer
                ? PhotonNetwork.InstantiateRoomObject(entry.Ref.ResourcePath, pos, Quaternion.identity, 0)
                : Object.Instantiate(entry.Ref.Prefab, pos, Quaternion.identity);

            var v = go ? go.GetComponent<ValuableObject>() : null;

            if (!v)
            {
                return;
            }

            // the game's own override field: its start-up code then uses this price and sends it
            // to everyone (DollarValueSet coroutine), exactly like a value it rolled itself
            if (price > 0)
            {
                v.dollarValueOverride = price;
            }

            v.DollarValueSetLogic();

            WorldCache.ForceRefresh();
        }
    }
}
