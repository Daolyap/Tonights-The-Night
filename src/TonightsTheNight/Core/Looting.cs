using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using TonightsTheNight.Config;
using TonightsTheNight.Factions;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// One person taking advantage of the situation.
    /// </summary>
    public sealed class LootJob
    {
        public TrackedPed Entry;
        public Vehicle Vehicle;
        public int EndsAt;
        public bool Driving;
    }

    /// <summary>
    /// Looting: the thing a riot does when it stops being about anything.
    ///
    /// GTA has no shop interiors to break into, so this is built from what the engine does have
    /// and does well — a prop in someone's hands and somewhere else to be. A man running down
    /// the middle of Vespucci Boulevard with a television reads as looting instantly; anything
    /// more literal would need interiors the game will not give us.
    ///
    /// Gated on the escalation phase, so it starts when the riot has been going a while rather
    /// than in the first thirty seconds, which is roughly how it goes.
    /// </summary>
    public sealed class Looting
    {
        private readonly ConfigStore _config;
        private readonly Random _random;
        private readonly EntityRegistry _registry;

        /// <summary>
        /// What they carry and how they hold it. A separate class because "which props" and
        /// "how does one sit in a hand" turned out to be the whole difference between looting
        /// that reads and looting that is funny for the wrong reason.
        /// </summary>
        private readonly CarryProps _carry;

        private readonly List<LootJob> _jobs = new List<LootJob>();
        private int _nextJobAt;
        private int _nextPassAt;

        public int Active { get { return _jobs.Count; } }
        public int Total { get; private set; }

        public Looting(ConfigStore config, ModelResolver models, Random random, EntityRegistry registry)
        {
            _config = config;
            _random = random;
            _registry = registry;
            _carry = new CarryProps(config, models, random);
        }

        public void Reset()
        {
            EndAll();
            _carry.Reset();
            Total = 0;
            _nextJobAt = 0;
            _nextPassAt = 0;
        }

        public void EndAll()
        {
            for (int i = _jobs.Count - 1; i >= 0; i--)
            {
                End(_jobs[i]);
            }
            _jobs.Clear();
        }

        /// <summary>
        /// <paramref name="allowed"/> is the current phase's looting flag, so a mode decides
        /// when the shops start going rather than this class guessing.
        /// </summary>
        public void Update(IReadOnlyList<TrackedPed> tracked, bool allowed)
        {
            if (!_config.GetBool("features.looting.enabled", true) || !allowed)
            {
                if (_jobs.Count > 0) { EndAll(); }
                return;
            }

            // Flavour, not physics. Four times a second is plenty to notice a job finishing or
            // a car thief finally getting a door open.
            if (Game.GameTime < _nextPassAt) { return; }
            _nextPassAt = Game.GameTime + 250;

            for (int i = _jobs.Count - 1; i >= 0; i--)
            {
                if (!Advance(_jobs[i])) { _jobs.RemoveAt(i); }
            }

            if (Game.GameTime < _nextJobAt) { return; }
            _nextJobAt = Game.GameTime + _config.GetInt("features.looting.intervalMs", 6000);

            if (_jobs.Count >= _config.GetInt("features.looting.maxActive", 6)) { return; }

            TrackedPed candidate = PickCandidate(tracked);
            if (candidate == null) { return; }

            Begin(candidate);
        }

        /// <summary>
        /// A random tracked ped near the player, sampled rather than sorted: looting is a
        /// flavour system and does not deserve a pass over the whole list every few seconds.
        /// </summary>
        private TrackedPed PickCandidate(IReadOnlyList<TrackedPed> tracked)
        {
            if (tracked.Count == 0) { return null; }

            Vector3 origin = Game.Player.Character.Position;
            float radius = _config.GetFloat("features.looting.radius", 90f);
            float radiusSquared = radius * radius;

            int start = _random.Next(tracked.Count);

            for (int offset = 0; offset < tracked.Count && offset < 24; offset++)
            {
                TrackedPed entry = tracked[(start + offset) % tracked.Count];

                if (entry.InPursuit || entry.Looting || !entry.IsUsable) { continue; }
                if (entry.Ped.IsInVehicle()) { continue; }

                // Fighters are busy. Looting is what the people who were never going to fight
                // do instead, which is also what keeps it from thinning out the riot.
                if (entry.Reaction == Reaction.Fight &&
                    _random.NextDouble() > _config.GetFloat("features.looting.fighterChance", 0.2f))
                {
                    continue;
                }

                if (origin.DistanceToSquared(entry.Ped.Position) > radiusSquared) { continue; }

                return entry;
            }

            return null;
        }

        private void Begin(TrackedPed entry)
        {
            var job = new LootJob
            {
                Entry = entry,
                EndsAt = Game.GameTime + _config.GetInt("features.looting.jobSeconds", 40) * 1000
            };

            bool stole = _config.GetBool("features.looting.stealVehicles", true) &&
                         _random.NextDouble() < _config.GetFloat("features.looting.vehicleChance", 0.25f) &&
                         StartVehicleTheft(job);

            if (!stole && !StartCarry(job)) { return; }

            entry.Looting = true;
            _jobs.Add(job);
            Total++;
        }

        /// <summary>Prop in hand, and somewhere else to be.</summary>
        private bool StartCarry(LootJob job)
        {
            Ped ped = job.Entry.Ped;

            Prop prop = null;

            try
            {
                // Never over the top of one already in hand: the old prop would be orphaned, and
                // a persistent orphan is one the engine can no longer take back.
                if (job.Entry.Loot == null) { prop = _carry.GiveTo(ped); }

                if (prop != null) { job.Entry.Loot = prop; }

                // Somewhere to be, far enough that they visibly leave with it. The bare ground
                // will do where the navmesh will not - out of town it usually will not.
                Vector3 away = Ground.Place(ped.Position.Around(
                    _config.GetFloat("features.looting.runDistance", 70f)));

                Function.Call(Hash.SET_PED_KEEP_TASK, ped, true);

                if (away != Vector3.Zero)
                {
                    Function.Call(Hash.TASK_GO_STRAIGHT_TO_COORD, ped, away.X, away.Y, away.Z,
                        2f, job.EndsAt - Game.GameTime, 0f, 0f);
                }
                else
                {
                    Function.Call(Hash.TASK_WANDER_STANDARD, ped, 10f, 10);
                }

                // After the movement task, not before: the carry animation is an upper-body
                // secondary, so it layers over the walk. Issued first, the walk replaces it.
                _carry.PlayCarryAnimation(ped, job.Entry.Loot);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Could not start a looting job", ex);

                // Whatever was created before the throw is ours and nothing else will free it.
                try
                {
                    if (prop != null && prop.Exists()) { prop.Delete(); }
                }
                catch (Exception) { }

                job.Entry.Loot = null;
                return false;
            }
        }

        /// <summary>Stealing a car is looting too, and the game already does it well.</summary>
        private bool StartVehicleTheft(LootJob job)
        {
            Ped ped = job.Entry.Ped;

            Vehicle target = null;
            float best = float.MaxValue;

            foreach (Vehicle vehicle in World.GetNearbyVehicles(ped.Position, _config.GetFloat("features.looting.vehicleSearchRadius", 30f)))
            {
                if (vehicle == null || !vehicle.Exists()) { continue; }
                if (!Function.Call<bool>(Hash.IS_VEHICLE_DRIVEABLE, vehicle, false)) { continue; }
                // A vehicle one of our own factions arrived in is fair game; somebody else's
                // mission vehicle is not.
                if (!_registry.OwnsVehicle(vehicle) &&
                    Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, vehicle)) { continue; }
                // Empty cars only: hauling a stranger out can reach a mission ped, or one
                // another mod owns.
                if (!Function.Call<bool>(Hash.IS_VEHICLE_SEAT_FREE, vehicle, -1)) { continue; }

                Vehicle ride = Game.Player.Character.CurrentVehicle;
                if (ride != null && ride.Exists() && ride.Handle == vehicle.Handle) { continue; }

                float distance = ped.Position.DistanceToSquared(vehicle.Position);
                if (distance >= best) { continue; }

                best = distance;
                target = vehicle;
            }

            if (target == null) { return false; }

            try
            {
                job.Vehicle = target;
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.CanUseVehicles, true);
                Function.Call(Hash.SET_PED_KEEP_TASK, ped, true);
                Function.Call(Hash.TASK_ENTER_VEHICLE, ped, target, 20000, -1, 2f, 1, 0);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Could not start a vehicle theft", ex);
                return false;
            }
        }

        private bool Advance(LootJob job)
        {
            if (!job.Entry.IsUsable || !EntityRegistry.IsSameEntity(job.Entry))
            {
                End(job);
                return false;
            }

            if (Game.GameTime > job.EndsAt)
            {
                End(job);
                return false;
            }

            // Second stage of a theft: they are in, so now they drive off with it.
            if (job.Vehicle != null && !job.Driving && job.Entry.Ped.CurrentVehicle != null &&
                job.Entry.Ped.CurrentVehicle.Handle == job.Vehicle.Handle)
            {
                job.Driving = true;
                try
                {
                    Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, job.Entry.Ped, job.Vehicle,
                        _config.GetFloat("features.looting.getawaySpeed", 25f),
                        _config.GetInt("features.looting.drivingStyle", 786603));
                }
                catch (Exception ex)
                {
                    Log.Error("Could not send a looter away in a car", ex);
                }
            }

            return true;
        }

        private void End(LootJob job)
        {
            TrackedPed entry = job.Entry;
            entry.Looting = false;

            try
            {
                if (entry.Loot != null && entry.Loot.Exists())
                {
                    // Detach first, or a deleted attachment can take the ped's arm with it.
                    Function.Call(Hash.DETACH_ENTITY, entry.Loot, true, true);
                    entry.Loot.Delete();

                    // Still holding an invisible television otherwise.
                    if (entry.IsUsable) { _carry.StopCarryAnimation(entry.Ped); }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not clean up a loot prop", ex);
            }

            entry.Loot = null;

            try
            {
                if (entry.IsUsable)
                {
                    Function.Call(Hash.SET_PED_KEEP_TASK, entry.Ped, false);
                    // Due for an ordinary riot task on the Director's next pass over it.
                    entry.LastTaskedAt = 0;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not release a looter", ex);
            }
        }

    }
}
