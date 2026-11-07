using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace REPOMenuInstaller
{
    /// <summary>
    /// Copies the payload (BepInEx 5.4.23.6 + REPOMenu; see Payload) into the R.E.P.O. folder and
    /// removes it again. Only files listed in the install record are ever deleted.
    ///
    /// R.E.P.O. players usually already run BepInEx (for other mods). So the BepInEx loader is
    /// installed ONLY when the folder has none yet; when BepInEx is already there, only the plugin
    /// under BepInEx\plugins\REPOMenu\ is written, and the existing BepInEx, its config and its other
    /// plugins are never touched. Uninstall removes only what this installer recorded, so it never
    /// takes BepInEx away from a folder that already had it.
    /// </summary>
    internal static class ModInstaller
    {
        private const string GameProcess = "REPO";
        internal const string PluginDirZip = "BepInEx/plugins/REPOMenu/";
        private static readonly string PluginDirRel = Path.Combine("BepInEx", "plugins", "REPOMenu");
        private static readonly string RecordRel = Path.Combine(PluginDirRel, "installed-files.txt");
        private static readonly string ConfigRel = Path.Combine("BepInEx", "config", "com.nitro70.repomenu.cfg");
        private static readonly string BepInExCoreRel = Path.Combine("BepInEx", "core", "BepInEx.dll");
        private static readonly string WinhttpRel = "winhttp.dll";
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        internal static bool IsGameRunning()
        {
            Process[] running = Process.GetProcessesByName(GameProcess);
            foreach (Process p in running) p.Dispose();
            return running.Length > 0;
        }

        internal static bool IsInstalled(string game) => File.Exists(Path.Combine(game, RecordRel));

        internal static bool HasBepInEx(string game) =>
            File.Exists(Path.Combine(game, BepInExCoreRel)) || File.Exists(Path.Combine(game, WinhttpRel));

        internal static bool CanWrite(string dir)
        {
            string probe = Path.Combine(dir, "repomenu-write-test.tmp");
            try
            {
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch (UnauthorizedAccessException) { return false; }
            catch (IOException) { return false; }
        }

        internal static void Install(string game, Action<string> log, Stream payload)
        {
            RefuseWhileRunning();

            List<string> oldRecord = File.Exists(Path.Combine(game, RecordRel))
                ? File.ReadAllLines(Path.Combine(game, RecordRel)).Select(l => l.Trim()).Where(l => l.Length > 0).ToList()
                : new List<string>();

            // Whether to (re)write the BepInEx loader. On a folder with no BepInEx we install it. On
            // a folder that already has BepInEx we don't touch it, UNLESS a previous run of this
            // installer is the one that put BepInEx there (its record lists loader files): then those
            // files stay ours, so we keep refreshing them and keep them in the record for uninstall.
            bool weOwnBepInEx = oldRecord.Any(r => !r.Replace('\\', '/').StartsWith(PluginDirZip, StringComparison.OrdinalIgnoreCase));
            bool installLoader = !HasBepInEx(game) || weOwnBepInEx;
            log(installLoader
                ? (weOwnBepInEx ? "Refreshing the BepInEx this installer set up, and the REPOMenu plugin."
                                : "No BepInEx found; installing BepInEx 5 and the REPOMenu plugin.")
                : "BepInEx is already here; installing only the REPOMenu plugin and leaving BepInEx and your other mods alone.");

            var written = new List<string>();
            int mod = 0, loader = 0;

            using (var zip = new ZipArchive(payload, ZipArchiveMode.Read, leaveOpen: true))
            {
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    if (entry.FullName.EndsWith("/")) continue;
                    bool isPlugin = entry.FullName.StartsWith(PluginDirZip, StringComparison.OrdinalIgnoreCase);

                    if (!isPlugin && !installLoader)
                    {
                        continue;
                    }

                    string rel = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    string target = InsideFolder(game, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (Stream src = entry.Open())
                    using (var dst = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                        src.CopyTo(dst);
                    written.Add(rel);
                    if (isPlugin) mod++; else loader++;
                }
            }

            if (mod == 0) throw new InvalidOperationException("The installer is damaged: it has no REPOMenu plugin inside.");
            log(installLoader ? $"Copied {mod} plugin files and {loader} BepInEx files." : $"Copied {mod} plugin files.");

            RemoveStaleFiles(game, oldRecord, written, log);
            File.WriteAllLines(Path.Combine(game, RecordRel), written, Utf8NoBom);
            log("Installed. Start R.E.P.O. (through Steam) and press F2 to open the menu.");
            if (installLoader)
                log("The first launch takes a little longer while BepInEx sets itself up.");
        }

        internal static void Uninstall(string game, Action<string> log)
        {
            RefuseWhileRunning();
            string record = Path.Combine(game, RecordRel);
            if (!File.Exists(record))
                throw new InvalidOperationException("No REPOMenu install was found in this folder.");

            var recorded = File.ReadAllLines(record).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            int removed = 0;
            foreach (string rel in recorded)
            {
                string target = InsideFolder(game, rel);
                if (File.Exists(target)) { File.Delete(target); removed++; }
            }

            // our own saved settings, and the record itself
            string cfg = Path.Combine(game, ConfigRel);
            if (File.Exists(cfg)) { File.Delete(cfg); removed++; }

            string pluginDir = Path.Combine(game, PluginDirRel);
            if (Directory.Exists(pluginDir)) Directory.Delete(pluginDir, true);

            // tidy up only empty folders our loader files may have created; never delete a BepInEx
            // folder that still holds anyone else's files
            foreach (var dirRel in new[]
                     {
                         Path.Combine("BepInEx", "plugins"),
                         Path.Combine("BepInEx", "patchers"),
                         Path.Combine("BepInEx", "cache"),
                         Path.Combine("BepInEx", "core"),
                         Path.Combine("BepInEx", "config"),
                         "BepInEx",
                     })
            {
                string dir = Path.Combine(game, dirRel);
                if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0)
                    Directory.Delete(dir);
            }

            log($"Removed {removed} files that REPOMenu installed.");
            log(HasBepInEx(game)
                ? "BepInEx was left in place (it was already here or still has other mods)."
                : "R.E.P.O. is back to normal.");
        }

        /// <summary>Deletes files an earlier version installed that this version no longer has.</summary>
        private static void RemoveStaleFiles(string game, List<string> oldRecord, List<string> written, Action<string> log)
        {
            var current = new HashSet<string>(written, StringComparer.OrdinalIgnoreCase);
            int removed = 0;
            foreach (string rel in oldRecord)
            {
                if (current.Contains(rel)) continue;
                string target = InsideFolder(game, rel);
                if (File.Exists(target)) { File.Delete(target); removed++; }
            }
            if (removed > 0) log($"Removed {removed} files from the older version that are no longer used.");
        }

        internal static void RefuseWhileRunning()
        {
#if DEBUG
            // Debug builds only: lets tools/test_installer.py use a fake game folder while the real game runs.
            if (Environment.GetEnvironmentVariable("REPOMENU_TEST_IGNORE_RUNNING") == "1") return;
#endif
            if (IsGameRunning())
                throw new InvalidOperationException("R.E.P.O. is running. Close the game, then try again.");
        }

        /// <summary>Joins a relative path onto the game folder and refuses anything that escapes it.</summary>
        private static string InsideFolder(string root, string rel)
        {
            string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(rootFull, rel));
            if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing a path outside the game folder: " + rel);
            return full;
        }
    }
}
