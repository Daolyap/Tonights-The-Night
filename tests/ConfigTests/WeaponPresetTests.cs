using System;
using System.Collections.Generic;
using TonightsTheNight.Config;
using TonightsTheNight.Factions;
using TonightsTheNight.Util;

/// <summary>
/// Validates the shipped weapon presets and the resolution of the weapons.preset setting.
///
/// The failure mode being guarded against is silence. A misspelled weapon name arms nobody, an
/// id present in the menu list but missing a table shows an option that does nothing, and a
/// preset whose weights are all zero picks the same weapon every time. None of those throw, none
/// of them appear in a build, and all of them look in-game like "the mod is a bit broken".
/// </summary>
public static class WeaponPresetTests
{
    private static int _failures;

    private static void Check(string label, bool ok, string detail = "")
    {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + label + (!ok && detail.Length > 0 ? "   <- " + detail : ""));
        if (!ok) { _failures++; }
    }

    public static int Run()
    {
        _failures = 0;
        Log.Threshold = LogLevel.Error;

        Console.WriteLine("Weapon presets");

        Check("menu ids and names are the same length",
              WeaponPresets.Ids.Length == WeaponPresets.Names.Length,
              WeaponPresets.Ids.Length + " ids vs " + WeaponPresets.Names.Length + " names");

        Check("the default is first in the list", WeaponPresets.Ids[0] == WeaponPresets.UseMode,
              WeaponPresets.Ids[0]);

        Check("the weapon list is populated", VanillaWeapons.Count > 90, VanillaWeapons.Count + " names");

        // Guard the guard. A checker that says yes to everything would pass every test below
        // while catching nothing, which is the worst kind of green.
        Check("the checker accepts a game name", VanillaWeapons.IsKnown("WEAPON_SAWNOFFSHOTGUN"));
        Check("the checker accepts an SHVDN enum name", VanillaWeapons.IsKnown("SawnOffShotgun"));
        Check("the checker accepts an underscored name", VanillaWeapons.IsKnown("StoneHatchet"));
        Check("the checker rejects a plausible invention", !VanillaWeapons.IsKnown("WEAPON_BASEBALLBAT"));
        Check("the checker rejects a model name", !VanillaWeapons.IsKnown("s_m_y_marine_01"));
        Check("the checker rejects nothing at all", !VanillaWeapons.IsKnown(""));

        foreach (string id in WeaponPresets.Ids)
        {
            Check("'" + id + "' has a description", !string.IsNullOrEmpty(WeaponPresets.DescriptionOf(id)));
        }

        // Every id in the picker except the two special cases must resolve to a real table, or
        // the menu offers an option that quietly does nothing.
        var builtin = new Dictionary<string, WeaponPreset>(StringComparer.OrdinalIgnoreCase);
        foreach (WeaponPreset preset in WeaponPresets.Builtin) { builtin[preset.Id] = preset; }

        foreach (string id in WeaponPresets.Ids)
        {
            if (id == WeaponPresets.UseMode || id == "custom") { continue; }
            Check("'" + id + "' is backed by a table", builtin.ContainsKey(id));
        }

        Check("no orphaned presets", builtin.Count == WeaponPresets.Ids.Length - 2,
              builtin.Count + " tables for " + (WeaponPresets.Ids.Length - 2) + " picker entries");

        foreach (WeaponPreset preset in WeaponPresets.Builtin)
        {
            Validate(preset);
        }

        CheckResolution();

        Console.WriteLine();
        return _failures;
    }

    private static void Validate(WeaponPreset preset)
    {
        string id = preset.Id;

        Check(id + ": has weapons", preset.Weapons.Count > 0);
        Check(id + ": has a name", !string.IsNullOrEmpty(preset.Name));
        Check(id + ": armour is in range", preset.Armour >= 0 && preset.Armour <= 100, preset.Armour.ToString());
        Check(id + ": armed chance is in range", preset.ArmedChance > 0f && preset.ArmedChance <= 1f,
              preset.ArmedChance.ToString());
        Check(id + ": carries ammunition", preset.Ammo > 0, preset.Ammo.ToString());

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (WeaponChoice choice in preset.Weapons.Choices)
        {
            Check(id + ": '" + choice.Name + "' is a base-game weapon", VanillaWeapons.IsKnown(choice.Name),
                  "not in the vanilla list - a shipped preset has to run on a clean install");
            Check(id + ": '" + choice.Name + "' has a positive weight", choice.Weight > 0f);
            Check(id + ": '" + choice.Name + "' appears once", seen.Add(choice.Name), "listed twice");
        }

        // Weighted picking has to actually reach the whole table. A bug in the weight walk shows
        // up as a riot where everyone somehow has the first weapon in the list.
        var picked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var random = new Random(20260906);
        for (int i = 0; i < 40000; i++) { picked.Add(preset.Weapons.Pick(random)); }

        Check(id + ": every weapon can be picked", picked.Count == preset.Weapons.Count,
              picked.Count + " of " + preset.Weapons.Count + " reachable");
    }

    /// <summary>
    /// The resolution rules, which are where the surprises live: the default must not touch a
    /// mode's own loadouts, and every way of getting it wrong must fall back to that rather than
    /// arming nobody.
    /// </summary>
    private static void CheckResolution()
    {
        var config = new ConfigStore();

        Check("nothing set resolves to the mode's own", WeaponPresets.Active(config) == null);

        config.SetLive("weapons.preset", JsonValue.Of("mode"));
        Check("'mode' resolves to the mode's own", WeaponPresets.Active(config) == null);

        config.SetLive("weapons.preset", JsonValue.Of("realistic"));
        WeaponPreset realistic = WeaponPresets.Active(config);
        Check("'realistic' resolves", realistic != null && realistic.Id == "realistic");

        config.SetLive("weapons.preset", JsonValue.Of("MILITARY"));
        WeaponPreset military = WeaponPresets.Active(config);
        Check("preset names are case-insensitive", military != null && military.Id == "military");

        config.SetLive("weapons.preset", JsonValue.Of("nonsense"));
        Check("an unknown preset falls back rather than disarming everyone",
              WeaponPresets.Active(config) == null);

        // Custom, empty: the fallback matters more than the feature. Somebody will select Custom
        // before writing the list.
        WeaponPresets.Reset();
        config.SetLive("weapons.preset", JsonValue.Of("custom"));
        Check("an empty custom list falls back to the mode's own", WeaponPresets.Active(config) == null);

        WeaponPresets.Reset();
        JsonValue list;
        string error;
        bool parsed = JsonValue.TryParse(
            "[\"WEAPON_BAT\", {\"name\": \"WEAPON_PISTOL\", \"weight\": 0.25}]", out list, out error);
        Check("the custom list example parses", parsed, error);

        config.SetLive("weapons.custom", list);
        config.SetLive("weapons.customArmour", JsonValue.Of(35));

        WeaponPreset custom = WeaponPresets.Active(config);
        Check("a custom list resolves", custom != null && custom.Weapons.Count == 2);
        Check("custom armour is read", custom != null && custom.Armour == 35);

        if (custom != null)
        {
            var picked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var random = new Random(7);
            for (int i = 0; i < 2000; i++) { picked.Add(custom.Weapons.Pick(random)); }
            Check("both custom weapons can be picked", picked.Count == 2, picked.Count.ToString());
        }

        WeaponPresets.Reset();
    }
}
