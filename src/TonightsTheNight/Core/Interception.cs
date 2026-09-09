using System;
using GTA;
using GTA.Math;
using GTA.Native;
using TonightsTheNight.Config;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// Everything that has to be true for a chase to still be a chase at a hundred miles an hour.
    ///
    /// A wave arrives on a bearing picked at random from a ring around the player and drives at
    /// whatever its car will do. Both of those are fine at walking pace and neither survives a
    /// motorway. At fifty metres a second the player crosses the entire spawn ring in five
    /// seconds, so three quarters of every wave arrives somewhere the player has already left or
    /// is never going, on the wrong side of a central reservation, in a Manana that tops out
    /// well below the car it is supposed to be chasing. The result is the complaint this class
    /// exists for: drive fast and nobody ever turns up behind you.
    ///
    /// Three things fix it, and all three are here rather than spread across the spawner, the
    /// pursuit and the driver tasking, because they are one idea:
    ///
    /// - Arrive where the player is *going*, not where they are. The anchor is pushed along
    ///   their travel by a second or so of it.
    /// - Arrive behind them on that line rather than on a random bearing, most of the time —
    ///   most, not all, or every wave comes from the mirror and the road ahead is always safe.
    /// - Be able to catch up once there. A chase car gets an engine that scales with how fast
    ///   the thing it is chasing is going, and a cruise speed to match, because a pursuit that
    ///   can be won by owning a faster car is not a pursuit.
    ///
    /// All of it is inert below the threshold speed, so nothing here touches an ordinary riot.
    /// </summary>
    public static class Interception
    {
        /// <summary>How fast the player is actually travelling, in metres per second.</summary>
        public static float PlayerSpeed()
        {
            try
            {
                Ped player = Game.Player.Character;
                if (player == null || !player.Exists()) { return 0f; }

                Vehicle ride = player.CurrentVehicle;
                Entity moving = ride != null && ride.Exists() ? (Entity)ride : player;

                return Function.Call<float>(Hash.GET_ENTITY_SPEED, moving);
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        /// <summary>
        /// Which way they are going, flattened and normalised, or zero when they are not really
        /// going anywhere. Velocity rather than heading: a car sliding sideways is going
        /// sideways, and reversing away from a roadblock is going backwards.
        /// </summary>
        public static Vector3 Travel()
        {
            try
            {
                Ped player = Game.Player.Character;
                if (player == null || !player.Exists()) { return Vector3.Zero; }

                Vehicle ride = player.CurrentVehicle;
                Vector3 velocity = ride != null && ride.Exists() ? ride.Velocity : player.Velocity;
                velocity.Z = 0f;

                return velocity.Length() < 1f ? Vector3.Zero : Vector3.Normalize(velocity);
            }
            catch (Exception)
            {
                return Vector3.Zero;
            }
        }

        /// <summary>Whether the player is moving fast enough for any of this to apply.</summary>
        public static bool Running(ConfigStore config)
        {
            if (!config.GetBool("features.interception.enabled", true)) { return false; }
            return PlayerSpeed() >= config.GetFloat("features.interception.speedThreshold", 18f);
        }

        /// <summary>
        /// Where a wave should be measured from.
        ///
        /// Pushed along the player's travel by about a second of it, so the ring is drawn round
        /// where they will be when the wave has finished spawning rather than round where they
        /// were when it started. Capped, because a player at terminal velocity in a jet should
        /// not have his reception committee placed in the next county.
        /// </summary>
        /// <param name="cap">
        /// The furthest the anchor may be pushed, on top of the configured ceiling. Callers pass
        /// a fraction of their own minimum spawn distance: without it, leading the player by a
        /// hundred metres and then placing an arrival a hundred and thirty metres behind that
        /// anchor puts a car thirty metres off their bumper, which is a chase that starts by
        /// materialising in the rear-view mirror.
        /// </param>
        public static Vector3 Anchor(ConfigStore config, Vector3 centre, float cap)
        {
            if (!Running(config)) { return centre; }

            Vector3 travel = Travel();
            if (travel == Vector3.Zero) { return centre; }

            float lead = PlayerSpeed() * config.GetFloat("features.interception.leadSeconds", 1.5f);
            float ceiling = config.GetFloat("features.interception.maxLead", 120f);
            if (cap > 0f && cap < ceiling) { ceiling = cap; }
            if (lead > ceiling) { lead = ceiling; }

            return centre + travel * lead;
        }

        /// <summary>
        /// A bearing to place an arrival on, in radians.
        ///
        /// Mostly behind the player along their own line, which is the only bearing a car can
        /// start a chase from. The rest of the time it is anything at all, so the road ahead is
        /// never guaranteed clear and a roadblock is still something that can happen to you.
        /// </summary>
        public static double Bearing(ConfigStore config, Random random)
        {
            double free = random.NextDouble() * Math.PI * 2.0;
            if (!Running(config)) { return free; }

            Vector3 travel = Travel();
            if (travel == Vector3.Zero) { return free; }

            if (random.NextDouble() > config.GetFloat("features.interception.behindShare", 0.75f)) { return free; }

            // Opposite the direction of travel, plus a spread so a wave is a scatter of cars on
            // the road rather than a queue in one lane.
            double behind = Math.Atan2(-travel.Y, -travel.X);
            double spread = config.GetFloat("features.interception.spreadDegrees", 55f) * Math.PI / 180.0;

            return behind + (random.NextDouble() * 2.0 - 1.0) * spread;
        }

        /// <summary>
        /// How much sooner the next wave should be due, because the player is outrunning the
        /// current one. One at rest, and never more than the configured ceiling.
        /// </summary>
        public static float Urgency(ConfigStore config)
        {
            if (!Running(config)) { return 1f; }

            float threshold = Math.Max(1f, config.GetFloat("features.interception.speedThreshold", 18f));
            float ceiling = Math.Max(1f, config.GetFloat("features.interception.maxUrgency", 2.5f));
            float urgency = PlayerSpeed() / threshold;

            return urgency > ceiling ? ceiling : urgency;
        }

        /// <summary>
        /// Puts a newly arrived chase car on the move before anybody looks at it.
        ///
        /// The single biggest reason waves never caught anybody. A car is created stationary, and
        /// a stationary car two hundred metres behind somebody travelling at fifty metres a
        /// second is not behind them for long: it spends the first four seconds of its existence
        /// getting to thirty while the gap doubles. Traffic the game spawns itself arrives at
        /// road speed for exactly this reason.
        ///
        /// Land vehicles only. A helicopter handed a forward speed at spawn flies into whatever
        /// is in front of it.
        /// </summary>
        public static void RollingStart(ConfigStore config, Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists()) { return; }
            if (!Running(config)) { return; }

            try
            {
                if (vehicle.ClassType == VehicleClass.Helicopters ||
                    vehicle.ClassType == VehicleClass.Planes ||
                    vehicle.ClassType == VehicleClass.Boats)
                {
                    return;
                }

                float share = config.GetFloat("features.interception.rollingStart", 0.9f);
                if (share <= 0f) { return; }

                // Only when it is already pointing roughly the right way.
                //
                // The speed is applied along the vehicle's own facing. For a wave car that is
                // the road node's heading and this is a car moving down its lane; for a parked
                // car a pursuit has just commandeered it could be a wall, and a saloon launched
                // into one at fifty metres a second is a chase that ends on the frame it starts.
                Vector3 travel = Travel();
                Vector3 facing = vehicle.ForwardVector;
                facing.Z = 0f;

                if (travel == Vector3.Zero || facing.Length() < 0.01f) { return; }
                if (Vector3.Dot(Vector3.Normalize(facing), travel) < 0.3f) { return; }

                Function.Call(Hash.SET_VEHICLE_FORWARD_SPEED, vehicle, PlayerSpeed() * share);
            }
            catch (Exception ex)
            {
                Log.Error("Could not give a chase vehicle a rolling start", ex);
            }
        }

        /// <summary>
        /// Gives a chase car the engine it needs to still be a threat.
        ///
        /// Nothing here makes it faster than it has to be: the multiplier is derived from how
        /// much quicker the player is than the speed at which any of this switches on, and it
        /// falls back to standard the moment they slow down. A pursuit you escape by braking is
        /// still an escape; one you escape by having bought a better car is not a pursuit.
        ///
        /// Deliberately not load-bearing. This native is community-documented and the community
        /// does not agree with itself about whether the argument is a multiplier or a percentage
        /// increase - so the values used stay inside the range that is harmless under either
        /// reading, and the work of keeping a chase alive is done by the rolling start and the
        /// cruise speed, both of which are plain metres per second.
        /// </summary>
        public static void Boost(ConfigStore config, Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists()) { return; }
            if (!config.GetBool("features.interception.enabled", true)) { return; }

            try
            {
                if (!Running(config))
                {
                    Function.Call(Hash.SET_VEHICLE_CHEAT_POWER_INCREASE, vehicle, 1f);
                    return;
                }

                float threshold = Math.Max(1f, config.GetFloat("features.interception.speedThreshold", 18f));
                float gain = config.GetFloat("features.interception.boostGain", 0.05f);
                float ceiling = Math.Max(1f, config.GetFloat("features.interception.maxBoost", 2.2f));

                float multiplier = 1f + Math.Max(0f, PlayerSpeed() - threshold) * gain;
                if (multiplier > ceiling) { multiplier = ceiling; }

                Function.Call(Hash.SET_VEHICLE_CHEAT_POWER_INCREASE, vehicle, multiplier);
            }
            catch (Exception ex)
            {
                Log.Error("Could not boost a chase vehicle", ex);
            }
        }

        /// <summary>
        /// The speed to tell a driver to aim for: their own mode's number, or enough to keep up,
        /// whichever is higher. Without this the driving task politely settles at its configured
        /// cruise while the player pulls away in a straight line.
        /// </summary>
        public static float Cruise(ConfigStore config, float configured)
        {
            if (!Running(config)) { return configured; }

            float wanted = PlayerSpeed() * config.GetFloat("features.interception.cruiseMargin", 1.25f);
            return wanted > configured ? wanted : configured;
        }

        /// <summary>Applies the cruise speed to a driver already holding a driving task.</summary>
        public static void Drive(ConfigStore config, Ped driver, float configured)
        {
            if (driver == null || !driver.Exists()) { return; }

            try
            {
                Function.Call(Hash.SET_DRIVE_TASK_CRUISE_SPEED, driver, Cruise(config, configured));
            }
            catch (Exception ex)
            {
                Log.Error("Could not set a chase cruise speed", ex);
            }
        }
    }
}
