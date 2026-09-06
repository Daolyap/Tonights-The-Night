# Tonight's The Night — disposable plan v0

> Status: **throwaway first pass.** Written to be argued with, cut down, and replaced.
> Date: 2026-09-06. Nothing here is committed to beyond the toolchain findings in §2–3.

---

## 1. Verdict: how it sounds

The vision is sound and almost all of it is achievable with today's tooling. The parts that
are hard aren't the parts you'd expect.

**Straightforwardly doable**
- Mod menu on F6/F7, mode picker, sliders, toggles.
- Factions as first-class objects with their own relationship rules, ped pools, loadouts, blips.
- Weapon presets, per-faction rather than global.
- Police behaving like police (sirens, liveries, escalating dispatch waves) while treating
  civilians as hostile — this does **not** need the wanted-level system, and shouldn't use it.
- Cops/soldiers ploughing vehicles through crowds. There's a native for exactly this
  (`SET_PED_STEERS_AROUND_PEDS(ped, false)`) plus ram-style vehicle missions.
- Civilians choosing flee vs. fight, with a tunable split.
- Aliens with the ray weapons — `RayGun` (Up-n-Atomizer), `UnholyHellbringer`, `Widowmaker`
  and `MovAlien01` are all real, already in the game files, and exposed in the SHVDN API.
- Custom/add-on vehicles and online vehicles for police and military — *if* we resolve models
  by name at runtime with graceful fallback rather than hardcoding hashes.

**Doable but constrained**
- Scale. This is the real enemy, not AI. Ped and vehicle pools are small and a riot mod's
  whole job is to overfill them. Design has to be budget-first (§5.3, §8.1).
- Animals. The models exist (`Coyote`, `MountainLion`, `Boar`, `Deer`, `Chimp`, `Rottweiler`…)
  but animal AI in V is thin and several models aren't reliably streamable in story mode.
  Expect comedy, not menace. Ship it, but set expectations low.
- Online-only vehicles in story mode can despawn or fail to stream depending on the user's
  setup. Treat as optional enrichment with a vanilla fallback, never as a hard dependency.

**Not plausible / would be a trap**
- Authoring genuinely new AI. We steer the vanilla ped brain via tasks, combat attributes and
  relationship groups. Behaviour will look like GTA AI, jank included. No pathfinding rewrite,
  no crowd simulation, no formations beyond what task natives give us.
- Depending on `SET_RIOT_MODE_ENABLED`. It's a real native and the name is irresistible, but
  it's a vestigial leftover that does close to nothing. Worth a five-minute test out of
  curiosity; not worth a line of architecture.
- Guaranteeing the mod survives Rockstar patches. We sit on ScriptHookV + SHVDN, both of which
  hook via memory patterns and break on every game update until their authors ship a fix.
  Nothing we can do about that except document it.

**One correction to the premise.** The "nothing in 10+ years" read isn't quite right — there
are recent, actively-updated riot/purge/chaos scripts (Los Santos Riot Mod, Ped Riot/Chaos
Mode, PedPurge, Chaos Mod V, Pedestrian Riot; most touched in 2025–2026). What *doesn't* exist
is anything with the depth you're describing: they're mostly a hotkey that arms peds and sets
them hating each other. **That's actually good news** — the toolchain is proven and current,
and the differentiator is exactly the part you care about (configurable factions, presets,
police that behave like police, escalation). We're not pioneering, we're going deeper.

---

## 2. Target and toolchain (verified 2026-09-06)

| Layer | Choice | Notes |
|---|---|---|
| Game | GTA V **Legacy and Enhanced**, one build | Enhanced (PC, Mar 2025) is fully modded now |
| Native hook | **ScriptHookV** (Alexander Blade) | Compat update Mar 2026 covers both editions |
| Managed hook | **ScriptHookVDotNet Enhanced** (Chiheb-Bacha fork), v1.1.0.6 | Drop-in SHVDN replacement, one build for Legacy + Enhanced, defaults raw scripts to the v3 API |
| Language | C# on **.NET Framework 4.8** | Fixed by SHVDN |
| Menu | **LemonUI.SHVDN3** 2.2.0 | NativeUI is dead; LemonUI confirmed working under SHVDN Enhanced |
| Config | JSON, hot-reloadable | Parser merged into our DLL (§5.7) |
| Output | single `TonightsTheNight.dll` + `TonightsTheNight/` config folder in `scripts/` | |

