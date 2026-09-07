# Changelog

## v0.7.0 — arrivals, not appearances

Spawning is rebuilt around how the game's own dispatch works. Three of the four complaints
behind this had a specific cause in the code.

### Fixed — units materialised in front of you

**Nothing checked whether you were looking at the spawn point.** A bearing and a distance were
picked and whatever was being made was dropped straight onto it. Arrivals are now placed
off-screen, with the first workable point kept as a fallback so an open hillside still gets its
wave.

**Vehicles spawned at a random heading.** Literally `random * 360` — which is why cars appeared
sideways across the carriageway facing a wall. They now spawn on a road node, facing the way the
road runs.

**They spawned too close to drive in.** Land factions now arrive 140–260m out and drive to you,
so what you see is a convoy coming down the road. Anyone who has to walk in instead gets a
shorter distance, or they would still be walking when the riot ended.

### Fixed — not enough of them in vehicles

`inVehicleChance` is up across every faction that owns vehicles. Police, NOOSE, infantry, armour
and the Martial Law police are now 100% — they arrive in something, always. Gangs are at 75–80%,
because a gang war partly on foot is right.

### Fixed — most aliens had no weapon

`GIVE_WEAPON_TO_PED` reports nothing, so a weapon that resolved to a valid hash but would not
attach left the ped empty-handed silently. Every ped is now checked with `HAS_PED_GOT_WEAPON`
afterwards, falls through to the rest of its faction's loadout, then to a plain pistol, and
whatever failed is named once in the log. The weapon is also forced into the ped's hands rather
than left holstered — a faction waiting for a target read as unarmed until the moment it found
one.

### Fixed — the police would not shoot you

This was mine, introduced with perception in v0.6.0. Their sight range was 60m and they spawn
70–150m away: they were outside their own sight range for the entire approach, so you read as
unseen, so they arrived **neutral** — and neutral police do not shoot.

Perception is now built to lose you only when you are genuinely hidden:

- Sight range 60m → **140m**
- Inside 30m, line of sight is not asked at all — somebody beside you knows you are there
- A car widens that to 54m, because a car is loud and large
- Anyone already shooting at you counts as seeing you
- Line-of-sight traces test the map only by default, so a bonnet or a bin is not concealment
- They keep searching for 20 seconds rather than 12

---

## v0.6.1 — QA pass before release

A full correctness review of the codebase turned up sixteen defects. All are fixed. Nothing here
is a new feature; several of them would have broken a fresh install.

### Would have broken a first install

**A config value the docs invite you to set killed the mod outright.** The percentage sliders did
not clamp their position, and LemonUI throws when a slider is set past its maximum — from inside
the menu constructor. `density.pedMultiplier: 4.0` produced step 27 of 20, an exception, and
"Tonight's The Night failed to start" with no menu to fix it from. Every hot reload threw too.

**Joining a side left you in a deleted relationship group.** Setting `player.side` moved the
player into a faction's group, and stopping the riot then deleted that group out from under
them. Every vanilla system keyed off `PLAYER` died — the police stop responding entirely — and
nothing put it back. This is the exact failure the code's own comments warn about.

**The parser could hard-crash the game.** Deeply nested JSON recursed until the stack gave out,
and a `StackOverflowException` cannot be caught in .NET: it takes the process, so GTA V would
close with no message and nothing in the log. Mode files are shared between players, so that
parser reads files written by strangers. Nesting is now capped at 64 levels — the deepest shipped
mode reaches about six.

### Fixed

- **The menu never paused the riot.** LemonUI closes the parent when you open a submenu, so
  tracking only the root meant the pause added in v0.5.0 was off for nearly all of the time the
  menu was actually on screen. F6 also opened a second menu on top of the first.
- **Reloading config froze your settings.** Refreshing a control re-entered its own change
  handler and wrote the value into the live layer, which outranks `user.json` — so after one
  reload those keys ignored the file for the rest of the session, and profiles saved settings you
  had never touched.
- **Fires stopped permanently after eight.** `maxActive` had become a lifetime total, so about
  seventy seconds into any mode there were no new fires or burning cars again.
