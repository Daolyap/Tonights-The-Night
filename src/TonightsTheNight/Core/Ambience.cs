using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using TonightsTheNight.Config;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// Weather, time, colour grade and fire — the cheap half of making a riot feel like one.
    ///
    /// Everything here is restored on stop and every part is individually switchable, because
    /// a mod that silently keeps your weather locked after you turned it off is a mod people
    /// uninstall.
    /// </summary>
    public sealed class Ambience
    {
        private readonly ConfigStore _config;
        private readonly Random _random;
        private readonly List<int> _fires = new List<int>();

        private bool _weatherApplied;
        private bool _timecycleApplied;
        private int _nextFireAt;

        public int ActiveFires { get { return _fires.Count; } }

        public Ambience(ConfigStore config, Random random)
        {
            _config = config;
            _random = random;
        }

        /// <summary>Applies a mode's look. All of it optional, none of it load-bearing.</summary>
        public void Apply(JsonValue node)
        {
            if (!_config.GetBool("features.ambience.enabled", true)) { return; }
            if (!node.IsObject) { return; }

            try
            {
                string weather = node["weather"].AsString(null);
                if (weather != null && _config.GetBool("features.ambience.setWeather", true))
                {
                    Function.Call(Hash.SET_WEATHER_TYPE_NOW_PERSIST, weather);
                    _weatherApplied = true;
                }

                if (node.Has("hour") && _config.GetBool("features.ambience.setTime", true))
                {
                    Function.Call(Hash.SET_CLOCK_TIME, node["hour"].AsInt(0), node["minute"].AsInt(0), 0);
                }

                string timecycle = node["timecycle"].AsString(null);
                if (timecycle != null && _config.GetBool("features.ambience.setTimecycle", true))
                {
                    Function.Call(Hash.SET_TIMECYCLE_MODIFIER, timecycle);
                    Function.Call(Hash.SET_TIMECYCLE_MODIFIER_STRENGTH, node["timecycleStrength"].AsFloat(1f));
                    _timecycleApplied = true;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not apply ambience", ex);
            }
        }

        /// <summary>
        /// Scatters fires around the player. Burning cars and street fires are the visual
        /// shorthand for a riot and cost almost nothing next to another twenty rioters.
        /// </summary>
        public void UpdateFires(Vector3 centre, bool allowed)
        {
            if (!allowed || !_config.GetBool("features.fires.enabled", true))
            {
                return;
            }

            if (Game.GameTime < _nextFireAt) { return; }

            int maximum = _config.GetInt("features.fires.maxActive", 8);
            _nextFireAt = Game.GameTime + _config.GetInt("features.fires.intervalMs", 9000);

            PruneFires();
            if (_fires.Count >= maximum) { return; }

            try
            {
                if (_config.GetBool("features.fires.burnVehicles", true) && _random.NextDouble() < 0.5)
                {
                    Vehicle[] nearby = World.GetNearbyVehicles(centre, _config.GetFloat("features.fires.radius", 120f));
                    foreach (Vehicle vehicle in nearby)
                    {
                        if (vehicle == null || !vehicle.Exists() || vehicle.IsOnFire) { continue; }
                        if (Game.Player.Character.CurrentVehicle == vehicle) { continue; }
                        if (vehicle.Driver != null && vehicle.Driver.Exists() && vehicle.Driver.IsAlive) { continue; }

                        Function.Call(Hash.START_ENTITY_FIRE, vehicle);
                        return;
                    }
                }

                double angle = _random.NextDouble() * Math.PI * 2.0;
                float distance = 15f + (float)_random.NextDouble() * _config.GetFloat("features.fires.radius", 120f);
                var spot = new Vector3(
                    centre.X + (float)Math.Cos(angle) * distance,
                    centre.Y + (float)Math.Sin(angle) * distance,
                    centre.Z);

                Vector3 ground = World.GetSafeCoordForPed(spot);
                if (ground == Vector3.Zero) { return; }

                int handle = Function.Call<int>(Hash.START_SCRIPT_FIRE, ground.X, ground.Y, ground.Z,
                    _config.GetInt("features.fires.size", 20), false);

                if (handle != 0) { _fires.Add(handle); }
            }
            catch (Exception ex)
            {
                Log.Error("Could not start a fire", ex);
            }
        }

        private void PruneFires()
        {
            // Script fires burn out on their own; the handles are just our accounting.
            if (_fires.Count > 40) { _fires.RemoveRange(0, _fires.Count - 40); }
        }

        public void Clear()
        {
            foreach (int handle in _fires)
            {
                try { Function.Call(Hash.REMOVE_SCRIPT_FIRE, handle); }
                catch (Exception) { }
            }
            _fires.Clear();

            try
            {
                if (_timecycleApplied)
                {
                    Function.Call(Hash.CLEAR_TIMECYCLE_MODIFIER);
                    _timecycleApplied = false;
                }

                if (_weatherApplied)
                {
                    Function.Call(Hash.CLEAR_WEATHER_TYPE_PERSIST);
                    _weatherApplied = false;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not clear ambience", ex);
            }
        }
    }
}
