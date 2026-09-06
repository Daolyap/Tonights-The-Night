using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using TonightsTheNight.Factions;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// Everything we touched, so we can put it all back.
    ///
    /// This is not bookkeeping for its own sake: a riot mod that forgets to release its peds
    /// leaks until the game crashes and leaves the world subtly wrong across saves. The Aborted
    /// path matters as much as the normal stop path, because a script reload is not a polite
    /// shutdown.
    /// </summary>
    public sealed class EntityRegistry
    {
        private readonly List<TrackedPed> _tracked = new List<TrackedPed>();
        private readonly Dictionary<int, TrackedPed> _byHandle = new Dictionary<int, TrackedPed>();

        public int Count { get { return _tracked.Count; } }
        public IReadOnlyList<TrackedPed> Tracked { get { return _tracked; } }

        public bool Contains(Ped ped)
        {
            return ped != null && _byHandle.ContainsKey(ped.Handle);
        }

        public TrackedPed Find(Ped ped)
        {
            TrackedPed entry;
            return ped != null && _byHandle.TryGetValue(ped.Handle, out entry) ? entry : null;
        }

        /// <summary>
        /// False once the handle has been recycled onto a different entity. Cheap, and the only
        /// thing standing between us and blips on wildlife.
        /// </summary>
        public static bool IsSameEntity(TrackedPed entry)
        {
            if (entry.Ped == null || !entry.Ped.Exists()) { return false; }
            return entry.OriginalModel == 0 || entry.Ped.Model.Hash == entry.OriginalModel;
        }

        public TrackedPed Add(Ped ped, Faction faction, Reaction reaction, bool spawned)
        {
            var entry = new TrackedPed
            {
                Ped = ped,
                Faction = faction,
                Reaction = reaction,
                Spawned = spawned,
                OriginalGroup = Function.Call<int>(Hash.GET_PED_RELATIONSHIP_GROUP_HASH, ped),
                OriginalModel = ped.Model.Hash,
                LastTaskedAt = 0
            };

            _tracked.Add(entry);
            _byHandle[ped.Handle] = entry;
            return entry;
        }

        public void Remove(TrackedPed entry, bool restore)
        {
            // The blip goes in every path. Deleting it only on the restore path is why blips
            // outlived the peds they were attached to.
            DeleteBlip(entry);

            if (restore) { Restore(entry); }

            _tracked.Remove(entry);
            if (entry.Ped != null) { _byHandle.Remove(entry.Ped.Handle); }
        }

        /// <summary>
        /// Drops entries whose peds are gone or whose handle now belongs to something else,
        /// without touching the survivors.
        /// </summary>
        public int PruneDead()
        {
            int removed = 0;
            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                TrackedPed entry = _tracked[i];
                if (IsSameEntity(entry)) { continue; }

                DeleteBlip(entry);
                if (entry.Ped != null) { _byHandle.Remove(entry.Ped.Handle); }
                _tracked.RemoveAt(i);
                removed++;
            }
            return removed;
        }

        public void RestoreAll()
        {
            Log.Info("Restoring " + _tracked.Count + " tracked ped(s).");

            foreach (TrackedPed entry in _tracked)
            {
                Restore(entry);
            }

            _tracked.Clear();
            _byHandle.Clear();
        }

        private static void Restore(TrackedPed entry)
        {
            DeleteBlip(entry);

            try
            {
                if (entry.Ped == null || !entry.Ped.Exists()) { return; }

                Ped ped = entry.Ped;

                if (entry.Spawned)
                {
                    // Ours to remove. Marking rather than deleting lets the engine reclaim it
                    // when the player is not looking, which is far less jarring.
                    ped.MarkAsNoLongerNeeded();
                    return;
                }

                Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_HASH, ped, entry.OriginalGroup);
                Function.Call(Hash.SET_PED_AS_ENEMY, ped, false);

                // Undo the conditioning before clearing tasks, or the ped re-enters combat off
                // the back of attributes we left set.
                Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, ped, 0, true);
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.AlwaysFight, false);
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.CanFightArmedPedsWhenNotArmed, false);
                Function.Call(Hash.SET_PED_KEEP_TASK, ped, false);

                Function.Call(Hash.CLEAR_PED_TASKS_IMMEDIATELY, ped);
                ped.MarkAsNoLongerNeeded();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to restore a ped", ex);
            }
        }

        private static void DeleteBlip(TrackedPed entry)
        {
            try
            {
                if (entry.Blip != null && entry.Blip.Exists())
                {
                    entry.Blip.Delete();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to delete a blip", ex);
            }
            entry.Blip = null;
        }
    }
}
