# v0.3.0 — test script

Three new systems, none of which have ever run in a game: **car chases**, **looting** and
**weapon presets**. Everything below is an assumption until you check it.

Roughly 20 minutes. Take the log either way: `scripts/TonightsTheNight.log`.

| | Test | The question it answers |
|---|---|---|
| C1 | **Car chases** | **Hurt someone, drive off — does a carload come after you?** |
| C2 | Chase crew | Do allies get in the *same* car, or does one person leave alone? |
| C3 | Losing them | Does the chase end sensibly, and does the crew go back to normal? |
| L1 | Looting | Do people carry things off once the riot escalates? |
| L2 | Car theft | Do some looters take a parked car instead? |
| W1 | Weapon presets | Does picking a preset change what new recruits carry? |
| X1 | Cleanup | After all of that, does stopping still restore the world? |

C1 and C3 are the two that decide whether chases work. The rest is detail.

---

## Before you start

Delete `scripts/TonightsTheNight/defaults.json`. It is rewritten on a version change anyway, but
deleting it makes sure you are reading the new one. **Leave `user.json` alone** — it is never
touched.

Mode files are unchanged this version, so there is no need to delete `modes/` this time.

Turn the debug overlay on (**F6 → Features → Debug Overlay**). It now shows a `chases:` and
`looting:` line, which is the fastest way to tell "it didn't trigger" from "it triggered and
looked wrong" — a distinction I can't make from a description.

---

## C1 — Car chases *(the real test)*

1. Start **Pedestrian Riot** or **The Purge**.
2. Get in a car.
3. **Shoot someone**, or run someone over. Anyone the riot has taken over.
4. **Drive away.** Properly — above about 20 km/h. Sitting still doesn't count.

**Expect:** within a few seconds, a notification (*"Red Mob are coming after you"*), a blip on
their car, and a carload behind you. The blip flashes when they're close.

**Tell me:**
- Did it trigger at all? If not, does the overlay's `chases:` count stay at 0?
- How long between the shot and the car appearing?
- Did they actually *find* you, or drive vaguely in your direction?
- Did the passengers shoot at you?

**If nothing happens:** the most likely cause is no suitable car near the ped you shot. They
prefer a car their side is already in and otherwise take the nearest empty one within 45m —
downtown should be dense enough, an empty stretch of freeway will not be. Try it on a busy
street. Second most likely: you weren't moving fast enough.

## C2 — Chase crew

The design intent is a **carload**, not a car: the driver waits until everyone assigned is in
before setting off, with a seven-second limit after which stragglers get put in.

**Tell me:** how many people were in the car, and whether you saw them run to it or just appear
in it. Both are legal; I want to know which one you actually get.

## C3 — Losing them, and what happens after

1. Drive away hard. Get 300m+ ahead and stay there.
2. **Expect:** after about twelve seconds they lose interest. Log line: `Pursuit by '...' ended:
   player escaped.`
3. Now go back and find them.

**Expect:** they get out of the car and rejoin the riot normally.

**This is the one I'd most like checked.** A chase leaves peds with "don't leave the vehicle"
set, and if ending the chase doesn't clear it they'd sit in a parked car for the rest of the
session. If you find a stationary car with four rioters just sitting in it, that's the bug.

Other endings to try: wreck their car (should end immediately), or kill the driver (someone else
should take the wheel, or the chase should end).

---

## L1 — Looting

Looting only runs during phases a mode marks as looting phases, so it will **not** start
immediately.

1. Start **Pedestrian Riot**, then **F6 → Skip To Next Phase** to skip ahead.
2. Watch the crowd for a minute.

**Expect:** people picking up a television, a case or a bin bag and running off with it. The
overlay's `looting:` count should climb.

**Tell me:** what they were carrying and whether it looked attached to their hand or floating
somewhere near it. The attachment offsets are guesswork — I can't see them.

The log says how many of the ten prop models your install actually has:
`Looting: N of 10 loot props available.` If that says 0, none of my prop names were right and
I'll need the log to pick better ones.

## L2 — Car theft

Some looters take a nearby parked car instead. **Expect** occasional peds getting into a car and
driving off with it. Roughly one looter in four, so give it a couple of minutes.

---

## W1 — Weapon presets

**F6 → Weapons → Preset.**

1. Start a riot on the default (**Mode's Own**) and note what people carry — mostly melee.
2. Switch the preset to **Military** mid-riot.
3. Wait. The riot keeps recruiting continuously, so new recruits should start appearing with
   carbines within a few seconds.

**Expect:** it changes what *new* people carry, not what the people already out there have.

**Also worth one look:** switch to **Chaos** and confirm it is as stupid as intended.

**Tell me** if any preset produces obviously unarmed crowds — that means a weapon name is wrong,
and the log will have `Unknown weapon '...'` lines naming it.

---

## X1 — Cleanup

After all of the above, in one session:

1. **F6 → Stop Riot.**
2. Look around, then save and reload.

**Expect:** everyone walking normally, no blips, no props stuck to anyone's hand, no cars full of
rioters, no chase blip left on the map.

---

## What I most want back

In rough priority:

1. **Did a chase trigger, and did it find you?** (C1)
2. **Did the crew come back to normal afterwards, or get stuck in the car?** (C3)
3. Did looters carry things, and did the props sit in their hands properly? (L1)
4. Did stopping still clean everything up? (X1)
5. The log file, whatever happened.

Screenshots of a chase or a looter are worth a lot here — the parts I can't check are all
visual.
