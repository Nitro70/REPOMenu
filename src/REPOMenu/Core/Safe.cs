using System;
using System.Collections.Generic;

namespace REPOMenu.Core
{
    // Runs feature code so one broken feature (for example after a game update renamed something)
    // can never take the menu or the game down. Each failure is logged once per feature, and a
    // feature that keeps failing is marked broken so its card says so.
    internal static class Safe
    {
        private static readonly Dictionary<string, int> failures = new();
        private static readonly HashSet<string> logged = new();

        public static readonly Dictionary<string, string> Broken = new();

        public static void Run(string id, Action action)
        {
            if (Broken.ContainsKey(id))
            {
                return;
            }

            try
            {
                action();
            }
            catch (Exception e)
            {
                Fail(id, e);
            }
        }

        public static T Get<T>(string id, Func<T> func, T fallback = default)
        {
            try
            {
                return func();
            }
            catch (Exception e)
            {
                Fail(id, e);
                return fallback;
            }
        }

        public static bool IsBroken(string id) => Broken.ContainsKey(id);

        private static void Fail(string id, Exception e)
        {
            failures.TryGetValue(id, out var n);
            failures[id] = ++n;

            // missing members mean the game changed under us: stop calling it
            if (e is MissingMethodException || e is MissingFieldException || e is TypeLoadException || n >= 20)
            {
                if (!Broken.ContainsKey(id))
                {
                    Broken[id] = e.GetType().Name + ": " + e.Message;
                    Plugin.Log.LogError($"[{id}] turned off after errors (game update?): {e}");
                }
                return;
            }

            if (logged.Add(id + e.GetType().Name))
            {
                Plugin.Log.LogWarning($"[{id}] {e}");
            }
        }

        public static void Reset(string id)
        {
            failures.Remove(id);
            Broken.Remove(id);
        }
    }
}
