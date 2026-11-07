"""Exercise the offline installer against fake R.E.P.O. folders, never the real game.

Checks: fresh install (BepInEx + plugin), reinstall changes nothing new, uninstall returns the
folder to exactly its original files, install into a folder that ALREADY has BepInEx touches only
the plugin and uninstall leaves that BepInEx intact, a non-game folder is refused, and uninstall
without an install is refused. Needs a Debug build of the installer (the running-game bypass and a
test only exist in Debug).
"""
import os
import shutil
import subprocess
import sys
import tempfile

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EXE = os.path.join(REPO, "build", "debug", "REPOMenu-Offline-Installer.exe")
ENV = dict(os.environ, REPOMENU_TEST_IGNORE_RUNNING="1")
PLUGIN = os.path.join("BepInEx", "plugins", "REPOMenu", "REPOMenu.dll")
WINHTTP = "winhttp.dll"


def run(game, action):
    log = os.path.join(game, "_log.txt")
    code = subprocess.call([EXE, "--game", game, "--" + action, "--quiet", "--log", log], env=ENV)
    text = open(log, encoding="utf-8").read() if os.path.exists(log) else ""
    if os.path.exists(log):
        os.remove(log)
    return code, text


def snapshot(folder):
    out = {}
    for root, _dirs, files in os.walk(folder):
        for name in files:
            full = os.path.join(root, name)
            out[os.path.relpath(full, folder)] = os.path.getsize(full)
    return out


def fake_game(extra=None):
    game = tempfile.mkdtemp(prefix="repo-test-")
    open(os.path.join(game, "REPO.exe"), "wb").write(b"MZ fake")
    os.makedirs(os.path.join(game, "REPO_Data"))
    open(os.path.join(game, "REPO_Data", "data.bin"), "wb").write(b"x" * 10)
    if extra:
        extra(game)
    return game


def check(name, ok):
    print(("PASS " if ok else "FAIL ") + name)
    return ok


def main():
    if not os.path.exists(EXE):
        sys.exit("build the Debug installer first: dotnet build installer -c Debug")
    passed = True

    # 1. fresh install on a vanilla folder
    game = fake_game()
    before = snapshot(game)
    code, text = run(game, "install")
    passed &= check("fresh install exit 0", code == 0)
    passed &= check("fresh install wrote the plugin", os.path.exists(os.path.join(game, PLUGIN)))
    passed &= check("fresh install wrote BepInEx loader", os.path.exists(os.path.join(game, WINHTTP)))

    # 2. reinstall adds nothing new
    after_first = snapshot(game)
    code, _ = run(game, "install")
    passed &= check("reinstall exit 0", code == 0)
    passed &= check("reinstall identical", snapshot(game) == after_first)

    # 3. uninstall returns the folder to exactly its original files
    code, _ = run(game, "uninstall")
    passed &= check("uninstall exit 0", code == 0)
    passed &= check("uninstall restores original", snapshot(game) == before)
    shutil.rmtree(game, ignore_errors=True)

    # 4. a folder that already has BepInEx + another plugin: only the plugin is added, and uninstall
    #    leaves BepInEx and the other plugin intact
    def with_bepinex(g):
        os.makedirs(os.path.join(g, "BepInEx", "core"))
        open(os.path.join(g, "BepInEx", "core", "BepInEx.dll"), "wb").write(b"existing")
        open(os.path.join(g, WINHTTP), "wb").write(b"existing-doorstop")
        os.makedirs(os.path.join(g, "BepInEx", "plugins", "SomeOtherMod"))
        open(os.path.join(g, "BepInEx", "plugins", "SomeOtherMod", "Other.dll"), "wb").write(b"other")

    game = fake_game(with_bepinex)
    before = snapshot(game)
    code, text = run(game, "install")
    passed &= check("install over existing BepInEx exit 0", code == 0)
    passed &= check("plugin installed", os.path.exists(os.path.join(game, PLUGIN)))
    passed &= check("existing BepInEx.dll untouched",
                    open(os.path.join(game, "BepInEx", "core", "BepInEx.dll"), "rb").read() == b"existing")
    passed &= check("existing winhttp untouched",
                    open(os.path.join(game, WINHTTP), "rb").read() == b"existing-doorstop")
    passed &= check("other mod untouched",
                    os.path.exists(os.path.join(game, "BepInEx", "plugins", "SomeOtherMod", "Other.dll")))
    code, _ = run(game, "uninstall")
    passed &= check("uninstall over existing BepInEx exit 0", code == 0)
    passed &= check("uninstall kept existing BepInEx + other mod", snapshot(game) == before)
    shutil.rmtree(game, ignore_errors=True)

    # 5. a non-game folder is refused
    empty = tempfile.mkdtemp(prefix="repo-test-empty-")
    code, _ = run(empty, "install")
    passed &= check("non-game folder refused", code != 0)
    shutil.rmtree(empty, ignore_errors=True)

    # 6. uninstall with nothing installed is refused
    game = fake_game()
    code, _ = run(game, "uninstall")
    passed &= check("uninstall without install refused", code != 0)
    shutil.rmtree(game, ignore_errors=True)

    print("ALL PASS" if passed else "SOME FAILED")
    return 0 if passed else 1


if __name__ == "__main__":
    raise SystemExit(main())
