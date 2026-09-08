using System;
using System.Collections.Generic;

namespace TonightsTheNight.Factions
{
    /// <summary>One weapon, as it appears in the in-game picker.</summary>
    public sealed class CatalogWeapon
    {
        /// <summary>The game's own name, which is what gets written into the loadout.</summary>
        public string Name;

        /// <summary>What a person would call it.</summary>
        public string Label;

        public CatalogWeapon(string name, string label)
        {
            Name = name;
            Label = label;
        }
    }

    /// <summary>A named group of weapons in the picker.</summary>
    public sealed class WeaponCategory
    {
        public string Name;
        public readonly List<CatalogWeapon> Weapons = new List<CatalogWeapon>();

        public WeaponCategory(string name) { Name = name; }

        public WeaponCategory Add(string gameName, string label)
        {
            Weapons.Add(new CatalogWeapon(gameName, label));
            return this;
        }
    }

    /// <summary>
    /// The base-game weapons, grouped, for building a loadout from inside the game.
    ///
    /// Custom loadouts were previously a JSON array in user.json and a menu item explaining how
    /// to write one, which is a fine thing to offer somebody who is already editing config and
    /// a bad thing to offer everybody else. This is the same list, browsable with a controller.
    ///
    /// Base game only, on purpose. A picker cannot know what an add-on pack calls its weapons,
    /// and the config file is still there for anyone who has one.
    /// </summary>
    public static class WeaponCatalog
    {
        private static List<WeaponCategory> _categories;

        public static IReadOnlyList<WeaponCategory> Categories
        {
            get { return _categories ?? (_categories = Build()); }
        }

        /// <summary>
        /// Rockets, launchers, miniguns and belt-fed machine guns.
        ///
        /// Named as a group because they are the difference between a fight and a cutscene about
        /// your own death, and because a mode author reaching for one is usually reaching for
        /// flavour rather than for a quarter of a faction carrying it. One weight of 1 against a
        /// 3 reads as "the odd RPG" and produces one man in four with a rocket launcher.
        /// </summary>
        private static readonly HashSet<string> Heavy = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "RPG", "GRENADELAUNCHER", "GRENADELAUNCHERSMOKE", "COMPACTLAUNCHER", "HOMINGLAUNCHER",
            "MINIGUN", "RAILGUN", "FIREWORK", "EMPLAUNCHER",
            "MG", "COMBATMG", "COMBATMGMK2", "GUSENBERG",
            "RAYMINIGUN", "WIDOWMAKER", "UNHOLYHELLBRINGER", "RAYCARBINE"
        };

