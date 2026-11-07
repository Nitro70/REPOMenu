using System.Collections.Generic;
using UnityEngine;

namespace REPOMenu.Core
{
    // Lists of things in the level, refreshed a few times a second instead of searched every frame.
    // Entries can be destroyed between refreshes, so every reader checks them with Unity's null test.
    internal static class WorldCache
    {
        public static readonly List<EnemyParent> Enemies = new();
        public static readonly List<ValuableObject> Valuables = new();
        public static readonly List<ItemAttributes> Items = new();
        public static readonly List<PhysGrabCart> Carts = new();
        public static readonly List<ExtractionPoint> Extractions = new();

        private static float next;

        public static void Tick()
        {
            if (Time.unscaledTime < next)
            {
                return;
            }

            next = Time.unscaledTime + 0.75f;

            if (!Game.InWorld)
            {
                Clear();
                return;
            }

            Safe.Run("cache.enemies", () =>
            {
                Enemies.Clear();

                if (EnemyDirector.instance && EnemyDirector.instance.enemiesSpawned != null)
                {
                    foreach (var e in EnemyDirector.instance.enemiesSpawned)
                    {
                        if (e)
                        {
                            Enemies.Add(e);
                        }
                    }
                }
            });

            Safe.Run("cache.valuables", () =>
            {
                Valuables.Clear();
                Valuables.AddRange(Object.FindObjectsByType<ValuableObject>(FindObjectsSortMode.None));
            });

            Safe.Run("cache.items", () =>
            {
                Items.Clear();
                Items.AddRange(Object.FindObjectsByType<ItemAttributes>(FindObjectsSortMode.None));
            });

            Safe.Run("cache.carts", () =>
            {
                Carts.Clear();
                Carts.AddRange(Object.FindObjectsByType<PhysGrabCart>(FindObjectsSortMode.None));
            });

            Safe.Run("cache.extractions", () =>
            {
                Extractions.Clear();
                Extractions.AddRange(Object.FindObjectsByType<ExtractionPoint>(FindObjectsSortMode.None));
            });
        }

        public static void ForceRefresh() => next = 0f;

        private static void Clear()
        {
            Enemies.Clear();
            Valuables.Clear();
            Items.Clear();
            Carts.Clear();
            Extractions.Clear();
        }

        // the enemy's live body: the parent object stays put while the Enemy child moves around
        public static Transform EnemyBody(EnemyParent p)
        {
            if (!p || !p.Enemy)
            {
                return null;
            }

            return p.Enemy.CenterTransform ? p.Enemy.CenterTransform : p.Enemy.transform;
        }

        public static bool EnemyActive(EnemyParent p) => p && p.Spawned && p.Enemy && p.Enemy.gameObject.activeInHierarchy;
    }
}
