using System;
using System.Windows.Forms;
using GTA;
using TonightsTheNight.Config;
using TonightsTheNight.Factions;
using TonightsTheNight.Menu;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// SHVDN entry point. Deliberately thin: wiring, keybinds, and the guarantee that nothing
    /// thrown in here reaches the game.
    /// </summary>
    public sealed class RiotScript : Script
    {
        private readonly ConfigStore _config = new ConfigStore();
        private readonly ModeLibrary _modes = new ModeLibrary();

        private Director _director;
        private RiotMenu _menu;
        private DebugOverlay _overlay;

        private Keys _menuKey = Keys.F6;
        private Keys _reloadKey = Keys.F5;
        private int _nextWatchCheck;
        private bool _initialised;
        private bool _greeted;

        public RiotScript()
        {
            Tick += OnTick;
            KeyDown += OnKeyDown;
            Aborted += OnAborted;

            try
            {
                Initialise();
                _initialised = true;
            }
            catch (Exception ex)
            {
                // A script that throws in its constructor is silently dead, which looks to the
                // player exactly like "the mod does not work". Make sure it says why.
                Log.Error("Startup failed", ex);
                GTA.UI.Notification.Show("~r~Tonight's The Night failed to start.~s~ See scripts/TonightsTheNight.log");
            }
        }

        private void Initialise()
        {
            Log.StartSession(DefaultConfig.Version);

            _config.LoadAll();
            ApplyLogLevel();
            ReadKeybinds();

            _modes.Load();

            _director = new Director(_config);
            _overlay = new DebugOverlay(_config);
            _menu = new RiotMenu(_config, _director, _modes, ReloadEverything);

            // Every frame. LemonUI needs it for responsive input, the overlay needs it or it
            // flickers, and the per-frame density natives need it or the crowd pulses. The
            // expensive work is throttled inside the Director instead.
            Interval = 0;

            Log.Info("Ready. Menu key: " + _menuKey + ", reload key: " + _reloadKey + ".");
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!_initialised) { return; }

            try
            {
                Greet();
                _menu.Process();
                _director.Tick();
                _overlay.Draw(_director, _modes.Modes.Count);
                CheckWatchedFiles();
            }
            catch (Exception ex)
            {
                Log.Error("Tick failed", ex);
            }
        }

        /// <summary>
        /// The startup notification is deferred out of the constructor. Natives called before
        /// the game is fully up can fail silently, and a mod that loads without saying so is
        /// indistinguishable from one that did not load at all.
        /// </summary>
        private void Greet()
        {
            if (_greeted) { return; }
            _greeted = true;

            GTA.UI.Notification.Show("~g~Tonight's The Night~s~ v" + DefaultConfig.Version + " loaded. Press ~b~" + _menuKey + "~s~.");

            if (_config.LoadError != null)
            {
                GTA.UI.Notification.Show("~o~Config problem:~s~ " + _config.LoadError);
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!_initialised) { return; }

            try
            {
                if (e.KeyCode == _menuKey)
                {
                    _menu.Toggle();
                }
                else if (e.KeyCode == _reloadKey && _config.GetBool("features.hotReload.enabled", true))
                {
                    ReloadEverything();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Key handling failed", ex);
            }
        }

        /// <summary>
        /// Aborted fires on script reload and on game shutdown. Skipping cleanup here is how a
        /// riot mod leaves peds hostile and blips stuck on the map after it is gone.
        /// </summary>
        private void OnAborted(object sender, EventArgs e)
        {
            try
            {
                Log.Info("Aborting - cleaning up.");
                if (_director != null) { _director.EmergencyCleanup(); }
            }
            catch (Exception ex)
            {
                Log.Error("Abort cleanup failed", ex);
            }
        }

        /// <summary>
        /// Re-reads every config and mode file. The running mode keeps going unless the config
        /// says otherwise, because losing a riot you spent two minutes building to change one
        /// number would defeat the point.
        /// </summary>
        private void ReloadEverything()
        {
            try
            {
                bool restart = _config.GetBool("features.hotReload.restartModeOnReload", false);
                string runningId = _director.IsRunning ? _director.ActiveMode.Id : null;

                if (runningId != null && restart) { _director.Stop(); }

                _config.LoadAll();
                ApplyLogLevel();
                ReadKeybinds();
                Compatibility.Reset();
                _modes.Load();
                _menu.Rebuild();

                if (runningId != null && restart)
                {
                    RiotMode again = _modes.Modes.Find(m => m.Id == runningId);
                    if (again != null) { _director.Start(again); }
                }

                Log.Info("Reloaded config and modes.");
                GTA.UI.Notification.Show("~g~Reloaded~s~ config and " + _modes.Modes.Count + " mode(s).");
            }
            catch (Exception ex)
            {
                Log.Error("Reload failed", ex);
                GTA.UI.Notification.Show("~r~Reload failed.~s~ See the log.");
            }
        }

        private void CheckWatchedFiles()
        {
            if (!_config.GetBool("features.hotReload.enabled", true)) { return; }
            if (!_config.GetBool("features.hotReload.watchFiles", true)) { return; }

            if (Game.GameTime < _nextWatchCheck) { return; }
            _nextWatchCheck = Game.GameTime + _config.GetInt("features.hotReload.watchIntervalMs", 2000);

            if (_config.FilesChangedOnDisk())
            {
                Log.Info("Config changed on disk - reloading.");
                ReloadEverything();
            }
        }

        private void ApplyLogLevel()
        {
            LogLevel level;
            if (Enum.TryParse(_config.GetString("logging.level", "Info"), true, out level))
            {
                Log.Threshold = level;
            }
        }

        private void ReadKeybinds()
        {
            _menuKey = ParseKey(_config.GetString("menu.key", "F6"), Keys.F6);
            _reloadKey = ParseKey(_config.GetString("menu.reloadKey", "F5"), Keys.F5);
        }

        private static Keys ParseKey(string text, Keys fallback)
        {
            Keys key;
            if (Enum.TryParse(text, true, out key)) { return key; }

            Log.Warn("Unknown key '" + text + "', using " + fallback + ".");
            return fallback;
        }
    }
}
