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

        /// <summary>
        /// Live blips, maintained rather than recounted. The cap is consulted once per recruit,
        /// and walking the tracked list to answer it was quadratic in the hot path.
        /// </summary>
        public int BlipCount { get; private set; }

        /// <summary>Called by the Director once a blip has actually been created.</summary>
        public void NoteBlipAdded() { BlipCount++; }
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
            // outlived the peds they were attached to. A carried loot prop is the same problem
            // wearing a different hat.
            DeleteBlip(entry);
            DeleteLoot(entry);

            if (restore) { Restore(entry); }

            _tracked.Remove(entry);
            Unindex(entry);
        }

        /// <summary>
        /// Drops the handle mapping only when it still points at this entry.
        ///
        /// Handles are recycled: a dead rioter's handle can be reissued and re-recruited before
        /// the old row is pruned, at which point _byHandle points at the new entry. Removing
        /// blindly by handle unindexed the live one, so it was recruited a second time - two
        /// rows, two blips, conditioned and armed twice - and its car mates could no longer see
        /// it to share a faction with.
        /// </summary>
        private void Unindex(TrackedPed entry)
        {
            if (entry.Ped == null) { return; }

            TrackedPed indexed;
            if (_byHandle.TryGetValue(entry.Ped.Handle, out indexed) && indexed == entry)
            {
                _byHandle.Remove(entry.Ped.Handle);
            }
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
                DeleteLoot(entry);
                Unindex(entry);
                _tracked.RemoveAt(i);
                removed++;
            }
            return removed;
        }

        public void RestoreAll() { ReleaseAll(true); }

        /// <summary>
        /// Lets go of every tracked ped, restoring them or not.
        ///
        /// The tracking always ends either way. Leaving the list populated because
        /// riot.restoreWorldOnStop was off stranded every entry and its blip: the next mode
        /// found the registry already at maxTrackedPeds and never recruited anybody, while the
        /// stale rows pointed at relationship groups that had since been deleted.
        /// </summary>
        public void ReleaseAll(bool restore)
        {
            Log.Info((restore ? "Restoring " : "Releasing ") + _tracked.Count + " tracked ped(s).");

            foreach (TrackedPed entry in _tracked)
            {
                if (restore)
                {
                    Restore(entry);
                    continue;
                }

                // Not restoring still means our own additions come off: a blip or a prop we
                // created is ours whatever the player wants done with the ped underneath.
                DeleteBlip(entry);
                DeleteLoot(entry);
                entry.InPursuit = false;

                try
                {
                    if (entry.Ped != null && entry.Ped.Exists()) { entry.Ped.MarkAsNoLongerNeeded(); }
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to release a ped", ex);
                }
            }

            _tracked.Clear();
            _byHandle.Clear();
            BlipCount = 0;
        }

        private void Restore(TrackedPed entry)
        {
            DeleteBlip(entry);
            DeleteLoot(entry);
            entry.InPursuit = false;

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
                // A pursuit turns these on. Leaving CanLeaveVehicle off would strand a ped in
                // whatever car the chase ended in for the rest of the session.
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.CanLeaveVehicle, true);
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.CanDoDrivebys, false);
                Function.Call(Hash.SET_PED_KEEP_TASK, ped, false);
                // Hand ambient events back, or the ped stays deaf to the world after the riot.
                ped.BlockPermanentEvents = false;

                Function.Call(Hash.CLEAR_PED_TASKS_IMMEDIATELY, ped);
                ped.MarkAsNoLongerNeeded();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to restore a ped", ex);
            }
        }

        /// <summary>
        /// A loot prop is attached to the ped's hand. Detaching before deleting matters: the
        /// game does not always take kindly to an attachment vanishing out from under a bone.
        /// </summary>
        private static void DeleteLoot(TrackedPed entry)
        {
            try
            {
                if (entry.Loot != null && entry.Loot.Exists())
                {
                    Function.Call(Hash.DETACH_ENTITY, entry.Loot, true, true);
                    entry.Loot.Delete();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to delete a loot prop", ex);
            }
            entry.Loot = null;
            entry.Looting = false;
        }

        private void DeleteBlip(TrackedPed entry)
        {
            if (entry.Blip == null) { return; }

            try
            {
                if (entry.Blip.Exists()) { entry.Blip.Delete(); }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to delete a blip", ex);
            }

            entry.Blip = null;
            if (BlipCount > 0) { BlipCount--; }
        }
    }
}
