using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using TonightsTheNight.Config;
using TonightsTheNight.Factions;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// Whether the riot actually knows where you are.
    ///
    /// Without this, hostility is a permanent property of a relationship group: every soldier in
    /// the district is your enemy from the moment the mode starts, through walls, around corners,
    /// from behind. Standing in an alley two streets away does not help, because nobody ever
    /// needed to see you in the first place.
    ///
    /// So being a target is a state rather than a fact. Nearby fighters are sampled for a clear
    /// line of sight; firing a weapon gives you away regardless. Lose them for long enough and
    /// every faction drops back to neutral, at which point their combat AI goes and finds
    /// somebody it can see — which, in a riot, is never in short supply.
    ///
    /// The relationship matrix does the work. Neutral is not a truce, it is being unremarkable.
    /// </summary>
    public sealed class Perception
    {
        /// <summary>Trace flags for the line-of-sight check: world, vehicles and objects.</summary>
        private const int LosTraceType = 17;

        private readonly ConfigStore _config;
        private readonly Random _random;

        private int _nextCheckAt;
        private int _lastSeenAt;
        private int _cursor;

        /// <summary>True while at least one hostile can account for your whereabouts.</summary>
        public bool Spotted { get; private set; }

        /// <summary>Set on the tick the answer changes, so the caller can react once.</summary>
        public bool Changed { get; private set; }

        /// <summary>How the player was found, for the log and the overlay.</summary>
        public string Reason { get; private set; }

        /// <summary>Faction ids that would be hostile to the player if they could see them.</summary>
        private readonly HashSet<string> _hostile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public Perception(ConfigStore config, Random random)
        {
            _config = config;
            _random = random;
            Reason = "not started";
            // A riot that has not started yet still counts as "they know where you are", so the
            // first standings applied are the hostile ones rather than a session-long truce.
            Spotted = true;
        }

        /// <summary>
        /// Told by the Director which factions actually care, after the menu's forced stance and
        /// each faction's own declaration have been resolved. Asking the faction directly got
        /// this wrong: a faction that declares itself neutral still hunts you when the menu
        /// forces "Target".
        /// </summary>
        public void SetHostileFactions(IEnumerable<string> factionIds)
        {
            _hostile.Clear();
            foreach (string id in factionIds) { _hostile.Add(id); }
        }

        public bool Enabled { get { return _config.GetBool("features.perception.enabled", true); } }

        /// <summary>Seconds since anyone last had eyes on the player.</summary>
        public int SecondsSinceSeen
        {
            get { return _lastSeenAt == 0 ? 0 : (Game.GameTime - _lastSeenAt) / 1000; }
        }

        public void Reset()
        {
            // A riot starts with everyone aware of you. Being ignored is something you earn by
            // getting out of sight, not the state you begin in.
            Spotted = true;
            Changed = true;
            Reason = "riot started";
            _lastSeenAt = Game.GameTime;
            _nextCheckAt = 0;
            _cursor = 0;
        }

        public void Update(IReadOnlyList<TrackedPed> tracked)
        {
            Changed = false;

            if (!Enabled)
            {
                // Off means the old behaviour: permanently visible to everyone.
                if (!Spotted) { Spotted = true; Changed = true; Reason = "perception disabled"; }
                return;
            }

            if (Game.GameTime < _nextCheckAt) { return; }
            _nextCheckAt = Game.GameTime + _config.GetInt("features.perception.checkIntervalMs", 400);

            string found = Detect(tracked);

            if (found != null)
            {
                _lastSeenAt = Game.GameTime;
                Reason = found;

                if (!Spotted)
                {
                    Spotted = true;
                    Changed = true;
                    Log.Debug("Perception: player spotted (" + found + ").");
                }
                return;
            }

            if (!Spotted) { return; }

            int forget = _config.GetInt("features.perception.forgetSeconds", 12);
            if (Game.GameTime - _lastSeenAt < forget * 1000) { return; }

            Spotted = false;
            Changed = true;
            Reason = "lost you";
            Log.Debug("Perception: player lost after " + forget + "s without sight.");
        }

        /// <summary>
        /// Returns how the player was detected, or null. Cheap checks first: making a noise
        /// costs nothing to test and gives you away more reliably than being visible.
        /// </summary>
        private string Detect(IReadOnlyList<TrackedPed> tracked)
        {
            Ped player = Game.Player.Character;

            if (!player.Exists() || player.IsDead) { return null; }

            // Shooting is the loudest thing you can do and does not care about walls.
            if (_config.GetBool("features.perception.gunfireGivesYouAway", true) &&
                Function.Call<bool>(Hash.IS_PED_SHOOTING, player))
            {
                return "gunfire";
            }

            if (tracked.Count == 0) { return null; }

            float range = SeeingRange(player);
            float rangeSquared = range * range;

            bool requireFacing = _config.GetBool("features.perception.requireFacing", false);
            int samples = Math.Max(1, _config.GetInt("features.perception.samplesPerCheck", 6));
            int examined = 0;

            // Round-robin rather than nearest-first: a raycast per ped is the expensive part, and
            // over a couple of seconds this covers the whole crowd anyway.
            for (int offset = 0; offset < tracked.Count && examined < samples; offset++)
            {
                TrackedPed entry = tracked[(_cursor + offset) % tracked.Count];

                if (!entry.IsUsable) { continue; }
                if (entry.Reaction != Reaction.Fight) { continue; }
                if (!_hostile.Contains(entry.Faction.Id)) { continue; }

                if (player.Position.DistanceToSquared(entry.Ped.Position) > rangeSquared) { continue; }

                examined++;

                try
                {
                    bool sees = requireFacing
                        ? Function.Call<bool>(Hash.HAS_ENTITY_CLEAR_LOS_TO_ENTITY_IN_FRONT, entry.Ped, player)
                        : Function.Call<bool>(Hash.HAS_ENTITY_CLEAR_LOS_TO_ENTITY, entry.Ped, player, LosTraceType);

                    if (sees)
                    {
                        _cursor = (_cursor + offset) % tracked.Count;
                        return entry.Faction.DisplayName + " have eyes on you";
                    }
                }
                catch (Exception)
                {
                    // One failed trace is not worth a log line every check.
                }
            }

            _cursor = tracked.Count == 0 ? 0 : (_cursor + samples) % tracked.Count;
            return null;
        }

        /// <summary>
        /// How far they can pick you out. Crouching and cover shorten it; sitting in a car under
        /// a streetlight does not.
        /// </summary>
        private float SeeingRange(Ped player)
        {
            float range = _config.GetFloat("features.perception.sightRange", 60f);

            try
            {
                bool sneaking = Function.Call<bool>(Hash.GET_PED_STEALTH_MOVEMENT, player) ||
                                Function.Call<bool>(Hash.IS_PED_IN_COVER, player, false);

                if (sneaking) { range *= _config.GetFloat("features.perception.stealthFactor", 0.45f); }
            }
            catch (Exception)
            {
                // Fall back to the plain range rather than reporting it every check.
            }

            return range;
        }
    }
}
