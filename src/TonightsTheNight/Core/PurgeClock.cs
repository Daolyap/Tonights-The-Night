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

        /// <summary>Returns true on the tick the purge ends, so the caller can stop the mode.</summary>
        public bool Update()
        {
            if (!Active) { return false; }

            // All crime is legal, so the game's own police should not be interested in you.
            // Re-applied rather than set once, because a wanted-system overhaul runs its own
            // script and will happily hand you a star back.
            HoldWantedLevelDown();

            int elapsed = (Game.GameTime - _startedAt) / 1000;
            SecondsRemaining = Math.Max(0, _totalSeconds - elapsed);

            if (SecondsRemaining != _lastAnnouncedSecond)
            {
                _lastAnnouncedSecond = SecondsRemaining;

                // Only the last ten seconds get a countdown; anything more is nagging.
                if (SecondsRemaining > 0 && SecondsRemaining <= 10)
                {
                    GTA.UI.Screen.ShowSubtitle("~r~" + SecondsRemaining + "~s~", 900);
                }
            }

            // A minute-by-minute line, so "it never ended" is answerable from the log instead of
            // from memory.
            if (Game.GameTime >= _nextLogAt)
            {
                _nextLogAt = Game.GameTime + 60000;
                Log.Info("Purge: " + (SecondsRemaining / 60) + "m " + (SecondsRemaining % 60) + "s remaining.");
            }

            if (SecondsRemaining > 0) { return false; }

            Active = false;
            GTA.UI.Screen.ShowSubtitle("~g~The purge has ended.~s~~n~Emergency services have resumed operation.", 7000);
            GTA.UI.Notification.Show("~g~The purge has ended.");
            Log.Info("Purge window elapsed - stopping the mode.");
            return true;
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
