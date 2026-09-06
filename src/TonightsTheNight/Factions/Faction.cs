using System;
using System.Collections.Generic;
using GTA;
using TonightsTheNight.Util;

namespace TonightsTheNight.Factions
{
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
        public WeaponTable Weapons { get; private set; }

        /// <summary>
        /// Whether a selected weapon preset replaces this faction's own loadout.
        ///
        /// Defaults to "yes for factions drawn from the ambient crowd, no for spawned ones",
        /// which is the line the preset concept actually cares about: a preset answers "what is
        /// the mob carrying", not "how is the army equipped". A mode author can override it.
        /// </summary>
        public bool TakesWeaponPreset { get; private set; }

        public int Ammo { get; private set; }
        public int Armour { get; private set; }
        public int Health { get; private set; }
        public float ArmedChance { get; private set; }

        public bool BlipEnabled { get; private set; }
        public BlipSprite BlipSprite { get; private set; }
        public BlipColor BlipColor { get; private set; }

        /// <summary>How this faction spawns, when it cannot be drawn from the ambient crowd.</summary>
        public SpawnProfile Spawn { get; private set; }

        /// <summary>
        /// The escalation phase this faction joins at. 0 means present from the start; a higher
        /// number is what makes the military arrive late rather than at the first punch.
        /// </summary>
        public int FromPhase { get; private set; }

        /// <summary>Accuracy override for this faction, or -1 to use the global setting.</summary>
        public int Accuracy { get; private set; }

        /// <summary>
        /// How this faction regards the player, or -1 for "whatever the mode says".
        ///
        /// This is the setting that makes Martial Law work. One mode-wide answer meant the army
        /// hating you and the civilians hating you too, so the crowd you were meant to be
        /// running alongside spent the riot punching you instead of the soldiers.
        /// </summary>
        public int PlayerRelationship { get; private set; }

        /// <summary>
        /// "default" or "random" clothing for spawned peds. Spawning without setting either
        /// leaves the ped on component 0 of every slot, which for some models is an untextured
        /// black figure and for others is half an outfit.
        /// </summary>
        public string Outfit { get; private set; }

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
                Weapons = WeaponTable.FromJson(node["weapons"]),
                Ammo = node["ammo"].AsInt(120),
                Armour = Math.Max(0, Math.Min(100, node["armour"].AsInt(0))),
                Health = node["health"].AsInt(0),
                ArmedChance = Clamp01(node["armedChance"].AsFloat(1f)),
                Spawn = node.Has("spawn") ? SpawnProfile.FromJson(node["spawn"]) : SpawnProfile.Disabled(),
                FromPhase = node["fromPhase"].AsInt(0),
                Accuracy = node["accuracy"].AsInt(-1),
                PlayerRelationship = RelationshipMatrix.Parse(node["playerRelationship"].AsString(null), -1),
                Outfit = node["outfit"].AsString("default"),
                Recruits = node["recruits"].AsString("none"),
                TakesWeaponPreset = node["weaponPreset"].AsBool(
                    !string.Equals(node["recruits"].AsString("none"), "none", StringComparison.OrdinalIgnoreCase)),
                BlipEnabled = node["blip"]["enabled"].AsBool(true),
                BlipSprite = ParseEnum(node["blip"]["sprite"].AsString("Standard"), BlipSprite.Standard),
                BlipColor = ParseEnum(node["blip"]["colour"].AsString(node["blip"]["color"].AsString("White")), BlipColor.White),
                Reaction = ParseEnum(node["reaction"].AsString("Fight"), Reaction.Fight)
            };
            return faction;
        }

        /// <summary>Weighted pick from this faction's own loadout.</summary>
        public string PickWeapon(Random random)
        {
            return Weapons.Pick(random);
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
