using System;
using System.Collections.Generic;
using GTA;
using TonightsTheNight.Util;

namespace TonightsTheNight.Factions
{
    /// <summary>
    /// One entry in a faction's loadout. Weights matter more than they look: a riot fought with
    /// bats and crowbars sustains itself, while one fought with pistols kills off the local
    /// population faster than the game can replace it and fizzles out into an empty street.
    /// </summary>
    public struct WeaponChoice
    {
        public string Name;
        public float Weight;
    }

    /// <summary>How members of a faction react when the shooting starts.</summary>
    public enum Reaction
    {
        /// <summary>Seek out and attack hated targets.</summary>
        Fight,
        /// <summary>Run from anything hostile.</summary>
        Flee,
        /// <summary>A share of members fight, the rest flee. See <see cref="FightBackChance"/>.</summary>
        Mixed,
        /// <summary>Stay put and cower.</summary>
        Cower,
        /// <summary>Stand and watch. Unarmed, unbothered, and oddly unsettling.</summary>
        Bystander
    }

    /// <summary>
    /// A faction is the unit everything else is built from: a relationship group, a pool of
    /// peds, a loadout, a blip style and a reaction profile. Riot modes are just sets of these
    /// plus a matrix of who hates whom, which is why a user-made faction needs no code.
    /// </summary>
    public sealed class Faction
    {
        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public Reaction Reaction { get; private set; }

        /// <summary>Only meaningful for <see cref="Reaction.Mixed"/>. 0..1.</summary>
        public float FightBackChance { get; private set; }

        /// <summary>
        /// Relative size of this faction among the recruits a mode makes. Shares are relative,
        /// not percentages: two factions at 1 each split evenly, and 0.2/0.2/0.6 makes the third
        /// three times either of the others.
        /// </summary>
        public float Share { get; private set; }

        /// <summary>
        /// Weighted loadout, resolved lazily so an unknown name is a log line, not a crash.
        /// Accepts either a bare name or {"name": ..., "weight": ...} in config.
        /// </summary>
        public List<WeaponChoice> Weapons { get; private set; }

        private float _weaponWeightTotal;

        public int Ammo { get; private set; }
        public int Armour { get; private set; }
        public int Health { get; private set; }
        public float ArmedChance { get; private set; }

        public bool BlipEnabled { get; private set; }
        public BlipSprite BlipSprite { get; private set; }
        public BlipColor BlipColor { get; private set; }

        /// <summary>Ped model names this faction spawns. Empty means conversion-only.</summary>
        public List<string> Models { get; private set; }

        /// <summary>
        /// Which ambient peds get recruited: "civilian", "male", "female", "any", or "none".
        /// Conversion is how we get scale without paying to spawn it.
        /// </summary>
        public string Recruits { get; private set; }

        public string RelationshipGroupName { get { return "TTN_" + Id.ToUpperInvariant(); } }

        public int GroupHash { get; set; }

        public static Faction FromJson(string id, JsonValue node)
        {
            var faction = new Faction
            {
                Id = id,
                DisplayName = node["name"].AsString(id),
                FightBackChance = Clamp01(node["fightBackChance"].AsFloat(0.25f)),
                Share = Math.Max(0f, node["share"].AsFloat(1f)),
                Weapons = ParseWeapons(node["weapons"]),
                Ammo = node["ammo"].AsInt(120),
                Armour = Math.Max(0, Math.Min(100, node["armour"].AsInt(0))),
                Health = node["health"].AsInt(0),
                ArmedChance = Clamp01(node["armedChance"].AsFloat(1f)),
                Models = node["models"].AsStringList(),
                Recruits = node["recruits"].AsString("none"),
                BlipEnabled = node["blip"]["enabled"].AsBool(true),
                BlipSprite = ParseEnum(node["blip"]["sprite"].AsString("Standard"), BlipSprite.Standard),
                BlipColor = ParseEnum(node["blip"]["colour"].AsString(node["blip"]["color"].AsString("White")), BlipColor.White),
                Reaction = ParseEnum(node["reaction"].AsString("Fight"), Reaction.Fight)
            };
            return faction;
        }

        private static List<WeaponChoice> ParseWeapons(JsonValue node)
        {
            var result = new List<WeaponChoice>();

            foreach (JsonValue entry in node.Items)
            {
                if (entry.IsObject)
                {
                    string name = entry["name"].AsString(null);
                    if (name == null)
                    {
                        Log.Warn("Weapon entry has no 'name'. Skipped.");
                        continue;
                    }
                    result.Add(new WeaponChoice { Name = name, Weight = Math.Max(0.01f, entry["weight"].AsFloat(1f)) });
                }
                else
                {
                    string name = entry.AsString(null);
                    if (name != null) { result.Add(new WeaponChoice { Name = name, Weight = 1f }); }
                }
            }

            return result;
        }

        /// <summary>Weighted pick, so a loadout can be mostly melee with the odd firearm.</summary>
        public string PickWeapon(Random random)
        {
            if (Weapons.Count == 0) { return null; }

            if (_weaponWeightTotal <= 0f)
            {
                foreach (WeaponChoice choice in Weapons) { _weaponWeightTotal += choice.Weight; }
            }

            double roll = random.NextDouble() * _weaponWeightTotal;
            foreach (WeaponChoice choice in Weapons)
            {
                roll -= choice.Weight;
                if (roll <= 0) { return choice.Name; }
            }

            return Weapons[Weapons.Count - 1].Name;
        }

        /// <summary>
        /// Resolves this faction's reaction for one ped. Mixed rolls per ped so a crowd splits
        /// instead of behaving as a bloc.
        /// </summary>
        public Reaction ResolveReaction(Random random)
        {
            if (Reaction != Reaction.Mixed) { return Reaction; }
            return random.NextDouble() < FightBackChance ? Reaction.Fight : Reaction.Flee;
        }

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : (value > 1f ? 1f : value);
        }

        private static T ParseEnum<T>(string text, T fallback) where T : struct
        {
            if (string.IsNullOrEmpty(text)) { return fallback; }

            T parsed;
            if (Enum.TryParse(text, true, out parsed)) { return parsed; }

            Log.Warn("Unknown " + typeof(T).Name + " '" + text + "', using " + fallback + ".");
            return fallback;
        }
    }
}
