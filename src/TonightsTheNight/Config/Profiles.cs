using System;
using System.IO;
using TonightsTheNight.Util;

namespace TonightsTheNight.Config
{
    /// <summary>
    /// Named setups you can save and come back to.
    ///
    /// A profile stores only the settings you actually changed, not a snapshot of everything,
    /// so a profile saved today still makes sense after an update changes a default. Slots
    /// rather than typed names because there is no text entry in the menu, and asking someone to
    /// alt-tab to name a file defeats the point.
    /// </summary>
    public static class Profiles
    {
        public const int Slots = 5;

        public static string PathFor(int slot)
        {
            return Path.Combine(Paths.ProfilesDir, "slot" + slot + ".json");
        }

        public static bool Exists(int slot)
        {
            try { return File.Exists(PathFor(slot)); }
            catch (Exception) { return false; }
        }

        public static bool Save(int slot, ConfigStore config)
        {
            try
            {
                JsonValue live = config.ExportLive();
                if (live.Count == 0)
                {
                    Log.Info("Nothing to save to slot " + slot + " - no settings changed this session.");
                    return false;
                }

                Directory.CreateDirectory(Paths.ProfilesDir);
                File.WriteAllText(PathFor(slot), live.ToJson());
                Log.Info("Saved " + live.Count + " setting(s) to slot " + slot + ".");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Could not save profile slot " + slot, ex);
                return false;
            }
        }

        public static bool Load(int slot, ConfigStore config)
        {
            try
            {
                string file = PathFor(slot);
                if (!File.Exists(file))
                {
                    Log.Info("Profile slot " + slot + " is empty.");
                    return false;
                }

                JsonValue parsed;
                string error;
                if (!JsonValue.TryParse(File.ReadAllText(file), out parsed, out error))
                {
                    Log.Error("Profile slot " + slot + " failed to parse (" + error + ").");
                    return false;
                }

                config.ImportLive(parsed);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Could not load profile slot " + slot, ex);
                return false;
            }
        }

        public static bool Delete(int slot)
        {
            try
            {
                string file = PathFor(slot);
                if (!File.Exists(file)) { return false; }

                File.Delete(file);
                Log.Info("Cleared profile slot " + slot + ".");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Could not clear profile slot " + slot, ex);
                return false;
            }
        }

        /// <summary>Applies the configured startup profile, if any.</summary>
        public static void Autoload(ConfigStore config)
        {
            if (!config.GetBool("features.profiles.enabled", true)) { return; }

            string name = config.GetString("features.profiles.autoload", string.Empty);
            if (string.IsNullOrEmpty(name)) { return; }

            int slot;
            if (int.TryParse(name.Trim(), out slot) && slot >= 1 && slot <= Slots)
            {
                if (Load(slot, config)) { Log.Info("Autoloaded profile slot " + slot + "."); }
                return;
            }

            Log.Warn("features.profiles.autoload should be a slot number 1-" + Slots + ", got '" + name + "'.");
        }
    }
}
