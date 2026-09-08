using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using TonightsTheNight.Config;
using TonightsTheNight.Factions;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>What a rioter behind a wheel decides to do about it.</summary>
    public enum VehicleRole
    {
        /// <summary>Not driving, or not decided yet.</summary>
        None,
        /// <summary>Stop, get out, and fight on foot.</summary>
        Dismount,
        /// <summary>Go after another rioter's car.</summary>
        HuntEnemy,
        /// <summary>Go after the player.</summary>
        HuntPlayer,
        /// <summary>Get away from the player.</summary>
        FleePlayer
    }

    /// <summary>
    /// Tasks for rioters who are already in a car when the riot reaches them.
    ///
    /// This exists because the obvious behaviour is wrong. Handing a driver
    /// FightAgainstHatedTargets makes them stop in the road and get out, so driving past a riot
    /// at speed produced a street of abandoned cars and people walking towards you — every
    /// single driver making the identical decision, which reads as a scripted trap rather than
    /// as a city coming apart.
    ///
    /// So a driver rolls once for what they do: get out and fight, go after another rioter, come
    /// after you, or get away from you. The roll is stored on the ped so it stays their decision
    /// rather than being re-made every few seconds.
    /// </summary>
    public sealed class VehicleTasking
    {
        /// <summary>FIRING_PATTERN_FULL_AUTO.</summary>
        private const uint FullAuto = 0xC6EE6B4C;


        private readonly ConfigStore _config;
        private readonly Random _random;

        /// <summary>
        /// Drivers currently assigned to come after the player, and when that assignment goes
        /// stale. Self-contained rather than plumbed through the Director, because the only
        /// question being asked is "have enough people already decided this".
        /// </summary>
        private readonly Dictionary<int, int> _huntingPlayer = new Dictionary<int, int>();

        public VehicleTasking(ConfigStore config, Random random)
        {
            _config = config;
            _random = random;
        }

        public bool Enabled { get { return _config.GetBool("vehicles.enabled", true); } }

        /// <summary>
        /// Weighted, so the mix is a config edit rather than a rebuild. A crowd where everyone
        /// makes the same choice is the bug this replaces.
        /// </summary>
        public VehicleRole Roll(Reaction reaction)
        {
            // Someone who was already running is not going to turn the car around.
            if (reaction != Reaction.Fight) { return VehicleRole.FleePlayer; }

            float dismount = Math.Max(0f, _config.GetFloat("vehicles.dismountWeight", 3f));
            float huntEnemy = Math.Max(0f, _config.GetFloat("vehicles.huntEnemyWeight", 3f));
            float huntPlayer = Math.Max(0f, _config.GetFloat("vehicles.huntPlayerWeight", 1f));
            float fleePlayer = Math.Max(0f, _config.GetFloat("vehicles.fleePlayerWeight", 2f));

            // The weights are a per-driver decision and say nothing about how many drivers have
            // already made it. Every car on the street rolling 20% independently is how a riot
            // ends up with six of them converging on one pedestrian, which does not read as a
            // riot at all - it reads as the traffic being out to get you.
            if (huntPlayer > 0f && HuntingPlayerCount() >= Math.Max(0, _config.GetInt("vehicles.maxHuntingPlayer", 1)))
            {
                huntPlayer = 0f;
            }

            float total = dismount + huntEnemy + huntPlayer + fleePlayer;
            if (total <= 0f) { return VehicleRole.Dismount; }

            double roll = _random.NextDouble() * total;

            if ((roll -= dismount) <= 0) { return VehicleRole.Dismount; }
            if ((roll -= huntEnemy) <= 0) { return VehicleRole.HuntEnemy; }
            if ((roll -= huntPlayer) <= 0) { return VehicleRole.HuntPlayer; }
            return VehicleRole.FleePlayer;
        }

        /// <summary>
        /// Issues the driving task. Returns false when the role needs the ped on foot, which the
        /// caller handles with its ordinary on-foot tasking.
        /// </summary>
        /// <summary>
        /// Undoes the "stay in the car" conditioning. Called when the car is no longer worth
        /// staying in, because a ped left with that flag set sits in a wreck for the rest of the
        /// riot waiting for a task it can no longer perform.
        /// </summary>
        public static void Release(Ped ped)
        {
            try
            {
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.CanLeaveVehicle, true);
                Function.Call(Hash.SET_PED_KEEP_TASK, ped, false);
            }
            catch (Exception ex)
            {
                Log.Error("Could not release a driver", ex);
            }
        }

        public bool Apply(Ped ped, Vehicle vehicle, VehicleRole role, Ped hostile)
        {
            if (role == VehicleRole.None || role == VehicleRole.Dismount) { return false; }

            Ped player = Game.Player.Character;
            Ped target = role == VehicleRole.HuntEnemy && hostile != null && hostile.Exists() ? hostile : player;
            bool afterPlayer = role == VehicleRole.HuntPlayer;

            try
            {
                PrepareDriver(ped, role);

                if (role == VehicleRole.FleePlayer)
                {
                    Function.Call(Hash.TASK_VEHICLE_MISSION_PED_TARGET,
                        ped, vehicle, player, _config.GetInt("vehicles.fleeMission", 8),
                        _config.GetFloat("vehicles.fleeSpeed", 45f),
                        _config.GetInt("vehicles.drivingStyle", 786603),
                        20f, 30f, true);
                }
                else if (_config.GetBool("vehicles.ram", false))
                {
                    Function.Call(Hash.TASK_VEHICLE_MISSION_PED_TARGET,
                        ped, vehicle, target, _config.GetInt("vehicles.ramMission", 6),
                        _config.GetFloat("vehicles.chaseSpeed", 55f),
                        _config.GetInt("vehicles.drivingStyle", 786603),
                        5f, 8f, true);
                }
                else if (afterPlayer)
                {
                    // Tail, do not drive at. A vehicle chase task against a target on foot is
                    // resolved by driving over them, which is why standing on a pavement during
                    // a riot used to mean being hit by a car every few seconds. Following keeps
                    // the threat - they are behind you, and the passengers still lean out.
                    Function.Call(Hash.TASK_VEHICLE_MISSION_PED_TARGET,
                        ped, vehicle, player, _config.GetInt("vehicles.playerMission", 7),
                        _config.GetFloat("vehicles.chaseSpeed", 55f),
                        _config.GetInt("vehicles.drivingStyle", 786603),
                        _config.GetFloat("vehicles.playerStandoff", 12f), 20f, true);
                }
                else
                {
                    Function.Call(Hash.TASK_VEHICLE_CHASE, ped, target);
                    Function.Call(Hash.SET_DRIVE_TASK_DRIVING_STYLE, ped, _config.GetInt("vehicles.drivingStyle", 786603));
                }

                Function.Call(Hash.SET_PED_KEEP_TASK, ped, true);
                NoteHunting(ped, afterPlayer);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Could not task a driver", ex);
                return false;
            }
        }

        private void PrepareDriver(Ped ped, VehicleRole role)
        {
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.CanUseVehicles, true);
            // Without this they abandon the car at the first junction to punch someone.
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.CanLeaveVehicle, false);

            // Shooting out of the window while driving, which is most of what makes a chase read
            // as a chase rather than as traffic that happens to be following you.
            bool armed = _config.GetBool("vehicles.driveBys", true) && HasGun(ped);
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.CanDoDrivebys, armed);

            Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, ped, _config.GetFloat("vehicles.aggressiveness", 0.9f));
            Function.Call(Hash.SET_DRIVER_ABILITY, ped, _config.GetFloat("vehicles.driverAbility", 0.8f));

            // Not when they are coming for you. "Stop steering around people" is what makes a
            // driver plough through a crowd, and applied to the one driver whose target is a
            // person it stops being atmosphere and becomes a car aimed at your back.
            if (role != VehicleRole.FleePlayer && role != VehicleRole.HuntPlayer &&
                _config.GetBool("vehicles.driveThroughCrowds", true))
            {
                Function.Call(Hash.SET_PED_STEERS_AROUND_PEDS, ped, false);
            }
        }

        /// <summary>
        /// Records that this driver is after the player, so the next one to roll for it can be
        /// told the seat is taken. Assignments expire: a driver who has crashed, been killed or
        /// simply lost you should not hold the slot for the rest of the riot.
        /// </summary>
        private void NoteHunting(Ped ped, bool hunting)
        {
            if (!hunting) { _huntingPlayer.Remove(ped.Handle); return; }

            _huntingPlayer[ped.Handle] =
                Game.GameTime + Math.Max(2000, _config.GetInt("vehicles.huntPlayerHoldMs", 20000));
        }

        private int HuntingPlayerCount()
        {
            if (_huntingPlayer.Count == 0) { return 0; }

            List<int> stale = null;
            int live = 0;

            foreach (var pair in _huntingPlayer)
            {
                if (Game.GameTime > pair.Value)
                {
                    if (stale == null) { stale = new List<int>(); }
                    stale.Add(pair.Key);
                    continue;
                }
                live++;
            }

            if (stale != null)
            {
                foreach (int handle in stale) { _huntingPlayer.Remove(handle); }
            }

            return live;
        }

        /// <summary>
        /// Passengers of a rioter's car. They cannot drive, so the only thing they can usefully
        /// do is lean out — and a carload staring straight ahead while the driver rams you is
        /// the detail that gives the whole thing away.
        /// </summary>
        public void TaskPassenger(Ped ped, Ped target)
        {
            if (!_config.GetBool("vehicles.driveBys", true)) { return; }
            if (target == null || !target.Exists() || !HasGun(ped)) { return; }

            try
            {
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.CanDoDrivebys, true);
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.CanLeaveVehicle, false);
                Function.Call(Hash.SET_PED_KEEP_TASK, ped, true);

                Function.Call(Hash.TASK_DRIVE_BY, ped, target, 0, 0f, 0f, 0f,
                    _config.GetFloat("vehicles.driveByRange", 60f),
                    _config.GetInt("vehicles.driveByAccuracy", 35),
                    true, FullAuto);
            }
            catch (Exception ex)
            {
                Log.Error("Could not task a passenger", ex);
            }
        }

        /// <summary>
        /// Unarmed peds should not be tasked to shoot from a window — they mime it, which looks
        /// far worse than sitting still.
        ///
        /// The flag argument to IS_PED_ARMED is community-documented rather than official, so it
        /// is config-exposed: if nobody ever shoots from a car, or everyone does including the
        /// man with the golf club, this is the value to change.
        /// </summary>
        private bool HasGun(Ped ped)
        {
            try
            {
                return Function.Call<bool>(Hash.IS_PED_ARMED, ped, _config.GetInt("vehicles.armedCheckFlags", 4));
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
