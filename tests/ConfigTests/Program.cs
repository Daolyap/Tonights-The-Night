using System;
using TonightsTheNight.Util;

class Program
{
    static int failures = 0;

    static void Check(string label, bool ok, string detail = "")
    {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + label + (ok ? "" : "   <- " + detail));
        if (!ok) failures++;
    }

    static JsonValue Walk(JsonValue root, string path)
    {
        JsonValue cur = root;
        foreach (string seg in path.Split('.'))
        {
            cur = cur[seg];
            if (cur.IsNull) return JsonValue.Null;
        }
        return cur;
    }

    /// <summary>Walks up from the test binary to the repo root, so CI and local runs agree.</summary>
    static string StockModesPath()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }
        if (dir == null) throw new System.IO.FileNotFoundException("Could not locate the repository root from " + AppContext.BaseDirectory);
        return System.IO.Path.Combine(dir.FullName, "src", "TonightsTheNight", "Factions", "StockModes.cs");
    }

    static readonly string[] Guns = { "Pistol", "SNSPistol", "MicroSMG" };

    static double MeleeWeight(JsonValue faction)
    {
        double total = 0;
        foreach (JsonValue w in faction["weapons"].Items)
        {
            if (Array.IndexOf(Guns, w["name"].AsString("")) < 0) { total += w["weight"].AsDouble(1); }
        }
        return total;
    }

    static double GunWeight(JsonValue faction)
    {
        double total = 0;
        foreach (JsonValue w in faction["weapons"].Items)
        {
            if (Array.IndexOf(Guns, w["name"].AsString("")) >= 0) { total += w["weight"].AsDouble(1); }
        }
        return total;
    }

    static int Main()
    {
        Console.WriteLine("--- lenient parsing (comments, trailing commas, bare keys) ---");
        string lenient = @"{
            // a line comment
            /* and a block one */
            name: 'Pedestrian Riot',   // bare key + single quotes
            enabled: true,
            count: 3,
            ratio: 0.85,
            negative: -12,
            sci: 1.5e2,
            list: [ 1, 2, 3, ],        // trailing comma in an array
            nested: { deep: { value: 42 } },
        }";
        JsonValue v; string err;
        Check("parses", JsonValue.TryParse(lenient, out v, out err), err);
        Check("bare key + single quotes", v["name"].AsString() == "Pedestrian Riot", v["name"].AsString());
        Check("bool", v["enabled"].AsBool() == true);
        Check("int", v["count"].AsInt() == 3);
        Check("float", Math.Abs(v["ratio"].AsDouble() - 0.85) < 1e-9);
        Check("negative", v["negative"].AsInt() == -12, v["negative"].AsInt().ToString());
        Check("scientific", Math.Abs(v["sci"].AsDouble() - 150.0) < 1e-9, v["sci"].AsDouble().ToString());
        Check("array trailing comma", v["list"].Count == 3);
        Check("dotted lookup", Walk(v, "nested.deep.value").AsInt() == 42);
        Check("missing key is null not throw", v["nope"].IsNull);
        Check("missing deep path is null", Walk(v, "nested.nope.value").IsNull);
        Check("fallback on missing", v["nope"].AsInt(99) == 99);

        Console.WriteLine("--- case-insensitive keys (config written 'Colour' vs 'colour') ---");
        Check("case insensitive", v["NAME"].AsString() == "Pedestrian Riot");

        Console.WriteLine("--- the shipped stock mode file ---");
        string stock = System.IO.File.ReadAllText(StockModesPath());
        int a = stock.IndexOf("private const string Pedestrians = @\"") + "private const string Pedestrians = @\"".Length;
        int b = stock.IndexOf("\";", a);
        string mode = stock.Substring(a, b - a).Replace("\"\"", "\"");
        Check("stock mode parses", JsonValue.TryParse(mode, out v, out err), err);
        Check("has 3 factions", v["factions"].Count == 3, v["factions"].Count.ToString());
        Check("mob_red weapons", v["factions"]["mob_red"]["weapons"].Count == 6);
        Check("weapons are weighted", v["factions"]["mob_red"]["weapons"].Items[0]["weight"].AsDouble() > 0);
        Check("melee outweighs firearms in mob_red", MeleeWeight(v["factions"]["mob_red"]) > 3 * GunWeight(v["factions"]["mob_red"]));
        Check("bystanders mixed", v["factions"]["bystanders"]["reaction"].AsString() == "Mixed");
        Check("fightBackChance", Math.Abs(v["factions"]["bystanders"]["fightBackChance"].AsDouble() - 0.15) < 1e-9);
        Check("3 relations", v["relations"].Count == 3);
        Check("relation mutual flag", v["relations"].Items[0]["mutual"].AsBool() == true);
        Check("mode config override", Math.Abs(Walk(v, "config.combat.accuracy").AsDouble() - 15) < 1e-9);
        Check("mob shares present", Math.Abs(v["factions"]["mob_red"]["share"].AsDouble() - 0.45) < 1e-9);
        Check("fighters outnumber explicit bystanders",
              v["factions"]["mob_red"]["share"].AsDouble() + v["factions"]["mob_blue"]["share"].AsDouble() >
              v["factions"]["bystanders"]["share"].AsDouble());
        Check("player is fair game in the stock mode",
              v["playerRelationship"].AsString() == "hate", v["playerRelationship"].AsString());
        Check("shares sum to 1", Math.Abs(
              v["factions"]["mob_red"]["share"].AsDouble() +
              v["factions"]["mob_blue"]["share"].AsDouble() +
              v["factions"]["bystanders"]["share"].AsDouble() - 1.0) < 1e-9);
        Check("stock marker present", mode.Contains("\"_stock\": true"));

        Console.WriteLine("--- round trip ---");
        string written = v.ToJson();
        JsonValue again;
        Check("re-parses own output", JsonValue.TryParse(written, out again, out err), err);
        Check("round trip preserves nesting", again["factions"]["mob_blue"]["name"].AsString() == "Blue Mob");
        Check("round trip preserves numbers", Math.Abs(again["factions"]["bystanders"]["fightBackChance"].AsDouble() - 0.15) < 1e-9);

        Console.WriteLine("--- malformed input must not throw ---");
        foreach (string bad in new[] { "{", "{ \"a\": }", "", "[1,2", "{ \"a\" 1 }", "not json at all", "{\"a\":\"unterminated" })
        {
            JsonValue outv; string e2;
            bool ok = true;
            try { JsonValue.TryParse(bad, out outv, out e2); }
            catch (Exception ex) { ok = false; e2 = ex.Message; }
            Check("TryParse survives: " + (bad.Length > 20 ? bad.Substring(0, 20) : bad), ok);
        }

        // A stack overflow cannot be caught in .NET - it takes the process, which here means
        // GTA V closing with no message and nothing in the log. Mode files get shared between
        // players, so this parser reads files written by strangers.
        Console.WriteLine("--- deeply nested input must be rejected, not fatal ---");
        {
            JsonValue deep; string deepError;
            bool bombRejected = !JsonValue.TryParse(new string('[', 100000) + new string(']', 100000), out deep, out deepError);
            Check("a nesting bomb is rejected", bombRejected);

            bool siblingsFine = JsonValue.TryParse("[[1],[2],[3],[4],[5]]", out deep, out deepError);
            Check("siblings are not nesting", siblingsFine);

            bool realShape = JsonValue.TryParse(
                "{\"factions\":{\"a\":{\"spawn\":{\"models\":[\"x\"]},\"blip\":{\"enabled\":true}}}}",
                out deep, out deepError);
            Check("a real mode shape still parses", realShape);
        }

        Console.WriteLine("--- every shipped riot mode ---");
        failures += StockModeTests.Run();

        Console.WriteLine("--- the shipped weapon presets ---");
        failures += WeaponPresetTests.Run();

        Console.WriteLine("--- every config path the code reads is declared in defaults ---");
        failures += ConfigPathCoverage.Run();

        Console.WriteLine();
        Console.WriteLine("--- release readiness ---");
        failures += ReleaseReadinessTests.Run();

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : failures + " CHECK(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
