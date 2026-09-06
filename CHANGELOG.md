# Changelog

## v0.1.3 — the riot was eating itself

The v0.1.2 log settled the "it feels sparse" question with numbers: **281 recruited over three
minutes, 12 still alive at the end.** The riot wasn't failing to start, it was consuming its own
participants faster than the game could repopulate the street. Twelve active rioters at any
moment is not a riot, and no amount of tuning conversion chance fixes it, because the limit was
supply, not recruitment.

### Changed

- **Loadouts are weighted, and the stock mode is now mostly melee.** Bats, crowbars, hammers and
  knives outweigh firearms about 5:1. A fight with bats lasts long enough to look like a riot; a
  fight with pistols is over in seconds and leaves an empty road. Weapons accept either a bare
  name or `{"name": ..., "weight": ...}`, so this is tunable per faction — and it's the mechanism
  the weapon presets will be built on.
- **Ped density raised 1.5 → 2.5.** More crowd to draw from. This is the setting that most wants
  a gameconfig and Heap Adjuster behind it.

### Added

- **Churn telemetry.** The overlay and log now show `lost` alongside `recruited`, and the log
  writes a stats line every 30 seconds. A single total at shutdown hid the shape of the run —
  "recruited 281, active 12" only means something once you can see whether the active count was
  climbing, flat, or collapsing.

## v0.1.2 — path fix, crowd rebalance, you are a target

### Fixed

- **Config and log were written nowhere findable.** v0.1.0 put them in `scripts/scripts`;
  v0.1.1's "fix" was worse, resolving from the assembly's own location — but SHVDN shadow-copies
  script assemblies, so that points at a temp cache. `AppDomain.BaseDirectory` *is* the scripts
  folder under SHVDN; it just must not have `scripts` appended to it. That is now the rule, with
  the reasoning written into the code so a third wrong answer doesn't happen.
- **This can no longer be invisible.** Startup shows the config path on screen, the log opens
  with every path candidate and the one chosen, the config folder is probed for writability at
  load, and a failure is now a red notification instead of silence. There is a **Show File
  Locations** item in the menu.
- **Peds still re-fought after a stop.** Clearing tasks once isn't enough — two peds who have
  already wounded each other re-engage from vanilla AI memory the moment they're free. They're
  now kept calm for a few seconds after a stop (`riot.pacifySeconds`).

### Changed

- **The crowd is rebalanced.** v0.1.1 overcorrected into a street where almost everyone ran away.
  Conversion is back up to 0.55, and the mobs now take 45% each of recruits against 10%
  bystanders. The reasoning changed too: most non-fighters should be **left alone entirely**
  rather than converted into fleeing bystanders. An untouched ped reacts to gunfire by itself and
  looks better doing it than one we explicitly told to run.
- **You are a target by default.** Standing in the middle of a riot untouched reads as a bug, not
  as neutrality — during a purge, being just another person on the street is the point. New
  `player.stance` setting (`ignored` / `disliked` / `target`, default `target`) with a
  **They Treat You As** picker in Tuning that applies live, mid-riot. Replaces
  `player.everyoneHatesPlayer`.
- Added `TonightsTheNight.sln` so the project opens and builds directly in Visual Studio.

## v0.1.1 — first test round fixes

All from the first in-game test. The engine itself worked: peds recruited and fought within
seconds, blips appeared, tick cost stayed at 0.01ms with an 8.64ms peak, and the
`alwaysFight` combat attribute ID of `46` turned out to be correct.

### Fixed

- **Config and log went to `scripts/scripts/`.** SHVDN's `AppDomain.BaseDirectory` is the
  scripts folder, not the game root, so appending `scripts` to it nested them one level too
  deep. Now resolved from the assembly's own location. This also silently disabled logging
  entirely — the log's directory didn't exist when the session banner was written, and the
  logger gave up for the rest of the session. It now creates the directory first.
- **Blips outlived the peds wearing them.** Blip deletion only happened on the restore path,
  and dead peds took the non-restore path.
- **Blips appeared on birds.** Entity handles get recycled: once a rioter died and despawned,
  its handle could be reissued to something else, which kept our entry alive and its blip
  attached. Each tracked ped now records its model and is dropped when the handle changes hands.
- **Rioters shot at a player the mode declared neutral.** `SET_PED_AS_ENEMY` makes a ped hostile
  to the player specifically, overriding the relationship matrix. Removed — who hates whom is
  the matrix's job alone.
- **Peds stuttered between attacking and fleeing when aimed at.** Retasking interrupted the
  game's own reaction, which restarted it, which we interrupted again. Peds in combat, fleeing,
  ragdolled, or being aimed at are now left alone, the retask interval went from 4s to 6s, and
  it's jittered so crowds don't retask in lockstep.
- **Some peds kept fighting after Stop.** Restore now clears the task queue outright and undoes
  the combat conditioning before doing so, rather than after.
- **Menu items were named from submenu subtitles** — "START A MODE" instead of "Riot Modes".
  LemonUI takes the parent item's text from the submenu's subtitle; the item is now named
  explicitly.
- **The startup notification fired during the loading screen**, so it was never seen. It now
  waits until the player has control.
- **A broken `user.json` reported nothing on reload.** The parse error was only surfaced at
  startup — the one time it's least likely to happen.
- **Switching modes announced a stop nobody asked for.**

### Changed

- **Most people are no longer in a faction.** Conversion chance dropped from 0.85 to 0.35, and
  factions gained a `share` weight. The stock mode is now 20% Red Mob, 20% Blue Mob, 60%
  bystanders — so a small core fights, some panic, and most of the street is untouched. Everyone
  brawling read as "everyone simultaneously decided to fight" rather than as a riot.
- **People sharing a vehicle join the same faction.** Occupants inherit the faction of whoever
  was recruited first, instead of being split across sides and immediately shooting each other.
- `combat.retaskIntervalMs` and `riot.groupVehicleOccupants` are new and configurable.

### Known and deliberate

- Bodies stay where they fall. The purge ending doesn't resurrect anyone.
- Only Pedestrian Riot exists so far. The other modes are Drop 2.
- Police responding to street violence is your wanted system working correctly. It should be
  less trigger-happy now that rioters aren't flagged hostile to you personally.
