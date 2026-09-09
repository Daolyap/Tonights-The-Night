using System;
using System.Collections.Generic;
using System.IO;
using TonightsTheNight.Config;
using TonightsTheNight.Factions;
using TonightsTheNight.Util;

/// <summary>
/// The shipped content that is neither a mode file nor a weapon preset.
///
/// Everything here is data that looks like configuration and behaves like code: a wrong style
/// name silently drops a prop, a melee-only preset with a pistol in it is a broken promise
/// rather than an error, and a fallback list that has drifted from the defaults it mirrors only
/// shows up on the one install where the config failed to load.
/// </summary>
public static class ContentTests
{
    private static int _failures;

    private static void Check(string label, bool ok, string detail = "")
    {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + label + (!ok && detail.Length > 0 ? "   <- " + detail : ""));
        if (!ok) { _failures++; }
    }

    /// <summary>
    /// Melee only has to mean melee only. It is the setting people pick because a riot fought
    /// with firearms kills off the street faster than the game repopulates it, so one pistol in
    /// the table defeats the entire reason the preset exists.
    /// </summary>
    private static readonly HashSet<string> Melee = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "WEAPON_UNARMED", "WEAPON_KNIFE", "WEAPON_NIGHTSTICK", "WEAPON_HAMMER", "WEAPON_BAT",
        "WEAPON_GOLFCLUB", "WEAPON_CROWBAR", "WEAPON_BOTTLE", "WEAPON_DAGGER", "WEAPON_HATCHET",
        "WEAPON_KNUCKLE", "WEAPON_MACHETE", "WEAPON_FLASHLIGHT", "WEAPON_SWITCHBLADE",
        "WEAPON_POOLCUE", "WEAPON_WRENCH", "WEAPON_BATTLEAXE", "WEAPON_STONE_HATCHET"
    };

    public static int Run()
    {
        _failures = 0;
        JsonValue defaults = DefaultConfig.Build();

        CheckMeleePreset();
        CheckCarryProps(defaults);
        CheckHunterDefaults(defaults);
        CheckHunterMoveset(defaults);
        CheckContagionDefaults(defaults);
        CheckInterceptionDefaults(defaults);
        CheckOutbreakIsNotAGunfight();
        CheckMenuDefaults(defaults);

        Console.WriteLine();
        return _failures;
    }

    private static void CheckMeleePreset()
    {
        WeaponPreset melee = null;
        foreach (WeaponPreset preset in WeaponPresets.Builtin)
        {
            if (preset.Id == "melee") { melee = preset; }
        }

        Check("a melee-only preset ships", melee != null);
        if (melee == null) { return; }

        Check("melee preset is in the picker", Array.IndexOf(WeaponPresets.Ids, "melee") >= 0);
        Check("melee preset has a table", melee.Weapons.Count >= 8, melee.Weapons.Count + " entries");
        Check("melee preset arms everyone", Math.Abs(melee.ArmedChance - 1f) < 0.001f);

        foreach (WeaponChoice choice in melee.Weapons.Choices)
        {
            Check("melee preset: '" + choice.Name + "' is a melee weapon", Melee.Contains(choice.Name),
                  "melee only has to mean melee only");
        }
    }

    /// <summary>
    /// The carryable props, and the fact that the code carries a second copy of them for when
    /// the config cannot be read. Two lists that are supposed to agree are two lists that will
    /// not, unless something checks.
    /// </summary>
    private static void CheckCarryProps(JsonValue defaults)
    {
        JsonValue props = defaults["features"]["looting"]["props"];

        Check("looting props are declared", props.Count > 0, props.Count + " declared");

        var declared = new List<string>();

        foreach (JsonValue prop in props.Items)
        {
            string model = prop["model"].AsString(null);
            Check("a looting prop has a model", model != null);
            if (model == null) { continue; }

            declared.Add(model);

            Check("prop '" + model + "' looks like a game model name",
                  model == model.ToLowerInvariant() && model.Contains("_"), "expected e.g. prop_tv_flat_01");

            string style = prop["style"].AsString("hand");
            Check("prop '" + model + "' has a known carry style", style == "hand" || style == "box", style);

            foreach (string axis in new[] { "x", "y", "z", "rx", "ry", "rz" })
            {
                Check("prop '" + model + "' declares " + axis, prop.Has(axis));
            }
        }

        // The mirrored list in CarryProps.Fallback, read out of the source. It only ever runs on
        // an install whose config failed to load, which is exactly the install nobody tests.
        string source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "TonightsTheNight", "Core", "CarryProps.cs"));

        foreach (string model in declared)
        {
            Check("fallback list also carries '" + model + "'", source.Contains("\"" + model + "\""),
                  "CarryProps.Fallback has drifted from DefaultConfig");
        }
    }

    private static void CheckHunterDefaults(JsonValue defaults)
    {
        JsonValue hunter = defaults["features"]["hunter"];

        Check("the hunter ships enabled", hunter["enabled"].AsBool(false));
        Check("the hunter has a resolve pool", hunter["resolve"].AsInt(0) > 0);

        double armoured = hunter["armouredMultiplier"].AsDouble(0);
        double exposed = hunter["vulnerableMultiplier"].AsDouble(0);

        Check("armoured damage is reduced but not zero", armoured > 0 && armoured < 1, armoured.ToString());
        Check("exposed damage is worth more than armoured", exposed > armoured, exposed + " vs " + armoured);

        // Without a break threshold the only way to open him up is to survive an ability, which
        // leaves a player with good aim and no explosives doing everything right and getting
        // nowhere. It is the difficulty curve's safety valve.
        Check("sustained fire can stagger him", hunter["breakThreshold"].AsDouble(0) > 0);
        Check("a big hit is worth more than an ordinary one",
              hunter["heavyHitMultiplier"].AsDouble(0) > armoured);

        // Every recovery has to be long enough to shoot into, or the openings are theoretical.
        foreach (string key in new[] { "rushRecoveryMs", "slamRecoveryMs", "phaseStaggerMs", "breakStaggerMs" })
        {
            Check("hunter." + key + " is a usable window", hunter[key].AsInt(0) >= 1000,
                  hunter[key].AsInt(0) + "ms");
        }

        // Timing invariants. These are here because getting one wrong is silent: the ability
        // still plays, it just never does anything, and there is no way to tell from watching.
        Check("a rush finishes before another can start",
              hunter["rushCooldownMs"].AsInt(0) > hunter["rushMs"].AsInt(0),
              hunter["rushCooldownMs"].AsInt(0) + "ms cooldown vs " + hunter["rushMs"].AsInt(0) + "ms rush");

        // A rush that connects lands a strike at the end of it. If the strike shared the
        // ability cooldown - which it did - the rush that set that cooldown made its own strike
        // impossible, and a lunge across fifty metres did nothing at all.
        Check("a connecting rush can land a strike",
              hunter["strikeIntervalMs"].AsInt(int.MaxValue) < hunter["rushCooldownMs"].AsInt(0),
              "strike interval must be shorter than the rush cooldown");

        Check("he recovers from a slam before he can slam again",
              hunter["slamCooldownMs"].AsInt(0) > hunter["slamRecoveryMs"].AsInt(0));

        Check("the hunter can be escaped only so far", hunter["leashDistance"].AsInt(0) > 50);
        Check("the hunter's effects are declared as asset/effect",
              hunter["fx"]["trail"].AsString("").Contains("/"));
    }

    /// <summary>
    /// The move set, and the promise it makes.
    ///
    /// Every heavy move the hunter has is supposed to be announced before it lands and to cost
    /// him an opening afterwards. Both halves are silent when they are wrong: a windup of zero
    /// is an attack that simply happens to you, and a recovery shorter than the time it takes to
    /// aim is an opening that only exists on paper. Neither is visible from watching one fight.
    /// </summary>
    private static void CheckHunterMoveset(JsonValue defaults)
    {
        JsonValue hunter = defaults["features"]["hunter"];

        foreach (string key in new[] { "rushWindupMs", "slamWindupMs", "cleaveWindupMs", "hurlWindupMs" })
        {
            // Long enough to see and react to from a standing start. Below about a third of a
            // second nobody is dodging anything; they are being told what killed them.
            Check("hunter." + key + " is long enough to read", hunter[key].AsInt(0) >= 350,
                  hunter[key].AsInt(0) + "ms");
        }

        foreach (string key in new[] { "cleaveRecoveryMs", "hurlRecoveryMs", "rushWhiffRecoveryMs" })
        {
            Check("hunter." + key + " is a usable window", hunter[key].AsInt(0) >= 1000,
                  hunter[key].AsInt(0) + "ms");
        }

        // The asymmetry that makes reading the tell worth anything. If a miss cost him no more
        // than a hit, dodging would only ever be worth the damage it avoided and the fight would
        // have no forward motion.
        Check("missing costs him more than connecting",
              hunter["rushWhiffRecoveryMs"].AsInt(0) > hunter["rushRecoveryMs"].AsInt(0),
              hunter["rushWhiffRecoveryMs"].AsInt(0) + "ms vs " + hunter["rushRecoveryMs"].AsInt(0) + "ms");

        // Same invariant the rush and the slam already hold: a move whose cooldown is shorter
        // than its own recovery can be started again before he has finished paying for it.
        Check("he recovers from a cleave before he can cleave again",
              hunter["cleaveCooldownMs"].AsInt(0) > hunter["cleaveRecoveryMs"].AsInt(0));
        Check("he recovers from a throw before he can throw again",
              hunter["hurlCooldownMs"].AsInt(0) > hunter["hurlRecoveryMs"].AsInt(0));

        // The charge steers; it does not track. A rate high enough to re-aim inside a frame is
        // the undodgeable behaviour this replaced, written as a number instead of a loop.
        double steer = hunter["rushSteerRate"].AsDouble(0);
        Check("the charge steers rather than tracks", steer > 0 && steer <= 3, steer.ToString());

        // Held back a phase, so the first third of the fight is a plain melee fight the tells
        // can be learned in.
        Check("the thrown attack is not in the opening phase", hunter["hurlFromPhase"].AsInt(0) >= 2);
        Check("he has something to throw", hunter["debris"].Count > 0);

        // Nothing he does may be an explosion. An explosion applies its impulse to whoever
        // raised it, which is how he spent the fight lying next to his own ground slam.
        //
        // The call, not the word: both files explain in prose why the native is gone, and a
        // check that cannot tell a comment from a call would forbid saying so.
        Check("the hunter raises no explosions",
              !SourceOf("Core", "Hunter.cs").Contains("Hash.ADD_EXPLOSION") &&
              !SourceOf("Core", "HunterFx.cs").Contains("Hash.ADD_EXPLOSION"),
              "a zero-damage explosion still knocks him over");
    }

    /// <summary>
    /// The epidemic underneath the visible spread.
    ///
    /// Its whole job is to be true while nobody is looking, which means none of it can be
    /// checked by playing for thirty seconds. A growth rate of zero, a front that does not move
    /// or a suppression that can reach the ceiling all produce the same symptom the layer was
    /// added to fix: drive away and the outbreak is over.
    /// </summary>
    private static void CheckContagionDefaults(JsonValue defaults)
    {
        JsonValue contagion = defaults["features"]["contagion"];

        double start = contagion["startingPrevalence"].AsDouble(0);
        double ceiling = contagion["maxPrevalence"].AsDouble(0);

        Check("the outbreak starts somewhere above nothing", start > 0, start.ToString());
        Check("the outbreak starts below its ceiling", start < ceiling, start + " vs " + ceiling);
        Check("the outbreak grows on its own", contagion["growthPerSecond"].AsDouble(0) > 0);
        Check("the front moves without being watched", contagion["frontMetresPerSecond"].AsDouble(0) > 0);
        Check("the front starts wide enough to be an outbreak", contagion["startingFront"].AsDouble(0) > 0);

        // The rim carries a share of the core, or the outbreak has a boundary you can stand
        // astride rather than an edge.
        double rim = contagion["edgeShare"].AsDouble(0);
        Check("the outbreak has an edge rather than a boundary", rim > 0 && rim < 1, rim.ToString());

        // Containment is local and partial by design. Suppression that could reach the core
        // prevalence would let a few kills in one street clear the district.
        Check("containment cannot cancel the outbreak",
              contagion["maxSuppression"].AsDouble(1) < ceiling,
              contagion["maxSuppression"].AsDouble(1) + " vs a ceiling of " + ceiling);
        Check("containment wears off", contagion["clearMs"].AsInt(0) > 0);
        Check("the count is on screen", contagion["showCount"].AsBool(false));
    }

    /// <summary>
    /// The numbers that decide whether a chase survives a motorway. Every one of them is inert
    /// below the threshold, so a threshold of zero would apply the whole thing to somebody
    /// walking - and a boost ceiling below one would make chase cars slower than standard.
    /// </summary>
    private static void CheckInterceptionDefaults(JsonValue defaults)
    {
        JsonValue interception = defaults["features"]["interception"];

        Check("interception ships enabled", interception["enabled"].AsBool(false));
        Check("it only applies above a real speed", interception["speedThreshold"].AsDouble(0) > 5,
              interception["speedThreshold"].AsDouble(0) + " m/s");
        Check("arrivals are led, not lagged", interception["leadSeconds"].AsDouble(0) > 0);
        Check("the lead is capped", interception["maxLead"].AsDouble(0) > 0);

        double behind = interception["behindShare"].AsDouble(0);
        Check("most arrivals come from behind", behind > 0.5 && behind < 1,
              behind + " - at 1 the road ahead is guaranteed safe");

        // The one that actually fixes it. A car created stationary behind somebody at speed
        // spends its first seconds losing ground and never gets them back.
        double rolling = interception["rollingStart"].AsDouble(0);
        Check("arrivals are already moving", rolling > 0.5 && rolling <= 1.2, rolling.ToString());

        Check("a chase car is never slower than standard", interception["maxBoost"].AsDouble(0) >= 1);
        Check("the boost has somewhere to go", interception["boostGain"].AsDouble(0) > 0);
        Check("drivers aim to close the gap, not match it", interception["cruiseMargin"].AsDouble(0) > 1);
        Check("urgency never slows anything down", interception["maxUrgency"].AsDouble(0) >= 1);
    }

    /// <summary>
    /// Patient Zero is supposed to be an outbreak, and it read as two crowds having a firefight.
    ///
    /// Half of that was the loadout and half was that conversion only ever added a weapon, so an
    /// ambient pedestrian who was already carrying kept it. Both halves are content decisions
    /// that look right in a diff and are wrong in the street, so they are asserted rather than
    /// remembered.
    /// </summary>
    private static void CheckOutbreakIsNotAGunfight()
    {
        JsonValue mode;
        string error;

        if (!JsonValue.TryParse(StockModeTests.ExtractModes()["contagion"], out mode, out error))
        {
            Check("the outbreak mode parses", false, error);
            return;
        }

        JsonValue infected = mode["factions"]["infected"];
        JsonValue people = mode["factions"]["public"];

        Check("the infected are unarmed", infected["armedChance"].AsDouble(1) <= 0,
              "an infected with a pistol is a person with a gun");
        Check("the infected give up what they were carrying", infected["disarm"].AsBool(false),
              "conversion only ever added a weapon, so they kept their own");

        foreach (JsonValue weapon in people["weapons"].Items)
        {
            string name = weapon["name"].AsString(weapon.AsString(null));
            Check("the uninfected carry no firearm: '" + name + "'",
                  name != null && Melee.Contains(Normalise(name)),
                  "a crowd with pistols turns the outbreak into a gunfight");
        }

        // A handful stand and swing; the rest run. The cordon is the side with rifles, and it
        // arrives later, which is what makes it read as the army rather than as more crowd.
        Check("most of the uninfected run", people["fightBackChance"].AsDouble(1) <= 0.2,
              people["fightBackChance"].AsDouble(1).ToString());
        Check("the cordon is the only side with firearms",
              mode["factions"]["cordon"]["armedChance"].AsDouble(0) > 0.9);
    }

    /// <summary>Config accepts "Bat" and "WEAPON_BAT"; the melee list is written the long way.</summary>
    private static string Normalise(string weapon)
    {
        return weapon.StartsWith("WEAPON_", StringComparison.OrdinalIgnoreCase)
            ? weapon.ToUpperInvariant()
            : "WEAPON_" + weapon.ToUpperInvariant();
    }

    private static string SourceOf(string folder, string file)
    {
        return File.ReadAllText(Path.Combine(RepoRoot(), "src", "TonightsTheNight", folder, file));
    }

    /// <summary>
    /// The header. Both of these shipped wrong, and both were visible in every screenshot: a
    /// banner with nothing behind it and a title in a font a third taller than the bar it sits in.
    /// </summary>
    private static void CheckMenuDefaults(JsonValue defaults)
    {
        JsonValue menu = defaults["menu"];

        string style = menu["bannerStyle"].AsString("");
        Check("the banner style is one the menu knows",
              style == "texture" || style == "flat" || style == "none", style);

        Check("the banner ships as the texture rather than a bare rectangle", style == "texture", style);

        double scale = menu["titleScale"].AsDouble(0);
        Check("the title scale is inside the clamp the code applies", scale >= 0.4 && scale <= 1.2, scale.ToString());

        Check("the title font is not the handwritten one",
              menu["titleFont"].AsString("") != "HouseScript",
              "HouseScript overhangs the subtitle bar and the first two items");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src"))) { dir = dir.Parent; }
        if (dir == null) { throw new DirectoryNotFoundException("Could not locate the repository root."); }
        return dir.FullName;
    }
}