- **`restoreWorldOnStop: false` leaked the entire registry.** The next mode found itself already
  at the ped ceiling and never recruited anyone.
- **Car thieves had their theft cancelled** six seconds in, and could be handed a second looting
  job while still walking to the first car.
- **Waves double-spawned** when a faction's vehicles were not installed, and the second vehicle
  of a wave could be abandoned in the street with its engine and siren running, tracked by
  nothing.
- **A recycled entity handle could unindex a live ped**, getting it recruited twice — two blips,
  conditioned and armed twice.
- **A negative `reinforcements.perLoss`** could divide the wave timer by zero.
- **The spawn ceiling was checked once per pass**, so every faction due at the same time could
  each add a full wave over it.
- **Blip counting walked the whole tracked list once per recruit** — about 1,400 iterations
  twenty times a second.
- **The perception scan re-walked the same peds** every check instead of advancing through the
  crowd.
- **Every log line opened and closed the file.** At the Debug level testers are asked to enable,
  that is synchronous disk I/O several times a frame — so the one session anybody is asked to
  capture was the one that stuttered, and the frame times in the log were polluted by the
  logging. Lines are now buffered and flushed on an interval, immediately for warnings and
  errors, and on shutdown.

---

## v0.6.0 — they have to actually see you

### Added — perception

Hostility used to be a permanent property of a relationship group. Every soldier in the district
was your enemy from the moment the mode started — through walls, around corners, from behind.
Standing in an alley two streets away did not help, because nobody ever needed to see you.

Being a target is now a state you can get out of. Nearby hostiles are sampled for a clear line
of sight; firing a weapon gives you away regardless of walls, as it should. Crouching or taking
cover shortens how far they can pick you out. Break away for twelve seconds and every faction
drops back to neutral — at which point their combat AI goes and finds somebody it *can* see,
which in a riot is never in short supply.

Neutral is not a truce. It is being unremarkable.

Losing you also tells the peds who were already fighting you, because a combat task that has
locked onto a target does not drop it just because a relationship changed underneath it. Only
the ones actually targeting you are cleared — clearing every nearby fighter would stop every
unrelated fight in the street at the same moment, which reads as the riot pausing rather than as
losing you.

Range, search time, stealth multiplier and whether a ped must be facing you are all settings.
The three you are most likely to want are in the menu under **Tuning**. Turn the whole thing off
there for the old behaviour.

### Fixed — a coyote could be assigned to drive a car

Nothing in the chase system checked whether the lead could drive. In Animal Uprising a provoked
coyote would be picked as a driver, fail to walk to the car, and then be *warped into the
driver's seat* by the boarding fallback.

### Changed

**No new chases start while they cannot find you.** One already under way keeps going on its own
give-up rules — a carload behind you does not forget where you are because you turned a corner.

---

## v0.5.0 — ships, jets, reinforcements, and a cheaper menu

### Fixed — the menu halving your frame rate

The most likely culprit is LemonUI's, not mine, but it is disableable. The instructional buttons
in the bottom-right corner are a **Scaleform**, which is one of the more expensive things a
script can put on screen, and it is redrawn every frame the menu is open. The banner is a texture
draw on top of that, and mouse support plus edge-of-screen camera rotation both do work per frame
whether or not you use them.

The menu now runs without any of those by default. It keeps its title, items and descriptions.
`menu.lightweight: false` gives you the banner and mouse back.

Separately, **the riot pauses its own expensive half while the menu is open** — recruiting,
spawning, retasking, chases and looting all wait. None of it needs to happen in the few seconds
you spend reading a slider, and it was landing on top of the menu's draw cost.

If the lightweight menu fixes it, it was the Scaleform. If it does not, the debug overlay's tick
time will say whether the remaining cost is the mod or the library.

### Added — reinforcements

Escalation phases answer *how long has this been going on*. They could not answer *how badly is
it going*, which is the difference between a deployment and a response: the army sent the same
two trucks every fifteen seconds whether it was walking through the crowd or being wiped out.

Every spawned member of a faction that dies now raises that faction's commitment. Waves get
bigger, arrive sooner, and the ceiling on how many can be on the street rises with them, up to
2.5× by default. Meet the army with nothing and the response stays a patrol; destroy three
carloads and the next ones come in force, with a notification when a faction steps up.

