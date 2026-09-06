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
        private readonly HashSet<int> _handles = new HashSet<int>();

        public int Count { get { return _tracked.Count; } }
        public IReadOnlyList<TrackedPed> Tracked { get { return _tracked; } }

        public bool Contains(Ped ped)
        {
            return ped != null && _handles.Contains(ped.Handle);
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
                LastTaskedAt = 0
            };

            _tracked.Add(entry);
            _handles.Add(ped.Handle);
            return entry;
        }

        public void Remove(TrackedPed entry, bool restore)
        {
            if (restore) { Restore(entry); }

            _tracked.Remove(entry);
            if (entry.Ped != null) { _handles.Remove(entry.Ped.Handle); }
        }

        /// <summary>Drops entries whose peds are gone, without touching the survivors.</summary>
        public int PruneDead()
        {
            int removed = 0;
            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                TrackedPed entry = _tracked[i];
                if (entry.Ped != null && entry.Ped.Exists()) { continue; }

                DeleteBlip(entry);
                _handles.Remove(entry.Ped != null ? entry.Ped.Handle : 0);
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
            _handles.Clear();
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

                // Undo the "never run away" conditioning.
                Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, ped, 0, true);
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.AlwaysFight, false);

                ped.Task.ClearAllImmediately();
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