**Compile target vs. runtime target.** We compile against the `ScriptHookVDotNet3` **3.6.0**
NuGet reference (the lowest common denominator) and run against whatever hook the user has
installed — stock SHVDN 3 or the Enhanced fork. Anything the 3.6.0 API surface doesn't expose,
we call as a raw native through `Function.Call(Hash.X, …)`, which is version-independent. This
keeps one artifact working across both editions and both hooks.

**Recommended (not required) user-side dependencies**, checked for at startup and warned about
rather than enforced: a modern **gameconfig**, **Heap Adjuster**, **Packfile Limit Adjuster**.
Without them the big modes will crash, and it will look like our bug.

---

## 3. What I already proved in the dev container

Because I can't run the game, I front-loaded the things I *can* verify. All of the following
was executed, not assumed:

- **A shippable `.dll` builds on Linux.** .NET SDK 8 + `Microsoft.NETFramework.ReferenceAssemblies.net48`
  compiles a `net48` SHVDN script with zero Mono and zero Windows. Only extra step needed was
  `<Reference Include="System.Windows.Forms" />` for the keybind handler.
- **SHVDN 3.6.0 and LemonUI 2.2.0 both resolve from NuGet** and link cleanly together.
- **A probe script compiled** using the exact natives this design leans on:
  `SET_PED_RELATIONSHIP_GROUP_HASH`, `SET_RELATIONSHIP_BETWEEN_GROUPS`,
  `SET_PED_COMBAT_ATTRIBUTES`, `SET_PED_DENSITY_MULTIPLIER_THIS_FRAME`,
  `TASK_VEHICLE_MISSION_PED_TARGET`, weapon granting, ped blips, LemonUI menu construction.
- **Model and weapon identifiers confirmed against the SHVDN assembly metadata**, not memory:
  `MovAlien01`, `RayGun`, `UnholyHellbringer`, `Widowmaker`, `Railgun`, `CompactEMPLauncher`,
  `Swat01SMY`, `Marine01SMM/SMY`, `Marine02/03`, `Blackops01–03SMY`, `Armoured01/02SMM`,
  `Cop01SMY/SFY`, `Hwaycop01SMY`, `Ranger01SMY/SFY`, `Prisoner01`, gang sets
  (`Ballas*`, `Families01*`, `MexGoon0*`, `Lost0*`, `Korean0*`), animals
  (`Coyote`, `MountainLion`, `Boar`, `Deer`, `Chimp`, `Rottweiler`, `Retriever`, `Husky`,
  `Shepherd`, `Poodle`, `Pigeon`, `Chop`), vehicles (`Police2/3/4`, `FBI2`, `Riot`, `Riot2`,
  `Barracks`, `Crusader`, `Insurgent`, `Marshall`, `Annihilator`, `Buzzard2`, `PatriotMilSpec`).
- **Confirmed the behaviour natives exist**: `SET_PED_STEERS_AROUND_PEDS`,
  `SET_DRIVER_AGGRESSIVENESS`, `SET_DRIVER_ABILITY`, `SET_PED_ACCURACY`, `SET_PED_COMBAT_ABILITY`,
  `SET_PED_FLEE_ATTRIBUTES`, `SET_PED_SEEING_RANGE`, `REGISTER_HATED_TARGETS_AROUND_PED`,
  `SET_VEHICLE_SIREN`, `SET_TIMECYCLE_MODIFIER`.

Practical consequence: **CI can produce a fresh, ready-to-test DLL on every push**, so your
loop is "download artifact → drop in `scripts/` → play", never "install a build environment".

Still unverified and only you can settle it: whether any of it *behaves* correctly in-game.

