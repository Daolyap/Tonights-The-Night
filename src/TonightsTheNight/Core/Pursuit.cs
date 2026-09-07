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
    /// <summary>One carload of people who have decided you are not driving away from that.</summary>
    public sealed class Chase
    {
        public Faction Faction;
        public Vehicle Vehicle;
        public readonly List<TrackedPed> Crew = new List<TrackedPed>();
        public Blip Blip;

        public int StartedAt;
        /// <summary>Last time the player was within giving-up range. Resets the escape timer.</summary>
        public int LastCloseAt;
        public int NextTaskAt;

        /// <summary>Deadline for the crew to reach the car on foot before we warp them in.</summary>
        public int BoardBy;
        public bool Driving;

        /// <summary>True when we took a car that was sitting there, so it is ours to release.</summary>
        public bool Commandeered;
    }

    /// <summary>
    /// Car chases.
    ///
    /// The trigger is provocation, not proximity: shoot someone, run someone over, and then try
    /// to drive away, and their side comes after you. That is the whole design goal — the riot
    /// should not let you commit to something and then simply leave.
    ///
    /// The part that makes it read as a mob rather than as traffic is that a chase is a
    /// *carload*. The nearest angry ped becomes the driver and their nearest allies run to the
    /// same car and get in, so what appears in your mirror is four people from one faction in
    /// one vehicle, leaning out of the windows. They pile in on foot where there is time for it
    /// and get warped in when there is not, because a chase that never starts is worse than one
    /// that starts slightly too neatly.
    ///
    /// No part of this touches the wanted system. A pursuit is peds, a vehicle and tasks — so a
    /// police or wanted overhaul carries on underneath it, and you can be chased by a mob while
    /// separately having four stars.
    /// </summary>
    public sealed class Pursuit
    {
        /// <summary>FIRING_PATTERN_FULL_AUTO. Drive-bys look wrong with single shots.</summary>
        private const uint FullAuto = 0xC6EE6B4C;

        private readonly ConfigStore _config;
        private readonly Random _random;

        private readonly List<Chase> _chases = new List<Chase>();

        /// <summary>Faction id to the game time its grudge expires.</summary>
        private readonly Dictionary<string, int> _grudges = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private int _nextUpdateAt;
        private int _nextFormAt;

        public int ActiveChases { get { return _chases.Count; } }
        public int Started { get; private set; }

        public bool Enabled { get { return _config.GetBool("features.pursuit.enabled", true); } }

        public Pursuit(ConfigStore config, Random random)
        {
            _config = config;
            _random = random;
        }

        /// <summary>
        /// Records that the player has done something to this faction. Called from the
        /// Director's round-robin, so it costs one native per ped it already had in hand.
        /// </summary>
        public void NoteProvoked(Faction faction)
        {
            if (!Enabled || faction == null) { return; }

            int seconds = _config.GetInt("features.pursuit.grudgeSeconds", 45);
            _grudges[faction.Id] = Game.GameTime + seconds * 1000;
        }

        public void Reset()
        {
            EndAll();
            _grudges.Clear();
            Started = 0;
            _nextUpdateAt = 0;
            _nextFormAt = 0;
        }

        public void EndAll()
        {
            for (int i = _chases.Count - 1; i >= 0; i--)
            {
                End(_chases[i], "riot stopped");
            }
            _chases.Clear();
        }

        /// <summary>
        /// <paramref name="playerVisible"/> gates new chases only. One already under way keeps
        /// going on its own give-up rules — a carload behind you does not forget where you are
        /// because you turned a corner.
        /// </summary>
        public void Update(IReadOnlyList<TrackedPed> tracked, bool playerVisible)
        {
            if (!Enabled)
            {
                if (_chases.Count > 0) { EndAll(); }
                return;
            }

            if (Game.GameTime < _nextUpdateAt) { return; }
            _nextUpdateAt = Game.GameTime + _config.GetInt("features.pursuit.updateIntervalMs", 500);

            ExpireGrudges();

            for (int i = _chases.Count - 1; i >= 0; i--)
            {
                if (!Advance(_chases[i])) { _chases.RemoveAt(i); }
            }

            if (playerVisible) { TryForm(tracked); }
        }

        private void ExpireGrudges()
        {
            if (_grudges.Count == 0) { return; }

            List<string> stale = null;
            foreach (var pair in _grudges)
            {
                if (Game.GameTime > pair.Value)
                {
                    if (stale == null) { stale = new List<string>(); }
                    stale.Add(pair.Key);
                }
            }

            if (stale == null) { return; }
            foreach (string id in stale) { _grudges.Remove(id); }
        }

        /// <summary>
        /// Starts a chase when the player has wronged somebody and is driving away from it.
        /// </summary>
        private void TryForm(IReadOnlyList<TrackedPed> tracked)
        {
            if (_grudges.Count == 0) { return; }
            if (_chases.Count >= _config.GetInt("features.pursuit.maxChases", 2)) { return; }
            if (Game.GameTime < _nextFormAt) { return; }

            Ped player = Game.Player.Character;
            Vehicle ride = player.CurrentVehicle;

            // On foot they simply fight you; a chase needs something to chase.
            if (ride == null || !ride.Exists()) { return; }

            // "Tries to drive off" is the actual condition. Sitting still in a car surrounded by
            // people you just shot should get you dragged out of it, not tailed.
            float minSpeed = _config.GetFloat("features.pursuit.minPlayerSpeed", 6f);
            if (ride.Speed < minSpeed) { return; }

            TrackedPed lead = PickLead(tracked, player);
            if (lead == null) { return; }

            Chase chase = Form(lead, tracked, player);
            if (chase == null)
            {
                // Nothing to drive nearby. Wait a little before looking again rather than
                // sweeping the vehicle list every update.
                _nextFormAt = Game.GameTime + 4000;
                return;
            }

            _chases.Add(chase);
            Started++;
            _grudges.Remove(chase.Faction.Id);
            _nextFormAt = Game.GameTime + _config.GetInt("features.pursuit.cooldownMs", 12000);

            Log.Info("Pursuit " + Started + ": " + chase.Crew.Count + " from '" + chase.Faction.Id +
                     "' in a " + chase.Vehicle.DisplayName + (chase.Commandeered ? " (commandeered)." : " (already theirs)."));

            if (_config.GetBool("features.pursuit.notify", true))
            {
                GTA.UI.Notification.Show("~r~" + chase.Faction.DisplayName + "~s~ are coming after you.");
            }
        }

        /// <summary>
        /// The angriest available body: a fighter of a grudged faction, nearest to the player,
        /// not already in a chase.
        /// </summary>
        private TrackedPed PickLead(IReadOnlyList<TrackedPed> tracked, Ped player)
        {
            float radius = _config.GetFloat("features.pursuit.formRadius", 120f);
            float best = radius * radius;
            TrackedPed lead = null;

            foreach (TrackedPed entry in tracked)
            {
                if (entry.InPursuit || entry.Looting || !entry.IsUsable) { continue; }
                if (entry.Reaction != Reaction.Fight) { continue; }
                if (!CanDrive(entry.Ped)) { continue; }
                if (!_grudges.ContainsKey(entry.Faction.Id)) { continue; }

                float distance = player.Position.DistanceToSquared(entry.Ped.Position);
                if (distance >= best) { continue; }

                best = distance;
                lead = entry;
            }

            return lead;
        }

        /// <summary>
        /// Animals cannot drive, and the boarding fallback would have warped one into the
        /// driver's seat rather than admitting it. A coyote at the wheel of a saloon is funny
        /// exactly once.
        /// </summary>
        private static bool CanDrive(Ped ped)
        {
            try
            {
                return Function.Call<bool>(Hash.IS_PED_HUMAN, ped);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private Chase Form(TrackedPed lead, IReadOnlyList<TrackedPed> tracked, Ped player)
        {
            bool commandeered;
            Vehicle vehicle = FindRide(lead, player, out commandeered);
            if (vehicle == null) { return null; }

            var chase = new Chase
            {
                Faction = lead.Faction,
                Vehicle = vehicle,
                Commandeered = commandeered,
                StartedAt = Game.GameTime,
                LastCloseAt = Game.GameTime,
                BoardBy = Game.GameTime + _config.GetInt("features.pursuit.boardTimeoutMs", 7000)
            };

            AddToCrew(chase, lead, -1);

            // The rest of the carload. This is the bit that makes it a mob: allies near the
            // driver run to the same car rather than each starting a chase of their own.
            int crewSize = Math.Max(1, _config.GetInt("features.pursuit.crewSize", 3));
            int seats = Function.Call<int>(Hash.GET_VEHICLE_MAX_NUMBER_OF_PASSENGERS, vehicle);
            float gatherRadius = _config.GetFloat("features.pursuit.gatherRadius", 35f);
            float gatherSquared = gatherRadius * gatherRadius;

            for (int seat = 0; seat < seats && chase.Crew.Count < crewSize; seat++)
            {
                if (!Function.Call<bool>(Hash.IS_VEHICLE_SEAT_FREE, vehicle, seat)) { continue; }

                TrackedPed mate = NearestFreeAlly(tracked, chase, lead.Ped.Position, gatherSquared);
                if (mate == null) { break; }

                AddToCrew(chase, mate, seat);
            }

            Function.Call(Hash.SET_VEHICLE_ENGINE_ON, vehicle, true, true, false);
            if (commandeered) { vehicle.IsPersistent = true; }

            AttachBlip(chase);
            return chase;
        }

        /// <summary>
        /// Prefers a car the faction is already sitting in, which is both free and the most
        /// natural-looking start. Otherwise the nearest parked car that is not yours.
        /// </summary>
        private Vehicle FindRide(TrackedPed lead, Ped player, out bool commandeered)
        {
            commandeered = false;

            Vehicle own = lead.Ped.CurrentVehicle;
            if (own != null && own.Exists() && own.Driver == lead.Ped && IsUsableRide(own, player))
            {
                return own;
            }

            float radius = _config.GetFloat("features.pursuit.vehicleSearchRadius", 45f);
            Vehicle[] nearby = World.GetNearbyVehicles(lead.Ped.Position, radius);

            Vehicle best = null;
            float bestDistance = float.MaxValue;

            foreach (Vehicle vehicle in nearby)
            {
                if (!IsUsableRide(vehicle, player)) { continue; }

                // An occupied car would mean dragging a stranger out, which can hit a mission
                // ped or one another mod owns. Empty cars only.
                if (vehicle.Driver != null && vehicle.Driver.Exists() && vehicle.Driver != lead.Ped) { continue; }
                if (!Function.Call<bool>(Hash.IS_VEHICLE_SEAT_FREE, vehicle, -1)) { continue; }

                float distance = lead.Ped.Position.DistanceToSquared(vehicle.Position);
                if (distance >= bestDistance) { continue; }

                bestDistance = distance;
                best = vehicle;
            }

            commandeered = best != null;
            return best;
        }

        private static bool IsUsableRide(Vehicle vehicle, Ped player)
        {
            if (vehicle == null || !vehicle.Exists()) { return false; }
            if (!Function.Call<bool>(Hash.IS_VEHICLE_DRIVEABLE, vehicle, false)) { return false; }

            // Never take the car the player is in, or the one they are about to be in.
            Vehicle ride = player.CurrentVehicle;
            if (ride != null && ride.Exists() && ride.Handle == vehicle.Handle) { return false; }

            if (Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, vehicle)) { return false; }

            // Bikes and boats make for a poor drive-by and a worse chase.
            return vehicle.ClassType != VehicleClass.Boats &&
                   vehicle.ClassType != VehicleClass.Helicopters &&
                   vehicle.ClassType != VehicleClass.Planes &&
                   vehicle.ClassType != VehicleClass.Trains &&
                   vehicle.ClassType != VehicleClass.Cycles &&
                   vehicle.ClassType != VehicleClass.Motorcycles;
        }

        private TrackedPed NearestFreeAlly(IReadOnlyList<TrackedPed> tracked, Chase chase, Vector3 origin, float maxSquared)
        {
            TrackedPed best = null;
            float bestDistance = maxSquared;

            foreach (TrackedPed entry in tracked)
            {
                if (entry.InPursuit || entry.Looting || !entry.IsUsable) { continue; }
                if (entry.Reaction != Reaction.Fight) { continue; }
                if (!CanDrive(entry.Ped)) { continue; }
                if (entry.Faction != chase.Faction) { continue; }

                float distance = origin.DistanceToSquared(entry.Ped.Position);
                if (distance >= bestDistance) { continue; }

                bestDistance = distance;
                best = entry;
            }

            return best;
        }

        /// <summary>Seat -1 is the driver. Everyone runs to the car; nobody is warped in yet.</summary>
        private void AddToCrew(Chase chase, TrackedPed entry, int seat)
        {
            entry.InPursuit = true;
            entry.PursuitSeat = seat;
            chase.Crew.Add(entry);

            Ped ped = entry.Ped;

            try
            {
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.CanUseVehicles, true);
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.CanDoDrivebys, true);
                // Otherwise they bail out at the first red light to fight someone on the pavement.
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.CanLeaveVehicle, false);
                Function.Call(Hash.SET_PED_KEEP_TASK, ped, true);
                ped.BlockPermanentEvents = true;

                if (ped.CurrentVehicle != null && ped.CurrentVehicle.Handle == chase.Vehicle.Handle) { return; }

                // Sprint to the car. Warping is the fallback, not the plan: people running for
                // a car and piling in is most of what sells this.
                Function.Call(Hash.TASK_ENTER_VEHICLE, ped, chase.Vehicle,
                    _config.GetInt("features.pursuit.boardTimeoutMs", 7000), seat, 3f, 1, 0);
            }
            catch (Exception ex)
            {
                Log.Error("Could not add a ped to a pursuit crew", ex);
            }
        }

        /// <summary>Returns false when the chase is over and should be dropped.</summary>
        private bool Advance(Chase chase)
        {
            Ped player = Game.Player.Character;

            PruneCrew(chase);

            if (chase.Crew.Count == 0) { return Ended(chase, "crew gone"); }
            if (chase.Vehicle == null || !chase.Vehicle.Exists() ||
                !Function.Call<bool>(Hash.IS_VEHICLE_DRIVEABLE, chase.Vehicle, false))
            {
                return Ended(chase, "vehicle wrecked");
            }

            // You are allowed to take their car off them. What must not happen is the chase
            // carrying on with you at the wheel, being tasked to pursue yourself.
            Vehicle playerRide = player.CurrentVehicle;
            if (playerRide != null && playerRide.Exists() && playerRide.Handle == chase.Vehicle.Handle)
            {
                return Ended(chase, "player took the car");
            }

            int maxSeconds = _config.GetInt("features.pursuit.maxSeconds", 150);
            if (maxSeconds > 0 && Game.GameTime - chase.StartedAt > maxSeconds * 1000)
            {
                return Ended(chase, "ran its course");
            }

            float distance = player.Position.DistanceTo(chase.Vehicle.Position);
            float giveUp = _config.GetFloat("features.pursuit.giveUpDistance", 320f);

            if (distance <= giveUp) { chase.LastCloseAt = Game.GameTime; }
            else if (Game.GameTime - chase.LastCloseAt > _config.GetInt("features.pursuit.giveUpSeconds", 12) * 1000)
            {
                return Ended(chase, "player escaped");
            }

            if (!chase.Driving) { Board(chase); }
            if (chase.Driving) { KeepChasing(chase, player); }

            UpdateBlip(chase);
            return true;
        }

        private void PruneCrew(Chase chase)
        {
            for (int i = chase.Crew.Count - 1; i >= 0; i--)
            {
                TrackedPed entry = chase.Crew[i];
                if (entry.IsUsable && EntityRegistry.IsSameEntity(entry)) { continue; }

                entry.InPursuit = false;
                chase.Crew.RemoveAt(i);
            }
        }

        /// <summary>
        /// Waits for the crew to get in on foot, then stops waiting. A chase that never leaves
        /// the kerb because one passenger took the long way round is not worth the realism.
        /// </summary>
        private void Board(Chase chase)
        {
            bool timedOut = Game.GameTime >= chase.BoardBy;

            // Wait for the whole carload, not just whoever reached the door first. A driver who
            // pulls away the moment he is seated leaves three people jogging after a car, which
            // is the opposite of what a mob looks like.
            if (!timedOut)
            {
                foreach (TrackedPed entry in chase.Crew)
                {
                    if (!entry.IsUsable) { continue; }
                    if (IsAboard(entry.Ped, chase.Vehicle)) { continue; }
                    return;
                }
            }
            else
            {
                // Out of patience. Warp whoever is still on the pavement.
                foreach (TrackedPed entry in chase.Crew)
                {
                    if (!entry.IsUsable || IsAboard(entry.Ped, chase.Vehicle)) { continue; }
                    Function.Call(Hash.SET_PED_INTO_VEHICLE, entry.Ped, chase.Vehicle, entry.PursuitSeat);
                }
            }

            // Nobody at the wheel - the assigned driver died on the way over, or was bumped out
            // of the seat. Promote whoever is aboard rather than abandoning the chase.
            if (chase.Vehicle.Driver == null || !chase.Vehicle.Driver.Exists())
            {
                foreach (TrackedPed entry in chase.Crew)
                {
                    if (!entry.IsUsable) { continue; }
                    Function.Call(Hash.SET_PED_INTO_VEHICLE, entry.Ped, chase.Vehicle, -1);
                    break;
                }
            }

            if (chase.Vehicle.Driver == null || !chase.Vehicle.Driver.Exists()) { return; }

            chase.Driving = true;
            chase.NextTaskAt = 0;
        }

        private static bool IsAboard(Ped ped, Vehicle vehicle)
        {
            Vehicle current = ped.CurrentVehicle;
            return current != null && current.Handle == vehicle.Handle;
        }

        private void KeepChasing(Chase chase, Ped player)
        {
            if (Game.GameTime < chase.NextTaskAt) { return; }
            chase.NextTaskAt = Game.GameTime + _config.GetInt("features.pursuit.retaskMs", 5000);

            Ped driver = chase.Vehicle.Driver;
            if (driver == null || !driver.Exists()) { chase.Driving = false; return; }

            try
            {
                Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, driver, _config.GetFloat("features.pursuit.aggressiveness", 1f));
                Function.Call(Hash.SET_DRIVER_ABILITY, driver, _config.GetFloat("features.pursuit.driverAbility", 1f));

                if (_config.GetBool("features.pursuit.ram", false))
                {
                    Function.Call(Hash.TASK_VEHICLE_MISSION_PED_TARGET,
                        driver, chase.Vehicle, player, 6, _config.GetFloat("features.pursuit.cruiseSpeed", 60f),
                        _config.GetInt("features.pursuit.drivingStyle", 786603), 5f, 8f, true);
                }
                else
                {
                    Function.Call(Hash.TASK_VEHICLE_CHASE, driver, player);
                    Function.Call(Hash.SET_DRIVE_TASK_DRIVING_STYLE, driver, _config.GetInt("features.pursuit.drivingStyle", 786603));
                }

                if (!_config.GetBool("features.pursuit.driveBys", true)) { return; }

                // Passengers lean out and shoot. The driver keeps both hands on the wheel,
                // which is the difference between a chase and four people in a stationary car.
                foreach (TrackedPed entry in chase.Crew)
                {
                    if (!entry.IsUsable) { continue; }

                    Ped ped = entry.Ped;
                    if (ped.Handle == driver.Handle) { continue; }
                    if (ped.CurrentVehicle == null || ped.CurrentVehicle.Handle != chase.Vehicle.Handle) { continue; }

                    Function.Call(Hash.TASK_DRIVE_BY, ped, player, 0, 0f, 0f, 0f,
                        _config.GetFloat("features.pursuit.driveByRange", 60f),
                        _config.GetInt("features.pursuit.driveByAccuracy", 40),
                        true, FullAuto);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not task a pursuit", ex);
            }
        }

        private bool Ended(Chase chase, string why)
        {
            End(chase, why);
            return false;
        }

        /// <summary>
        /// Hands the crew back to ordinary riot behaviour. Leaving CanLeaveVehicle off would
        /// strand them in a parked car for the rest of the riot.
        /// </summary>
        private void End(Chase chase, string why)
        {
            Log.Info("Pursuit by '" + chase.Faction.Id + "' ended: " + why + ".");

            foreach (TrackedPed entry in chase.Crew)
            {
                entry.InPursuit = false;

                try
                {
                    if (!entry.IsUsable) { continue; }

                    Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, entry.Ped, CombatAttribute.CanLeaveVehicle, true);
                    Function.Call(Hash.SET_PED_KEEP_TASK, entry.Ped, false);
                    Function.Call(Hash.CLEAR_PED_TASKS, entry.Ped);
                    // Due for a fresh riot task on the Director's next pass over it.
                    entry.LastTaskedAt = 0;
                }
                catch (Exception ex)
                {
                    Log.Error("Could not release a pursuit crew member", ex);
                }
            }

            chase.Crew.Clear();

            try
            {
                if (chase.Blip != null && chase.Blip.Exists()) { chase.Blip.Delete(); }
                chase.Blip = null;

                // A car we took is ours to let go of; one they already owned was never ours.
                if (chase.Commandeered && chase.Vehicle != null && chase.Vehicle.Exists())
                {
                    chase.Vehicle.IsPersistent = false;
                    chase.Vehicle.MarkAsNoLongerNeeded();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not clean up after a pursuit", ex);
            }
        }

        private void AttachBlip(Chase chase)
        {
            if (!_config.GetBool("features.pursuit.blip", true)) { return; }

            try
            {
                Blip blip = chase.Vehicle.AddBlip();
                blip.Sprite = BlipSprite.PersonalVehicleCar;
                blip.Color = chase.Faction.BlipColor;
                blip.Scale = 0.8f;
                blip.Name = chase.Faction.DisplayName + " (chasing you)";
                chase.Blip = blip;
            }
            catch (Exception ex)
            {
                Log.Error("Could not blip a pursuit vehicle", ex);
            }
        }

        private static void UpdateBlip(Chase chase)
        {
            if (chase.Blip == null || !chase.Blip.Exists()) { return; }

            // Flashing once they are close is the only warning you get from behind.
            bool close = Game.Player.Character.Position.DistanceToSquared(chase.Vehicle.Position) < 60f * 60f;
            if (chase.Blip.IsFlashing != close) { chase.Blip.IsFlashing = close; }
        }
    }
}
