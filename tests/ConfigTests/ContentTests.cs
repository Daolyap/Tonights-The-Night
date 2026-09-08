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