---

## 4. Naming

Repo is already `Tonights-The-Night`, which is a better name than anything I'd pick. Mod ships
as **Tonight's The Night**; "riot modes" are the internal concept, "Purge" is one of them.

---

## 5. Architecture

### 5.1 Shape

```
src/TonightsTheNight/
  Core/
    RiotScript.cs        # Script entry: Tick, KeyDown, Aborted
    Director.cs          # owns the active scenario, phases, escalation
    Scheduler.cs         # frame budget + round-robin work queue
    EntityRegistry.cs    # everything we touched, for cleanup
    Spawner.cs           # model resolution, streaming, spawn caps
    Blips.cs             # pooled, distance-capped, per-faction styling
  Factions/
    Faction.cs           # data model (id, peds, loadout, blip, behaviour, reaction)
    RelationshipMatrix.cs
    Behaviours/          # Rioter, Defender, Flee, PoliceResponse, MilitaryLockdown,
                         # AlienHunter, Beast, Bystander
  Modes/
    ScenarioDefinition.cs
  Menu/RiotMenu.cs
  Config/ConfigStore.cs
  Util/Log.cs, ModelCache.cs, Natives.cs
config/
  modes/*.json  factions/*.json  presets/weapons/*.json
docs/  TESTING.md  CONFIG.md
.github/workflows/build.yml
```

### 5.2 Everything is data

A **riot mode is a JSON file**, not a C# class. It names the factions in play, their spawn
rules, the relationship matrix, the weather/timecycle, and the escalation timeline. The eight
stock modes (Criminals, Pedestrians, Police, Military, Animals, Aliens, Purge, Everything) ship
as eight JSON files that use the same schema a user-made mode would.

This is the single decision that makes the rest work. It means:
- "Customisable factions" is free — a custom faction is just another file.
- You can retune balance between test runs **without me rebuilding anything**.
- Hot-reload on a keybind, so you don't restart the game to try a number.

### 5.3 Convert, don't just spawn

Two supply lines for riot participants:

1. **Conversion (primary, nearly free).** Sweep ambient peds the game already streamed, assign
   them to a faction by model/type, restyle their relationships, loadout and tasks. The engine's
   own population system keeps doing the expensive work.
2. **Injection (secondary, budgeted).** Spawn only what has no ambient analogue — soldiers,
   aliens, SWAT waves, gang reinforcements — under a hard cap.

Getting this ratio right is what separates a riot mod that runs from one that hitches and
crashes. Every existing riot mod I looked at spawns; that's why they're small.

### 5.4 Scheduler and LOD

Never touch every ped every tick. A round-robin queue processes N entities per tick across a
~500 ms cycle, with distance bands:

| Band | Range | Treatment |
|---|---|---|
| Near | ≤ 150 m | Full behaviour, retasking, blips |
| Mid | 150–400 m | Relationship + loadout only, cheap retask on a slow cadence |
| Far | > 400 m | Released with `MarkAsNoLongerNeeded`, deregistered |

Budget is adaptive: measure our own tick cost, shrink N when frame time rises. The debug
overlay (§7) shows this live so your test reports can say *"it hitched when active peds hit 90"*
instead of *"it hitched."*

### 5.5 Feature flags

Every extra in §7 is a named flag with a declared default, readable from config, overridable
per-mode, and surfaced as a menu switch. The core riot loop must run correctly with all flags
off — that's the acceptance criterion, and it's what lets us bisect misbehaviour to a single
subsystem when you report something looking wrong.

### 5.6 Cleanup contract

Every entity we create or modify is registered. On mode stop, script abort, player death,
mission start, or fast-travel we restore or release. Missing this is how riot mods poison saves
and leak until crash — `Aborted` handler is not optional.

### 5.7 Config parsing

JSON via **Newtonsoft.Json merged into our assembly** (ILRepack, which runs fine on Linux).
Ships as one DLL, so we can never collide with another mod's copy of Newtonsoft in `scripts/`
— a classic and very annoying SHVDN failure mode.

