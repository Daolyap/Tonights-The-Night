# Installing Tonight's The Night

## Requirements

| | |
|---|---|
| Game | GTA V **Legacy** (the reference target) or Enhanced |
| Required | [ScriptHookV](http://www.dev-c.com/gtav/scripthookv/) |
| Required | ScriptHookVDotNet 3 — stock, or the Enhanced fork if you run Enhanced |
| Required | [LemonUI](https://www.gta5-mods.com/tools/lemonui) — the `LemonUI.SHVDN3.dll` from its SHVDN3 folder |
| Strongly recommended | a modern **gameconfig** and **Heap Adjuster** |

The gameconfig and Heap Adjuster are not optional for the larger modes. GTA V's ped pool is
small, and a riot mod exists to fill it. Without them the game will run out of ped slots and
crash, and it will look like this mod's fault.

## Install

1. Drop `TonightsTheNight.dll` into your `GTA V/scripts/` folder.
2. Make sure `LemonUI.SHVDN3.dll` is in the same folder.
3. Start the game and load into story mode.

On first run the mod creates `scripts/TonightsTheNight/` containing:

```
defaults.json      shipped defaults - do not edit, it is overwritten on update
user.json          your overrides - never touched by an update
modes/             riot modes, one JSON file each
profiles/          saved setups
```

## Keys

| Key | Action |
|---|---|
| **F6** | Open the menu |
| **F5** | Reload config and mode files from disk |

Both are configurable in `user.json` under `menu.key` and `menu.reloadKey`.

## Configuring

Never edit `defaults.json` — it gets rewritten. Copy the keys you want to change into
`user.json`, which keeps the same shape:

```jsonc
{
  // Comments and trailing commas are allowed in every config file.
  "riot": {
    "conversionChance": 0.95,
    "recruitRadius": 220
  },
  "combat": {
    "accuracy": 35
  },
  "features": {
    "debugOverlay": { "enabled": true }
  }
}
```

Press **F5** in game and the change applies without a restart. Settings resolve in four layers,
each overriding the one below: live menu changes, then the running mode's own overrides, then
`user.json`, then `defaults.json`.

## Riot modes

Each file in `modes/` is one mode: the factions in play, who hates whom, and any config that
mode overrides. To make your own, copy a stock file, rename it, and edit — a custom faction
needs no code.

Stock mode files carry a `"_stock": true` marker. Remove that line (or just edit the file) and
updates will stop overwriting your version.

## When something goes wrong

`scripts/TonightsTheNight.log` records the version, what config loaded, every model or weapon
that failed to resolve, and any exception. It's truncated at the start of each session, so it's
always about the run you just had. Attach it to any bug report.

If the mod misbehaves, turn features off one at a time in the menu under **Features** —
everything optional is a switch, so you can narrow the cause down without a new build.
