using System;
using GTA;
using GTA.Native;
using TonightsTheNight.Config;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// The countdown, the announcement, and the moment it ends.
    ///
    /// Almost no mechanics — a timer and some text — but it is what turns "everyone is fighting"
    /// into an event with a shape: it starts at a time, it runs for a while, and at dawn it
    /// stops and everyone goes home.
    /// </summary>
    public sealed class PurgeClock
    {
        private readonly ConfigStore _config;
        private bool _armed;
        private int _startedAt;
        private int _totalSeconds;
        private int _lastAnnouncedSecond = -1;
        private int _nextLogAt;

        /// <summary>The wanted ceiling before we suppressed it, so a 6-star pack gets its 6 back.</summary>
        private int _previousMaxWanted = -1;

        public bool Active { get; private set; }

        /// <summary>
        /// True for the last stretch of the event, after the clock has run out and before the
        /// mode stops.
        ///
        /// The purge used to end on a single frame: the timer hit zero, the mode stopped, and
        /// everything that had been happening simply was not any more. Whether that read as
        /// "it ended" or as "it never ended" depended entirely on whether you happened to be
        /// looking at a fight at the time. So there is now a window with a shape to it — the
        /// sirens go, nothing new arrives, and what is already out there is talked down — and
        /// the caller can see it in <see cref="WindingDown"/> and stop feeding the riot.
        /// </summary>
        public bool WindingDown { get; private set; }

        private int _windDownEndsAt;
        private bool _warned;

        /// <summary>Counts down. It used to hold the starting total forever, which made every
        /// display of it - overlay, log, menu - look like a stopped clock.</summary>
        public int SecondsRemaining { get; private set; }

        public PurgeClock(ConfigStore config)
        {
            _config = config;
        }

        public void Begin(JsonValue node)
        {
            _armed = node.IsObject && node["enabled"].AsBool(true) &&
                     _config.GetBool("features.purge.enabled", true);

            if (!_armed) { Active = false; return; }

            int hour = node["startHour"].AsInt(_config.GetInt("features.purge.startHour", 22));
            int minute = node["startMinute"].AsInt(0);

            if (_config.GetBool("features.purge.setClock", false))
            {
                Function.Call(Hash.SET_CLOCK_TIME, hour, minute, 0);
            }

            _startedAt = Game.GameTime;
            Active = true;
            WindingDown = false;
            _warned = false;
            _windDownEndsAt = 0;
            _lastAnnouncedSecond = -1;
            _nextLogAt = 0;

            int minutes = node["durationMinutes"].AsInt(_config.GetInt("features.purge.durationMinutes", 12));
            _totalSeconds = Math.Max(1, minutes) * 60;
            SecondsRemaining = _totalSeconds;

            SuppressWantedLevel();

            if (node["announce"].AsBool(true))
            {
                GTA.UI.Screen.ShowSubtitle(
                    "~r~THIS IS NOT A TEST.~s~~n~Commencing at the siren, all crime is legal for the next " +
                    minutes + " minutes.~n~~r~May God be with you all.", 9000);
            }

            Log.Info("Purge armed: " + minutes + " minute(s) from " + hour.ToString("00") + ":" + minute.ToString("00") + ".");
        }

        /// <summary>Returns true on the tick the purge is fully over, so the caller stops the mode.</summary>
        public bool Update()
        {
            if (!Active) { return false; }

            // All crime is legal, so the game's own police should not be interested in you.
            // Re-applied rather than set once, because a wanted-system overhaul runs its own
            // script and will happily hand you a star back.
            HoldWantedLevelDown();

            if (WindingDown) { return UpdateWindDown(); }

            int elapsed = (Game.GameTime - _startedAt) / 1000;
            SecondsRemaining = Math.Max(0, _totalSeconds - elapsed);

            DrawClock(SecondsRemaining, SecondsRemaining <= 60 ? "~r~" : "~s~");

            if (SecondsRemaining != _lastAnnouncedSecond)
            {
                _lastAnnouncedSecond = SecondsRemaining;

                // Only the last ten seconds get a countdown; anything more is nagging.
                if (SecondsRemaining > 0 && SecondsRemaining <= 10)
                {
                    GTA.UI.Screen.ShowSubtitle("~r~" + SecondsRemaining + "~s~", 900);
                }
            }

            // One warning before the end, so the last minute is something you can act on rather
            // than something you find out about afterwards.
            int warnAt = Math.Max(0, _config.GetInt("features.purge.finalWarningSeconds", 60));
            if (!_warned && warnAt > 0 && SecondsRemaining <= warnAt && SecondsRemaining > 0)
            {
                _warned = true;
                GTA.UI.Notification.Show("~o~The purge ends in " + Hud.Clock(SecondsRemaining) + ".");
                GTA.UI.Screen.ShowSubtitle(
                    "~o~The commencement of the annual purge will conclude in " + Hud.Clock(SecondsRemaining) +
                    ".~s~~n~Weapons are to be surrendered at the siren.", 6000);
            }

            // A minute-by-minute line, so "it never ended" is answerable from the log instead of
            // from memory.
            if (Game.GameTime >= _nextLogAt)
            {
                _nextLogAt = Game.GameTime + 60000;
                Log.Info("Purge: " + (SecondsRemaining / 60) + "m " + (SecondsRemaining % 60) + "s remaining.");
            }

            if (SecondsRemaining > 0) { return false; }

            BeginWindDown();
            return false;
        }

        /// <summary>
        /// The siren, and the part where everybody has to stop. Nothing new arrives from here
        /// on and the caller talks down whoever is still out there.
        /// </summary>
        private void BeginWindDown()
        {
            WindingDown = true;
            _windDownEndsAt = Game.GameTime + Math.Max(0, _config.GetInt("features.purge.windDownSeconds", 25)) * 1000;

            GTA.UI.Screen.ShowSubtitle(
                "~g~THE SIREN.~s~~n~The annual purge has concluded. Emergency services have resumed operation.~n~" +
                "~o~Return to your homes.", 8000);
            GTA.UI.Notification.Show("~g~The purge has ended.~s~ Stand down.");
            Log.Info("Purge window elapsed - winding down.");
        }

        private bool UpdateWindDown()
        {
            int left = Math.Max(0, (_windDownEndsAt - Game.GameTime) / 1000);
            DrawClock(left, "~g~", "STAND DOWN");

            if (Game.GameTime < _windDownEndsAt) { return false; }

            Active = false;
            WindingDown = false;
            Log.Info("Purge wind-down complete - stopping the mode.");
            return true;
        }

        /// <summary>
        /// The countdown, on screen. The single most effective answer to "does this ever end":
        /// a number that is visibly going down.
        /// </summary>
        private void DrawClock(int seconds, string colour, string label = "PURGE")
        {
            if (!_config.GetBool("features.purge.showTimer", true)) { return; }

            Hud.Banner(colour + label + " " + Hud.Clock(seconds), 0.035f, 0.6f,
                System.Drawing.Color.FromArgb(235, 255, 255, 255));
        }

        /// <summary>Minutes and seconds for the overlay, or an empty string when not running.</summary>
        public string Remaining
        {
            get
            {
                if (!Active) { return string.Empty; }
                return (SecondsRemaining / 60) + ":" + (SecondsRemaining % 60).ToString("00");
            }
        }

        public void Clear()
        {
            RestoreWantedLevel();
            Active = false;
            WindingDown = false;
            _armed = false;
            SecondsRemaining = 0;
        }

        /// <summary>
        /// The one place this mod deliberately touches the wanted system, because "all crime is
        /// legal" is the entire premise and being chased by the LSPD for it is the opposite.
        ///
        /// It lowers the ceiling rather than clearing stars, and puts back whatever the ceiling
        /// was — so a six-star overhaul gets its six back the moment the purge ends, and its
        /// behaviour outside the window is untouched.
        /// </summary>
        private void SuppressWantedLevel()
        {
            if (!_config.GetBool("features.purge.noWantedLevel", true)) { return; }

            try
            {
                _previousMaxWanted = Function.Call<int>(Hash.GET_MAX_WANTED_LEVEL);
                Function.Call(Hash.SET_MAX_WANTED_LEVEL, 0);
                Log.Info("Purge: wanted level suppressed (previous ceiling " + _previousMaxWanted + ").");
            }
            catch (Exception ex)
            {
                Log.Error("Could not suppress the wanted level", ex);
                _previousMaxWanted = -1;
            }
        }

        private void HoldWantedLevelDown()
        {
            if (_previousMaxWanted < 0) { return; }

            try
            {
                Player player = Game.Player;
                Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, player, true);

                if (Function.Call<int>(Hash.GET_PLAYER_WANTED_LEVEL, player) > 0)
                {
                    Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, player, 0, false);
                    Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, player, false);
                }
            }
            catch (Exception)
            {
                // Once a frame; a log line here would be thousands of them.
            }
        }

        private void RestoreWantedLevel()
        {
            if (_previousMaxWanted < 0) { return; }

            try
            {
                Function.Call(Hash.SET_MAX_WANTED_LEVEL, _previousMaxWanted);
                Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player, false);
                Log.Info("Purge: wanted ceiling restored to " + _previousMaxWanted + ".");
            }
            catch (Exception ex)
            {
                Log.Error("Could not restore the wanted level", ex);
            }

            _previousMaxWanted = -1;
        }
    }
}
