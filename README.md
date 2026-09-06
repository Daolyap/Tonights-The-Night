# Tonight's The Night

A configurable riot mod for Grand Theft Auto V.

Riot modes built from **factions** — who they are, what they carry, who they hate, and how they
react when it starts. Modes and factions are JSON files, not code, so a custom faction needs no
build step. Every optional feature is a switch with its own settings block.

**Status:** v0.4.0 — playable. Nine modes, spawned factions, escalation, zones, car chases,
looting and weapon presets are in. See [`CHANGELOG.md`](CHANGELOG.md) for what landed and
[`PLAN.md`](PLAN.md) for the design.

| Mode | |
|---|---|
| Pedestrian Riot | Two mobs form out of the crowd |
| Pedestrian Chaos | A free-for-all. Only your passengers are on your side |
| Gang War | Four gangs settle up at once |
| Police State | Sirens on, driving through crowds, helicopters overhead |
| Martial Law | Police, then infantry, then armour, air and sea |
| Animal Uprising | Deliberately silly |
| Invasion | Aliens, with weapons already in your game |
| The Purge | Twelve minutes, then it stops |
| Everything | All of it, escalating |

Plus, across all of them: **car chases** (hurt someone, drive off, and a carload of their side
comes after you), **rioters who drive** rather than all stopping and getting out, **looting**,
**escalation phases**, **riot zones**, **fires**, and **weapon presets** — realistic, armed and
armoured, military, chaos, or your own list.

It does not change your time of day, weather or colour grading unless you turn that on, and it
does not touch the wanted system — with one exception you can switch off: during The Purge, all
crime is legal, so your wanted ceiling drops for the window and is restored exactly as it was.

## Install

`TonightsTheNight.dll` into `GTA V/scripts/`, alongside ScriptHookV, ScriptHookVDotNet 3 and
LemonUI. Full detail in [`INSTALL.md`](INSTALL.md) — including the gameconfig and Heap Adjuster
you'll want before the bigger modes arrive.

**F6** opens the menu. **F5** reloads config without restarting the game.

## Building

Targets .NET Framework 4.8 because SHVDN does, but builds on any platform with the .NET SDK —
no Mono, no Windows required.

```
dotnet build src/TonightsTheNight/TonightsTheNight.csproj -c Release
dotnet run --project tests/ConfigTests/ConfigTests.csproj -c Release
```

CI builds every push and attaches a ready-to-drop-in DLL as an artifact.

## Licence

Not yet chosen.
