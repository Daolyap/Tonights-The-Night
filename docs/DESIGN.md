# Design notes

Why this mod is built the way it is. Useful if you are reading the source, writing your own
riot mode, or wondering why an obvious approach was not taken.

## Factions are the only unit

A riot mode is a set of factions plus a matrix of who hates whom. A faction is a relationship
group, a pool of peds, a loadout, a reaction profile and a blip style. Everything else —
police, military, aliens, animals, the crowd — is that same object with different values.

This is why a custom faction needs no code. It is also why the mode files are worth reading:
they are the whole vocabulary.

## It never uses the wanted system

Riot police are a faction. They are hostile to civilians because a relationship group says so,
not because anyone has stars.

That started as a technical decision — the wanted system is player-centric, so it cannot express
"these people hate those people" — and turned out to be the important compatibility decision.
The wanted system is exactly what a police overhaul, an LSPDFR-style pack or a 6-star mod
replaces. By never touching it, this mod and those never collide.

Every wanted-system call routes through one gate in `Compatibility` that defaults to refusing.
There is precisely one exception, and it is opt-out: during The Purge all crime is legal, so the
wanted *ceiling* is lowered for the window and restored to whatever it was afterwards.

## The player is not a special case

Factions point their hostility at the vanilla `PLAYER` relationship group. The player is never
moved out of it.

This was learned the hard way. Moving the player into a custom group breaks every vanilla system
that keys off `PLAYER` — most visibly the police, whose hostility is declared against that group,
so with the player no longer in it they simply stop responding.

Standing is resolved **per faction**, falling back to the mode. That is what lets Martial Law
work: the army wants you and the crowd it is shooting at does not. One answer for a whole mode
had civilians spending the riot punching the player instead of the soldiers.

## Convert, don't spawn

The ped pool is the ceiling every riot mod eventually hits, and spawning is what exhausts it.
Ambient pedestrians have already been streamed and paid for, so the primary supply line is
recruiting them, and spawning is reserved for factions with no ambient equivalent — soldiers,
aliens, animals, SWAT.

Every spawning faction carries its own cap, waves are timed rather than continuous, and the
whole system stops at the global `engine.maxTrackedPeds`.

## Work is budgeted, not exhaustive

The script ticks every frame, because LemonUI needs it for input, the overlay flickers without
it, and the density natives are per-frame. But nothing walks the whole tracked list on a tick.
Recruiting and retasking are round-robined under a budget that shrinks when frame time rises and
grows back when it recovers.

## Everything is put back

A riot mod that forgets to release its peds leaks until the game crashes, and leaves the world
subtly wrong across saves. `EntityRegistry` records the relationship group and model of every ped
it touches so all of it can be undone — on stop, on script reload, and on abort, which is not a
polite shutdown.

Entity handles are recycled, so a dead rioter's handle can be reissued to a seagull. Every
tracked ped's model is compared before it is acted on. That is why blips stopped appearing on
birds.

## Config is four layers

Live menu changes, then the running mode's overrides, then `user.json`, then `defaults.json`.
Each overrides the one below. The menu writes to the live layer, which is why a setting can be
changed mid-riot, saved to a profile, or written into `user.json` without any of those being
special cases.

`defaults.json` is rewritten on every version change. `user.json` is never touched.

## The JSON parser is hand-rolled

Deliberately. It means the mod ships as one DLL with no assembly-resolution tricks and no chance
of clashing with another mod's copy of a JSON library. It accepts comments, trailing commas,
bare keys and single quotes, because config written by hand should forgive being written by hand.
It never throws; a malformed file is a log line and a fallback.

## Community-documented values live in config

Several natives this mod depends on — combat attribute IDs, vehicle mission types, driving
style flags, `IS_PED_ARMED` type flags, `TASK_HELI_MISSION`'s arguments — are documented by the
community rather than by Rockstar, and are the most likely things here to be quietly wrong.

Every one of them is a config value rather than a constant. If rioters stand around doing
nothing, or helicopters park on a roof, that is a number in `defaults.json` and a reload rather
than a new build.