---

## 6. Feature design

### 6.1 Factions

A faction is: `id` + relationship group + ped source (models or conversion filter) + weapon
preset + blip style + behaviour profile + reaction profile + optional vehicle set + optional
territory.

Relationships come from `ADD_RELATIONSHIP_GROUP` / `SET_RELATIONSHIP_BETWEEN_GROUPS`, with the
standard scale (`0` companion, `1` respect, `2` like, `3` neutral, `4` dislike, `5` hate).
The matrix is declared in the mode file, so a mode is genuinely just "who hates whom, and how
do they show it."

The **player is their own group**, which unlocks a lot for free: be a target, be neutral press
walking through a war zone, or join a side.

### 6.2 Weapon presets

Presets are per-faction, not global — police can be on *Realistic* while rioters are on *Chaos*.

| Preset | Contents | Also sets |
|---|---|---|
| Realistic | Melee, pistols, a few SMGs, occasional shotgun | Low ammo, low accuracy, no armour |
| Armed & Armoured | Rifles, SMGs, shotguns | Body armour, raised health, mid accuracy |
| Military | Carbines, MGs, grenade launchers, RPGs | Full armour, high accuracy, high combat ability |
| Chaos | RPG, minigun, railgun, firework launcher, explosives | High accuracy, no restraint |
| Custom | Weighted list from JSON | Per-entry ammo, attachments, tints |

Alien loadouts are a preset too: `RayGun`, `UnholyHellbringer`, `Widowmaker`, `CompactEMPLauncher`.

### 6.3 Police that act like police

Deliberately **not** built on the wanted system — that's player-centric and fights us. Instead:

- A `POLICE` faction hostile to whoever the mode says, with real cop models and liveried cars.
- **Sirens on**: `SET_VEHICLE_SIREN(veh, true)`, lights running, with an option for
  lights-without-audio.
- **Ploughing through crowds**: `SET_PED_STEERS_AROUND_PEDS/VEHICLES/OBJECTS(ped, false)` plus
  `SET_DRIVER_AGGRESSIVENESS(1.0)` and a ram-flavoured `TASK_VEHICLE_MISSION_PED_TARGET`.
  This is the exact "mercilessly smashing into people" you described, and it's a few natives.
- **Escalation waves** that mimic star levels without using them, driven by a per-mode "heat"
  value that climbs with kills and time:

  | Tier | Units |
  |---|---|
  | 1 | `Police`, `Police2` cruisers, `Cop01SMY/SFY` |
  | 2 | + `Police3`, `Police4` unmarked, `Hwaycop01SMY` |
  | 3 | + `Riot` / `Riot2` vans, `FBI2`, `Swat01SMY` |
  | 4 | + `Polmav` air support, snipers |
  | 5 | hand off to military lockdown |

- Combat attribute tuning per intensity (accuracy, seeing range, willingness to leave cover).
  Exact attribute IDs get confirmed against nativedb during implementation rather than trusted
  from memory — the commonly-cited numbers are close but not reliable.
- Player-relationship toggle: are you a bystander, a target, or on their side.

### 6.4 Military

Same machinery, different tier table: `Marine01–03`, `Blackops01–03`, `Armoured01/02`,
vehicles `Barracks`, `Crusader`, `Insurgent`, `PatriotMilSpec`, `Rhino`, `Annihilator`,
`Buzzard2`. Adds **checkpoints and roadblocks** — prop barricades plus a squad at chokepoints,
which is what actually sells "martial law" visually.

### 6.5 Custom and online vehicles

Config takes **model names, never hardcoded hashes**. The loader checks `IsInCdImage` and
`IsValid`, requests with a timeout, and on failure falls back to a declared vanilla substitute
and logs it. Net effect: add-on packs and MP-in-SP unlockers "just work" if you have them
installed, and nothing hard-crashes if you don't. This is the correct answer to "police and
military may have custom mods."

### 6.6 Civilian reactions

