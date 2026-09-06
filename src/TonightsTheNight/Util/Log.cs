using System;
using System.IO;
using System.Text;

namespace TonightsTheNight.Util
{
    public enum LogLevel { Debug = 0, Info = 1, Warn = 2, Error = 3 }

    /// <summary>
    /// The only telemetry we get. Everything interesting goes here: this file plus a sentence
    /// from the tester has to be enough to diagnose a problem, because nobody developing this
    /// mod can run the game.
    /// </summary>
    public static class Log
    {
        private static readonly object Gate = new object();
        private static string _path;
        private static bool _failed;

        public static LogLevel Threshold = LogLevel.Info;

        public static string Path
        {
            get
            {
                if (_path == null)
                {
                    _path = System.IO.Path.Combine(Paths.ScriptsDir, "TonightsTheNight.log");
                }
                return _path;
            }
        }

        /// <summary>Truncates the log and writes the banner. Called once per game session.</summary>
        public static void StartSession(string version)
        {
            lock (Gate)
            {
                _failed = false;
                try
                {
                    string dir = System.IO.Path.GetDirectoryName(Path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) { Directory.CreateDirectory(dir); }

                    File.WriteAllText(Path, string.Empty, Encoding.UTF8);
                }
                catch (Exception)
                {
                    _failed = true;
                    return;
                }
            }

            Info("=== Tonight's The Night " + version + " ===");
            Info("Session started " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Info("Scripts directory: " + Paths.ScriptsDir);
            Info("Config directory: " + Paths.ConfigDir);
            Info("Log file: " + Path);
        }

        public static void Debug(string message) { Write(LogLevel.Debug, message); }
        public static void Info(string message) { Write(LogLevel.Info, message); }
        public static void Warn(string message) { Write(LogLevel.Warn, message); }
        public static void Error(string message) { Write(LogLevel.Error, message); }

        public static void Error(string message, Exception ex)
        {
            Write(LogLevel.Error, message + " -> " + ex.GetType().Name + ": " + ex.Message);
            if (ex.StackTrace != null)
            {
                Write(LogLevel.Error, ex.StackTrace);
            }
            if (ex.InnerException != null)
            {
                Error("  inner", ex.InnerException);
            }
        }

        private static void Write(LogLevel level, string message)
        {
            if (level < Threshold || _failed)
            {
                return;
            }

            lock (Gate)
            {
                try
                {
                    File.AppendAllText(
                        Path,
                        DateTime.Now.ToString("HH:mm:ss.fff") + " [" + level.ToString().ToUpperInvariant() + "] " + message + Environment.NewLine,
                        Encoding.UTF8);
                }
                catch (Exception)
                {
                    // A log that throws is worse than no log. Give up quietly for the session.
                    _failed = true;
                }
            }
        }
    }

    public static class Paths
    {
        private static string _scriptsDir;

        /// <summary>
        /// The folder the mod DLL lives in, which is the game's scripts folder.
        ///
        /// Do not assume AppDomain.BaseDirectory is the game root: under SHVDN it is the
        /// scripts folder itself, so appending "scripts" to it produces scripts/scripts. The
        /// assembly's own location is the reliable answer; the rest is fallback for hosts that
        /// load the assembly from memory and leave Location empty.
        /// </summary>
        public static string ScriptsDir
        {
            get
            {
                if (_scriptsDir != null) { return _scriptsDir; }

                try
                {
                    string location = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    if (!string.IsNullOrEmpty(location))
                    {
                        _scriptsDir = System.IO.Path.GetDirectoryName(location);
                        if (!string.IsNullOrEmpty(_scriptsDir)) { return _scriptsDir; }
                    }
                }
                catch (Exception)
                {
                    // Fall through to the directory-shape guess below.
                }

                string base_ = (AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory())
                    .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);

                if (string.Equals(new DirectoryInfo(base_).Name, "scripts", StringComparison.OrdinalIgnoreCase))
                {
                    _scriptsDir = base_;
                }
                else if (Directory.Exists(System.IO.Path.Combine(base_, "scripts")))
                {
                    _scriptsDir = System.IO.Path.Combine(base_, "scripts");
                }
                else
                {
                    _scriptsDir = base_;
                }

                return _scriptsDir;
            }
        }

        /// <summary>scripts/TonightsTheNight/ — all config lives here.</summary>
        public static string ConfigDir { get { return System.IO.Path.Combine(ScriptsDir, "TonightsTheNight"); } }

        public static string ModesDir { get { return System.IO.Path.Combine(ConfigDir, "modes"); } }

        public static string ProfilesDir { get { return System.IO.Path.Combine(ConfigDir, "profiles"); } }

        public static string DefaultsFile { get { return System.IO.Path.Combine(ConfigDir, "defaults.json"); } }

        public static string UserFile { get { return System.IO.Path.Combine(ConfigDir, "user.json"); } }
    }
}
