# Publishing a release

Everything needed to put a version on GTA5-Mods.com, in order.

## 1. Before you tag

The test suite covers most of this and fails the build if any of it is wrong:

```
dotnet run --project tests/ConfigTests/ConfigTests.csproj -c Release
```

It checks that the version in `TonightsTheNight.csproj` matches `DefaultConfig.Version`, that
`CHANGELOG.md` has an entry for it, that the README names it, that the debug overlay and verbose
logging ship off, that no development aids are left in the menu, and that the licence and docs
exist.

What it cannot check, and you have to. Anything that calls a game native is unverifiable
outside GTA V, so this list is the whole of the real QA.

**It loads and shuts down**

- [ ] Clean install, no other script mods: it loads, the notification appears, F6 opens the menu.
- [ ] Your usual mod list: it loads and nothing else broke.
- [ ] `scripts/TonightsTheNight/` is created with defaults, user.json, modes and profiles.
- [ ] Stop a riot, save, reload: no blips, no props in anyone's hands, nobody stuck in a parked
      car, everyone walking normally.
- [ ] Alt-F4 mid-riot, restart: the world is clean and the log has its last lines.

**The three that would have broken a first install** (fixed in v0.6.1, worth confirming)

- [ ] Put `"density": { "pedMultiplier": 4.0 }` in user.json. The mod must still start.
- [ ] Set `player.side` to a faction id, run a mode, stop it, then start a fight with the police.
      You must still be able to reach a full wanted level.
- [ ] Drop a junk .json into `modes/`. It must be skipped with a log line, not crash the game.

**Behaviour**

- [ ] Martial Law: civilians fight the army, not you and not each other.
- [ ] Drive through a riot at speed: drivers do four different things, not all the same one.
- [ ] Break line of sight for fifteen seconds: they lose you and go back to fighting each other.
- [ ] Shoot someone from cover: gunfire gives you away through walls.
- [ ] Kill three carloads of soldiers: the next waves get visibly bigger.
- [ ] Hurt someone and drive off: a carload forms up and comes after you.
- [ ] Invasion: ships overhead, aliens arriving under them, complete alien skins.
- [ ] The Purge: it counts down, ends on its own, and your wanted ceiling comes back.

**Performance**

- [ ] Open the menu during a busy riot. Compare the frame rate to v0.5.0 if you can.
- [ ] Debug overlay on: tick time and peak tick under a full Martial Law deployment.
- [ ] Note the ped count where it starts to hurt.

**For the listing**

- [ ] Screenshots and, ideally, thirty seconds of video. This is what it is judged on.

## 2. Tag it

```
git tag v0.4.1
git push origin v0.4.1
```

CI builds the archive, attaches it to the workflow run, and publishes a GitHub release with it.

The archive is laid out the way a player expects — they drag `scripts/` into their game folder:

```
TonightsTheNight-v0.4.1/
  scripts/TonightsTheNight.dll
  README.md
  INSTALL.md
  CHANGELOG.md
  LICENSE
```

ScriptHookV, ScriptHookVDotNet and LemonUI are **not** bundled. They are linked instead: their
licences are theirs to distribute under, and a stale bundled copy of LemonUI is worse for a user
than no copy at all.

## 3. The listing

**Category:** Scripts → Gameplay
**Tags:** riot, chaos, gameplay, police, military, .net, lemonui

**Requirements to list:** ScriptHookV, ScriptHookVDotNet 3, LemonUI. Mention that a modern
gameconfig and Heap Adjuster are strongly recommended — GTA V's ped pool is small and this mod
exists to fill it.

---

### Description (paste this)

**Tonight's The Night** turns Los Santos into a riot, and lets you decide what kind.

Nine modes, from two mobs forming out of the crowd to the army taking the city street by street.
Every mode is built from **factions** — who they are, what they carry, who they hate, how they
react — and every faction is a JSON file, so making your own needs no code and no rebuild.

**The modes**

• **Pedestrian Riot** — two mobs form out of the crowd
• **Pedestrian Chaos** — a free-for-all. The only people on your side are in your car
• **Gang War** — four gangs settle up at once
• **Police State** — sirens running, driving through crowds, helicopters overhead
• **Martial Law** — police, then infantry, then armour, air and sea
• **Animal Uprising** — deliberately silly
• **Invasion** — aliens, using weapons already in your game files
• **The Purge** — twelve minutes, an announcement, a countdown, then it stops
• **Everything** — all of it, escalating through four phases

**What makes it different**

**Car chases.** Hurt someone and try to drive away, and their side comes after you — a whole
carload, who run to the same vehicle and get in before it sets off. Passengers lean out and
shoot.

**Rioters who drive.** Someone already behind a wheel decides for themselves: get out and fight,
chase another rioter, come after you, or get out of your way.

**Riots that build.** Phases advance on elapsed time *or* body count, so a quiet riot still
progresses and a bloodbath escalates immediately. Factions declare which phase they join at,
which is what makes the army arrive late rather than at the first thrown punch.

**Riot zones.** Confine it to a radius around you, or let it run citywide. One district in flames
while the rest of the map carries on is more interesting than uniform chaos — and it is the
single most effective performance control here.

**Weapon presets.** Realistic, armed and armoured, military, chaos, or your own weighted list.

Plus looting, fires, five profile slots, a debug overlay, and config reloading in-game on F5.

**It stays out of your way**

It does not change your time of day, your weather or your colour grading. Those switches exist
and they are all off by default.

**It does not use the wanted system.** Riot police are built from relationship groups instead, so
a police overhaul, an LSPDFR-style pack or a 6-star mod runs alongside it completely untouched —
you can be chased by a mob while separately holding four stars from the game's own police. It
never recruits police, SWAT, army, medics, firefighters or story peds either; those belong to
whatever pack you have installed.

The one exception is deliberate and can be switched off: during The Purge all crime is legal, so
your wanted ceiling drops for the window and is restored to exactly what it was afterwards.

Every ped it touches is recorded and handed back the way it was found — on stop, on script
reload, and on game exit.

**Requirements**

• ScriptHookV
• ScriptHookVDotNet 3
• LemonUI
• A modern gameconfig and Heap Adjuster — strongly recommended, and not really optional for the
  larger modes

**Install**

Drop `TonightsTheNight.dll` into your `scripts/` folder alongside `LemonUI.SHVDN3.dll`. Press
**F6** in game.

Everything is configurable from the menu or from `scripts/TonightsTheNight/user.json`, which is
never overwritten by an update. Full details in the included INSTALL.md.

**Source:** https://github.com/Daolyap/Tonights-The-Night — MIT licensed.

---

## 4. After publishing

Update the mod's page whenever you release, rather than uploading a new mod. Paste the relevant
`CHANGELOG.md` section into the version notes.
