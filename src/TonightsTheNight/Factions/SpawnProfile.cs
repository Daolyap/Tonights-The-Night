using System.Collections.Generic;
using TonightsTheNight.Util;

namespace TonightsTheNight.Factions
{
    /// <summary>
    /// How a faction puts bodies on the street when converting ambient peds is not an option.
    ///
    /// Soldiers, aliens, SWAT and animals have no ambient equivalent to convert, so they have to
    /// be spawned. Spawning is the expensive path and the one that exhausts the ped pool, so
    /// every faction that uses it carries its own hard ceiling rather than relying on a global.
    /// </summary>
    public sealed class SpawnProfile
    {
        public bool Enabled { get; private set; }

        /// <summary>Ped models, tried in order. Missing ones are skipped, not fatal.</summary>
        public List<string> Models { get; private set; }

        /// <summary>Optional vehicles. Names, so add-on packs work and their absence does not.</summary>
        public List<string> Vehicles { get; private set; }

        public int MaxAlive { get; private set; }
        public int PerWave { get; private set; }
        public int WaveIntervalMs { get; private set; }
        public float MinDistance { get; private set; }
        public float MaxDistance { get; private set; }

        /// <summary>Chance a wave arrives by vehicle rather than on foot.</summary>
        public float InVehicleChance { get; private set; }

        public int Occupants { get; private set; }

        /// <summary>Lights and audio on. What makes police read as police rather than as gunmen.</summary>
        public bool Siren { get; private set; }

        /// <summary>
        /// Drivers stop steering around people. This is the whole of "mercilessly ploughing
        /// through a crowd" — one native, applied to the driver.
        /// </summary>
        public bool DriveThroughCrowds { get; private set; }

        public static SpawnProfile FromJson(JsonValue node)
        {
            return new SpawnProfile
            {
                Enabled = node["enabled"].AsBool(node.IsObject),
                Models = node["models"].AsStringList(),
                Vehicles = node["vehicles"].AsStringList(),
                MaxAlive = node["maxAlive"].AsInt(10),
                PerWave = node["perWave"].AsInt(2),
                WaveIntervalMs = node["waveIntervalMs"].AsInt(12000),
                MinDistance = node["minDistance"].AsFloat(70f),
                MaxDistance = node["maxDistance"].AsFloat(150f),
                InVehicleChance = node["inVehicleChance"].AsFloat(0f),
                Occupants = node["occupants"].AsInt(2),
                Siren = node["siren"].AsBool(false),
                DriveThroughCrowds = node["driveThroughCrowds"].AsBool(false)
            };
        }

        public static SpawnProfile Disabled()
        {
            return new SpawnProfile
            {
                Enabled = false,
                Models = new List<string>(),
                Vehicles = new List<string>()
            };
        }
    }
}
