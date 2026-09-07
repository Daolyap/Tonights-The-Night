using System;
using System.IO;
using System.Text.RegularExpressions;
using TonightsTheNight.Config;
using TonightsTheNight.Util;

/// <summary>
/// The checks that only matter when someone other than the author installs this.
///
/// Each one here has already gone wrong once, or is the kind of thing that goes wrong silently:
/// a version number in two places drifting apart, a debug aid left switched on, a development
/// default shipping as the release default. None of them are visible in a build, and all of them
/// are embarrassing in public.
/// </summary>
public static class ReleaseReadinessTests
{
    private static int _failures;

    private static void Check(string label, bool ok, string detail = "")
    {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + label + (!ok && detail.Length > 0 ? "   <- " + detail : ""));
        if (!ok) { _failures++; }
    }

    public static int Run()
    {
        _failures = 0;
        string root = RepoRoot();

        CheckVersionsAgree(root);
        CheckShippedDefaults();
        CheckNoDevelopmentAids(root);
        CheckDistributionFiles(root);

        Console.WriteLine();
        return _failures;
    }

    /// <summary>
    /// The version a user sees in the loading notification and the version in the DLL's file
    /// properties come from different files. They had already drifted two releases apart.
    /// </summary>
    private static void CheckVersionsAgree(string root)
    {
        string csproj = File.ReadAllText(Path.Combine(root, "src", "TonightsTheNight", "TonightsTheNight.csproj"));
        Match version = Regex.Match(csproj, @"<Version>([^<]+)</Version>");

        Check("the project declares a version", version.Success);
        if (!version.Success) { return; }

        Check("assembly version matches DefaultConfig.Version",
              version.Groups[1].Value.Trim() == DefaultConfig.Version,
              "csproj " + version.Groups[1].Value.Trim() + " vs code " + DefaultConfig.Version);

        string changelog = File.ReadAllText(Path.Combine(root, "CHANGELOG.md"));
        Check("the changelog has an entry for this version",
              changelog.Contains("## v" + DefaultConfig.Version),
              "no '## v" + DefaultConfig.Version + "' heading");

        // However it is phrased, the number a visitor reads has to be the number they get.
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        Check("the readme names this version", readme.Contains(DefaultConfig.Version),
              "README does not mention " + DefaultConfig.Version);
    }

    /// <summary>
    /// Settings whose shipped value is a decision rather than a preference. A debug overlay left
    /// on, or verbose logging left enabled, is what a development build looks like.
    /// </summary>
    private static void CheckShippedDefaults()
    {
        JsonValue defaults = DefaultConfig.Build();

        Check("the debug overlay ships off", !defaults["features"]["debugOverlay"]["enabled"].AsBool(true));
        Check("logging ships at Info, not Debug", defaults["logging"]["level"].AsString("") == "Info");
        Check("the wanted system is left alone by default",
              !defaults["compatibility"]["manageWantedSystem"].AsBool(true));
        Check("emergency services are protected by default",
              defaults["compatibility"]["protectEmergencyServices"].AsBool(false));
        Check("mission peds are protected by default",
              defaults["compatibility"]["protectMissionPeds"].AsBool(false));
        Check("the clock is not touched by default", !defaults["features"]["ambience"]["setTime"].AsBool(true));
        Check("the purge does not set the clock by default", !defaults["features"]["purge"]["setClock"].AsBool(true));
        Check("weather and grading ship off", !defaults["features"]["ambience"]["enabled"].AsBool(true));
        Check("the player stance defers to the mode", defaults["player"]["stance"].AsString("") == "mode");
        Check("no weapon preset is forced", defaults["weapons"]["preset"].AsString("") == "mode");
    }

    /// <summary>
    /// Aids that exist to debug the mod's own installation rather than to play with. A menu item
    /// printing filesystem paths on screen is the clearest example: useful during testing,
    /// meaningless to somebody who just wants a riot.
    /// </summary>
    private static void CheckNoDevelopmentAids(string root)
    {
        string menu = File.ReadAllText(Path.Combine(root, "src", "TonightsTheNight", "Menu", "RiotMenu.cs"));

        Check("no file-locator item in the menu", !menu.Contains("Show File Locations"));
        Check("the menu prints no filesystem paths on screen", !menu.Contains("Paths.Diagnostics"));

        string script = File.ReadAllText(Path.Combine(root, "src", "TonightsTheNight", "Core", "RiotScript.cs"));
        Check("startup does not announce a config path", !script.Contains("Notification.Show(\"~b~Config:"));

        int scanned = 0;

        foreach (string file in Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            // Generated assembly attributes are build output, not source.
            if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) ||
                file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            string text = File.ReadAllText(file);
            string name = Path.GetFileName(file);
            scanned++;

            // Reported per file only when one fails: a clean pass is one line, not eighty.
            if (text.Contains("TODO") || text.Contains("FIXME") || text.Contains("HACK:"))
            {
                Check(name + ": no unfinished markers", false, "left a TODO, FIXME or HACK in shipped source");
            }

            if (text.Contains("Console.Write"))
            {
                Check(name + ": no console writes", false, "a script has no console to write to");
            }
        }

        Check("no unfinished markers or console writes in " + scanned + " source files", true);
    }

    /// <summary>What a stranger downloading this needs to find in it.</summary>
    private static void CheckDistributionFiles(string root)
    {
        foreach (string required in new[] { "README.md", "INSTALL.md", "CHANGELOG.md", "LICENSE" })
        {
            Check("ships a " + required, File.Exists(Path.Combine(root, required)));
        }

        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        Check("the readme states a licence", !readme.Contains("Not yet chosen"));

        string install = File.ReadAllText(Path.Combine(root, "INSTALL.md"));
        Check("install names ScriptHookV", install.Contains("ScriptHookV"));
        Check("install names LemonUI", install.Contains("LemonUI"));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src"))) { dir = dir.Parent; }
        if (dir == null) { throw new DirectoryNotFoundException("Could not locate the repository root."); }
        return dir.FullName;
    }
}
