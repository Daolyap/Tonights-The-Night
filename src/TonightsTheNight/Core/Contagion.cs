using System;
using System.Collections.Generic;
using GTA;
using TonightsTheNight.Config;
using TonightsTheNight.Factions;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// A riot that recruits for itself.
    ///
    /// Every other mode here has the same underlying arc, because every other mode is fed by the
    /// same two supply lines: convert the crowd that is already streamed in, and spawn what has
    /// no ambient equivalent. Both are capped, so a riot rises to a level and stays there.
    ///
    /// Contagion is a third supply line, and it is the interesting one: the riot converts people
    /// itself, by reaching them. Nothing is spawned and nothing is recruited from a radius around
    /// the player — a faction grows only where its members physically are, which means the thing
    /// spreads outward from wherever it started, thins where it is being killed, and can be
    /// contained by killing the right people rather than by a setting.
    ///
    /// It is also the only mechanic here that gets *stronger* the longer you leave it, which is
    /// what makes a mode built on it feel unlike the other nine.
    /// </summary>
    public sealed class Contagion
    {
        private readonly ConfigStore _config;
        private readonly Random _random;

        private JsonValue _declared = JsonValue.Null;
        private int _nextPassAt;
        private int _cursor;

        /// <summary>How many people this infection has taken. Shown on screen and logged.</summary>
        public int Converted { get; private set; }

        public Contagion(ConfigStore config, Random random)
        {
            _config = config;
            _random = random;
        }

        public bool Active
        {
            get
            {
                return _declared.IsObject && _declared["enabled"].AsBool(true) &&
                       _config.GetBool("features.contagion.enabled", true);
            }
        }

        public void Begin(JsonValue declared)
        {
            _declared = declared == null ? JsonValue.Null : declared;
            Converted = 0;
            _nextPassAt = 0;
            _cursor = 0;

            if (Active)
            {
                Log.Info("Contagion armed: '" + _declared["source"].AsString("?") + "' spreads to '" +
                         _declared["target"].AsString("?") + "'.");
            }
        }

        public void Clear()
        {
            _declared = JsonValue.Null;
            Converted = 0;
        }

        /// <summary>
        /// One pass of spreading. <paramref name="convert"/> does the actual work, because
        /// changing somebody's faction means re-conditioning and re-blipping them and the
        /// Director owns both of those.
        /// </summary>
        public void Update(IReadOnlyList<TrackedPed> tracked, RiotMode mode, Action<TrackedPed, Faction> convert)
        {
            if (!Active || tracked.Count == 0) { return; }

            if (Game.GameTime < _nextPassAt) { return; }
            _nextPassAt = Game.GameTime + Math.Max(200, _declared["intervalMs"].AsInt(1200));

            Faction carrier = mode.Find(_declared["source"].AsString(null));
            Faction victim = mode.Find(_declared["target"].AsString(null));

            if (carrier == null || victim == null)
            {
                Log.Warn("Contagion names a faction this mode does not define. Disabled for this run.");
                _declared = JsonValue.Null;
                return;
            }

            int ceiling = _declared["max"].AsInt(0);
            if (ceiling > 0 && Converted >= ceiling) { return; }

            float radius = _declared["radius"].AsFloat(3.5f);
            float radiusSquared = radius * radius;
            double chance = _declared["chance"].AsFloat(0.55f);

            // Bounded like everything else that walks the tracked list. Over a few seconds this
            // covers the whole crowd, and an infection that spreads a fraction of a second later
            // than it could have is not something anybody can see.
            int window = Math.Min(tracked.Count, Math.Max(12, _declared["samplesPerPass"].AsInt(20)));
            int spread = 0;
            int perPass = Math.Max(1, _declared["perPass"].AsInt(3));

            for (int offset = 0; offset < window && spread < perPass; offset++)
            {
                TrackedPed source = tracked[(_cursor + offset) % tracked.Count];

                if (!source.IsUsable || source.Faction != carrier) { continue; }
                if (_random.NextDouble() > chance) { continue; }

                for (int i = 0; i < tracked.Count && spread < perPass; i++)
                {
                    TrackedPed near = tracked[i];

                    if (near == source || !near.IsUsable || near.Faction != victim) { continue; }
                    if (near.Ped.Position.DistanceToSquared(source.Ped.Position) > radiusSquared) { continue; }

                    convert(near, carrier);
                    Converted++;
                    spread++;

                    if (ceiling > 0 && Converted >= ceiling) { return; }
                }
            }

            _cursor = (_cursor + window) % tracked.Count;
        }
    }
}
