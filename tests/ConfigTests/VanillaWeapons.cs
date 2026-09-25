using System;
using System.Collections.Generic;

/// <summary>
/// The weapons the base game ships with.
///
/// This exists because a wrong weapon name is completely silent: the mod logs one warning to a
/// file nobody reads and the faction goes out unarmed. Nothing in the build catches it, and
/// nothing in the game announces it — you just notice, eventually, that a whole side is punching.
///
/// Two spellings are both legal in config, and the mod accepts either: SHVDN's enum name
/// ("SawnOffShotgun", which the shipped modes use) and the game's own name
/// ("WEAPON_SAWNOFFSHOTGUN", which the presets use). <see cref="IsKnown"/> normalises both to the
/// same key, so the list below is written once in game-name form.
///
/// A name missing from this list is not necessarily wrong — an add-on weapon pack is a perfectly
/// good thing to reference from your own config. It is wrong in *shipped* content, which has to
/// run on a clean install, and that is all this checks.
/// </summary>
public static class VanillaWeapons
{
    private static readonly string[] Names =
    {
        // Melee
        "WEAPON_UNARMED", "WEAPON_KNIFE", "WEAPON_NIGHTSTICK", "WEAPON_HAMMER", "WEAPON_BAT",
        "WEAPON_GOLFCLUB", "WEAPON_CROWBAR", "WEAPON_BOTTLE", "WEAPON_DAGGER", "WEAPON_HATCHET",
        "WEAPON_KNUCKLE", "WEAPON_MACHETE", "WEAPON_FLASHLIGHT", "WEAPON_SWITCHBLADE",
        "WEAPON_POOLCUE", "WEAPON_WRENCH", "WEAPON_BATTLEAXE", "WEAPON_STONE_HATCHET",

        // Handguns
        "WEAPON_PISTOL", "WEAPON_PISTOL_MK2", "WEAPON_COMBATPISTOL", "WEAPON_APPISTOL",
        "WEAPON_STUNGUN", "WEAPON_PISTOL50", "WEAPON_SNSPISTOL", "WEAPON_SNSPISTOL_MK2",
        "WEAPON_HEAVYPISTOL", "WEAPON_VINTAGEPISTOL", "WEAPON_FLAREGUN", "WEAPON_MARKSMANPISTOL",
        "WEAPON_REVOLVER", "WEAPON_REVOLVER_MK2", "WEAPON_DOUBLEACTION", "WEAPON_CERAMICPISTOL",
        "WEAPON_NAVYREVOLVER", "WEAPON_GADGETPISTOL", "WEAPON_RAYPISTOL",

        // Submachine guns
        "WEAPON_MICROSMG", "WEAPON_SMG", "WEAPON_SMG_MK2", "WEAPON_ASSAULTSMG", "WEAPON_COMBATPDW",
        "WEAPON_MACHINEPISTOL", "WEAPON_MINISMG", "WEAPON_RAYCARBINE", "WEAPON_UNHOLYHELLBRINGER",

        // Shotguns
        "WEAPON_PUMPSHOTGUN", "WEAPON_PUMPSHOTGUN_MK2", "WEAPON_SAWNOFFSHOTGUN",
        "WEAPON_ASSAULTSHOTGUN", "WEAPON_BULLPUPSHOTGUN", "WEAPON_MUSKET", "WEAPON_HEAVYSHOTGUN",
        "WEAPON_DBSHOTGUN", "WEAPON_AUTOSHOTGUN", "WEAPON_COMBATSHOTGUN",

        // Rifles
        "WEAPON_ASSAULTRIFLE", "WEAPON_ASSAULTRIFLE_MK2", "WEAPON_CARBINERIFLE",
        "WEAPON_CARBINERIFLE_MK2", "WEAPON_ADVANCEDRIFLE", "WEAPON_SPECIALCARBINE",
        "WEAPON_SPECIALCARBINE_MK2", "WEAPON_BULLPUPRIFLE", "WEAPON_BULLPUPRIFLE_MK2",
        "WEAPON_COMPACTRIFLE", "WEAPON_MILITARYRIFLE", "WEAPON_HEAVYRIFLE", "WEAPON_TACTICALRIFLE",

        // Machine guns and snipers
        "WEAPON_MG", "WEAPON_COMBATMG", "WEAPON_COMBATMG_MK2", "WEAPON_GUSENBERG",
        "WEAPON_SNIPERRIFLE", "WEAPON_HEAVYSNIPER", "WEAPON_HEAVYSNIPER_MK2",
        "WEAPON_MARKSMANRIFLE", "WEAPON_MARKSMANRIFLE_MK2", "WEAPON_PRECISIONRIFLE",

        // Heavy
        "WEAPON_RPG", "WEAPON_GRENADELAUNCHER", "WEAPON_GRENADELAUNCHER_SMOKE", "WEAPON_MINIGUN",
        "WEAPON_FIREWORK", "WEAPON_RAILGUN", "WEAPON_HOMINGLAUNCHER", "WEAPON_COMPACTLAUNCHER",
        "WEAPON_RAYMINIGUN", "WEAPON_EMPLAUNCHER", "WEAPON_WIDOWMAKER",

        // Thrown
        "WEAPON_GRENADE", "WEAPON_BZGAS", "WEAPON_MOLOTOV", "WEAPON_STICKYBOMB", "WEAPON_PROXMINE",
        "WEAPON_SNOWBALL", "WEAPON_PIPEBOMB", "WEAPON_BALL", "WEAPON_SMOKEGRENADE", "WEAPON_FLARE",

        // Everything else you can hold
        "WEAPON_PETROLCAN", "WEAPON_FIREEXTINGUISHER", "WEAPON_HAZARDCAN", "WEAPON_FERTILIZERCAN",
        "WEAPON_PARACHUTE", "WEAPON_DIGISCANNER", "WEAPON_BRIEFCASE", "WEAPON_BRIEFCASE_02",
        "WEAPON_GARBAGEBAG", "WEAPON_HANDCUFFS", "WEAPON_METALDETECTOR"
    };

    private static readonly HashSet<string> Keys = Build();

    public static int Count { get { return Keys.Count; } }

    public static bool IsKnown(string name)
    {
        return !string.IsNullOrEmpty(name) && Keys.Contains(Key(name));
    }

    private static HashSet<string> Build()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (string name in Names) { set.Add(Key(name)); }
        return set;
    }

    /// <summary>
    /// Collapses "WEAPON_SAWNOFFSHOTGUN" and "SawnOffShotgun" onto the same key. Underscores go
    /// because the enum name for WEAPON_STONE_HATCHET is StoneHatchet.
    /// </summary>
    private static string Key(string name)
    {
        string trimmed = name.Trim();
        if (trimmed.StartsWith("WEAPON_", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring("WEAPON_".Length);
        }
        return trimmed.Replace("_", string.Empty).ToUpperInvariant();
    }
}
