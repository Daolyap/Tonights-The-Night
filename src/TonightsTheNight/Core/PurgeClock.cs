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
        private int _lastAnnouncedSecond = -1;

        public bool Active { get; private set; }
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

            if (_config.GetBool("features.purge.setClock", true))
            {
                Function.Call(Hash.SET_CLOCK_TIME, hour, minute, 0);
            }

            _startedAt = Game.GameTime;
            Active = true;
            _lastAnnouncedSecond = -1;

            int minutes = node["durationMinutes"].AsInt(_config.GetInt("features.purge.durationMinutes", 12));
            SecondsRemaining = minutes * 60;

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

            int elapsed = (Game.GameTime - _startedAt) / 1000;
            int total = SecondsRemaining;
            int left = Math.Max(0, total - elapsed);

            if (left != _lastAnnouncedSecond)
            {
                _lastAnnouncedSecond = left;

                // Only the last ten seconds get a countdown; anything more is nagging.
                if (left > 0 && left <= 10)
                {
                    GTA.UI.Screen.ShowSubtitle("~r~" + left + "~s~", 900);
                }
            }

            if (left > 0) { return false; }

            Active = false;
            GTA.UI.Screen.ShowSubtitle("~g~The purge has ended.~s~~n~Emergency services have resumed operation.", 7000);
            Log.Info("Purge window elapsed.");
            return true;
        }

        public void Clear()
        {
            Active = false;
            _armed = false;
        }
    }
}
