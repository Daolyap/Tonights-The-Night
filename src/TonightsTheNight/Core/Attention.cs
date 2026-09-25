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
    /// How much of the riot is looking at you at once.
    ///
    /// Perception decides *whether* the riot knows where you are. This decides how many of them
    /// act on it, which turns out to be the more important question. A mode where the army is
    /// hostile to you is correct; a mode where thirty soldiers are all in a combat task against
    /// you personally is a firing squad, and every large mode became one the moment you were
    /// spotted, because "hate" is a property of a relationship group and a group has no size.
    ///
    /// So hostility stays declared and the *engagement* is budgeted. A handful of them come for
    /// you; the rest carry on fighting the people they were already fighting, which is both far
    /// more survivable and much closer to what a riot looks like from inside one.
    ///
    /// Trimmed peds are not pacified. They are given back the ordinary "fight whoever you hate
    /// nearby" task — and since you are still hated, one of them coming back round to you later
    /// is exactly right. It is a rotation, not an amnesty.
    /// </summary>
    public sealed class Attention
    {
        private readonly ConfigStore _config;

        /// <summary>Ped handle to the game time it may be counted as an attacker again.</summary>
        private readonly Dictionary<int, int> _cooldowns = new Dictionary<int, int>();

        private int _nextCheckAt;
        private int _cursor;

        /// <summary>How many were engaging the player at the last check. For the overlay and log.</summary>
        public int Engaged { get; private set; }

        /// <summary>How many have been told to go and find somebody else. Cumulative.</summary>
        public int Trimmed { get; private set; }

        public Attention(ConfigStore config)
        {
            _config = config;
        }

        public bool Enabled { get { return Cap > 0; } }

        /// <summary>
        /// Zero disables the whole system, which is the "come and get me" setting rather than
        /// an off switch for hostility.
        /// </summary>
        private int Cap { get { return Math.Max(0, _config.GetInt("combat.maxPlayerAttackers", 4)); } }

        public void Reset()
        {
            _cooldowns.Clear();
            _nextCheckAt = 0;
            _cursor = 0;
            Engaged = 0;
            Trimmed = 0;
        }

        public void Update(IReadOnlyList<TrackedPed> tracked)
        {
            if (!Enabled || tracked.Count == 0) { return; }

            if (Game.GameTime < _nextCheckAt) { return; }
            _nextCheckAt = Game.GameTime + Math.Max(250, _config.GetInt("combat.attackerCheckMs", 1500));

            ExpireCooldowns();

            Ped player = Game.Player.Character;
            if (player == null || !player.Exists() || player.IsDead) { return; }

            int cap = Cap;
            int found = 0;

            // Bounded: this is a native per ped and the whole point is to be cheap enough to run
            // every second and a half without anybody noticing.
            int window = Math.Min(tracked.Count, Math.Max(24, cap * 8));
            var excess = new List<TrackedPed>();

            for (int offset = 0; offset < window; offset++)
            {
                TrackedPed entry = tracked[(_cursor + offset) % tracked.Count];

                if (!entry.IsUsable) { continue; }
                // A chase is a whole carload who decided to come after you. Trimming one out of
                // it leaves a car with an empty seat and no explanation.
                if (entry.InPursuit) { continue; }
                if (entry.Ped.IsInVehicle()) { continue; }
                if (!IsEngagingPlayer(entry.Ped, player)) { continue; }

                found++;

                // Recently trimmed peds still count towards the budget - they are on their way
                // to somebody else - but are not trimmed twice in a row.
                if (_cooldowns.ContainsKey(entry.Ped.Handle)) { continue; }

                if (found > cap) { excess.Add(entry); }
            }

            _cursor = (_cursor + window) % tracked.Count;
            Engaged = found;

            foreach (TrackedPed entry in excess) { SendElsewhere(entry); }
        }

        /// <summary>
        /// Breaks one ped off the player and points them back at the riot.
        ///
        /// Registering hated targets first matters: without it the ped clears its task, finds
        /// nothing, stands still for a moment and then reacquires the nearest thing it hates,
        /// which is you.
        /// </summary>
        private void SendElsewhere(TrackedPed entry)
        {
            try
            {
                float range = _config.GetFloat("combat.seeingRange", 60f);

                Function.Call(Hash.CLEAR_PED_TASKS, entry.Ped);
                Function.Call(Hash.REGISTER_HATED_TARGETS_AROUND_PED, entry.Ped, range);
                Function.Call(Hash.TASK_COMBAT_HATED_TARGETS_AROUND_PED, entry.Ped, range, 0);

                // The Director must not immediately re-task them either.
                entry.LastTaskedAt = Game.GameTime;

                _cooldowns[entry.Ped.Handle] =
                    Game.GameTime + Math.Max(1000, _config.GetInt("combat.attackerCooldownMs", 8000));

                Trimmed++;
            }
            catch (Exception ex)
            {
                Log.Error("Could not break a ped off the player", ex);
            }
        }

        private void ExpireCooldowns()
        {
            if (_cooldowns.Count == 0) { return; }

            List<int> stale = null;
            foreach (var pair in _cooldowns)
            {
                if (Game.GameTime <= pair.Value) { continue; }
                if (stale == null) { stale = new List<int>(); }
                stale.Add(pair.Key);
            }

            if (stale == null) { return; }
            foreach (int handle in stale) { _cooldowns.Remove(handle); }
        }

        private static bool IsEngagingPlayer(Ped ped, Ped player)
        {
            try
            {
                if (!ped.IsInCombat) { return false; }
                return Function.Call<int>(Hash.GET_PED_TARGET_FROM_COMBAT_PED, ped, 0) == player.Handle;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