Per faction, deliberately — killing soldiers brings more soldiers, not more aliens.

### Added — craft over the invasion

GTA V's UFOs are props, not vehicles: there is nothing to fly and nobody to put inside one. So
they are held at altitude over the riot and drifted, and **the aliens spawn underneath them**.
That second half is what makes it read as an arrival rather than as a decoration.

Model names are resolved at runtime with fallback, as everything here is. If none of the
candidates exist in your install the invasion arrives without ships and says so in the log.

### Added — jets

Martial Law's final phase brings the air force. They stay high and circle: a fighter told to
attack a street mostly flies into a building, so the mission is a presence rather than a weapon.
Heights, speed and mission id are all in `features.air`.

### Changed

**The zone circle is off by default.** With the zone following you, a circle centred on your own
blip tells you nothing. Turn it on if you anchor a riot somewhere and drive away from it.

---

## v0.4.1 — release preparation

No gameplay changes. This is the pass that makes the repository something a stranger can
download rather than something one tester was being handed.

### Removed

**The file-locator menu item** and the startup notification that printed your config path. Both
existed to debug the mod's own installation during testing; the path resolution is still written
to the log, where it belongs for bug reports.

### Fixed

**The assembly version had drifted two releases behind** the version the mod reports in game —
0.2.0 in the DLL's file properties against 0.4.0 in the loading notification, which would make
any bug report unattributable. There is now a build check that fails if they disagree, and if
the changelog or readme has not been updated to match.

**Modes appeared in alphabetical filename order**, so the first thing a new player saw was
Invasion and Pedestrian Riot was seventh. Modes now declare their own menu position; anything
you write yourself goes to the end of the list.

**The install guide still promised riot police as future work.** They shipped in v0.2.0.

### Added

**A licence.** MIT.

**`docs/DESIGN.md`** — how the mod works and why it is built the way it is. Worth reading before
writing your own mode.

**`docs/PUBLISHING.md`** — the release checklist, the archive layout, and the listing text.

**A release-readiness test suite.** It checks that version numbers agree across the project, that
the debug overlay and verbose logging ship off, that the wanted system and emergency services are
left alone by default, that no development aids remain in the menu, and that the licence and
documentation exist.

**Proper release packaging.** CI now produces a `TonightsTheNight-vX.Y.Z.zip` laid out the way a
player expects — drag `scripts/` into the game folder — with the readme, install guide, changelog
and licence alongside it. Tagging `vX.Y.Z` publishes a GitHub release with the archive attached.

Dependencies are linked rather than bundled: their licences are theirs to distribute under, and a
stale bundled copy of LemonUI is worse for a user than no copy.

### Removed from the repository

The throwaway design plan and the tester-specific script, both of which were written to one
person and had gone stale. Their durable content is in `docs/DESIGN.md`.

---

## v0.4.0 — the crowd picks the right enemy

Your diagnosis was right and it was one design mistake, not several: **a mode had one answer for
how the whole riot regarded you.** So in Martial Law the army hated you and the civilians hated
you too, and the crowd you were meant to be running alongside spent the riot punching you
instead of the soldiers shooting at them.

### Fixed — who fights whom

**Player standing is now per faction.** Each faction says how it regards you; the mode is only
the fallback. In Police State, Martial Law, Invasion and Animal Uprising the civilians are now
neutral to you and hostile only to whatever is attacking them. The armed side still wants you.

**Civilians are one crowd, not two mobs.** Nothing in those modes makes pedestrians hostile to
each other any more, so they unite. Some run, some fight back — fight-back is up from 20-30% to
45-55%.

**The menu default changed** from "Target" to "Mode's Own". The three forcing options are still
there, but they force one answer on everybody, including the people you would otherwise be
running with.

**A build check now catches this class of bug.** Two factions both recruited from the ambient
crowd may not hate each other unless the mode explicitly declares `crowdFightsItself` — which
Pedestrian Riot, Pedestrian Chaos and The Purge do, because there it is the point.

### Fixed — rioters in cars

