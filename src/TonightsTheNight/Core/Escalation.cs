using System;
using System.Collections.Generic;
using GTA;
using TonightsTheNight.Config;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    public sealed class Phase
    {
        public string Name;
        public int DurationSeconds;
        public bool Fires;
        public bool Looting;

        /// <summary>The city's power fails from this phase on.</summary>
        public bool Blackout;

        /// <summary>Rioters start dragging things across the roads.</summary>
        public bool Barricades;

        /// <summary>Kills that also advance to this phase, so a violent riot escalates faster.</summary>
        public int KillThreshold;
    }

    /// <summary>
    /// The riot builds instead of simply existing.
    ///
    /// Phase 0 is unrest, later phases bring fires, looting and — through faction
    /// <c>fromPhase</c> — police and then the military. Advancing on elapsed time OR body count
    /// means a quiet riot still progresses while a bloodbath escalates immediately, which is the
    /// difference between a timer and something that feels like a response.
    /// </summary>
    public sealed class Escalation
    {
        private readonly ConfigStore _config;
        private List<Phase> _phases = new List<Phase>();
        private int _phaseStartedAt;

        public int Current { get; private set; }
        public bool Enabled { get { return _config.GetBool("features.escalation.enabled", true) && _phases.Count > 0; } }

        public Escalation(ConfigStore config)
        {
            _config = config;
        }

        public Phase CurrentPhase
        {
            get { return _phases.Count == 0 ? null : _phases[Math.Min(Current, _phases.Count - 1)]; }
        }

        public string CurrentName
        {
            get { Phase phase = CurrentPhase; return phase == null ? "—" : phase.Name; }
        }

        /// <summary>With escalation off, everything is available immediately.</summary>
        public bool Allows(int fromPhase)
        {
            return !Enabled || fromPhase <= Current;
        }

        public void Load(JsonValue node)
        {
            _phases = new List<Phase>();

            foreach (JsonValue entry in node["phases"].Items)
            {
                _phases.Add(new Phase
                {
                    Name = entry["name"].AsString("Phase " + (_phases.Count + 1)),
                    DurationSeconds = entry["durationSeconds"].AsInt(120),
                    Fires = entry["fires"].AsBool(false),
                    Looting = entry["looting"].AsBool(false),
                    Blackout = entry["blackout"].AsBool(false),
                    Barricades = entry["barricades"].AsBool(false),
                    KillThreshold = entry["killThreshold"].AsInt(0)
                });
            }

            Current = 0;
            _phaseStartedAt = Game.GameTime;

            if (_phases.Count > 0)
            {
                Log.Info("Escalation: " + _phases.Count + " phase(s), starting at '" + _phases[0].Name + "'.");
            }
        }

        public void Reset()
        {
            Current = 0;
            _phaseStartedAt = Game.GameTime;
        }

        /// <summary>Returns true on the tick the phase changes, so the caller can announce it.</summary>
        public bool Update(int kills)
        {
            if (!Enabled || Current >= _phases.Count - 1) { return false; }

            Phase next = _phases[Current + 1];
            Phase active = _phases[Current];

            bool byTime = active.DurationSeconds > 0 &&
                          Game.GameTime - _phaseStartedAt > active.DurationSeconds * 1000;
            bool byBlood = next.KillThreshold > 0 && kills >= next.KillThreshold;

            if (!byTime && !byBlood) { return false; }

            Current++;
            _phaseStartedAt = Game.GameTime;

            Log.Info("Escalated to phase " + Current + " '" + next.Name + "' (" +
                     (byBlood ? "kill count" : "elapsed time") + ").");
            return true;
        }

        /// <summary>Menu-driven skip, for testing and for impatience.</summary>
        public bool Advance()
        {
            if (Current >= _phases.Count - 1) { return false; }

            Current++;
            _phaseStartedAt = Game.GameTime;
            Log.Info("Skipped to phase " + Current + " '" + CurrentName + "'.");
            return true;
        }
    }
}