        /// <summary>
        /// Whether a weapon name is one of the heavy ones. Accepts either spelling, the same way
        /// everything else that takes a weapon name does.
        /// </summary>
        public static bool IsHeavy(string name)
        {
            if (string.IsNullOrEmpty(name)) { return false; }

            string trimmed = name.Trim();
            if (trimmed.StartsWith("WEAPON_", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring("WEAPON_".Length);
            }

            return Heavy.Contains(trimmed.Replace("_", string.Empty));
        }

        /// <summary>The label for a game name, or the name itself for anything not in the list.</summary>
        public static string LabelFor(string gameName)
        {
            foreach (WeaponCategory category in Categories)
            {
                foreach (CatalogWeapon weapon in category.Weapons)
                {
                    if (string.Equals(weapon.Name, gameName, StringComparison.OrdinalIgnoreCase)) { return weapon.Label; }
                }
            }
            return gameName;
        }

        private static List<WeaponCategory> Build()
        {
            return new List<WeaponCategory>
            {
                new WeaponCategory("Melee")
                    .Add("WEAPON_BAT", "Baseball Bat")
                    .Add("WEAPON_BOTTLE", "Broken Bottle")
                    .Add("WEAPON_CROWBAR", "Crowbar")
                    .Add("WEAPON_GOLFCLUB", "Golf Club")
                    .Add("WEAPON_HAMMER", "Hammer")
                    .Add("WEAPON_WRENCH", "Pipe Wrench")
                    .Add("WEAPON_POOLCUE", "Pool Cue")
                    .Add("WEAPON_NIGHTSTICK", "Nightstick")
                    .Add("WEAPON_KNUCKLE", "Brass Knuckles")
                    .Add("WEAPON_FLASHLIGHT", "Flashlight")
                    .Add("WEAPON_KNIFE", "Knife")
                    .Add("WEAPON_DAGGER", "Antique Cavalry Dagger")
                    .Add("WEAPON_SWITCHBLADE", "Switchblade")
                    .Add("WEAPON_MACHETE", "Machete")
                    .Add("WEAPON_HATCHET", "Hatchet")
                    .Add("WEAPON_BATTLEAXE", "Battle Axe")
                    .Add("WEAPON_STONE_HATCHET", "Stone Hatchet"),

                new WeaponCategory("Handguns")
                    .Add("WEAPON_PISTOL", "Pistol")
                    .Add("WEAPON_PISTOL_MK2", "Pistol Mk II")
                    .Add("WEAPON_COMBATPISTOL", "Combat Pistol")
                    .Add("WEAPON_APPISTOL", "AP Pistol")
                    .Add("WEAPON_PISTOL50", "Pistol .50")
                    .Add("WEAPON_SNSPISTOL", "SNS Pistol")
                    .Add("WEAPON_HEAVYPISTOL", "Heavy Pistol")
                    .Add("WEAPON_VINTAGEPISTOL", "Vintage Pistol")
                    .Add("WEAPON_REVOLVER", "Heavy Revolver")
                    .Add("WEAPON_DOUBLEACTION", "Double-Action Revolver")
                    .Add("WEAPON_NAVYREVOLVER", "Navy Revolver")
                    .Add("WEAPON_CERAMICPISTOL", "Ceramic Pistol")
                    .Add("WEAPON_STUNGUN", "Stun Gun")
                    .Add("WEAPON_FLAREGUN", "Flare Gun"),

                new WeaponCategory("Submachine Guns")
                    .Add("WEAPON_MICROSMG", "Micro SMG")
                    .Add("WEAPON_MACHINEPISTOL", "Machine Pistol")
                    .Add("WEAPON_MINISMG", "Mini SMG")
                    .Add("WEAPON_SMG", "SMG")
                    .Add("WEAPON_ASSAULTSMG", "Assault SMG")
                    .Add("WEAPON_COMBATPDW", "Combat PDW")
                    .Add("WEAPON_GUSENBERG", "Gusenberg Sweeper"),

                new WeaponCategory("Shotguns")
                    .Add("WEAPON_SAWNOFFSHOTGUN", "Sawed-Off Shotgun")
                    .Add("WEAPON_PUMPSHOTGUN", "Pump Shotgun")
                    .Add("WEAPON_BULLPUPSHOTGUN", "Bullpup Shotgun")
                    .Add("WEAPON_ASSAULTSHOTGUN", "Assault Shotgun")
                    .Add("WEAPON_HEAVYSHOTGUN", "Heavy Shotgun")
                    .Add("WEAPON_DBSHOTGUN", "Double Barrel Shotgun")
                    .Add("WEAPON_COMBATSHOTGUN", "Combat Shotgun")
                    .Add("WEAPON_MUSKET", "Musket"),

                new WeaponCategory("Rifles")
                    .Add("WEAPON_ASSAULTRIFLE", "Assault Rifle")
                    .Add("WEAPON_CARBINERIFLE", "Carbine Rifle")
                    .Add("WEAPON_ADVANCEDRIFLE", "Advanced Rifle")
                    .Add("WEAPON_SPECIALCARBINE", "Special Carbine")
                    .Add("WEAPON_BULLPUPRIFLE", "Bullpup Rifle")
                    .Add("WEAPON_COMPACTRIFLE", "Compact Rifle")
                    .Add("WEAPON_MILITARYRIFLE", "Military Rifle")
                    .Add("WEAPON_HEAVYRIFLE", "Heavy Rifle")
                    .Add("WEAPON_TACTICALRIFLE", "Service Carbine"),

                new WeaponCategory("Heavy Weapons")
                    .Add("WEAPON_MG", "MG")
                    .Add("WEAPON_COMBATMG", "Combat MG")
                    .Add("WEAPON_MINIGUN", "Minigun")
                    .Add("WEAPON_SNIPERRIFLE", "Sniper Rifle")
                    .Add("WEAPON_MARKSMANRIFLE", "Marksman Rifle")
                    .Add("WEAPON_HEAVYSNIPER", "Heavy Sniper")
                    .Add("WEAPON_RPG", "RPG")
                    .Add("WEAPON_GRENADELAUNCHER", "Grenade Launcher")
                    .Add("WEAPON_COMPACTLAUNCHER", "Compact Grenade Launcher")
                    .Add("WEAPON_HOMINGLAUNCHER", "Homing Launcher")
                    .Add("WEAPON_RAILGUN", "Railgun")
                    .Add("WEAPON_FIREWORK", "Firework Launcher"),

                new WeaponCategory("Thrown")
                    .Add("WEAPON_MOLOTOV", "Molotov")
                    .Add("WEAPON_GRENADE", "Grenade")
                    .Add("WEAPON_STICKYBOMB", "Sticky Bomb")
                    .Add("WEAPON_PIPEBOMB", "Pipe Bomb")
                    .Add("WEAPON_PROXMINE", "Proximity Mine")
                    .Add("WEAPON_SMOKEGRENADE", "Tear Gas")
                    .Add("WEAPON_BZGAS", "BZ Gas")
                    .Add("WEAPON_FLARE", "Flare")
                    .Add("WEAPON_SNOWBALL", "Snowball")
                    .Add("WEAPON_BALL", "Baseball"),

                new WeaponCategory("Alien And Odd")
                    .Add("WEAPON_RAYPISTOL", "Up-n-Atomizer")
                    .Add("WEAPON_RAYCARBINE", "Unholy Hellbringer")
                    .Add("WEAPON_RAYMINIGUN", "Widowmaker")
                    .Add("WEAPON_UNHOLYHELLBRINGER", "Unholy Hellbringer (alt)")
                    .Add("WEAPON_WIDOWMAKER", "Widowmaker (alt)")
                    .Add("WEAPON_PETROLCAN", "Jerry Can")
                    .Add("WEAPON_FIREEXTINGUISHER", "Fire Extinguisher")
                    .Add("WEAPON_HAZARDCAN", "Hazardous Jerry Can")
                    .Add("WEAPON_FERTILIZERCAN", "Fertilizer Can")
            };
        }
    }
}