**Driving past at speed no longer produces a street of abandoned cars.** Every driver was being
given "fight the nearest enemy", which makes a ped stop in the road and get out — so all of them
made the identical decision, which reads as a scripted trap rather than a city coming apart.

A driver now rolls once for what they do, and keeps that decision:

| | Default weight |
|---|---|
| Stop, get out and fight | 3 |
| Chase another rioter | 3 |
| Come after you | 2 |
| Drive away from you | 2 |

Drivers and passengers shoot from the windows if they have something to shoot with. All four
weights are sliders under **Rioters Driving**.

### Fixed — the rest of the list

**The clock is never touched.** `setTime` and the purge's `setClock` both default off.

**No weather or colour grading.** The shipped modes no longer ask for any, and the whole ambience
feature is off by default. The switches remain if you want the look back.

**Never wanted during the purge.** It lowers your wanted *ceiling* for the window rather than
clearing stars, and puts back exactly what it was — so a six-star overhaul gets its six back the
moment the purge ends, and is untouched outside the window.

**The purge ends.** Its remaining time was frozen at the starting value, so every display of it
was a stopped clock. It now counts down, logs a line a minute, shows on the debug overlay, and
the mode's phases end inside the window instead of running past it.

**The alien invasion looked like a man in black.** A ped created without a component variation
keeps component 0 in every slot, which for some models is half an outfit and for others an
untextured black figure. Every spawned ped now gets its default outfit.

### Added — Pedestrian Chaos

A free-for-all. Everyone kills everyone, police included, and you are in it. The only people on
your side are the ones sharing your car — that still works, because recruits inherit the faction
of whoever they are riding with.

### Added — Martial Law actually takes control

Police from the start (they are the ones who call the army in), then infantry, then armour, then
helicopters holding station overhead, and coastal patrol boats where there is water to put them
on. Troop carriers arrive carrying troops: two vehicles a wave, six to a truck, every nine
seconds, up to thirty-two infantry on the street.

Police State got the same treatment — two cars of four every eight seconds, riot vans of six,
and a police helicopter in the final phase.

### Changed — spawn rates

Roughly doubled across every mode. Waves are bigger, more frequent, and can now be several
vehicles at once. Spawning also respects the global ped ceiling, which it did not before — with
five spawning factions in Martial Law their individual caps add up to well past the engine
budget, and nothing was stopping them.

---

## v0.3.0 — chases, looting, weapon presets

The three things from the original vision that were still only a plan.

### Added — car chases

Hurt someone and then drive away, and their side comes after you.

The trigger is **provocation, not proximity**: shooting someone, running them over, or killing
them puts a grudge on their faction for 45 seconds. Try to drive off while that grudge is live
and a carload forms up behind you. Standing still in a car does not qualify — that should get
you dragged out of it, not tailed.

It is a **carload**, not a car. The nearest angry ped becomes the driver, their nearest allies
run to the same vehicle and get in, and the chase does not start until everyone is aboard. They
pile in on foot where there is time and get warped in when there is not, because a chase that
never leaves the kerb is worse than one that starts slightly too neatly. Passengers lean out and
shoot; the driver keeps both hands on the wheel.

They prefer a car the faction is already sitting in — which combines with the existing "people
sharing a car are on the same side" rule — and otherwise take the nearest empty one. Never an
occupied car: hauling a stranger out can reach a mission ped or one another mod owns.

They give up if you get 320m ahead and stay there for twelve seconds, if the car is wrecked, or
after two and a half minutes. All of that is on sliders, along with crew size, how many carloads
can be after you at once, drive-bys, and a "ram instead of chase" switch.

**No part of this touches the wanted system.** A chase is peds, a car and tasks — so a police or
wanted overhaul carries on underneath it, and you can be chased by a mob while separately having
four stars from the game's own police.

### Added — looting

GTA has no shop interiors to break into, so this is built from what the engine does do well: a
prop in someone's hands and somewhere else to be. A man running down the middle of the street
with a television reads as looting instantly.

Some looters take a parked car instead and drive off in it. Gated on the escalation phase, so it
starts once the riot has been going a while rather than in the first thirty seconds — and mostly
done by the people who were never going to fight, which stops it thinning out the riot.

