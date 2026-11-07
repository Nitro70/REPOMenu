"""Build build/payload.zip: everything the installer writes into the R.E.P.O. game folder.

Contents: the runtime files from the official BepInEx 5.4.23.6 (Mono, win-x64) release (downloaded
once into build/ and checked against its SHA-256), plus this project's built REPOMenu.dll and its
docs under BepInEx/plugins/REPOMenu/. The offline installer embeds the zip; the online installer
downloads the same zip (dist/REPOMenu-Payload.zip, a release asset, with its .sha256) from the
latest GitHub release.

The zip mirrors the game folder. The installer treats everything under BepInEx/plugins/REPOMenu/ as
"the mod" (always installed) and everything else as "the BepInEx loader" (installed only when the
folder has no BepInEx yet, so an existing BepInEx and its other plugins are never touched).
"""
import hashlib
import os
import shutil
import urllib.request
import zipfile

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BUILD = os.path.join(REPO, "build")
DIST = os.path.join(REPO, "dist")
DLL = os.path.join(REPO, "src", "REPOMenu", "bin", "Release", "REPOMenu.dll")

BEPINEX_VERSION = "5.4.23.6"
BEPINEX_URL = f"https://github.com/BepInEx/BepInEx/releases/download/v{BEPINEX_VERSION}/BepInEx_win_x64_{BEPINEX_VERSION}.zip"
BEPINEX_SHA256 = "6c57a714a892efb4e9e1fef63d608a3391f30ec984031c465404e4ee381a0755"
BEPINEX_ZIP = os.path.join(BUILD, f"BepInEx_win_x64_{BEPINEX_VERSION}.zip")

PAYLOAD = os.path.join(BUILD, "payload.zip")
RELEASE_ZIP = os.path.join(DIST, "REPOMenu-Payload.zip")

PLUGIN_DIR = "BepInEx/plugins/REPOMenu/"
# BepInEx files we do NOT ship: docs, and the user's config/plugins, so a fresh install leaves an
# empty config folder for BepInEx to fill on first launch and never carries a stale config.
SKIP = {"changelog.txt"}


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def fetch_bepinex():
    os.makedirs(BUILD, exist_ok=True)
    if not os.path.isfile(BEPINEX_ZIP) or sha256(BEPINEX_ZIP) != BEPINEX_SHA256:
        print("downloading", BEPINEX_URL)
        urllib.request.urlretrieve(BEPINEX_URL, BEPINEX_ZIP)
    got = sha256(BEPINEX_ZIP)
    if got != BEPINEX_SHA256:
        raise SystemExit(f"BepInEx zip hash mismatch: {got} (expected {BEPINEX_SHA256})")


def main():
    if not os.path.isfile(DLL):
        raise SystemExit(f"{DLL} is missing. Build the mod first: dotnet build src/REPOMenu -c Release")
    fetch_bepinex()

    docs = {
        PLUGIN_DIR + "README.txt": os.path.join(REPO, "installer", "PLUGIN-README.txt"),
        PLUGIN_DIR + "LICENSE.txt": os.path.join(REPO, "LICENSE"),
        PLUGIN_DIR + "THIRD-PARTY-NOTICES.md": os.path.join(REPO, "THIRD-PARTY-NOTICES.md"),
    }
    for src in docs.values():
        if not os.path.isfile(src):
            raise SystemExit(f"missing {src}")

    with zipfile.ZipFile(BEPINEX_ZIP) as src, zipfile.ZipFile(PAYLOAD, "w", zipfile.ZIP_DEFLATED) as out:
        for info in src.infolist():
            # keep only the files that actually bootstrap BepInEx; drop its empty dirs and docs
            if info.is_dir() or info.filename in SKIP:
                continue
            out.writestr(info.filename, src.read(info))
        out.write(DLL, PLUGIN_DIR + "REPOMenu.dll")
        for rel, full in sorted(docs.items()):
            out.write(full, rel)

    with zipfile.ZipFile(PAYLOAD) as z:
        names = z.namelist()
        plugin = [n for n in names if n.startswith(PLUGIN_DIR)]
    print(f"{PAYLOAD}: {len(names)} files ({len(plugin)} mod, {len(names) - len(plugin)} BepInEx), {os.path.getsize(PAYLOAD):,} bytes")

    os.makedirs(DIST, exist_ok=True)
    shutil.copyfile(PAYLOAD, RELEASE_ZIP)
    digest = sha256(RELEASE_ZIP)
    with open(RELEASE_ZIP + ".sha256", "w", encoding="ascii", newline="\n") as f:
        f.write(f"{digest}  {os.path.basename(RELEASE_ZIP)}\n")
    print(f"{RELEASE_ZIP} (+ .sha256): {digest}")


if __name__ == "__main__":
    main()
