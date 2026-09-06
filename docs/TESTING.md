# Drop 1 — test script

Drop 1 exists to answer one question: **does this approach work on a real machine?** Nothing in
it has ever run in a game. Everything below is an assumption until you check it.

Roughly 15 minutes. Take the log either way: `scripts/TonightsTheNight.log`.

| | Test | The question it answers |
|---|---|---|
| T1 | It loads | Does the DLL start at all on your setup? |
| T2 | Config files appear | Did it create its own config folder correctly? |
| T3 | The menu | Does F6 open a working menu? |
| T4 | **The riot itself** | **Do pedestrians actually fight each other?** |
| T5 | **It cleans up** | **When you stop, does everyone go back to normal — and does a save/reload stay clean?** |
| T6 | Debug overlay | Are the performance numbers sane? |
| T7 | Hot reload | Can you retune settings without restarting the game? |

T4 and T5 are the two that decide whether the design works. The rest is plumbing.

---

## Before you start

Ideally do the first run with your **police OIV pack disabled**, so we get one clean baseline.
It changes ped stats, weapons, vehicles and the wanted system, and without a baseline I can't
tell whether "cops feel wrong" is us or it.

If reinstalling the pack is a hassle, don't bother — this build never touches police, SWAT,
army or emergency peds at all (see **Compatibility** in `INSTALL.md`), so the two shouldn't
interact. Run it as-is and tell me if anything police-related changes; that itself is a useful
result.

---

## T1 — It loads

1. Put `TonightsTheNight.dll` in `scripts/`, confirm `LemonUI.SHVDN3.dll` is there too.
2. Load story mode.

**Expect:** a notification, *"Tonight's The Night vX.Y.Z loaded. Press F6"*. It waits for you to
have control of your character, so it arrives after the loading screen, not during it.

**Also check:** `scripts/TonightsTheNight.log` exists and opens with a version banner, the
resolved paths, and `Loaded 1 mode(s): Pedestrian Riot`. The banner prints its own log path — if
anything is ever in the wrong place, that line says where it actually went.

> If there's no notification and no log, the script didn't load at all — that's a SHVDN problem,
> and the SHVDN log (`ScriptHookVDotNet.log`) will say more than mine will.

## T2 — Config files appear

Check `scripts/TonightsTheNight/` now contains `defaults.json`, `modes/pedestrians.json`, and
empty `profiles/`. There should be **no** `user.json` — that's yours to create.

Specifically **not** `scripts/scripts/TonightsTheNight/`. If you still have that folder from
v0.1.0, delete it.

Open `defaults.json`. It should be readable, commented, and complete.

## T3 — The menu

Press **F6**.

**Expect:** a LemonUI menu titled *Tonight's The Night / RIOT CONTROL*, with `Riot Modes`,
`Tuning`, `Features`, `Stop Riot` (greyed out, labelled *nothing running*) and `Reload Config`.

> In v0.1.0 these read "START A MODE", "LIVE SETTINGS" and "OPTIONAL EXTRAS" — LemonUI names a
> submenu's parent item from its subtitle rather than its title. Fixed in v0.1.1.

Walk into each submenu. Sliders and toggles should move.

## T4 — The riot itself *(the real test)*

Stand somewhere busy — Vespucci Beach, Del Perro, downtown. Then **F6 → Riot Modes → Pedestrian Riot**.

**Expect:**
- A notification naming the mode.
- Within a few seconds, pedestrians around you start fighting each other.
- Red and blue blips on the minimap.
- Some peds run rather than fight (about 1 in 3 recruits is a bystander, and only 15% of those
  fight back).

**Tell me:**
1. Do they actually fight, or just stand there? *(If they stand there, the combat-attribute IDs
   are wrong — that's the single most likely bug in this build, and §T7 is how we find out.)*
2. Roughly how long from starting the mode to visible fighting?
3. Does the mix of fighters and fleeing peds read as a riot, or as noise?
4. Any frame drop? What does the overlay say (§T6)?

## T5 — It cleans up

Stopping is meant to feel like the purge ending: everyone drops what they're doing and carries
on as normal. Mechanically that means we hand every ped we touched back to the game exactly as
we found it — original relationship group restored, combat conditioning undone, tasks cleared,
blips deleted, and the ped released so the engine can recycle it. That is all "restore the
world" means; there is no wind-down animation and no slow calm-down.

**F6 → Stop Riot.**

**Expect:** a notification, blips gone, and peds stopping and returning to normal wandering
within a few seconds. No lingering hostility, no stuck blips.

Bodies stay where they fell — the purge ending doesn't resurrect anyone. That's intended.

Then **save and reload** the game. The world should be entirely normal. This is the test that
catches a save-poisoning bug, and it matters more than any feature: it's the difference between
a mod you can leave installed and one that quietly ruins a playthrough.

## T6 — Debug overlay

**F6 → Features → Debug Overlay** on.

**Expect:** a text block top-left with mode, tracked/recruited/culled counts, and tick timing.

`tick` should sit well under 1ms. If `peak` is high, tell me the number — the budget is supposed
to shrink automatically when we cost too much, and that's the readout that proves it works.

## T7 — Hot reload *(the thing that saves us both time)*

1. Create `scripts/TonightsTheNight/user.json`:

```jsonc
{
  "riot": { "conversionChance": 1.0, "recruitRadius": 250 },
  "combat": { "accuracy": 60 },
  "features": { "debugOverlay": { "enabled": true } }
}
```

2. **Without leaving the game**, press **F5**.

**Expect:** a *"Reloaded config and 1 mode(s)"* notification, and the log showing the user
config loaded. Start the mode again — recruitment should be noticeably more aggressive.

3. Now break it on purpose — put a stray `}` in `user.json` and press F5.

**Expect:** an orange *config problem* notification and an error line in the log. **The mod must
keep working** on the previous settings rather than dying.

### If T4 showed peds standing around

This is the failure I most expect, and it's why the combat-attribute IDs are tunable rather than
compiled in. They're community-documented rather than official, and `alwaysFight` is the one
most likely to be wrong.

Put this in `user.json`:

```jsonc
{ "combat": { "attributeIds": { "alwaysFight": 46 } } }
```

Then, without leaving the game: change the number, press **F5**, stop and restart the mode,
watch. Try **46**, then **5**, then **1**, then **17**. If one of them makes peds fight, tell me
which — that turns a guessing game across four builds into four minutes.

The full set is in `defaults.json` under `combat.attributeIds` if you want to see what else is
adjustable.

---

## What I most want back

In rough priority:

1. **Did it load?** (T1)
2. **Did peds fight?** (T4)
3. **Did stopping restore the world, and did a save/reload stay clean?** (T5)
4. The log file, whatever happened.
5. Tick timings and the ped count where things got rough.

Everything else in the design is built on those four answers.