## Hostility has no size, so engagement is budgeted

A relationship group is a set with no cardinality. "The army hates the player" is one fact, and
it is true of every soldier in the district simultaneously — so the moment one of them could see
you, all of them were individually in a combat task against you. That is correct as a model of
hostility and wrong as a fight.

`Attention` puts a ceiling on how many are engaging the player at once and sends the rest back to
the targets they can see. It is deliberately a rotation rather than an amnesty: you are still
hated, the ones sent away come back round later, and nothing about the relationship matrix
changes. Hostility stays declarative; only the acting on it is rationed.

The same argument applies to drivers, one level down. Each driver rolls independently for what to
do about the riot, and a weight of "2 in 10 come after the player" says nothing about how many
drivers have already rolled it. On a busy street that is four cars converging on you at once,
which reads as the traffic being out to get you rather than as a riot. `VehicleTasking` counts
its own assignments.

## The pursuer is not invincible, it is illegible

An unkillable pursuer has to be beatable or it is not a fight. Making its health very large only
moves the problem: the player has no way to tell whether they are getting anywhere, so it reads
as invincible whether or not it is.

So the hunter's real health is pinned and meaningless, and damage is accounted separately into a
Resolve pool at a fraction of its value — in whole during a stagger. Staggers are caused by what
it does (the recovery after a rush, a slam, a phase change) rather than by anything the player
can force directly, which turns the fight into a rhythm rather than a damage race. Two escape
valves keep that fair: sustained damage fills a Break meter that staggers it independently, so
accuracy alone is a route; and any single large hit is always worth more than its share, so
explosives, vehicles and whatever a physics mod does all count.

All of which is only legible because there is a bar on screen. The bar is not a convenience —
without it the entire design reads as an invincible ped.

## Contagion is a third supply line

Every mode is fed by two things: converting the ambient crowd near the player, and spawning what
has no ambient equivalent. Both are capped, so a riot rises to a level and stays there.

Contagion is the third: a faction that grows by *reaching* people. Nothing is spawned and nothing
is recruited from a radius around the player, so it spreads outward from where it started, thins
where it is being killed, and gets worse the longer it is left. It is also the only mechanic here
that can be contained by killing the right people rather than by changing a setting.

## The navmesh is a preference, not a requirement

`GET_SAFE_COORD_FOR_PED` answers "where is the nearest point a pedestrian could stand" by asking
the pedestrian navmesh, which downtown is everywhere and in the desert, the hills, the docks and
half of Blaine County is nowhere.

Every caller that treated a zero return as "nothing arrives" therefore worked perfectly in the
city and silently produced nothing at all outside it. `Ground` tries the navmesh, then the ground
itself, then the nearest road. A soldier walking out of scrub is fine; no soldier is not.

## Lists in config are sets, not preference orders

`ModelResolver` has two lookups and the difference between them was a bug for a long time.
`TryResolve` returns the first candidate the install has, which is right when the list is a
preference order — the craft models, the hunter's models, a fallback chain. Every *spawn profile*
in the mod is a set to draw from, and using the ordered lookup for those meant a faction
declaring four vehicles always got whichever was written first: every armoured column was
Rhinos, and every coastal patrol was the one marine model that is a man in a white t-shirt.

`TryPick` draws at random from the resolvable candidates, and is used per ped rather than per
wave so a squad of six is six people.

## What is checked without the game

Nothing that calls a native can be tested outside GTA V. Everything else is, in CI:

- the JSON parser, including malformed input
- every shipped riot mode — relations reference real factions, factions can actually get
  members, spawn caps are sane, `fromPhase` exists, and model names look like game names
  (`s_m_y_marine_01`, not SHVDN's `Marine01SMY`, which compiles fine and resolves to nothing)
- every weapon name in every shipped mode and preset, against a base-game weapon list
- crowd cohesion: two factions both recruited from the ambient crowd may not hate each other
  unless the mode declares `crowdFightsItself`
- config coverage in both directions — nothing the code reads is undeclared, and nothing
  declared goes unread
- release readiness — version numbers agree, debug aids are off, licence and docs exist
