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

                if (faction.Spawn.DriveThroughCrowds && _config.GetBool("features.police.driveThroughCrowds", true))
                {
                    Function.Call(Hash.SET_PED_STEERS_AROUND_PEDS, driver, false);
                    Function.Call(Hash.SET_PED_STEERS_AROUND_VEHICLES, driver, false);
                    Function.Call(Hash.SET_PED_STEERS_AROUND_OBJECTS, driver, false);
                }

                if (target == null || !target.Exists()) { return; }

                // Mission type and driving style are community-documented rather than official,
                // so both are config-exposed: if the ramming reads wrong, it is a config edit
                // and a reload rather than a new build.
                Function.Call(Hash.TASK_VEHICLE_MISSION_PED_TARGET,
                    driver, vehicle, target,
                    _config.GetInt("features.police.vehicleMission", 6),      // 6 = ram
                    _config.GetFloat("features.police.cruiseSpeed", 45f),
                    _config.GetInt("features.police.drivingStyle", 786603),
                    _config.GetFloat("features.police.targetReachedDistance", 5f),
                    _config.GetFloat("features.police.straightLineDistance", 8f),
                    true);
            }
            catch (Exception ex)
            {
                Log.Error("Could not apply vehicle behaviour for faction '" + faction.Id + "'", ex);
            }
        }
    }
}
