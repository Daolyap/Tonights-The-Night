# Installing Tonight's The Night

## Requirements

| | |
|---|---|
| Game | GTA V **Legacy** (the reference target) or Enhanced |
| Required | [ScriptHookV](http://www.dev-c.com/gtav/scripthookv/) |
| Required | [ScriptHookVDotNet 3](https://github.com/scripthookvdotnet/scripthookvdotnet) — stock, or the Enhanced fork if you run Enhanced |
| Required | [LemonUI](https://www.gta5-mods.com/tools/lemonui) — the `LemonUI.SHVDN3.dll` from its SHVDN3 folder |
| Strongly recommended | a modern **gameconfig** and **Heap Adjuster** |

The gameconfig and Heap Adjuster are not optional for the larger modes. GTA V's ped pool is
small, and a riot mod exists to fill it. Without them the game will run out of ped slots and
crash, and it will look like this mod's fault.

## Install

1. Install ScriptHookV, ScriptHookVDotNet 3 and LemonUI first, if you have not already.
2. Drop `TonightsTheNight.dll` into your `GTA V/scripts/` folder.
3. Make sure `LemonUI.SHVDN3.dll` is in the same folder.
4. Start the game and load into story mode.

You should see a notification once you have control of your character. If you do not, check
`scripts/ScriptHookVDotNet.log` — a missing LemonUI is the usual cause, and it fails before this
mod can report anything itself.

On first run the mod creates `scripts/TonightsTheNight/` containing:

```
defaults.json      shipped defaults - do not edit, it is overwritten on update
user.json          your overrides - never touched by an update
modes/             riot modes, one JSON file each
profiles/          saved setups
```

To uninstall, delete `TonightsTheNight.dll`. Delete `scripts/TonightsTheNight/` too if you do not
want to keep your settings. Nothing is written anywhere else.

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

Stock mode files carry a `"_stock": true` marker, which is what tells an update the file is
still ours to replace. **Delete that line** and the file becomes yours permanently — updates
will never touch it again. Editing a file while leaving the marker in place is not enough: the
next update will overwrite your changes.

The safer habit is to copy a stock file to a new name and edit the copy. Only files matching a
shipped mode name are ever rewritten.

## Compatibility with police, wanted and overhaul mods

This mod is built to sit alongside police overhauls, wanted-system replacements, LSPDFR-style
packs and 6-star mods rather than compete with them. Two design decisions do the work:

**1. It does not use the wanted system.** Riot police are built from relationship groups, not
wanted levels. That was chosen because the wanted system is player-centric and fights us — but
the happy side effect is that it's exactly the system an overhaul pack replaces, so the two
never collide. There is a `compatibility.manageWantedSystem` switch, it defaults to **off**, and
it is expected to stay off. Every wanted-system call in the codebase routes through one gate
that respects it.

There is exactly one exception, described at the end of this section: The Purge lowers your
wanted ceiling for the length of its window, because "all crime is legal" is the entire premise.
It restores whatever the ceiling was.

**2. It leaves police and emergency peds alone.** Cops, SWAT, army, medics and firefighters are
never recruited into a faction. An overhaul pack owns those peds; this mod hijacking them would
break that pack in ways that look like the pack's fault.

```jsonc
{
  "compatibility": {
    "protectEmergencyServices": true,   // leave cops, SWAT, army, medics, firefighters alone
    "protectMissionPeds": true,         // never recruit story peds
    "manageWantedSystem": false         // never write to the wanted system
  }
}
```

Both protections are also toggles in the menu under **Features**.

Things that will still interact, by design and harmlessly:

- **Your pack's models and stats apply to us.** If it replaces vanilla models under the same
  names, anything this mod spawns renders with your versions automatically. We reference vanilla
  names only, so the mod stays shareable rather than being built for one setup.
- **Rioting near police provokes your wanted system normally.** Civilians murdering each other
  in the street is your pack's business to respond to, and it should respond exactly as it
  always does.

Riot police are a separate faction with their own relationship group, not a modification of the
game's police. Your pack's dispatch, stars and units keep running untouched alongside them.

**One deliberate exception.** During The Purge all crime is legal, so your wanted *ceiling* is
lowered for the length of the window and restored to exactly what it was when it ends — if your
pack gives you six stars, you have six stars again the moment the purge is over. Switch it off
with `features.purge.noWantedLevel: false`, or in the menu under **Features**.

## When something goes wrong

`scripts/TonightsTheNight.log` records the version, what config loaded, every model or weapon
that failed to resolve, and any exception. It's truncated at the start of each session, so it's
always about the run you just had. Attach it to any bug report.

If the mod misbehaves, turn features off one at a time in the menu under **Features** —
everything optional is a switch, so you can narrow the cause down without a new build.
