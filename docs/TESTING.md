# v0.4.0 — test script

This build is mostly your bug list. The important thing to check is whether **the crowd now
picks the right enemy**, because that was one design mistake showing up as four separate
symptoms.

**Delete `scripts/TonightsTheNight/modes/` before you start.** Every mode file changed. Leave
`user.json` alone — it is never touched.

Turn the debug overlay on (**F6 → Features → Debug Overlay**). It shows the purge countdown and
the chase and looting counts.

| | Test | The question it answers |
|---|---|---|
| A1 | **Martial Law** | **Do civilians fight the army instead of you?** |
| A2 | Police State | Same, plus: does the response feel like a police state? |
| A3 | Invasion | Do the aliens look like aliens? |
| B1 | **Rioters driving** | **Do drivers do four different things instead of all getting out?** |
| B2 | Pedestrian Chaos | Does a free-for-all still let your passengers be on your side? |
| C1 | The Purge | Does it end, and are you left alone by the police? |
| D1 | No side effects | Is your clock, weather and wanted level untouched? |

---

## A1 — Martial Law *(the real test)*

1. Start **Martial Law** in a busy area.
2. Stand in the crowd and watch. Do not shoot anything.

**Expect:** police first, then soldiers. Civilians run or fight back — roughly half each — and
they fight *the soldiers*. They should walk past you, not at you.

**Tell me:**
- Did any civilian attack you unprovoked? That is the bug returning.
- Did civilians fight each other? Also the bug.
- Did enough of them fight back, or is it still mostly running?

3. Now stay in it for three minutes and watch the phases.

**Expect:** phase two brings armour and helicopters. Phase three adds boats *if you are near
water* — try Vespucci Beach for that, and expect nothing inland, which is correct.

**Tell me:** did helicopters turn up, and did they do anything once they had? `TASK_HELI_MISSION`
has more arguments than anyone is confident about, so "they hovered uselessly" and "they parked
on a roof" are both real possibilities and both fixable from `defaults.json` under
`features.air`.

## A2 — Police State

Same test. Additionally: does it feel like a *state* now? Two cars of four every eight seconds,
riot vans of six from phase two, a helicopter in phase three.

**Tell me** if it is now too much rather than too little — that is a slider, and easier to judge
from playing than from guessing.

## A3 — Invasion

The aliens were arriving as "half a suit and a man in all black". Every spawned ped now gets its
default outfit set explicitly, which is the usual cause.

**Tell me** whether they look right. If they still don't, a screenshot would settle it — and
there is a per-faction `"outfit": "random"` in the mode file to try before I rebuild anything.

---

## B1 — Rioters driving *(the other real test)*

1. Start **Pedestrian Riot** or **Pedestrian Chaos**.
2. Drive through it at speed. Then park and watch the traffic.

**Expect:** four different behaviours, roughly 3:3:2:2 — some stop and get out to fight, some
drive off after another rioter, some come after you, some drive away from you. Anyone with a gun
shoots out of the window.

**Before, all of them stopped and got out.** If that is still what you see, tell me and check the
sliders under **F6 → Rioters Driving** — the weights are live.

**Tell me** if the mix is wrong. It is four numbers in a menu, so "too many chase me" is a
two-second fix rather than a rebuild.

## B2 — Pedestrian Chaos

New mode. Everyone kills everyone, police included, and you are fair game.

**Expect:** people in the same car do *not* kill each other. That is the one exception, and it
works by putting car-mates on the same side under the hood.

**Tell me** if you ever see two people in one car fighting.

---

## C1 — The Purge

1. Start **The Purge**. Note the time on the overlay.
2. Get a wanted level on purpose — shoot at something, ram a police car.
3. Wait it out. Twelve minutes.

**Expect:**
- Your wanted level drops and stays down for the whole window.
- The overlay counts down, and the log has a line a minute.
- At zero it announces the end and the mode stops.
- **Your wanted ceiling comes back.** With your OIV pack that means six stars again.

**Check the ceiling afterwards** — start a fight with the police after the purge ends and confirm
you can still reach six stars. This is the one place the mod touches the wanted system at all,
and getting the restore wrong would look exactly like your police pack breaking.

If twelve minutes is too long to sit through, drop `features.purge.durationMinutes` to 2 in
`user.json` and reload with **F5**.

---

## D1 — No side effects

Across all of the above:

**Expect:** your clock never changes. Your weather never changes. No colour grading. If any of
those move, that is a bug now rather than a feature — the switches are all off by default and
the modes no longer ask for any of it.

Then **F6 → Stop Riot**, save and reload.

**Expect:** everyone normal, no blips, no props in hands, nobody stuck sitting in a parked car,
your wanted ceiling back where it was.

---

## What I most want back

1. **Did civilians fight the army instead of you?** (A1)
2. **Did drivers do four different things?** (B1)
3. Did the purge end, and did your six stars come back? (C1)
4. Are the new spawn rates right, too heavy, or still too light?
5. The log file, whatever happened.

Points 4 and 5 matter most for the things I cannot see. Everything in this build is either a
slider or a value in `defaults.json`, so "too much" and "too little" are both cheap to fix — but
only if I know which.
