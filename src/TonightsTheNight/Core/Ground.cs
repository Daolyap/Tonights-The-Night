using System;
using GTA;
using GTA.Math;
using GTA.Native;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// Finding somewhere a ped can actually stand.
    ///
    /// This exists because <c>GET_SAFE_COORD_FOR_PED</c> answers a narrower question than it
    /// looks like it does: it wants a point on the pedestrian navmesh, which downtown is
    /// everywhere and in the desert, the hills, the docks and half of Blaine County is nowhere.
    /// Every caller that treated a zero return as "nothing arrives" therefore worked perfectly
    /// in the city and silently produced nothing at all out of it — which is exactly the shape
    /// of "no aliens spawn in quieter areas".
    ///
    /// So the navmesh is the preference, not the requirement. Failing it, the ground itself will
    /// do: a soldier walking out of scrub is fine, and is enormously better than no soldier.
    /// </summary>
    public static class Ground
    {
        /// <summary>
        /// How high above the candidate the ground probe starts. The native traces downwards, so
        /// starting level with a point that is already underground finds nothing.
        /// </summary>
        private const float ProbeHeight = 60f;

        /// <summary>
        /// A standable point at or near <paramref name="candidate"/>, or Vector3.Zero if even the
        /// ground could not be found — which in practice means the area is not streamed in.
        /// </summary>
        public static Vector3 Place(Vector3 candidate)
        {
            if (candidate == Vector3.Zero) { return Vector3.Zero; }

            try
            {
                Vector3 safe = World.GetSafeCoordForPed(candidate);
                if (safe != Vector3.Zero) { return safe; }
            }
            catch (Exception)
            {
                // Fall through to the ground probe rather than reporting a failed navmesh query.
            }

            return OnGround(candidate);
        }

        /// <summary>
        /// The height of the ground under a point, and whether the probe actually answered.
        ///
        /// Worth having as well as <see cref="OnGround"/> because that one reports failure as
        /// Vector3.Zero, and a caller doing arithmetic on the result cannot tell the difference
        /// between "no ground here" and "the ground is at sea level" - which around Los Santos
        /// is most of the map reading as a two-hundred-metre drop.
        /// </summary>
        public static bool TryHeight(Vector3 candidate, out float height)
        {
            Vector3 placed = OnGround(candidate);

            if (placed == Vector3.Zero)
            {
                height = 0f;
                return false;
            }

            height = placed.Z;
            return true;
        }

        /// <summary>
        /// The ground under a point, ignoring the navmesh entirely.
        ///
        /// Collision is requested first: the probe reads streamed geometry, so asking about
        /// somewhere the engine has not loaded yet reports no ground and would have the caller
        /// conclude the place does not exist.
        /// </summary>
        public static Vector3 OnGround(Vector3 candidate)
        {
            try
            {
                Function.Call(Hash.REQUEST_COLLISION_AT_COORD, candidate.X, candidate.Y, candidate.Z);

                var height = new OutputArgument();
                bool found = Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD,
                    candidate.X, candidate.Y, candidate.Z + ProbeHeight, height, false, false);

                if (found)
                {
                    float z = height.GetResult<float>();
                    // A zero answer is the native's way of saying it does not know, not sea level.
                    if (Math.Abs(z) > 0.01f) { return new Vector3(candidate.X, candidate.Y, z); }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Ground probe failed", ex);
            }

            try
            {
                // Last resort. A road is not scenery a ped should stand in the middle of, but it
                // is a real position on real geometry, and something arriving is the point.
                Vector3 street = World.GetNextPositionOnStreet(candidate);
                if (street != Vector3.Zero) { return street; }
            }
            catch (Exception)
            {
                // Nothing left to try.
            }

            return Vector3.Zero;
        }
    }
}
