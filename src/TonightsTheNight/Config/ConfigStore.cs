using System;
using System.Collections.Generic;
using System.IO;
using TonightsTheNight.Util;

namespace TonightsTheNight.Config
{
    /// <summary>
    /// Four-layer settings resolution. Each layer overrides the ones below it:
    ///
    ///   4. Live    - menu changes, this session only unless saved to a profile
    ///   3. Mode    - per-mode overrides, so Purge can force night without touching your globals
    ///   2. User    - user.json, yours, survives every mod update untouched
    ///   1. Defaults- defaults.json, ships with the mod, rewritten on version change
    ///
    /// Values are addressed by dotted path ("fires.maxSimultaneous"). A missing key falls
    /// through to the next layer and finally to the caller's fallback, so a partial or
    /// out-of-date config file is never fatal.
    /// </summary>
    public sealed class ConfigStore
    {
        private JsonValue _defaults = JsonValue.NewObject();
        private JsonValue _user = JsonValue.NewObject();
        private JsonValue _mode = JsonValue.NewObject();
        private readonly Dictionary<string, JsonValue> _live = new Dictionary<string, JsonValue>(StringComparer.OrdinalIgnoreCase);

        private DateTime _userStamp = DateTime.MinValue;
        private DateTime _defaultsStamp = DateTime.MinValue;

        public string LoadError { get; private set; }

        /// <summary>Raised after any reload so subsystems can re-read what they cache.</summary>
        public event EventHandler Reloaded;

        public void LoadAll()
        {
            LoadError = null;

            EnsureDirectories();
            EnsureDefaultsFile();

            _defaults = ReadFile(Paths.DefaultsFile, "defaults");
            _user = ReadFile(Paths.UserFile, "user");

            _defaultsStamp = StampOf(Paths.DefaultsFile);
            _userStamp = StampOf(Paths.UserFile);

            ReportUnknownKeys(_user, _defaults, string.Empty);

            EventHandler handler = Reloaded;
            if (handler != null) { handler(this, EventArgs.Empty); }
        }

        /// <summary>
        /// Returns true when a watched file changed on disk since the last load. Cheap enough
        /// to poll, but the menu keybind is the reliable path.
        /// </summary>
        public bool FilesChangedOnDisk()
        {
            return StampOf(Paths.UserFile) != _userStamp || StampOf(Paths.DefaultsFile) != _defaultsStamp;
        }

        public void SetModeOverrides(JsonValue overrides)
        {
            _mode = overrides != null && overrides.IsObject ? overrides : JsonValue.NewObject();
        }

        public void ClearModeOverrides()
        {
            _mode = JsonValue.NewObject();
        }

        /// <summary>Menu-driven override for this session. Survives a hot-reload on purpose.</summary>
        public void SetLive(string path, JsonValue value)
        {
            _live[path] = value ?? JsonValue.Null;
            Log.Debug("Live override: " + path + " = " + (value == null ? "null" : value.ToJson(false)));
        }

        public void ClearLive(string path) { _live.Remove(path); }

        public void ClearAllLive() { _live.Clear(); }

        /// <summary>
        /// The live overrides as a flat path -> value object. This is what a profile is: not a
        /// copy of every setting, just the ones you actually changed, so loading a profile
        /// layers your choices over whatever the defaults happen to be in a later version.
        /// </summary>
        public JsonValue ExportLive()
        {
            JsonValue result = JsonValue.NewObject();
            foreach (var pair in _live) { result.Set(pair.Key, pair.Value); }
            return result;
        }

        public void ImportLive(JsonValue node)
        {
            if (node == null || !node.IsObject) { return; }

            foreach (var pair in node.Members) { _live[pair.Key] = pair.Value; }
            Log.Info("Applied " + node.Count + " setting(s) from a profile.");

            EventHandler handler = Reloaded;
            if (handler != null) { handler(this, EventArgs.Empty); }
        }

        public JsonValue Resolve(string path)
        {
            JsonValue live;
            if (_live.TryGetValue(path, out live)) { return live; }

            JsonValue value = Walk(_mode, path);
            if (!value.IsNull) { return value; }

            value = Walk(_user, path);
            if (!value.IsNull) { return value; }

            return Walk(_defaults, path);
        }

        public bool GetBool(string path, bool fallback)
        {
            JsonValue v = Resolve(path);
            return v.IsNull ? fallback : v.AsBool(fallback);
        }

        public int GetInt(string path, int fallback)
        {
            JsonValue v = Resolve(path);
            return v.IsNull ? fallback : v.AsInt(fallback);
        }

