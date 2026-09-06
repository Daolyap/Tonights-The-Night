# Changelog

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