Per-faction reaction profile: `Flee`, `FightBack`, `Mixed(p)`, `Cower`, `Bystander`
(stand and film it on a phone — genuinely great texture). `Mixed(0.3)` = 30% of civilians turn
and fight, which is your "some will fight back," exposed as a menu slider.

### 6.7 Aliens

Vanilla-only path so it works with no extra downloads: `MovAlien01` peds, ray weapons,
`SpaceDocker` and `Thruster`, green timecycle push, and an abduction gag (light beam +
upward attach) as a stretch. If you later want proper alien models, the by-name loader
(§6.5) already handles them.

### 6.8 Blips

Pooled, hard-capped, distance-limited, per-faction sprite and colour from config, toggleable
per faction in the menu. Uncapped ped blips are a known framerate and minimap killer.

---

## 7. Extras beyond the original vision

**Everything in this section is optional and individually toggleable.** No extra is ever
load-bearing: each one is a config flag plus a menu switch, the mod runs correctly with all of
them off, and a mode file can opt in or out per-mode. That's a hard architectural rule, not a
nice-to-have — it keeps the core testable in isolation and means a broken extra can never take
the mod down with it.

### Confirmed in scope

| # | Extra | Default |
|---|---|---|
| 1 | **Fires and debris.** Burning cars, tyre fires, smoke columns. The visual language of a riot, and the cheapest big win on the list. | On |
| 2 | **Escalation phases.** The riot *builds* — unrest → looting and fires → lockdown. Mostly a timer over machinery we're building anyway. | On |
| 3 | **Riot zones.** Confine to a radius or a named neighbourhood. Solves performance *and* creates the best moments: Vinewood under martial law while Sandy Shores is calm. | On, citywide selectable |
| 4 | **Pick a side.** Join the rioters, or be neutral press until you shoot first. Nearly free once the player is its own relationship group. | Off (neutral-observer default) |

### The other six (only four fit in the picker — your call on these)

| # | Extra | Effort | My read |
|---|---|---|---|
| 5 | **Purge specifics.** Countdown, announcement, curfew window, everyone home at 07:00. | Low once phases exist | Strong yes — it's the mode's whole identity |
| 6 | **Weather and timecycle per mode.** Purge = night and orange haze, aliens = green fog, military = overcast. | Trivial, one line per mode | Yes |
| 7 | **Looting behaviour.** Peds converge on store entrances with carry anims. | Medium | Nice texture, first thing I'd cut if M5 runs long |
| 8 | **Debug/stats overlay.** Active peds, spawns, kills per faction, tick cost, budget headroom. | Low | Yes, and selfishly — it's my only telemetry |
| 9 | **Hot-reload configs on a keybind.** | Low | Effectively non-negotiable for how we work |
| 10 | **Profiles.** Save a tuned setup by name; share as a file. | Low-medium | Yes, but late — M7 |

Items 8 and 9 I'd argue are workflow infrastructure rather than features, and I'd build them in
M0–M2 regardless unless you object. 5 and 6 are near-free. 7 and 10 are the genuinely optional ones.

---

## 8. Risks

### 8.1 Scale (the real one)
Ped and vehicle pools are small; a riot mod exists to overfill them. Mitigation: convert-first,
adaptive budget, zones, LOD, hard caps, startup check that warns when gameconfig/Heap Adjuster
aren't detected. **"Everything" mode is explicitly the mode that needs the extra tooling**, and
should say so in the menu.

### 8.2 Update fragility
ScriptHookV and SHVDN hook by memory pattern. Every Rockstar patch breaks them until their
authors update. We inherit that entirely. Document it in the README; don't promise otherwise.

### 8.3 Legacy vs. Enhanced divergence
**Legacy is the reference target** — that's what you run, so that's what gets tested. The
single-build story via SHVDN Enhanced still holds and I'll keep Enhanced compiling and
nominally working, but I won't claim Enhanced support we haven't verified. Enhanced becomes a
fix-on-report path, and the README should say exactly that rather than implying parity.

