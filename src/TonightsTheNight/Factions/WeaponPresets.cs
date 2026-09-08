using System;
using System.Collections.Generic;
using TonightsTheNight.Config;
using TonightsTheNight.Util;

namespace TonightsTheNight.Factions
{
    /// <summary>
    /// A named answer to "what is the mob carrying".
    ///
    /// Armour and armed-chance travel with the weapons because they are the same decision: an
    /// "armed and armoured" riot is not a realistic one with better guns, it is a different
    /// event, and splitting those knobs across three menus would only let you build the
    /// incoherent middle.
    /// </summary>
    public sealed class WeaponPreset
    {
        public string Id;
        public string Name;
        public string Description;
        public WeaponTable Weapons;
        public float ArmedChance;
        public int Armour;
        public int Ammo;
    }

    /// <summary>
    /// The shipped weapon presets, and the resolution of the <c>weapons.preset</c> setting.
    ///
    /// A preset replaces the loadout of the factions drawn from the ambient crowd and leaves
    /// spawned ones alone (see <see cref="Faction.TakesWeaponPreset"/>). That line is deliberate:
    /// picking "military" should hand the mob carbines, not re-equip the actual army — which
    /// already has the kit its mode author gave it.
    ///
    /// "Mode" is the default and means "do not interfere": each mode's own loadouts stand, which
    /// is what keeps the pedestrian riot melee-heavy enough to sustain itself.
    /// </summary>
    public static class WeaponPresets
    {
        public const string UseMode = "mode";

        /// <summary>Ids in menu order. "mode" first because it is the default.</summary>
        public static readonly string[] Ids = { UseMode, "melee", "realistic", "armed", "military", "chaos", "custom" };

        public static readonly string[] Names =
        {
            "Mode's Own", "Melee Only", "Realistic", "Armed And Armoured", "Military", "Chaos", "Custom"
        };

        private static readonly Dictionary<string, WeaponPreset> Built =
            new Dictionary<string, WeaponPreset>(StringComparer.OrdinalIgnoreCase);

        private static WeaponPreset _custom;
        private static bool _customBuilt;
        private static bool _reportedEmptyCustom;

        /// <summary>
        /// Called on config reload and when a mode starts, so an edited custom list is picked up
        /// without a restart.
        /// </summary>
        public static void Reset()
        {
            _custom = null;
            _customBuilt = false;
            _reportedEmptyCustom = false;
        }

