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

        private const int FlushIntervalMs = 1000;

        private static readonly StringBuilder _pending = new StringBuilder();
        private static int _lastFlush;

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
                _pending.Length = 0;
                _lastFlush = Environment.TickCount;
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
            Info("Path resolution:");
            foreach (string line in Paths.Diagnostics().Split('\n'))
            {
                Info("  " + line.TrimEnd('\r'));
            }
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

        /// <summary>
        /// Buffered, and flushed on an interval rather than per line.
        ///
        /// Every message used to open, append to and close the file. That is synchronous disk
        /// I/O on the game thread, and at the Debug level testers are asked to enable it fires
        /// several times a frame - so the one session anybody is asked to capture is the one
        /// that stutters, and the frame times recorded in the log are polluted by the logging.
        /// </summary>
        private static void Write(LogLevel level, string message)
        {
            if (level < Threshold || _failed)
            {
                return;
            }

            lock (Gate)
            {
                _pending.Append(DateTime.Now.ToString("HH:mm:ss.fff"))
                        .Append(" [").Append(level.ToString().ToUpperInvariant()).Append("] ")
                        .AppendLine(message);

                // Warnings and errors reach the disk immediately. The run that produces one is
                // quite often the run that ends before a timer would have fired, and a hard
                // crash is precisely when the log has to already be there.
                bool urgent = level >= LogLevel.Warn || _pending.Length > 16384;

                if (urgent || Environment.TickCount - _lastFlush >= FlushIntervalMs)
                {
                    FlushLocked();
                }
            }
        }

        /// <summary>
        /// Writes anything buffered. Called on shutdown, so a crash-adjacent session still has
        /// its last lines on disk.
        /// </summary>
        public static void Flush()
        {
            lock (Gate) { FlushLocked(); }
        }

        private static void FlushLocked()
        {
            _lastFlush = Environment.TickCount;

            if (_pending.Length == 0 || _failed) { return; }

            try
            {
                File.AppendAllText(Path, _pending.ToString(), Encoding.UTF8);
                _pending.Length = 0;
            }
            catch (Exception)
            {
                // A log that throws is worse than no log. Give up quietly for the session.
                _failed = true;
                _pending.Length = 0;
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
        /// <summary>
        /// The game's scripts folder.
        ///
        /// Two wrong answers have already shipped here, so the reasoning is written down.
        ///
        /// Under SHVDN, <c>AppDomain.BaseDirectory</c> IS the scripts folder — appending
        /// "scripts" to it produced scripts/scripts (v0.1.0). The assembly's own Location is
        /// worse: the script domain shadow-copies assemblies, so Location points at a temp
        /// cache and config lands somewhere nobody will ever find it (v0.1.1).
        ///
        /// So: trust BaseDirectory, and only append "scripts" when it is clearly the game root
        /// rather than the scripts folder. Every candidate is recorded in
        /// <see cref="Diagnostics"/> and logged at startup, so a third wrong answer is at
        /// least visible rather than silent.
        /// </summary>
        public static string ScriptsDir
        {
            get
            {
                if (_scriptsDir != null) { return _scriptsDir; }

                string root = (AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory())
                    .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);

                if (IsScriptsFolder(root))
                {
                    _scriptsDir = root;
                }
                else if (Directory.Exists(System.IO.Path.Combine(root, "scripts")))
                {
                    _scriptsDir = System.IO.Path.Combine(root, "scripts");
                }
                else
                {
                    _scriptsDir = root;
                }

                return _scriptsDir;
            }
        }

        private static bool IsScriptsFolder(string path)
        {
            try
            {
                return string.Equals(new DirectoryInfo(path).Name, "scripts", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Every path candidate and where we landed, written to the log at startup.
        ///
        /// It is here for bug reports: "the mod created no config folder" is unanswerable
        /// without knowing which directory it decided it was in, and a user should never have
        /// to hunt the disk to find that out for us.
        /// </summary>
        public static string Diagnostics()
        {
            string assembly;
            try
            {
                string location = System.Reflection.Assembly.GetExecutingAssembly().Location;
                assembly = string.IsNullOrEmpty(location) ? "(none - loaded from memory)" : location;
            }
            catch (Exception ex)
            {
                assembly = "(unavailable: " + ex.GetType().Name + ")";
            }

            return "AppDomain.BaseDirectory : " + (AppDomain.CurrentDomain.BaseDirectory ?? "(null)") + Environment.NewLine +
                   "Assembly.Location       : " + assembly + Environment.NewLine +
                   "Current directory       : " + Directory.GetCurrentDirectory() + Environment.NewLine +
                   "-> scripts folder       : " + ScriptsDir + Environment.NewLine +
                   "-> config folder        : " + ConfigDir + Environment.NewLine +
                   "-> log file             : " + System.IO.Path.Combine(ScriptsDir, "TonightsTheNight.log");
        }

        /// <summary>scripts/TonightsTheNight/ — all config lives here.</summary>
        public static string ConfigDir { get { return System.IO.Path.Combine(ScriptsDir, "TonightsTheNight"); } }

        public static string ModesDir { get { return System.IO.Path.Combine(ConfigDir, "modes"); } }

        public static string ProfilesDir { get { return System.IO.Path.Combine(ConfigDir, "profiles"); } }

        public static string DefaultsFile { get { return System.IO.Path.Combine(ConfigDir, "defaults.json"); } }

        public static string UserFile { get { return System.IO.Path.Combine(ConfigDir, "user.json"); } }
    }
}
