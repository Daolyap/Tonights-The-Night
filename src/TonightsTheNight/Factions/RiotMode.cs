using System;
using System.Collections.Generic;
using TonightsTheNight.Util;

namespace TonightsTheNight.Factions
{
    public struct Relation
    {
        public string From;
        public string To;
        public int Value;
        public bool Mutual;
    }

    /// <summary>
    /// A riot mode is a data file, not a class: the factions in play, who hates whom, and any
    /// config this mode overrides. The stock modes use exactly the same schema a user-made mode
    /// would, which is what makes "customisable factions" free rather than a feature.
    /// </summary>
    public sealed class RiotMode
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public List<Faction> Factions { get; private set; }
        public List<Relation> Relations { get; private set; }

        /// <summary>Config layer 3. Lets Purge force night without touching your globals.</summary>
        public JsonValue Overrides { get; private set; }

        /// <summary>Phase definitions, if this mode escalates.</summary>
        public JsonValue Escalation { get; private set; }

        /// <summary>Weather, time and colour grade for this mode.</summary>
        public JsonValue Ambience { get; private set; }

        /// <summary>Countdown and curfew, for Purge and anything shaped like it.</summary>
        public JsonValue Purge { get; private set; }

        /// <summary>How this mode treats the player when they have not picked a side.</summary>
        public int PlayerRelationship { get; private set; }

        public static RiotMode FromJson(string id, JsonValue node)
        {
            var mode = new RiotMode
            {
                Id = id,
                Name = node["name"].AsString(id),
                Description = node["description"].AsString(string.Empty),
                Factions = new List<Faction>(),
                Relations = new List<Relation>(),
                Overrides = node["config"],
                Escalation = node["escalation"],
                Ambience = node["ambience"],
                Purge = node["purge"],
                PlayerRelationship = ParseRelationship(node["playerRelationship"].AsString("neutral"))
            };

            foreach (var pair in node["factions"].Members)
            {
                try
                {
                    mode.Factions.Add(Faction.FromJson(pair.Key, pair.Value));
                }
                catch (Exception ex)
                {
                    Log.Error("Skipping faction '" + pair.Key + "' in mode '" + id + "'", ex);
                }
            }

            foreach (JsonValue entry in node["relations"].Items)
            {
                string from = entry["from"].AsString(null);
                string to = entry["to"].AsString(null);
                if (from == null || to == null)
                {
                    Log.Warn("Relation in mode '" + id + "' is missing 'from' or 'to'. Skipped.");
                    continue;
                }

                mode.Relations.Add(new Relation
                {
                    From = from,
                    To = to,
                    Value = ParseRelationship(entry["value"].AsString("hate")),
                    Mutual = entry["mutual"].AsBool(true)
                });
            }

            if (mode.Factions.Count == 0)
            {
                Log.Warn("Mode '" + id + "' defines no factions.");
            }

            return mode;
        }

        public Faction Find(string factionId)
        {
            foreach (Faction faction in Factions)
            {
                if (string.Equals(faction.Id, factionId, StringComparison.OrdinalIgnoreCase)) { return faction; }
            }
            return null;
        }

        private static int ParseRelationship(string text)
        {
            switch ((text ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "companion": return RelationshipMatrix.Companion;
                case "respect": return RelationshipMatrix.Respect;
                case "like": return RelationshipMatrix.Like;
                case "neutral": return RelationshipMatrix.Neutral;
                case "dislike": return RelationshipMatrix.Dislike;
                case "hate": return RelationshipMatrix.Hate;
                default:
                    int numeric;
                    if (int.TryParse(text, out numeric) && numeric >= 0 && numeric <= 5) { return numeric; }
                    Log.Warn("Unknown relationship '" + text + "', treating as hate.");
                    return RelationshipMatrix.Hate;
            }
        }
    }
}
