"""List every [PunRPC] method in the decompiled game with the sender check it uses, and flag the
ones a guest can call that do something interesting (spawn, money, items, values).
usage: python rpc_scan.py <decompiled Assembly-CSharp dir>"""
import os, re, sys

root = sys.argv[1]
interesting = re.compile(r"Instantiate|InstantiateRoomObject|StatSetRunCurrency|SetRunStatSet|ItemPurchase|DollarValue|Spawn\(|CosmeticTokenAdd|CosmeticUnlock|Upgrade|Revive|Heal\(|haul|Haul|currency|Currency")
checks = re.compile(r"MasterOnlyRPC|OwnerOnlyRPC|MasterAndOwnerOnlyRPC|IsMasterClient|IsMasterClientOrSingleplayer|IsMine")

rows = []
for dirpath, _, files in os.walk(root):
    for f in files:
        if not f.endswith(".cs"):
            continue
        src = open(os.path.join(dirpath, f), encoding="utf-8", errors="ignore").read()
        for m in re.finditer(r"\[PunRPC\]\s*\n\s*(?:public|private|internal|protected)[^\n(]*\s(\w+)\((.*)\)[ \t]*\n\s*\{", src):
            name, params = m.group(1), m.group(2)
            # body by brace matching
            i = m.end(); depth = 1
            while depth and i < len(src):
                if src[i] == "{": depth += 1
                elif src[i] == "}": depth -= 1
                i += 1
            body = src[m.end():i]
            first = body.strip().splitlines()[0] if body.strip() else ""
            guard = checks.findall(body[:400])
            rows.append((f[:-3], name, params.replace("PhotonMessageInfo _info = default(PhotonMessageInfo)", "info"), guard, bool(interesting.search(body)), body))

open_rows = [r for r in rows if not any(g in ("MasterOnlyRPC", "OwnerOnlyRPC", "MasterAndOwnerOnlyRPC") for g in r[3])]
print(f"{len(rows)} RPCs, {len(open_rows)} without a sender check")
for cls, name, params, guard, hot, body in sorted(open_rows):
    if hot:
        print(f"  {cls}.{name}({params})  guards={sorted(set(guard))}")
