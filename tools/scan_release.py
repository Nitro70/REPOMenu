"""Scan the release exes (and every file bundled in the payload) for personal or build-machine strings.

Run before publishing. Prints each hit with the surrounding bytes; exit code 1 if anything is found.
"""
import getpass
import os
import sys
import zipfile

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EXES = [os.path.join(REPO, "dist", "REPOMenu-Offline-Installer.exe"),
        os.path.join(REPO, "dist", "REPOMenu-Online-Installer.exe"),
        os.path.join(REPO, "src", "REPOMenu", "bin", "Release", "REPOMenu.dll")]
PAYLOAD = os.path.join(REPO, "build", "payload.zip")
B = chr(92)

NEEDLES = [getpass.getuser(), os.path.expanduser("~"), REPO,
           B + "obj" + B, ".pdb", "SteamLibrary", "AppData", "Temp" + B, "7656119",
           "claude", "anthropic"] + sys.argv[1:]


# BepInEx's own release assemblies were built on the BepInEx project's CI; any paths in them are
# theirs, not ours. We only allow that inside files that come from the BepInEx part of the payload.
ALLOWED = [b"pdb.dll"]


def hits(data, label):
    found = 0
    low = data.lower()
    for n in NEEDLES:
        for enc in ("ascii", "utf-16le"):
            try:
                b = n.lower().encode(enc)
            except UnicodeEncodeError:
                continue
            i = low.find(b)
            while i != -1:
                context = low[max(0, i - 120):i + 10]
                if not any(a in context for a in ALLOWED):
                    found += 1
                    print(f"HIT in {label}: {n!r} ({enc}) ...{data[max(0, i - 40):i + 60]!r}")
                    break
                i = low.find(b, i + 1)
    return found


def main():
    total = 0
    for exe in EXES:
        total += hits(open(exe, "rb").read(), os.path.basename(exe))
    with zipfile.ZipFile(PAYLOAD) as z:
        for name in z.namelist():
            # Our files live under BepInEx/plugins/REPOMenu/; everything else in the payload is the
            # unmodified BepInEx release, whose own build paths are not ours to scrub.
            if not name.startswith("BepInEx/plugins/REPOMenu/"):
                continue
            total += hits(z.read(name), "payload/" + name)
    print(f"{total} hits")
    return 1 if total else 0


if __name__ == "__main__":
    raise SystemExit(main())
