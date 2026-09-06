# Tonight's The Night

A configurable riot mod for Grand Theft Auto V.

Riot modes built from **factions** — who they are, what they carry, who they hate, and how they
react when it starts. Modes and factions are JSON files, not code, so a custom faction needs no
build step. Every optional feature is a switch with its own settings block.

**Status:** Drop 1 — early. The engine, config system and one mode are in; police, military and
the remaining modes are not. See [`PLAN.md`](PLAN.md) for the design and
[`docs/TESTING.md`](docs/TESTING.md) for what to check.

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
