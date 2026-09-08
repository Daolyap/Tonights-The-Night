# Tonight's The Night

A configurable riot mod for Grand Theft Auto V.

Twelve modes built from **factions** — who they are, what they carry, who they hate, and how they
react when it starts. Modes and factions are JSON files, not code, so a custom faction needs no
build step. Every optional feature is a switch with its own settings block.

**Version 1.0.0.** Requires [ScriptHookV](http://www.dev-c.com/gtav/scripthookv/),
ScriptHookVDotNet 3 and [LemonUI](https://www.gta5-mods.com/tools/lemonui). **F6** opens the
menu.

## Modes

| Mode | |
|---|---|
| Pedestrian Riot | Two mobs form out of the crowd |
| **Tonight's The Night** | One thing is coming for you. Ground, air and water. It is beatable |
| Pedestrian Chaos | A free-for-all. Only your passengers are on your side |
| **Car Chase** | A price on your head that goes up. It ends when you do |
| Gang War | Four gangs settle up at once |
| Police State | Sirens on, driving through crowds, helicopters overhead |
| Martial Law | Police, then infantry, then armour, helicopters, boats and jets |
| Animal Uprising | Deliberately silly |
| Invasion | Ships overhead, and aliens walking out from under them |
| **Patient Zero** | An infection that spreads by reaching people, and a cordon that shoots everyone |
| The Purge | Twelve minutes, a siren, and then it stops |
| Everything | All of it, escalating through four phases |

## What it does

**Something that hunts you.** Tonight's The Night is one fight rather than a riot. Its health is
pinned and meaningless; damage goes into a separate pool at a fraction of its value, in whole
while it is staggered — and staggering it is a consequence of what it does, not something you can
force. Survive the ability, punish the recovery. Sustained fire opens it up on its own, and a big
hit always counts for more than its share, so the rocket launcher in your boot matters.

**An infection that spreads.** Every other mode is fed by converting the crowd near you and by
spawning, both capped. Patient Zero grows where its members physically are, so it moves outward,
thins where it is being killed, and gets worse the longer it is left.

**Car chases.** Hurt someone and drive away, and their side comes after you — a whole carload of
them, who run to the same vehicle and get in before it sets off. Passengers lean out and shoot.
Or pick Car Chase and skip the provocation: five waves of escalation, counted from your own body
count rather than a timer.

**Rioters who drive.** Someone already behind a wheel decides for themselves: get out and fight,
chase another rioter, come after you, or get out of your way. Four weights, all sliders.

**Escalation.** Riots build. Phases advance on elapsed time *or* body count, so a quiet riot
still progresses and a bloodbath escalates immediately. Factions declare which phase they join
at, which is what makes the army arrive late rather than at the first thrown punch.

**The city goes with it.** The power fails, rioters drag burning barricades across the roads,
smoke columns rise over them, and helicopter searchlights sweep the dark. All phase-gated, so the
riot earns it.

**They have to see you.** Hostility is a state, not a fact. Break line of sight, stay quiet, and
the riot loses you and goes back to fighting itself. Firing a weapon gives you away through
walls; crouching and cover shorten how far they can pick you out.

**Reinforcements.** A faction that takes losses sends more, sooner. Meet the army with nothing
and the response stays a patrol; destroy three carloads and the next ones come in force.

**Riot zones.** Confine it to a radius around you or let it run citywide. The single most
effective performance control here.

**Weapon presets.** Melee only, realistic, armed and armoured, military, chaos — or build your own
in the menu, weapon by weapon, with a weight on each.

**Only so much of it is aimed at you.** Hostility is a property of a group and a group has no
size, so without a ceiling every soldier who can see you is individually trying to kill you. A
handful come; the rest carry on fighting each other and rotate back round later. There is a
ceiling on how many cars can be after you, too, and a driver who is after you tails you rather
than driving through you.

**One slider for how much.** Intensity scales every spawning faction's wave size, ceiling and
arrival rate at once. It is the answer to any mode being too much or not enough.

**Plus** looting with props that sit correctly in a hand, a configurable blackout with a flicker,
fires, five profile slots, a debug overlay, and config hot-reload on **F5**.

## What it does not do

It does not change your time of day, your weather or your colour grading. Those switches exist
and are all off.

It does not touch the wanted system. Riot police are built from relationship groups instead, so a
police overhaul, an LSPDFR-style pack or a 6-star mod runs alongside it untouched — you can be
chased by a mob while separately holding four stars. The one exception is opt-out: during The
Purge all crime is legal, so your wanted ceiling drops for the window and is restored exactly as
it was.

It never recruits police, SWAT, army, medics, firefighters or story peds.

## Install

`TonightsTheNight.dll` into `GTA V/scripts/`, alongside `LemonUI.SHVDN3.dll`. Full detail in
[`INSTALL.md`](INSTALL.md) — including the gameconfig and Heap Adjuster you want before the
larger modes.

## Documentation

| | |
|---|---|
| [`INSTALL.md`](INSTALL.md) | Requirements, install, configuring, writing your own modes |
| [`docs/DESIGN.md`](docs/DESIGN.md) | How it works and why it is built this way |
| [`docs/PUBLISHING.md`](docs/PUBLISHING.md) | Release checklist and packaging |
| [`CHANGELOG.md`](CHANGELOG.md) | What changed in each version |

## Building

Targets .NET Framework 4.8 because ScriptHookVDotNet does, but builds on any platform with the
.NET SDK — no Mono, no Windows required.

```
dotnet build src/TonightsTheNight/TonightsTheNight.csproj -c Release
dotnet run --project tests/ConfigTests/ConfigTests.csproj -c Release
```

CI builds every push and attaches a ready-to-install archive as an artifact.

## Credits

Built on [ScriptHookV](http://www.dev-c.com/gtav/scripthookv/) by Alexander Blade,
[ScriptHookVDotNet](https://github.com/scripthookvdotnet/scripthookvdotnet), and
[LemonUI](https://github.com/LemonUIbyLemon/LemonUI) by Lemon. Neither is redistributed here —
install them yourself from the links above.

## Licence

[MIT](LICENSE). Do what you like with it, including reusing the faction and mode system in your
own mod; keep the copyright notice.
