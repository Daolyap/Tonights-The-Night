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
        private readonly Reinforcements _reinforcements;

        private readonly Dictionary<string, int> _nextWaveAt = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _aliveByFaction = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public Spawner(ConfigStore config, ModelResolver models, Random random, Reinforcements reinforcements)
        {
            _config = config;
            _models = models;
            _random = random;
            _reinforcements = reinforcements;
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
            if (alive >= CapFor(faction)) { return false; }

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

            // Losses shorten the gap between waves as well as widening them.
            float commitment = _reinforcements.Commitment(faction);
            _nextWaveAt[faction.Id] = Game.GameTime + (int)(profile.WaveIntervalMs / commitment);

            Model pedModel;
            if (!_models.TryResolve(profile.Models, out pedModel) || !_models.Load(pedModel))
            {
                Log.Warn("Faction '" + faction.Id + "' has no usable ped model. Skipping its wave.");
                return spawned;
            }

            bool byVehicle = profile.Vehicles.Count > 0 && _random.NextDouble() < profile.InVehicleChance;

            if (byVehicle)
            {
                int vehicles = Math.Max(1, (int)Math.Round(profile.VehiclesPerWave * commitment));

                for (int i = 0; i < vehicles; i++)
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

        /// <summary>How many arrive in one wave, after casualties are taken into account.</summary>
        private int WaveSizeFor(Faction faction)
        {
            float commitment = _reinforcements.Commitment(faction);
            int size = (int)Math.Round(faction.Spawn.PerWave * commitment);
            return size < 1 ? 1 : size;
        }

        /// <summary>
        /// The ceiling on how many of this faction can be alive at once. It rises with losses
        /// too, or a faction being wiped out fast would send bigger waves into the same cap and
        /// nothing would visibly change.
        /// </summary>
        private int CapFor(Faction faction)
        {
            float commitment = _reinforcements.Commitment(faction);
            return (int)Math.Round(faction.Spawn.MaxAlive * commitment);
        }

        private void SpawnFootWave(Faction faction, Vector3 anchor, Model model, List<Ped> spawned)
        {
            int size = WaveSizeFor(faction);

            for (int i = 0; i < size; i++)
            {
                SpawnPoint point = PickPoint(anchor, faction.Spawn, false);
                if (!point.Valid) { continue; }

                Ped ped = World.CreatePed(model, point.Position);
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
                // No fallback here. This runs once per vehicle in the wave, so falling back to
                // foot inside it spawned a full foot wave per vehicle - double or triple the
                // declared size. SpawnWave's own "nothing arrived" branch handles it once.
                return;
            }

            bool air = string.Equals(faction.Spawn.VehicleType, "air", StringComparison.OrdinalIgnoreCase);
            bool water = string.Equals(faction.Spawn.VehicleType, "water", StringComparison.OrdinalIgnoreCase);

            SpawnPoint point = air ? PickAirPoint(anchor, faction.Spawn)
                             : water ? PickWaterPoint(anchor, faction.Spawn)
                             : PickPoint(anchor, faction.Spawn, true);

            // No river within reach is the normal case for most of Los Santos, so a failed water
            // spawn is a shrug rather than a problem.
            if (!point.Valid) { return; }

            // The road's own heading, not a random one. A random heading is how cars ended up
            // across the carriageway facing a wall.
            Vehicle vehicle = World.CreateVehicle(vehicleModel, point.Position, point.Heading);
            if (vehicle == null || !vehicle.Exists()) { return; }

            // Where this vehicle's own occupants start in the shared list, so an empty vehicle
            // can be told apart from one whose crew simply came after somebody else's.
            int firstSeat = spawned.Count;

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
                Ped ped = World.CreatePed(pedModel, point.Position);
                if (ped == null || !ped.Exists()) { continue; }

                Prepare(ped, faction);
                // -1 is the driver's seat; passengers count up from 0.
                Function.Call(Hash.SET_PED_INTO_VEHICLE, ped, vehicle, seat == 0 ? -1 : seat - 1);
                spawned.Add(ped);
            }

            if (spawned.Count == firstSeat)
            {
                // Nobody made it in - the ped pool is spent. Testing the shared list instead
                // left the second and later vehicles of a wave abandoned in the street with
                // their engines running and sirens on, registered nowhere and cleaned up by
                // nothing.
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
        private SpawnPoint PickAirPoint(Vector3 anchor, SpawnProfile profile)
        {
            double angle = _random.NextDouble() * Math.PI * 2.0;
            float distance = profile.MinDistance +
                             (float)_random.NextDouble() * Math.Max(1f, profile.MaxDistance - profile.MinDistance);

            return new SpawnPoint
            {
                Position = new Vector3(
                    anchor.X + (float)Math.Cos(angle) * distance,
                    anchor.Y + (float)Math.Sin(angle) * distance,
                    anchor.Z + profile.FlightHeight),
                // Nose pointed at the riot, so it flies in rather than away from it.
                Heading = (float)((angle * 180.0 / Math.PI) + 180.0)
            };
        }

        /// <summary>
        /// A point on actual water, or nowhere. Most of Los Santos is not near any, so callers
        /// treat failure as "no boats this wave" rather than as an error.
        /// </summary>
        private SpawnPoint PickWaterPoint(Vector3 anchor, SpawnProfile profile)
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

                return new SpawnPoint
                {
                    Position = new Vector3(x, y, surface),
                    Heading = (float)(_random.NextDouble() * 360.0)
                };
            }

            return SpawnPoint.None;
        }
        /// <summary>Where and which way round something arrives.</summary>
        private struct SpawnPoint
        {
            public Vector3 Position;
            public float Heading;

            public bool Valid { get { return Position != Vector3.Zero; } }

            public static SpawnPoint None { get { return new SpawnPoint(); } }
        }

        /// <summary>
        /// A place to arrive from, chosen the way the game's own dispatch does it: on a road,
        /// pointing along the road, and out of sight.
        ///
        /// The previous version picked a bearing and a distance and dropped whatever it was
        /// making straight onto it. That put units in the middle of the street in front of you,
        /// and gave every vehicle a random heading - so cars materialised sideways across the
        /// carriageway facing a wall, which is most of why arrivals never looked like arrivals.
        /// </summary>
        private SpawnPoint PickPoint(Vector3 anchor, SpawnProfile profile, bool onRoad)
        {
            int attempts = Math.Max(1, _config.GetInt("spawn.attempts", 14));
            bool offscreen = _config.GetBool("spawn.offscreenOnly", true);
            float visibility = _config.GetFloat("spawn.visibilityRadius", 4f);

            // A faction with vehicles has distances written for driving in. Somebody arriving on
            // foot at the same range would still be walking when the riot ended, so those - and
            // only those - are brought in closer.
            float scale = !onRoad && profile.Vehicles.Count > 0
                ? _config.GetFloat("spawn.footDistanceFactor", 0.45f)
                : 1f;

            float minDistance = profile.MinDistance * scale;
            float span = Math.Max(1f, profile.MaxDistance * scale - minDistance);

            SpawnPoint fallback = SpawnPoint.None;

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                double angle = _random.NextDouble() * Math.PI * 2.0;
                float distance = minDistance + (float)_random.NextDouble() * span;

                var candidate = new Vector3(
                    anchor.X + (float)Math.Cos(angle) * distance,
                    anchor.Y + (float)Math.Sin(angle) * distance,
                    anchor.Z);

                SpawnPoint placed = onRoad ? OnRoad(candidate) : OnFoot(candidate);
                if (!placed.Valid) { continue; }

                // Keep the first workable point whatever happens: somewhere visible beats
                // nowhere at all, and on an open hillside every point is visible.
                if (!fallback.Valid) { fallback = placed; }

                if (!offscreen) { return placed; }

                if (!Function.Call<bool>(Hash.IS_SPHERE_VISIBLE,
                        placed.Position.X, placed.Position.Y, placed.Position.Z, visibility))
                {
                    return placed;
                }
            }

            return fallback;
        }

        /// <summary>
        /// The nearest road node and the direction traffic runs on it, so a car arrives facing
        /// down its own lane rather than across it.
        /// </summary>
        private static SpawnPoint OnRoad(Vector3 candidate)
        {
            try
            {
                var position = new OutputArgument();
                var heading = new OutputArgument();

                bool found = Function.Call<bool>(Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING,
                    candidate.X, candidate.Y, candidate.Z, position, heading, 1, 3f, 0);

                if (found)
                {
                    return new SpawnPoint
                    {
                        Position = position.GetResult<Vector3>(),
                        Heading = heading.GetResult<float>()
                    };
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not query a road node", ex);
            }

            // No node within reach - the old behaviour rather than not arriving at all.
            Vector3 street = World.GetNextPositionOnStreet(candidate);
            return street == Vector3.Zero
                ? SpawnPoint.None
                : new SpawnPoint { Position = street, Heading = 0f };
        }

        private static SpawnPoint OnFoot(Vector3 candidate)
        {
            Vector3 safe = World.GetSafeCoordForPed(candidate);
            return safe == Vector3.Zero
                ? SpawnPoint.None
                : new SpawnPoint { Position = safe, Heading = 0f };
        }
    }
}