Practical upside of Legacy as reference: it's the mature ecosystem, so gameconfig, Heap
Adjuster, add-on vehicle packs and MP-in-SP unlockers are all well-trodden there. §6.5 and M4
get easier.

### 8.4 Online content in SP
May not stream or may despawn. Optional-with-fallback, never a hard dependency.

### 8.5 I cannot run the game
The largest project risk. Everything above is compile-verified and native-name-verified; none of
it is behaviour-verified. Mitigation is the whole of §9 and §10: tiny milestones, heavy logging,
config hot-reload, and an in-game overlay so your feedback is precise.

---

## 9. Milestones

Each one ends in a build you can drop in and test in under five minutes.

| # | Deliverable | Your test |
|---|---|---|
| **M0** | Skeleton. Loads, logs a version banner, F6 opens an empty LemonUI menu. Feature-flag system and CI publishing the DLL. | Does it load on *your* setup, and does the log appear? |
| **M1** | Faction engine: relationship matrix, conversion sweep, loadouts, blips, cleanup. One mode: Pedestrians. | Do peds actually fight? Does stopping restore the world? |
| **M2** | Menu v1: mode picker, intensity, **riot zones** (radius / neighbourhood / citywide), faction toggles, weapon preset picker, hot-reload, debug overlay. | Is it navigable, and does confining the riot to a zone hold? |
| **M3** | Police response + civilian reaction model (flee / fight / mixed slider) + **pick a side**. | The centrepiece — does "sirens on, ploughing through crowds" actually read right? |
| **M4** | Military, roadblocks, model-by-name loader with fallbacks. | Do your add-on/online vehicles resolve? Does it degrade cleanly without them? |
| **M5** | Criminals (gang territories), Animals, Aliens. **Escalation phases** and **fires/debris**. | Are animals worth keeping, or a joke mode? Does a riot that builds feel better than one that just is? |
| **M6** | Purge (timed event, curfew, announcement, timecycle) and Everything. | Where does performance break, and at what ped count? |
| **M7** | Custom faction builder in-menu, presets, profile save/load. | Can you build a faction without touching JSON? |
| **M8** | Polish: looting, perf pass, docs, release packaging. | Ship it. |

M0–M3 is the honest vertical slice. If M3 feels good, the rest is content.

Every confirmed extra lands behind its own flag, so any of M2's zones, M3's pick-a-side or M5's
phases and fires can be switched off in the field without a rebuild if it misbehaves.

---

## 10. How we work

- **I build, CI packages, you test.** Every push produces a downloadable zip: DLL + configs +
  install notes. No build environment on your machine.
- **Logs are my eyes.** `TonightsTheNight.log` records version, edition/hook detected, config
  load results, model resolution failures, spawn caps hit, tick timings, and every caught
  exception. When something's wrong, the log plus a sentence from you should be enough.
- **`docs/TESTING.md`** carries numbered scenarios with expected observations, so "test M3"
  means something specific rather than "go play."
- **Config-first tuning.** If a number feels wrong, change it in JSON and hot-reload. Only ask
  me to rebuild for behaviour, not for balance.

---

## 11. Open questions

1. ~~Which edition do you run?~~ **Answered: Legacy.** Reference target set (§8.3).
2. **What's already installed?** ScriptHookV, SHVDN (which one), gameconfig, Heap Adjuster,
   LemonUI, any add-on vehicle packs or MP-in-SP unlockers. Changes what M4 can assume.
3. **F6 or F7?** Going with **configurable, default F6** unless you say otherwise — F7 is
   already taken by both Los Santos Riot Mod and Ped Riot/Chaos Mode, F10 by PedPurge. We won't
   try to detect clashes, just document them.
4. **Story mode only, or should FiveM be on the roadmap?** It's a different runtime; assuming
   single-player unless you say otherwise.
5. ~~Cut list.~~ **Partly answered.** Fires, escalation phases, riot zones and pick-a-side are
   in, all as optional toggles (§7). Items 5–10 in the new §7 table are still open — my
   recommendation is yes to Purge specifics, timecycles, the debug overlay and hot-reload;
   defer looting and profiles.