        /// <summary>
        /// The active preset, or null for "leave every faction's own loadout alone". Null is the
        /// default and the quiet path: nothing about a mode changes unless you ask for it.
        /// </summary>
        public static WeaponPreset Active(ConfigStore config)
        {
            string id = config.GetString("weapons.preset", UseMode);

            if (string.IsNullOrEmpty(id) || string.Equals(id, UseMode, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (string.Equals(id, "custom", StringComparison.OrdinalIgnoreCase))
            {
                return BuildCustom(config);
            }

            WeaponPreset preset;
            if (Stock().TryGetValue(id, out preset)) { return preset; }

            Log.Warn("Unknown weapon preset '" + id + "'. Using each mode's own loadouts.");
            return null;
        }

        /// <summary>
        /// The built-in presets, excluding "mode" (which is the absence of one) and "custom"
        /// (which is the player's).
        /// </summary>
        public static IEnumerable<WeaponPreset> Builtin { get { return Stock().Values; } }

        public static string DescriptionOf(string id)
        {
            if (string.Equals(id, UseMode, StringComparison.OrdinalIgnoreCase))
            {
                return "Leave every mode's own loadouts alone. The riot stays as its author balanced it.";
            }
            if (string.Equals(id, "custom", StringComparison.OrdinalIgnoreCase))
            {
                return "Your own list from weapons.custom in user.json. Empty falls back to the mode's own.";
            }

            WeaponPreset preset;
            return Stock().TryGetValue(id, out preset) ? preset.Description : string.Empty;
        }

        private static WeaponPreset BuildCustom(ConfigStore config)
        {
            if (_customBuilt) { return _custom; }
            _customBuilt = true;

            WeaponTable table = WeaponTable.FromJson(config.Resolve("weapons.custom"));

            if (table.Count == 0)
            {
                if (!_reportedEmptyCustom)
                {
                    _reportedEmptyCustom = true;
                    Log.Warn("Weapon preset is 'custom' but weapons.custom is empty. " +
                             "Add names to user.json, e.g. \"custom\": [\"WEAPON_BAT\", {\"name\": \"WEAPON_PISTOL\", \"weight\": 0.2}]. " +
                             "Falling back to each mode's own loadouts.");
                }
                _custom = null;
                return null;
            }

            _custom = new WeaponPreset
            {
                Id = "custom",
                Name = "Custom",
                Description = "Your own list.",
                Weapons = table,
                ArmedChance = config.GetFloat("weapons.customArmedChance", 1f),
                Armour = config.GetInt("weapons.customArmour", 0),
                Ammo = config.GetInt("weapons.customAmmo", 120)
            };

            Log.Info("Custom weapon preset: " + table.Count + " entries.");
            return _custom;
        }

        private static Dictionary<string, WeaponPreset> Stock()
        {
            if (Built.Count > 0) { return Built; }

            // Melee only. Not a novelty setting: with nothing that shoots, a riot stops killing
            // its own participants faster than the game can stream replacements, so it is by
            // some distance the longest-lived and densest thing this mod can produce. It is also
            // the only preset in which a crowd is genuinely dangerous to walk into and genuinely
            // survivable to fight, which is most of what people want from a riot.
            Add("melee", "Melee Only",
                "Nothing that shoots. Bats, blades and bottles - the longest-lasting riot there " +
                "is, because nobody can clear a street from across it.",
                1f, 0, 1,
                new WeaponTable()
                    .Add("WEAPON_BAT", 5f)
                    .Add("WEAPON_BOTTLE", 4f)
                    .Add("WEAPON_CROWBAR", 3.5f)
                    .Add("WEAPON_GOLFCLUB", 3f)
                    .Add("WEAPON_HAMMER", 3f)
                    .Add("WEAPON_MACHETE", 2.5f)
                    .Add("WEAPON_KNIFE", 2.5f)
                    .Add("WEAPON_WRENCH", 2f)
                    .Add("WEAPON_POOLCUE", 2f)
                    .Add("WEAPON_HATCHET", 1.5f)
                    .Add("WEAPON_SWITCHBLADE", 1.5f)
                    .Add("WEAPON_DAGGER", 1f)
                    .Add("WEAPON_KNUCKLE", 1f)
                    .Add("WEAPON_NIGHTSTICK", 1f)
                    .Add("WEAPON_BATTLEAXE", 0.5f)
                    .Add("WEAPON_FLASHLIGHT", 0.5f));

            // Realistic is deliberately mostly melee. It is not squeamishness: a crowd armed
            // with pistols kills off the local population faster than the game can stream
            // replacements, and the riot fizzles out into an empty street in about two minutes.
            Add("realistic", "Realistic",
                "What a crowd would actually pick up. Bats, bottles, crowbars, the odd pistol. " +
                "Sustains itself the longest.",
                0.85f, 0, 60,
                new WeaponTable()
                    .Add("WEAPON_BAT", 5f)
                    .Add("WEAPON_BOTTLE", 4f)
                    .Add("WEAPON_CROWBAR", 3f)
                    .Add("WEAPON_GOLFCLUB", 3f)
                    .Add("WEAPON_HAMMER", 3f)
                    .Add("WEAPON_KNIFE", 2.5f)
                    .Add("WEAPON_WRENCH", 2f)
                    .Add("WEAPON_POOLCUE", 2f)
                    .Add("WEAPON_MACHETE", 1.5f)
                    .Add("WEAPON_KNUCKLE", 1f)
                    .Add("WEAPON_SWITCHBLADE", 1f)
                    .Add("WEAPON_HATCHET", 1f)
                    .Add("WEAPON_MOLOTOV", 1f)
                    .Add("WEAPON_PETROLCAN", 0.5f)
                    .Add("WEAPON_PISTOL", 1f)
                    .Add("WEAPON_SNSPISTOL", 0.7f)
                    .Add("WEAPON_SAWNOFFSHOTGUN", 0.4f));

            Add("armed", "Armed And Armoured",
                "Everyone is tooled up and wearing a vest. Firefights instead of brawls, and " +
                "they take a few rounds to put down.",
                1f, 50, 200,
                new WeaponTable()
                    .Add("WEAPON_PISTOL", 4f)
                    .Add("WEAPON_COMBATPISTOL", 3f)
                    .Add("WEAPON_SNSPISTOL", 2f)
                    .Add("WEAPON_HEAVYPISTOL", 1.5f)
                    .Add("WEAPON_REVOLVER", 1f)
                    .Add("WEAPON_MICROSMG", 3f)
                    .Add("WEAPON_MACHINEPISTOL", 2f)
                    .Add("WEAPON_SMG", 2f)
                    .Add("WEAPON_ASSAULTSMG", 1f)
                    .Add("WEAPON_PUMPSHOTGUN", 2f)
                    .Add("WEAPON_SAWNOFFSHOTGUN", 2f)
                    .Add("WEAPON_BAT", 1f));

            Add("military", "Military",
                "Carbines, machine guns and grenades. An insurgency rather than a riot - and " +
                "short, because everything dies quickly.",
                1f, 100, 250,
                new WeaponTable()
                    .Add("WEAPON_CARBINERIFLE", 4f)
                    .Add("WEAPON_ASSAULTRIFLE", 4f)
                    .Add("WEAPON_SPECIALCARBINE", 2f)
                    .Add("WEAPON_BULLPUPRIFLE", 2f)
                    .Add("WEAPON_ADVANCEDRIFLE", 1f)
                    .Add("WEAPON_COMBATMG", 1f)
                    .Add("WEAPON_MG", 1f)
                    .Add("WEAPON_ASSAULTSHOTGUN", 1f)
                    .Add("WEAPON_MARKSMANRIFLE", 0.5f)
                    .Add("WEAPON_GRENADE", 1f)
                    .Add("WEAPON_SMOKEGRENADE", 0.5f)
                    .Add("WEAPON_PISTOL", 1f));

            Add("chaos", "Chaos",
                "Rockets, miniguns, fireworks and fire extinguishers. Not balanced, not " +
                "sustainable, and not meant to be.",
                1f, 25, 250,
                new WeaponTable()
                    .Add("WEAPON_MOLOTOV", 3f)
                    .Add("WEAPON_GRENADE", 3f)
                    .Add("WEAPON_RPG", 2f)
                    .Add("WEAPON_GRENADELAUNCHER", 2f)
                    .Add("WEAPON_STICKYBOMB", 2f)
                    .Add("WEAPON_PIPEBOMB", 2f)
                    .Add("WEAPON_FIREWORK", 2f)
                    .Add("WEAPON_PETROLCAN", 2f)
                    .Add("WEAPON_FIREEXTINGUISHER", 2f)
                    .Add("WEAPON_STUNGUN", 2f)
                    .Add("WEAPON_FLAREGUN", 2f)
                    .Add("WEAPON_MINIGUN", 1f)
                    .Add("WEAPON_MUSKET", 1f)
                    .Add("WEAPON_BZGAS", 1f)
                    .Add("WEAPON_RAILGUN", 0.5f)
                    .Add("WEAPON_HOMINGLAUNCHER", 0.5f));

            return Built;
        }

        private static void Add(string id, string name, string description, float armedChance, int armour, int ammo, WeaponTable table)
        {
            Built[id] = new WeaponPreset
            {
                Id = id,
                Name = name,
                Description = description,
                Weapons = table,
                ArmedChance = armedChance,
                Armour = armour,
                Ammo = ammo
            };
        }
    }
}
