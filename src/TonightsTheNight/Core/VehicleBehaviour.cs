using System;
using GTA;
using GTA.Native;
using TonightsTheNight.Config;
using TonightsTheNight.Factions;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// Makes a driver behave like a unit responding to a riot rather than like traffic.
    ///
    /// This is the piece that sells police as police: lights and siren running, driving like
    /// they own the road, and — the part that actually reads as brutality —
    /// SET_PED_STEERS_AROUND_PEDS(false), which stops the driver avoiding people. One native
    /// turns "a police car arrives" into "a police car arrives through the crowd".
    ///
    /// Note this never touches the wanted system. Riot police are a faction; your own police or
    /// wanted overhaul keeps running alongside them untouched.
    /// </summary>
    public sealed class VehicleBehaviour
    {
        private readonly ConfigStore _config;

        public VehicleBehaviour(ConfigStore config)
        {
            _config = config;
        }

        public void Apply(Ped driver, Vehicle vehicle, Faction faction, Ped target)
        {
            if (driver == null || !driver.Exists() || vehicle == null || !vehicle.Exists()) { return; }

            try
            {
                if (faction.Spawn.Siren)
                {
                    Function.Call(Hash.SET_VEHICLE_SIREN, vehicle, true);
                    if (_config.GetBool("features.police.muteSirens", false))
                    {
                        Function.Call(Hash.SET_VEHICLE_HAS_MUTED_SIRENS, vehicle, true);
                    }
                }

                Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, driver, _config.GetFloat("features.police.aggressiveness", 1f));
                Function.Call(Hash.SET_DRIVER_ABILITY, driver, _config.GetFloat("features.police.driverAbility", 1f));

                // Ploughing through a crowd is atmosphere. Ploughing through the crowd while
                // aimed at you personally is a car chase you did not agree to, so a driver whose
                // target is the player keeps their steering.
                bool ploughs = faction.Spawn.DriveThroughCrowds &&
                               _config.GetBool("features.police.driveThroughCrowds", true) &&
                               (target == null || !target.Exists() || target.Handle != Game.Player.Character.Handle);

                if (ploughs)
                {
                    Function.Call(Hash.SET_PED_STEERS_AROUND_PEDS, driver, false);
                    Function.Call(Hash.SET_PED_STEERS_AROUND_VEHICLES, driver, false);
                    Function.Call(Hash.SET_PED_STEERS_AROUND_OBJECTS, driver, false);
                }

                if (target == null || !target.Exists()) { return; }

                // A helicopter cannot be given a road mission, and giving it one is why air
                // support previously did nothing at all.
                if (vehicle.ClassType == VehicleClass.Helicopters)
                {
                    ApplyHelicopter(driver, vehicle, target);
                    return;
                }

                if (vehicle.ClassType == VehicleClass.Planes)
                {
                    ApplyPlane(driver, vehicle, target);
                    return;
                }

                // Driving *at* a person is a different thing from driving at a car, and the
                // target here is often the player on foot. A squad car told to attack one ends
                // up resolving it with the bumper, which is where "the police keep running me
                // over" came from - so a target that is the player gets followed instead, from
                // a standoff distance, and the officers get out and shoot like officers.
                bool afterPlayer = target.Handle == Game.Player.Character.Handle;

                int mission = afterPlayer
                    ? _config.GetInt("features.police.playerVehicleMission", 7)   // 7 = follow
                    : _config.GetInt("features.police.vehicleMission", 6);        // 6 = attack

                float reached = afterPlayer
                    ? _config.GetFloat("features.police.playerStandoff", 15f)
                    : _config.GetFloat("features.police.targetReachedDistance", 5f);

                float cruise = _config.GetFloat("features.police.cruiseSpeed", 45f);

                // The first task a spawned unit gets, and for twelve seconds the only one. A
                // wave that arrives behind a player doing a hundred and forty and is then told
                // to cruise at a hundred and sixty is a wave that never gets any closer, which
                // is what "they do not drive quick enough to start chasing you" was.
                if (afterPlayer)
                {
                    Interception.Boost(_config, vehicle);
                    cruise = Interception.Cruise(_config, cruise);
                }

                // Mission type and driving style are community-documented rather than official,
                // so both are config-exposed: if the driving reads wrong, it is a config edit
                // and a reload rather than a new build.
                Function.Call(Hash.TASK_VEHICLE_MISSION_PED_TARGET,
                    driver, vehicle, target,
                    mission,
                    cruise,
                    _config.GetInt("features.police.drivingStyle", 786603),
                    reached,
                    _config.GetFloat("features.police.straightLineDistance", 8f),
                    true);
            }
            catch (Exception ex)
            {
                Log.Error("Could not apply vehicle behaviour for faction '" + faction.Id + "'", ex);
            }
        }

        /// <summary>
        /// A gunship holding station over the riot.
        ///
        /// The mission id and the heights are config-exposed because TASK_HELI_MISSION is
        /// community-documented rather than official and has more arguments than anyone is
        /// confident about. If air support ends up parked on a rooftop, this is the knob.
        /// </summary>
        private void ApplyHelicopter(Ped pilot, Vehicle vehicle, Ped target)
        {
            GTA.Math.Vector3 over = target.Position;

            // A searchlight sweeping a blacked-out street is most of what a helicopter is for.
            if (_config.GetBool("features.air.searchlight", true))
            {
                Function.Call(Hash.SET_VEHICLE_SEARCHLIGHT, vehicle, true, true);
            }

            Function.Call(Hash.TASK_HELI_MISSION,
                pilot, vehicle, 0, target,
                over.X, over.Y, over.Z,
                _config.GetInt("features.air.heliMission", 4),
                _config.GetFloat("features.air.speed", 40f),
                _config.GetFloat("features.air.radius", 60f),
                -1f,
                (int)_config.GetFloat("features.air.maxHeight", 90f),
                (int)_config.GetFloat("features.air.minHeight", 35f),
                -1f, 0);
        }

        /// <summary>
        /// A jet making passes over the city.
        ///
        /// Fast jets are a presence rather than a weapon here: they cannot loiter, and a fighter
        /// tasked to attack a crowd mostly flies into a building. The default mission circles the
        /// target at altitude, which is what a no-fly zone looks like from the ground. Every
        /// value is config-exposed because TASK_PLANE_MISSION is community-documented.
        /// </summary>
        private void ApplyPlane(Ped pilot, Vehicle vehicle, Ped target)
        {
            GTA.Math.Vector3 over = target.Position;

            Function.Call(Hash.TASK_PLANE_MISSION,
                pilot, vehicle, 0, target,
                over.X, over.Y, over.Z,
                _config.GetInt("features.air.planeMission", 6),
                _config.GetFloat("features.air.planeSpeed", 90f),
                -1f,
                _config.GetFloat("features.air.planeHeading", -1f),
                (int)_config.GetFloat("features.air.planeMaxHeight", 260f),
                (int)_config.GetFloat("features.air.planeMinHeight", 160f));
        }

        /// <summary>
        /// Everyone else in the vehicle. A troop carrier whose passengers stare straight ahead
        /// while the driver ploughs through a crowd is the detail that gives it away.
        /// </summary>
        public void ApplyOccupants(Vehicle vehicle, Ped driver, Ped target)
        {
            if (vehicle == null || !vehicle.Exists() || target == null || !target.Exists()) { return; }
            if (!_config.GetBool("vehicles.driveBys", true)) { return; }

            try
            {
                foreach (Ped occupant in vehicle.Occupants)
                {
                    if (occupant == null || !occupant.Exists()) { continue; }
                    if (driver != null && occupant.Handle == driver.Handle) { continue; }
                    if (occupant.Handle == Game.Player.Character.Handle) { continue; }

                    Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, occupant, CombatAttribute.CanDoDrivebys, true);
                    Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, occupant, CombatAttribute.CanLeaveVehicle, true);
                    Function.Call(Hash.TASK_DRIVE_BY, occupant, target, 0, 0f, 0f, 0f,
                        _config.GetFloat("vehicles.driveByRange", 60f),
                        _config.GetInt("vehicles.driveByAccuracy", 35),
                        true, 0xC6EE6B4C);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not task vehicle occupants", ex);
            }
        }
    }
}
