using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TonightsTheNight.Util;

/// <summary>
/// Validates every riot mode that ships with the mod.
///
/// The modes are several thousand lines of hand-written JSON referencing model names, weapon
/// names, faction ids and relationship values. A typo in any of them is invisible until someone
/// loads the game and a faction silently does nothing. This is the cheapest place to catch that,
/// and the only one that does not cost a test session.
/// </summary>
public static class StockModeTests
{
    private static readonly string[] ValidReactions = { "Fight", "Flee", "Mixed", "Cower", "Bystander" };
    private static readonly string[] ValidRelations = { "companion", "respect", "like", "neutral", "dislike", "hate" };
    private static readonly string[] ValidRecruits = { "civilian", "male", "female", "criminal", "any", "none" };

    private static int _failures;

    private static void Check(string label, bool ok, string detail = "")
    {
        if (!ok)
        {
            Console.WriteLine("  FAIL  " + label + (detail.Length > 0 ? "   <- " + detail : ""));
            _failures++;
        }
    }

    public static int Run()
    {
        _failures = 0;

        Dictionary<string, string> modes = ExtractModes();
        Check("found the shipped modes", modes.Count >= 8, modes.Count + " found");

        foreach (var pair in modes)
        {
            string id = pair.Key;

            JsonValue mode;
            string error;
            if (!JsonValue.TryParse(pair.Value, out mode, out error))
            {
                Check(id + ": parses", false, error);
                continue;
            }

            Check(id + ": has a name", mode["name"].AsString(null) != null);
            Check(id + ": has a description", mode["description"].AsString(null) != null);
            Check(id + ": carries the stock marker", pair.Value.Contains("\"_stock\": true"));

            string playerRelationship = mode["playerRelationship"].AsString("neutral");
            Check(id + ": player relationship is valid", Array.IndexOf(ValidRelations, playerRelationship) >= 0, playerRelationship);

            var factionIds = new List<string>();
            int recruiting = 0;

            foreach (var entry in mode["factions"].Members)
            {
                string fid = id + "/" + entry.Key;
                JsonValue faction = entry.Value;
                factionIds.Add(entry.Key);

                Check(fid + ": has a name", faction["name"].AsString(null) != null);

                string reaction = faction["reaction"].AsString("Fight");
                Check(fid + ": reaction is valid", Array.IndexOf(ValidReactions, reaction) >= 0, reaction);

                string recruits = faction["recruits"].AsString("none");
                Check(fid + ": recruits filter is valid", Array.IndexOf(ValidRecruits, recruits) >= 0, recruits);
                if (recruits != "none") { recruiting++; }

                foreach (JsonValue weapon in faction["weapons"].Items)
                {
                    string weaponName;

                    if (weapon.IsObject)
                    {
                        weaponName = weapon["name"].AsString(null);
                        Check(fid + ": weapon entry has a name", weaponName != null);
                        Check(fid + ": weapon weight is positive", weapon["weight"].AsDouble(1) > 0);
                    }
                    else
                    {
                        weaponName = weapon.AsString(null);
                        Check(fid + ": bare weapon entry is a string", weaponName != null);
                    }

                    // A wrong weapon name is silent - the faction simply goes out unarmed - so
                    // shipped content is held to weapons a clean install actually has. Your own
                    // config may reference an add-on pack; this only covers what we ship.
                    if (weaponName != null)
                    {
                        Check(fid + ": weapon '" + weaponName + "' is a base-game weapon",
                              VanillaWeapons.IsKnown(weaponName), "not in the vanilla list");
                    }
                }

                // A faction that neither recruits nor spawns can never have any members.
                bool spawns = faction.Has("spawn") && faction["spawn"]["models"].Count > 0;
                Check(fid + ": can actually get members", recruits != "none" || spawns,
                      "neither recruits nor spawns");

                if (faction.Has("spawn"))
                {
                    JsonValue spawn = faction["spawn"];
                    Check(fid + ": spawn has models", spawn["models"].Count > 0);
                    Check(fid + ": spawn cap is sane", spawn["maxAlive"].AsInt(10) > 0 && spawn["maxAlive"].AsInt(10) <= 40,
                          spawn["maxAlive"].AsInt(10).ToString());
                    Check(fid + ": wave size is sane", spawn["perWave"].AsInt(2) > 0 && spawn["perWave"].AsInt(2) <= 8);

                    // Riding in something requires something to ride in.
                    if (spawn["inVehicleChance"].AsDouble(0) > 0)
                    {
                        Check(fid + ": has vehicles if it arrives by vehicle", spawn["vehicles"].Count > 0);
                    }

                    // Model names are the game's, not SHVDN's enum names: lowercase with
                    // underscores. "Marine01SMY" compiles fine and resolves to nothing.
                    foreach (string model in spawn["models"].AsStringList())
                    {
                        Check(fid + ": model '" + model + "' looks like a game model name",
                              model == model.ToLowerInvariant() && model.Contains("_"),
                              "expected e.g. s_m_y_marine_01");
                    }

                    foreach (string vehicle in spawn["vehicles"].AsStringList())
                    {
                        Check(fid + ": vehicle '" + vehicle + "' is lowercase",
                              vehicle == vehicle.ToLowerInvariant());
                    }
                }

                int fromPhase = faction["fromPhase"].AsInt(0);
                int phaseCount = mode["escalation"]["phases"].Count;
                Check(fid + ": fromPhase exists in this mode",
                      phaseCount == 0 || fromPhase < phaseCount,
                      "fromPhase " + fromPhase + " but only " + phaseCount + " phase(s)");
            }

            Check(id + ": defines factions", factionIds.Count > 0);
            Check(id + ": something can populate the riot", recruiting > 0 || factionIds.Count > 0);

            foreach (JsonValue relation in mode["relations"].Items)
            {
                string from = relation["from"].AsString("");
                string to = relation["to"].AsString("");
                string value = relation["value"].AsString("hate");

                Check(id + ": relation source '" + from + "' is a defined faction", factionIds.Contains(from));
                Check(id + ": relation target '" + to + "' is a defined faction", factionIds.Contains(to));
                Check(id + ": relation value '" + value + "' is valid", Array.IndexOf(ValidRelations, value) >= 0);
                Check(id + ": relation is not self-referential", from != to);
            }

            // A faction nobody has any relationship with just stands around.
            foreach (string fid in factionIds)
            {
                bool mentioned = false;
                foreach (JsonValue relation in mode["relations"].Items)
                {
                    if (relation["from"].AsString("") == fid || relation["to"].AsString("") == fid) { mentioned = true; break; }
                }
                Check(id + ": faction '" + fid + "' appears in the relationship matrix", mentioned);
            }

            Console.WriteLine("  PASS  " + id + " (" + factionIds.Count + " factions, " +
                              mode["relations"].Count + " relations, " +
                              mode["escalation"]["phases"].Count + " phases)");
        }

        return _failures;
    }