### Added — weapon presets

The five from the original plan, as one picker under **Weapons**:

| Preset | |
|---|---|
| **Mode's Own** | Default. Leaves each mode's loadouts exactly as their author balanced them |
| **Realistic** | Bats, bottles, crowbars, the odd pistol. Sustains itself the longest |
| **Armed And Armoured** | Everyone tooled up and wearing a vest. Firefights instead of brawls |
| **Military** | Carbines, machine guns, grenades. Short, because everything dies quickly |
| **Chaos** | Rockets, miniguns, fireworks and fire extinguishers. Not meant to be balanced |
| **Custom** | Your own weighted list in `user.json` |

A preset carries armour and how many people are armed at all, not just the weapons, because
those are the same decision — splitting them across three sliders would only let you build the
incoherent middle.

A preset replaces the loadout of the factions drawn from the **ambient crowd** and leaves spawned
ones alone. Picking Military arms the mob with carbines; it does not re-equip the actual army,
which already has the kit its mode author gave it. Animals stay animals.

### Testing

New in CI: the shipped weapon presets, and a base-game weapon list that every weapon name in
every shipped preset and mode is now checked against. A wrong weapon name is completely silent
in-game — the faction just goes out unarmed — so it is checked here instead. The checker has its
own tests, because one that says yes to everything would pass every check while catching nothing.

---

## v0.2.0 — the rest of the mod

All eight riot modes, spawned factions, escalation, zones, fires, weather and profiles.

### Fixed — the bug behind both symptoms

**Police ignored you, and rioters attacked you when set to Ignored.** One cause: the mod moved
the player into its own relationship group. Every vanilla system that keys off `PLAYER` —
police response most visibly — stopped applying, because you were no longer in the group their
hostility is declared against. Factions now point their hostility *at* the vanilla `PLAYER`
group instead of the player being moved out of it. Your OIV pack's police should behave normally
again.

**Everyone ran off.** Clearing a ped's flee attributes isn't enough: ambient events fire
constantly during a riot — a gunshot, a scream, a car mounting the pavement — and each one can
pull a ped out of its fight task into a panic run. Fighters now block those events; panickers
still receive them. This is the difference between a crowd that fights and one that scatters at
the first shot.

### Added — seven new modes

| Mode | What it is |
|---|---|
| **Gang War** | Four gangs settle up at once, spawned with their own vehicles and colours |
| **Police State** | Sirens running, driving through crowds, NOOSE arriving in phase two |
| **Martial Law** | Infantry, then an armoured column |
| **Animal Uprising** | Coyotes, mountain lions and boars. Deliberately silly |
| **Invasion** | Aliens with the Up-n-Atomizer, Unholy Hellbringer and Widowmaker — all already in your game files |
| **The Purge** | Twelve minutes, announcement, countdown, then it ends and everyone goes home |
| **Everything** | All of it, escalating through four phases |

### Added — systems

- **Spawned factions.** Soldiers, police, aliens and animals have no ambient equivalent to
  convert, so they arrive in waves, on foot or in vehicles, capped per faction.
- **Escalation phases.** Riots build. Phases advance on elapsed time *or* body count, so a quiet
  riot still progresses and a bloodbath escalates immediately. Factions declare which phase they
  join at — that's what makes the army arrive late rather than at the first punch.
- **Riot zones.** Radius or citywide, optionally following you, drawn on the map. The cheapest
  performance control there is.
- **Police driving.** Sirens, aggression, and `SET_PED_STEERS_AROUND_PEDS(false)` — the one
  native that turns "a police car arrives" into "a police car arrives through the crowd". Still
  no contact with the wanted system.
- **Fires, weather, time and colour grading** per mode, all individually switchable.
- **Profiles.** Five slots, storing only the settings you changed so they survive updates.
- **Skip Phase** in the menu, for testing and impatience.

### Testing

Every shipped mode is now validated in CI: relations reference real factions, factions can
actually get members, spawn caps are sane, `fromPhase` exists, and model names look like game
names rather than SHVDN enum names — `s_m_y_marine_01`, not `Marine01SMY`, which compiles fine
and resolves to nothing.

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
