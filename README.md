# REPOMenu

A feature-rich BepInEx mod menu for **R.E.P.O.**, by Nitro70. Press **F2** in game to open it.

> **Status: early, untested.** This build compiles and is verified against the current game's
> assemblies, but it has **not been run in game yet**. Treat the first release as a pre-release.
> Use it in singleplayer or in private lobbies with friends who are happy to have it on.

## Install

Easiest: download an installer from the [latest release](../../releases/latest) and run it.

- **REPOMenu-Offline-Installer.exe** - everything built in, works with no internet.
- **REPOMenu-Online-Installer.exe** - small; downloads the newest version from GitHub each time.

Both find R.E.P.O. through Steam on their own (Browse for it if not), install **BepInEx 5** when the
game doesn't already have it, drop the mod into `BepInEx\plugins\REPOMenu\`, and leave any BepInEx
and other mods you already have untouched. They refuse to run while the game is open, and have an
**Uninstall** button that removes only what they installed. Close the game first.

Manual: grab **REPOMenu.dll** from the release and drop it in `BepInEx\plugins\` yourself (you need
BepInEx 5 Mono already set up). Launch R.E.P.O. through Steam, not a mod manager profile.

## Features

Every tab marks its cards **CLIENT** (changes only you or your screen; works in any lobby) or
**HOST** (changes the world or other players; works only when you host, or play alone).

- **Self** - god, can't-die, auto-heal, live max health, infinite stamina, no-ragdoll, kill-self
- **Movement** - speed, jump height, extra air jumps, fly/noclip, long reach, strong grab and throw
- **ESP** - enemies (+health), valuables (+$ value), items, players, extraction points, carts;
  boxes, tracers, distance and value filters; reveal valuables on the map
- **Visuals** - fullbright, no fog, field of view, the game's own freecam, hide HUD
- **Teleport** - to your aim, the truck, extraction, the best valuable, players, saved spots
- **Character** - live cosmetics: wardrobe, colours, rainbow, unlock all (with save backup),
  outfit copy/paste, tokens, all applied without a new round
- **Players** (host) - heal, revive, move, kill, and every upgrade for anyone
- **Enemies** (host) - kill, freeze, despawn, remove, one-hit, spawn any enemy
- **Spawner** (host) - spawn any item, valuable or enemy; "every item on the floor"
- **World** (host) - money, extraction goals and instant extract, valuable prices, change level
- plus favourites, a search box over every card, and keybinds (right-click a card to bind a key)

Lists of items, valuables, enemies, levels, upgrades, cosmetics and colours are read from the game
at runtime, so new game updates and other mods' content appear by themselves. If a game update
changes something a card relies on, that card switches itself off and says so, and the rest keeps
working.

## Scope

Singleplayer and private lobbies with consenting friends. The HOST tabs use the game's own
host-authority, so they only take effect when you host; the menu does not forge the network to make
a guest act as host, and it does not hide itself from anyone.

## Building

```bash
dotnet build src/REPOMenu -c Release
```

References the game's assemblies from the Steam install (override with `-p:GameDir=...`), publicized
at build time. The installers are built from `installer/` after `python tools/make_payload.py`.

## Credits and licence

REPOMenu is MIT licensed (see `LICENSE`). It bundles [BepInEx](https://github.com/BepInEx/BepInEx)
to load; see `THIRD-PARTY-NOTICES.md`. Unofficial and not affiliated with semiwork or BepInEx.
