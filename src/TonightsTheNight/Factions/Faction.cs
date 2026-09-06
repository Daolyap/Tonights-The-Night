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

        /// <summary>Weapon names, resolved lazily so an unknown name is a log line, not a crash.</summary>
        public List<string> Weapons { get; private set; }

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
                Weapons = node["weapons"].AsStringList(),
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