    /// <summary>Pulls the verbatim string constants back out of StockModes.cs.</summary>
    private static Dictionary<string, string> ExtractModes()
    {
        var result = new Dictionary<string, string>();

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src"))) { dir = dir.Parent; }
        if (dir == null) { throw new DirectoryNotFoundException("Could not locate the repository root."); }

        string source = File.ReadAllText(Path.Combine(dir.FullName, "src", "TonightsTheNight", "Factions", "StockModes.cs"));

        int at = 0;
        while (true)
        {
            int declaration = source.IndexOf("private const string ", at, StringComparison.Ordinal);
            if (declaration < 0) { break; }

            int nameStart = declaration + "private const string ".Length;
            int nameEnd = source.IndexOf(' ', nameStart);
            string name = source.Substring(nameStart, nameEnd - nameStart);

            int open = source.IndexOf("@\"", nameEnd, StringComparison.Ordinal);
            if (open < 0) { break; }
            open += 2;

            var body = new StringBuilder();
            int i = open;
            while (i < source.Length)
            {
                if (source[i] == '"')
                {
                    if (i + 1 < source.Length && source[i + 1] == '"') { body.Append('"'); i += 2; continue; }
                    break;
                }
                body.Append(source[i]);
                i++;
            }

            if (name != "Marker") { result[name.ToLowerInvariant()] = body.ToString(); }
            at = i;
        }

        return result;
    }
}
