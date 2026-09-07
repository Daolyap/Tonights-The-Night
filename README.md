# Tonight's The Night

A configurable riot mod for Grand Theft Auto V.

Nine riot modes built from **factions** — who they are, what they carry, who they hate, and how
they react when it starts. Modes and factions are JSON files, not code, so a custom faction needs
no build step. Every optional feature is a switch with its own settings block.

**Version 0.8.0.** Requires [ScriptHookV](http://www.dev-c.com/gtav/scripthookv/),
ScriptHookVDotNet 3 and [LemonUI](https://www.gta5-mods.com/tools/lemonui). **F6** opens the
menu.

## Modes

| Mode | |
|---|---|
| Pedestrian Riot | Two mobs form out of the crowd |
| Pedestrian Chaos | A free-for-all. Only your passengers are on your side |
| Gang War | Four gangs settle up at once |
| Police State | Sirens on, driving through crowds, helicopters overhead |
| Martial Law | Police, then infantry, then armour, helicopters, boats and jets |
| Animal Uprising | Deliberately silly |
| Invasion | Ships overhead, and aliens walking out from under them |
| The Purge | Twelve minutes, then it stops |
| Everything | All of it, escalating through four phases |

## What it does

**Car chases.** Hurt someone and drive away, and their side comes after you — a whole carload of
them, who run to the same vehicle and get in before it sets off. Passengers lean out and shoot.

**Rioters who drive.** Someone already behind a wheel decides for themselves: get out and fight,
chase another rioter, come after you, or get out of your way. Four weights, all sliders.

**Escalation.** Riots build. Phases advance on elapsed time *or* body count, so a quiet riot
still progresses and a bloodbath escalates immediately. Factions declare which phase they join
at, which is what makes the army arrive late rather than at the first thrown punch.

**They have to see you.** Hostility is a state, not a fact. Break line of sight, stay quiet, and
the riot loses you and goes back to fighting itself. Firing a weapon gives you away through
walls; crouching and cover shorten how far they can pick you out.

**Reinforcements.** A faction that takes losses sends more, sooner. Meet the army with nothing
and the response stays a patrol; destroy three carloads and the next ones come in force.

**Riot zones.** Confine it to a radius around you or let it run citywide. The single most
effective performance control here.

**Weapon presets.** Realistic, armed and armoured, military, chaos, or your own weighted list.

**Plus** looting, fires, five profile slots, a debug overlay, and config hot-reload on **F5**.

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
