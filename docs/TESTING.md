# Drop 1 — test script

Drop 1 exists to answer one question: **does this approach work on a real machine?** Nothing in
it has ever run in a game. Everything below is an assumption until you check it.

Roughly 15 minutes. Take the log either way: `scripts/TonightsTheNight.log`.

---

## Before you start

Please do the first run with your **military/police enhancer disabled**. Not because it will
break anything, but because it changes ped and vehicle stats, and I need one clean baseline to
compare against before your rig stops being representative. Re-enable it afterwards and note
anything that changes.

---

## T1 — It loads

1. Put `TonightsTheNight.dll` in `scripts/`, confirm `LemonUI.SHVDN3.dll` is there too.
2. Load story mode.

**Expect:** a notification, *"Tonight's The Night v0.1.0 loaded. Press F6"*.

**Also check:** `scripts/TonightsTheNight.log` exists and opens with a version banner, the game
directory, and `Loaded 1 mode(s): Pedestrian Riot`.

> If there's no notification and no log, the script didn't load at all — that's a SHVDN problem,
> and the SHVDN log (`ScriptHookVDotNet.log`) will say more than mine will.

## T2 — Config files appear

Check `scripts/TonightsTheNight/` now contains `defaults.json`, `modes/pedestrians.json`, and
empty `profiles/`. There should be **no** `user.json` — that's yours to create.

Open `defaults.json`. It should be readable, commented, and complete.

## T3 — The menu

Press **F6**.

**Expect:** a LemonUI menu titled *Tonight's The Night / RIOT CONTROL*, with `Riot Modes`,
`Tuning`, `Features`, `Stop Riot` (greyed out, labelled *nothing running*) and `Reload Config`.

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

**F6 → Stop Riot.**

**Expect:** notification, blips gone, peds stop fighting and go back to normal behaviour within
a few seconds. No lingering hostility, no stuck blips.

Then **save and reload** the game. The world should be entirely normal. This is the test that
catches a save-poisoning bug, and it matters more than any feature.

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

This is why the combat-attribute IDs are exposed in config. Add this to `user.json` and press F5:

```jsonc
{ "combat": { "attributeIds": { "alwaysFight": 46 } } }
```

Try `46`, then `5`, then `1`, then `17`, restarting the mode after each F5. If one of them makes
peds fight, tell me which — that's a one-line fix rather than a guessing game across builds.

---

## What I most want back

In rough priority:

1. **Did it load?** (T1)
2. **Did peds fight?** (T4)
3. **Did stopping restore the world, and did a save/reload stay clean?** (T5)
4. The log file, whatever happened.
5. Tick timings and the ped count where things got rough.

Everything else in the design is built on those four answers.
