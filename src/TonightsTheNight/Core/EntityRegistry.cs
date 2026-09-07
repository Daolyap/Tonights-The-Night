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

        /// <summary>
        /// Vehicles this mod created. Nothing tracked them before, so a spawned troop carrier
        /// was owned by nobody: never released on stop, and - being non-persistent - reclaimed
        /// by the engine almost as soon as it arrived.
        /// </summary>
        private readonly List<Vehicle> _vehicles = new List<Vehicle>();
        private readonly Dictionary<int, TrackedPed> _byHandle = new Dictionary<int, TrackedPed>();

        public int Count { get { return _tracked.Count; } }

        public int VehicleCount { get { return _vehicles.Count; } }

        public void AddVehicle(Vehicle vehicle)
        {
            if (vehicle != null && vehicle.Exists() && !OwnsVehicle(vehicle)) { _vehicles.Add(vehicle); }
        }

        /// <summary>
        /// Whether this is one of ours.
        ///
        /// Owning a vehicle makes it a mission entity, and the chase and theft code both refuse
        /// mission entities - that guard is there to avoid touching story vehicles and ones
        /// another mod owns. Without this, taking ownership of a squad car meant its own crew
        /// could no longer chase you in it.
        /// </summary>
        public bool OwnsVehicle(Vehicle vehicle)
        {
            if (vehicle == null) { return false; }

            for (int i = 0; i < _vehicles.Count; i++)
            {
                if (_vehicles[i] != null && _vehicles[i].Handle == vehicle.Handle) { return true; }
            }

            return false;
        }

        /// <summary>
        /// Lets go of vehicles that are gone, wrecked, or further away than we care about.
        /// Persistent vehicles are ours to release; nothing else will do it.
        /// </summary>
        /// <summary>
        /// Lets go of vehicles we are finished with: gone, wrecked, empty and left behind, or
        /// far enough away not to matter. Also enforces a ceiling, because a vehicle nobody ever
        /// decides to release is a vehicle pinned in the pool for the session.
        /// </summary>
        public int PruneVehicles(GTA.Math.Vector3 origin, float cullDistance, float abandonDistance, int ceiling)
        {
            float cullSquared = cullDistance * cullDistance;
            float abandonSquared = abandonDistance * abandonDistance;
            int released = 0;

            for (int i = _vehicles.Count - 1; i >= 0; i--)
            {
                Vehicle vehicle = _vehicles[i];

                try
                {
                    if (vehicle == null || !vehicle.Exists())
                    {
                        _vehicles.RemoveAt(i);
                        continue;
                    }

                    float distance = origin.DistanceToSquared(vehicle.Position);

                    // A wreck is finished with wherever it is. Waiting for it to be 450m away
                    // means a burnt-out troop carrier stays pinned for as long as you stand
                    // near it.
                    bool wrecked = !Function.Call<bool>(Hash.IS_VEHICLE_DRIVEABLE, vehicle, false);

                    // An empty one has served its purpose: its crew got out or was killed.
                    bool abandoned = distance > abandonSquared && !HasLivingCrew(vehicle);

                    // Over the ceiling, the oldest go first - the list is in arrival order.
                    bool surplus = _vehicles.Count - released > ceiling;

                    if (!wrecked && !abandoned && !surplus && distance <= cullSquared) { continue; }

                    ReleaseVehicle(vehicle);
                    _vehicles.RemoveAt(i);
                    released++;
                }
                catch (Exception ex)
                {
                    // Release first: removing it from the list without letting go would leave it
                    // persistent with nothing left holding a reference to it.
                    Log.Error("Failed to prune a vehicle", ex);
                    ReleaseVehicle(vehicle);
                    _vehicles.RemoveAt(i);
                }
            }

            return released;
        }

        private bool HasLivingCrew(Vehicle vehicle)
        {
            foreach (Ped occupant in vehicle.Occupants)
            {
                if (occupant == null || !occupant.Exists() || occupant.IsDead) { continue; }
                if (Contains(occupant)) { return true; }
            }

            return false;
        }

        private static void ReleaseVehicle(Vehicle vehicle)
        {
            try
            {
                if (vehicle == null || !vehicle.Exists()) { return; }

                vehicle.IsPersistent = false;
                vehicle.MarkAsNoLongerNeeded();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to release a vehicle", ex);
            }
        }

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

            if (restore)
            {
                Restore(entry);
            }
            else
            {
                // The no-restore path is a corpse or a handle we have lost. Ownership still has
                // to go back either way: now that tracked peds are persistent, skipping this
                // would leave every body in the riot pinned in the pool for the session, which
                // is precisely the exhaustion the persistence was meant to prevent.
                Disown(entry);
            }

            _tracked.Remove(entry);
            Unindex(entry);
        }

        /// <summary>
        /// Releases our claim without touching anything else. Only when the entry still refers
        /// to the ped we took over - a recycled handle belongs to somebody else now.
        /// </summary>
        private static void Disown(TrackedPed entry)
        {
            try
            {
                if (!IsSameEntity(entry)) { return; }

                entry.Ped.IsPersistent = false;
                entry.Ped.MarkAsNoLongerNeeded();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to release a ped", ex);
            }
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

                // No Disown here: this branch is reached only when the entry no longer refers
                // to the ped we took over, and releasing a handle that now belongs to something
                // else would be releasing somebody else's ped.
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

            foreach (Vehicle vehicle in _vehicles) { ReleaseVehicle(vehicle); }

            _vehicles.Clear();
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
