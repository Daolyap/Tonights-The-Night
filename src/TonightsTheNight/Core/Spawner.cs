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
    /// Puts spawned factions on the street: soldiers, police waves, aliens, animals.
    ///
    /// Deliberately the secondary supply line. Converting ambient peds costs almost nothing
    /// because the engine has already paid to stream them; spawning is what exhausts the ped
    /// pool and crashes riot mods. So every spawning faction is capped individually, waves are
    /// timed rather than continuous, and arrivals are placed behind the player where possible so
    /// they walk into the scene instead of popping into view.
    /// </summary>
    public sealed class Spawner
    {
        private readonly ConfigStore _config;
        private readonly ModelResolver _models;
        private readonly Random _random;

        private readonly Dictionary<string, int> _nextWaveAt = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _aliveByFaction = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public Spawner(ConfigStore config, ModelResolver models, Random random)
        {
            _config = config;
            _models = models;
            _random = random;
        }

        public void Reset()
        {
            _nextWaveAt.Clear();
            _aliveByFaction.Clear();
        }

        public void NoteAliveCounts(IReadOnlyList<TrackedPed> tracked)
        {
            _aliveByFaction.Clear();
            foreach (TrackedPed entry in tracked)
            {
                if (!entry.Spawned) { continue; }

                int count;
                _aliveByFaction.TryGetValue(entry.Faction.Id, out count);
                _aliveByFaction[entry.Faction.Id] = count + 1;
            }
        }

        public bool WaveDue(Faction faction)
        {
            if (!faction.Spawn.Enabled) { return false; }
            if (faction.Spawn.Models.Count == 0) { return false; }

            int alive;
            _aliveByFaction.TryGetValue(faction.Id, out alive);
            if (alive >= faction.Spawn.MaxAlive) { return false; }

            int due;
            if (_nextWaveAt.TryGetValue(faction.Id, out due) && Game.GameTime < due) { return false; }

            return true;
        }

        /// <summary>
        /// Spawns one wave and returns what it created. The caller registers them, which keeps
        /// ownership of cleanup in one place.
        /// </summary>
        public List<Ped> SpawnWave(Faction faction, Vector3 anchor)
        {
            var spawned = new List<Ped>();
            SpawnProfile profile = faction.Spawn;

            _nextWaveAt[faction.Id] = Game.GameTime + profile.WaveIntervalMs;

            Model pedModel;
            if (!_models.TryResolve(profile.Models, out pedModel) || !_models.Load(pedModel))
            {
                Log.Warn("Faction '" + faction.Id + "' has no usable ped model. Skipping its wave.");
                return spawned;
            }

            bool byVehicle = profile.Vehicles.Count > 0 && _random.NextDouble() < profile.InVehicleChance;

            if (byVehicle)
            {
                for (int i = 0; i < profile.VehiclesPerWave; i++)
                {
                    SpawnVehicleWave(faction, anchor, pedModel, spawned);
                }

                // Every vehicle failed to place - arrive on foot rather than not at all, unless
                // arriving on foot would be absurd for this faction.
                if (spawned.Count == 0 && profile.FootFallback)
                {
                    SpawnFootWave(faction, anchor, pedModel, spawned);
                }
            }
            else
            {
                SpawnFootWave(faction, anchor, pedModel, spawned);
            }

            return spawned;
        }

        private void SpawnFootWave(Faction faction, Vector3 anchor, Model model, List<Ped> spawned)
        {
            for (int i = 0; i < faction.Spawn.PerWave; i++)
            {
                Vector3 point = PickPoint(anchor, faction.Spawn, false);
                if (point == Vector3.Zero) { continue; }

                Ped ped = World.CreatePed(model, point);
                if (ped == null || !ped.Exists()) { continue; }

                Prepare(ped, faction);
                spawned.Add(ped);
            }
        }

        private void SpawnVehicleWave(Faction faction, Vector3 anchor, Model pedModel, List<Ped> spawned)
        {
            Model vehicleModel;
            if (!_models.TryResolve(faction.Spawn.Vehicles, out vehicleModel) || !_models.Load(vehicleModel))
            {
                // None of the declared vehicles are installed. Walking in beats not arriving,
                // except where walking in makes no sense.
                if (faction.Spawn.FootFallback) { SpawnFootWave(faction, anchor, pedModel, spawned); }
                return;
            }

            bool air = string.Equals(faction.Spawn.VehicleType, "air", StringComparison.OrdinalIgnoreCase);
            bool water = string.Equals(faction.Spawn.VehicleType, "water", StringComparison.OrdinalIgnoreCase);

            Vector3 point = air ? PickAirPoint(anchor, faction.Spawn)
                          : water ? PickWaterPoint(anchor, faction.Spawn)
                          : PickPoint(anchor, faction.Spawn, true);

            // No river within reach is the normal case for most of Los Santos, so a failed water
            // spawn is a shrug rather than a problem.
            if (point == Vector3.Zero) { return; }

            Vehicle vehicle = World.CreateVehicle(vehicleModel, point, (float)(_random.NextDouble() * 360.0));
            if (vehicle == null || !vehicle.Exists()) { return; }

            vehicle.IsPersistent = false;
            Function.Call(Hash.SET_VEHICLE_ENGINE_ON, vehicle, true, true, false);

            if (air)
            {
                // Otherwise it spawns with the rotors stopped and drops out of the sky.
                Function.Call(Hash.SET_HELI_BLADES_FULL_SPEED, vehicle);
            }

            if (faction.Spawn.Siren)
            {
                Function.Call(Hash.SET_VEHICLE_SIREN, vehicle, true);
            }

            int seats = Math.Max(1, Math.Min(faction.Spawn.Occupants,
                Function.Call<int>(Hash.GET_VEHICLE_MAX_NUMBER_OF_PASSENGERS, vehicle) + 1));

            for (int seat = 0; seat < seats; seat++)
            {
                Ped ped = World.CreatePed(pedModel, point);
                if (ped == null || !ped.Exists()) { continue; }

                Prepare(ped, faction);
                // -1 is the driver's seat; passengers count up from 0.
                Function.Call(Hash.SET_PED_INTO_VEHICLE, ped, vehicle, seat == 0 ? -1 : seat - 1);
                spawned.Add(ped);
            }

            if (spawned.Count == 0)
            {
                vehicle.Delete();
            }
        }

        private static void Prepare(Ped ped, Faction faction)
        {
            ped.IsPersistent = false;
            // Otherwise ambient events (a car horn, a nearby scream) pull spawned peds out of
            // whatever we tasked them with, and a squad wanders off mid-deployment.
            ped.BlockPermanentEvents = true;
            Function.Call(Hash.SET_PED_DROPS_WEAPONS_WHEN_DEAD, ped, false);

            // A ped created without this keeps component 0 in every slot, which for some models
            // is half an outfit and for others an untextured black figure. It is why the alien
            // invasion arrived as a man in black with part of a suit on.
            try
            {
                if (string.Equals(faction.Outfit, "random", StringComparison.OrdinalIgnoreCase))
                {
                    Function.Call(Hash.SET_PED_RANDOM_COMPONENT_VARIATION, ped, 0);
                }
                else
                {
                    Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, ped);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not set component variation on a " + faction.Id + " ped", ex);
            }
        }

        /// <summary>
        /// Above and to one side of the riot. Helicopters need clear air rather than navmesh, so
        /// this skips the ground checks entirely.
        /// </summary>
        private Vector3 PickAirPoint(Vector3 anchor, SpawnProfile profile)
        {
            double angle = _random.NextDouble() * Math.PI * 2.0;
            float distance = profile.MinDistance +
                             (float)_random.NextDouble() * Math.Max(1f, profile.MaxDistance - profile.MinDistance);

            return new Vector3(
                anchor.X + (float)Math.Cos(angle) * distance,
                anchor.Y + (float)Math.Sin(angle) * distance,
                anchor.Z + profile.FlightHeight);
        }

        /// <summary>
        /// A point on actual water, or nowhere. Most of Los Santos is not near any, so callers
        /// treat failure as "no boats this wave" rather than as an error.
        /// </summary>
        private Vector3 PickWaterPoint(Vector3 anchor, SpawnProfile profile)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                double angle = _random.NextDouble() * Math.PI * 2.0;
                float distance = profile.MinDistance +
                                 (float)_random.NextDouble() * Math.Max(1f, profile.MaxDistance - profile.MinDistance);

                float x = anchor.X + (float)Math.Cos(angle) * distance;
                float y = anchor.Y + (float)Math.Sin(angle) * distance;

                var height = new OutputArgument();
                if (!Function.Call<bool>(Hash.GET_WATER_HEIGHT, x, y, anchor.Z, height)) { continue; }

                float surface = height.GetResult<float>();
                if (surface <= 0f) { continue; }

                return new Vector3(x, y, surface);
            }

            return Vector3.Zero;
        }

        /// <summary>
        /// A point at a plausible distance, on a road for vehicles and on navmesh for people.
        /// Vector3.Zero means "nowhere sensible", which callers treat as skip-this-one.
        /// </summary>
        private Vector3 PickPoint(Vector3 anchor, SpawnProfile profile, bool onRoad)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                double angle = _random.NextDouble() * Math.PI * 2.0;
                float distance = profile.MinDistance +
                                 (float)_random.NextDouble() * Math.Max(1f, profile.MaxDistance - profile.MinDistance);

                var candidate = new Vector3(
                    anchor.X + (float)Math.Cos(angle) * distance,
                    anchor.Y + (float)Math.Sin(angle) * distance,
                    anchor.Z);

                Vector3 placed = onRoad
                    ? World.GetNextPositionOnStreet(candidate)
                    : World.GetSafeCoordForPed(candidate);

                if (placed != Vector3.Zero) { return placed; }
            }

            return Vector3.Zero;
        }
    }
}
