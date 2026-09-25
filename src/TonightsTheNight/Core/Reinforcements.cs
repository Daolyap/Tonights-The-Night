using System;
using System.Collections.Generic;
using TonightsTheNight.Config;
using TonightsTheNight.Factions;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// A faction that is losing sends more.
    ///
    /// Escalation phases answer "how long has this been going on". They cannot answer "how badly
    /// is it going", which is the difference between a deployment and a response: an army that
    /// sends the same two trucks every fifteen seconds whether it is walking through the crowd
    /// or being wiped out is not reacting to anything.
    ///
    /// So every spawned member of a faction that dies raises that faction's commitment. Waves
    /// get bigger and arrive sooner, up to a ceiling. Meet them with nothing and the response
    /// stays a patrol; destroy three carloads and the next ones come in force.
    ///
    /// Deliberately per faction rather than global. Killing soldiers should bring more soldiers,
    /// not more aliens.
    /// </summary>
    public sealed class Reinforcements
    {
        private readonly ConfigStore _config;

        /// <summary>Faction id to spawned members confirmed dead.</summary>
        private readonly Dictionary<string, int> _losses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Last multiplier announced per faction, so escalation is reported once.</summary>
        private readonly Dictionary<string, int> _announced = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public Reinforcements(ConfigStore config)
        {
            _config = config;
        }

        public bool Enabled { get { return _config.GetBool("features.reinforcements.enabled", true); } }

        public void Reset()
        {
            _losses.Clear();
            _announced.Clear();
        }

        /// <summary>Called when a spawned member of a faction is confirmed dead.</summary>
        public void NoteLoss(Faction faction)
        {
            if (faction == null) { return; }

            int count;
            _losses.TryGetValue(faction.Id, out count);
            _losses[faction.Id] = count + 1;
        }

        public int LossesFor(Faction faction)
        {
            int count;
            return faction != null && _losses.TryGetValue(faction.Id, out count) ? count : 0;
        }

        /// <summary>
        /// 1.0 when a faction has lost nobody, rising with casualties to a configured ceiling.
        /// Wave size multiplies by it; the interval between waves divides by it.
        /// </summary>
        public float Commitment(Faction faction)
        {
            if (!Enabled || faction == null) { return 1f; }

            // Only factions that arrive in waves can send more of themselves.
            if (!faction.Spawn.Enabled) { return 1f; }

            float perLoss = _config.GetFloat("features.reinforcements.perLoss", 0.06f);
            float ceiling = Math.Max(1f, _config.GetFloat("features.reinforcements.maxMultiplier", 2.5f));

            float multiplier = 1f + LossesFor(faction) * perLoss;

            // Clamped at both ends. The spawner divides the wave interval by this, so a negative
            // perLoss - a reasonable thing to try when you want a losing faction to back off -
            // could reach zero and turn the next-wave time into int.MinValue, firing a wave
            // every tick forever.
            if (multiplier < 1f) { return 1f; }
            return multiplier > ceiling ? ceiling : multiplier;
        }

        /// <summary>
        /// Announces a faction stepping up, at most once per whole step. Without this the only
        /// evidence that the system exists is a slow change in numbers nobody would attribute
        /// to their own body count.
        /// </summary>
        public string StepUp(Faction faction)
        {
            if (!Enabled || !_config.GetBool("features.reinforcements.announce", true)) { return null; }

            int step = (int)Commitment(faction);

            int last;
            _announced.TryGetValue(faction.Id, out last);
            if (last == 0) { last = 1; }

            if (step <= last) { return null; }

            _announced[faction.Id] = step;
            Log.Info("Reinforcements: '" + faction.Id + "' stepping up to x" + step +
                     " after " + LossesFor(faction) + " losses.");

            return faction.DisplayName + " are sending more.";
        }
    }
}
