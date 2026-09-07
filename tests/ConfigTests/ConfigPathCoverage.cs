using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TonightsTheNight.Config;
using TonightsTheNight.Util;

/// <summary>
/// Scans the source for every settings path the code actually reads, and asserts each one is
/// declared in the shipped defaults.
///
/// A path missing from defaults still *works* — lookups fall through to the caller's fallback —
/// but it is invisible to anyone reading defaults.json, and the loader reports it as an unknown
/// key when a user writes it into user.json. That is a confusing way to lose an afternoon, so
/// it fails the build instead. (It has already caught one: combat.attributeIds, the exact key
/// the testing guide tells a tester to add by hand.)
/// </summary>
public static class ConfigPathCoverage
{
    private static readonly Regex Reads = new Regex(
        // Colour is a wrapper that reads a setting and parses it, so it counts as a read.
        @"(?:GetBool|GetInt|GetFloat|GetStringList|GetString|Resolve|SetLive|Colour)\(\s*""([a-zA-Z0-9_.]+)""",
        RegexOptions.Compiled);

    public static int Run()
    {
        int failures = 0;

        string src = Path.Combine(RepoRoot(), "src");
        JsonValue defaults = DefaultConfig.Build();
        var paths = new SortedSet<string>(StringComparer.Ordinal);

        foreach (string file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match match in Reads.Matches(File.ReadAllText(file)))
            {
                paths.Add(match.Groups[1].Value);
            }
        }

        if (paths.Count < 15)
        {
            Console.WriteLine("  FAIL  found only " + paths.Count + " config reads - the scanner is probably broken");
            return 1;
        }
        Console.WriteLine("  PASS  scanned " + paths.Count + " distinct config paths");

        foreach (string path in paths)
        {
            bool declared = !Walk(defaults, path).IsNull;
            Console.WriteLine((declared ? "  PASS  " : "  FAIL  ") + path +
                              (declared ? "" : "   <- not declared in DefaultConfig.Build()"));
            if (!declared) { failures++; }
        }

        // And the reverse: defaults.json must not advertise a knob nothing reads. A setting
        // that looks tunable but silently does nothing is worse than no setting at all.
        foreach (string path in Leaves(defaults, string.Empty))
        {
            if (path == "version" || path.StartsWith("_")) { continue; }

            bool used = paths.Contains(path);
            Console.WriteLine((used ? "  PASS  " : "  FAIL  ") + path +
                              (used ? " (declared and read)" : "   <- declared in defaults but nothing reads it"));
            if (!used) { failures++; }
        }

        return failures;
    }

    private static IEnumerable<string> Leaves(JsonValue node, string prefix)
    {
        foreach (var pair in node.Members)
        {
            string path = prefix.Length == 0 ? pair.Key : prefix + "." + pair.Key;

            if (pair.Value.IsObject)
            {
                foreach (string nested in Leaves(pair.Value, path)) { yield return nested; }
            }
            else
            {
                yield return path;
            }
        }
    }

    private static JsonValue Walk(JsonValue root, string path)
    {
        JsonValue current = root;
        foreach (string segment in path.Split('.'))
        {
            current = current[segment];
            if (current.IsNull) { return JsonValue.Null; }
        }
        return current;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }
        if (dir == null)
        {
            throw new DirectoryNotFoundException("Could not locate the repository root from " + AppContext.BaseDirectory);
        }
        return dir.FullName;
    }
}