        public float GetFloat(string path, float fallback)
        {
            JsonValue v = Resolve(path);
            return v.IsNull ? fallback : v.AsFloat(fallback);
        }

        public string GetString(string path, string fallback)
        {
            JsonValue v = Resolve(path);
            return v.IsNull ? fallback : v.AsString(fallback);
        }

        public List<string> GetStringList(string path)
        {
            return Resolve(path).AsStringList();
        }

        private static JsonValue Walk(JsonValue root, string path)
        {
            if (root == null || !root.IsObject) { return JsonValue.Null; }

            JsonValue current = root;
            int start = 0;
            while (start <= path.Length)
            {
                int dot = path.IndexOf('.', start);
                string segment = dot < 0 ? path.Substring(start) : path.Substring(start, dot - start);

                if (segment.Length == 0) { return JsonValue.Null; }
                current = current[segment];
                if (current.IsNull) { return JsonValue.Null; }

                if (dot < 0) { return current; }
                start = dot + 1;
            }
            return JsonValue.Null;
        }

        private JsonValue ReadFile(string file, string label)
        {
            if (!File.Exists(file))
            {
                Log.Info("No " + label + " config at " + file + " (using lower layers).");
                return JsonValue.NewObject();
            }

            try
            {
                string text = File.ReadAllText(file);
                JsonValue value;
                string error;
                if (!JsonValue.TryParse(text, out value, out error))
                {
                    // A broken file must not take the mod down; fall through to lower layers.
                    string message = label + " config failed to parse (" + error + "). Ignoring it.";
                    Log.Error(message);
                    LoadError = message;
                    return JsonValue.NewObject();
                }

                if (!value.IsObject)
                {
                    Log.Warn(label + " config is not a JSON object. Ignoring it.");
                    return JsonValue.NewObject();
                }

                Log.Info("Loaded " + label + " config: " + value.Count + " top-level section(s).");
                return value;
            }
            catch (Exception ex)
            {
                Log.Error("Could not read " + label + " config", ex);
                LoadError = label + " config could not be read: " + ex.Message;
                return JsonValue.NewObject();
            }
        }

        /// <summary>
        /// Logs keys the shipped defaults don't know about rather than rejecting them, so a
        /// config written against a newer build never hard-fails an older DLL.
        /// </summary>
        private static void ReportUnknownKeys(JsonValue candidate, JsonValue reference, string prefix)
        {
            if (!candidate.IsObject || !reference.IsObject) { return; }

            foreach (var pair in candidate.Members)
            {
                string path = prefix.Length == 0 ? pair.Key : prefix + "." + pair.Key;
                if (!reference.Has(pair.Key))
                {
                    Log.Warn("Unknown config key ignored: " + path);
                    continue;
                }
                ReportUnknownKeys(pair.Value, reference[pair.Key], path);
            }
        }

        private static DateTime StampOf(string file)
        {
            try
            {
                return File.Exists(file) ? File.GetLastWriteTimeUtc(file) : DateTime.MinValue;
            }
            catch (Exception)
            {
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// Creates the config folders and proves one is writable.
        ///
        /// Reported rather than swallowed: a mod that silently writes nowhere is indist-
        /// inguishable from a mod that is broken, and that has now cost two test rounds.
        /// </summary>
        private void EnsureDirectories()
        {
            foreach (string dir in new[] { Paths.ConfigDir, Paths.ModesDir, Paths.ProfilesDir })
            {
                try
                {
                    if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); }
                }
                catch (Exception ex)
                {
                    Log.Error("Could not create " + dir, ex);
                    LoadError = "cannot create " + dir + " (" + ex.GetType().Name + ")";
                    return;
                }
            }

            try
            {
                string probe = Path.Combine(Paths.ConfigDir, ".writetest");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
            }
            catch (Exception ex)
            {
                Log.Error("Config folder is not writable: " + Paths.ConfigDir, ex);
                LoadError = Paths.ConfigDir + " is not writable (" + ex.GetType().Name + ")";
            }
        }

        /// <summary>
        /// defaults.json is ours to own, so it gets rewritten whenever the shipped version
        /// changes. user.json is never touched — that is the whole point of the split.
        /// </summary>
        private void EnsureDefaultsFile()
        {
            try
            {
                string expected = DefaultConfig.Build().ToJson();
                if (File.Exists(Paths.DefaultsFile) && File.ReadAllText(Paths.DefaultsFile) == expected)
                {
                    return;
                }
                File.WriteAllText(Paths.DefaultsFile, expected);
                Log.Info("Wrote shipped defaults to " + Paths.DefaultsFile);
            }
            catch (Exception ex)
            {
                Log.Error("Could not write defaults.json", ex);
            }
        }
    }
}
