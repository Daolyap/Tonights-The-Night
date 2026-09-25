using System;
using System.Drawing;
using GTA;
using TonightsTheNight.Config;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// The shape of a mode that is only ever about you.
    ///
    /// The mechanics of a chase mode are already here — factions that arrive in cars, drivers
    /// tasked at the player, passengers leaning out, waves that grow as you destroy them. What
    /// is missing when you put those together is any sense of progress: cars keep coming, you
    /// keep killing them, and nothing ever says so. Ten minutes in you cannot tell whether it
    /// is escalating or repeating.
    ///
    /// So this counts. A wave per so many of them put down, announced when it turns over, and a
    /// line on screen for how many are currently after you. That is the whole class, and it is
    /// most of the difference between "cars keep spawning" and a chase you are surviving.
    /// </summary>
    public sealed class Manhunt
    {
        private readonly ConfigStore _config;

        private JsonValue _declared = JsonValue.Null;
        private int _wave;
        private int _announcedWave;

        public Manhunt(ConfigStore config)
        {
            _config = config;
        }

        public bool Active { get { return _declared.IsObject && _declared["enabled"].AsBool(true); } }

        /// <summary>Which wave you are on. One-based, because nobody survives wave zero.</summary>
        public int Wave { get { return _wave; } }

        public void Begin(JsonValue declared)
        {
            _declared = declared == null ? JsonValue.Null : declared;
            _wave = 1;
            _announcedWave = 1;

            if (!Active) { return; }

            Log.Info("Manhunt armed: a wave every " + KillsPerWave() + " kill(s).");
            GTA.UI.Screen.ShowSubtitle("~r~They know what you look like.~s~~n~Keep moving.", 5000);
        }

        public void Clear()
        {
            _declared = JsonValue.Null;
            _wave = 0;
            _announcedWave = 0;
        }

        /// <summary>
        /// <paramref name="kills"/> is the mode's running body count and <paramref name="alive"/>
        /// how many of them are currently tracked. Both come from the Director, which already
        /// has them, rather than being recounted here.
        /// </summary>
        public void Update(int kills, int alive)
        {
            if (!Active) { return; }

            int perWave = KillsPerWave();
            _wave = 1 + (perWave <= 0 ? 0 : kills / perWave);

            if (_wave > _announcedWave)
            {
                _announcedWave = _wave;
                Announce();
            }

            Draw(alive);
        }

        private void Announce()
        {
            if (!_declared["announce"].AsBool(true)) { return; }

            GTA.UI.Notification.Show("~r~Wave " + _wave + ".~s~ They are sending better.");
            GTA.UI.Screen.ShowSubtitle("~r~WAVE " + _wave + "~s~", 2500);
            Log.Info("Manhunt reached wave " + _wave + ".");
        }

        private void Draw(int alive)
        {
            if (!_config.GetBool("features.manhunt.showWave", true)) { return; }

            Hud.Banner("~r~WAVE " + _wave + "~s~   " + alive + " after you", 0.035f, 0.6f,
                       Color.FromArgb(235, 255, 255, 255));
        }

        private int KillsPerWave()
        {
            return Math.Max(1, _declared["killsPerWave"].AsInt(
                _config.GetInt("features.manhunt.killsPerWave", 6)));
        }
    }
}
