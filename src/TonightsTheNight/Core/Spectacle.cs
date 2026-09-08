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
    /// The things that change the city rather than the people in it.
    ///
    /// Everything else in this mod arranges pedestrians. That is the part GTA is good at, and it
    /// is also the part that stops short: a hundred people fighting on an otherwise normal street
    /// still looks like a normal street. What makes a riot read as a riot from a rooftop is that
    /// the city itself is wrong — the power is out, the roads are blocked, and there are columns
    /// of smoke where there should not be any.
    ///
    /// All of it is phase-gated, so it arrives as the riot earns it.
    /// </summary>
    public sealed class Spectacle
    {
        /// <summary>One barricade: a line of props across a road, optionally alight.</summary>
        private sealed class Barricade
        {
            public readonly List<Prop> Props = new List<Prop>();
            public Vector3 Centre;
            public int Fire;
        }

        private readonly ConfigStore _config;
        private readonly ModelResolver _models;
        private readonly Random _random;

        private readonly List<Barricade> _barricades = new List<Barricade>();
        private readonly List<int> _smoke = new List<int>();

        /// <summary>
        /// SET_ARTIFICIAL_LIGHTS_STATE_AFFECTS_VEHICLES. Absent from the SHVDN 3.6.0 enum this
        /// is compiled against, so it goes in by hash — which is version-independent anyway.
        /// </summary>
        private const ulong ArtificialLightsAffectVehicles = 0xE18E628C1E2CFF7C;

        private List<Model> _props;
        private bool _blackedOut;
        private bool _lightsOn = true;
        private int _nextFlickerAt;
        private bool _ptfxRequested;
        private int _nextBarricadeAt;
        private int _nextSmokeAt;
        private bool _reportedNoProps;

        public int BarricadeCount { get { return _barricades.Count; } }
        public bool BlackedOut { get { return _blackedOut; } }

        /// <summary>
        /// Vanilla street furniture, deliberately a mixed bag so a barricade looks improvised
        /// rather than issued. Anything missing is skipped.
        /// </summary>
        private static readonly string[] BarricadeProps =
        {
            "prop_barrier_work05",
            "prop_mp_barrier_02b",
            "prop_barrier_work06a",
            "prop_barier_conc_01a",
            "prop_bollard_01a",
            "prop_roadcone02a",
            "prop_dumpster_02a",
            "prop_bin_05a"
        };

        public Spectacle(ConfigStore config, ModelResolver models, Random random)
        {
            _config = config;
            _models = models;
            _random = random;
        }

        public void Reset()
        {
            Clear();
            _props = null;
            _reportedNoProps = false;
            _nextBarricadeAt = 0;
            _nextSmokeAt = 0;
        }

        /// <summary>Puts the city back. Must run on stop and on abort, or the lights stay off.</summary>
        public void Clear()
        {
            RestoreLights();

            foreach (Barricade barricade in _barricades)
            {
                try
                {
                    if (barricade.Fire != 0) { Function.Call(Hash.REMOVE_SCRIPT_FIRE, barricade.Fire); }

                    foreach (Prop prop in barricade.Props)
                    {
                        if (prop != null && prop.Exists()) { prop.Delete(); }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Could not remove a barricade", ex);
                }
            }
            _barricades.Clear();

            foreach (int handle in _smoke)
            {
                try { Function.Call(Hash.REMOVE_PARTICLE_FX, handle, false); }
                catch (Exception) { }
            }
            _smoke.Clear();
        }

        /// <summary>
        /// <paramref name="phase"/> decides what the riot has earned. A null phase means the mode
        /// has no escalation, in which case everything it declares is available immediately.
        /// </summary>
        public void Update(Vector3 centre, Phase phase, bool unphased)
        {
            try
            {
                UpdateBlackout(unphased || (phase != null && phase.Blackout));
                UpdateBarricades(centre, unphased || (phase != null && phase.Barricades));
                UpdateSmoke(centre);
            }
            catch (Exception ex)
            {
                Log.Error("Spectacle update failed", ex);
            }
        }

        /// <summary>
        /// The single largest change available for one native call. Street lights, shop signs and
        /// window light across the whole map go out; at night the difference is total.
        /// </summary>
        /// <summary>
        /// The power.
        ///
        /// <paramref name="phaseWants"/> is whether the riot has escalated far enough for the
        /// mode to have asked for a blackout. That used to be the only question, which meant
        /// the single largest thing this mod can do to the look of the city was reachable only
        /// by playing three quarters of a mode that happens to declare it. "when" is a setting
        /// now: never, once the mode asks, or from the moment it starts.
        /// </summary>
        private void UpdateBlackout(bool phaseWants)
        {
            if (!_config.GetBool("features.spectacle.blackout", true)) { RestoreLights(); return; }

            string when = _config.GetString("features.spectacle.blackoutWhen", "phase");
            bool wanted;

            if (string.Equals(when, "always", StringComparison.OrdinalIgnoreCase)) { wanted = true; }
            else if (string.Equals(when, "never", StringComparison.OrdinalIgnoreCase)) { wanted = false; }
            else { wanted = phaseWants; }

            if (!wanted) { RestoreLights(); return; }

            if (!_blackedOut)
            {
                _blackedOut = true;
                _lightsOn = true;
                _nextFlickerAt = 0;

                // Headlights are a separate switch. Leaving them on is what keeps a blacked-out
                // street readable rather than pitch black, so it is off by default.
                try
                {
                    Function.Call((Hash)ArtificialLightsAffectVehicles,
                        _config.GetBool("features.spectacle.blackoutVehicles", false));
                }
                catch (Exception ex)
                {
                    Log.Error("Could not set whether the blackout reaches vehicles", ex);
                }

                GTA.UI.Notification.Show("~r~The power is out.");
                Log.Info("Spectacle: blackout on (" + when + ").");
            }

            Flicker();
        }

        /// <summary>
        /// The grid coming back for a second and going again. Costs one native and is most of
        /// the difference between "the lights are off" and "something is wrong with the city".
        /// </summary>
        private void Flicker()
        {
            if (!_config.GetBool("features.spectacle.blackoutFlicker", true))
            {
                SetLights(false);
                return;
            }

            if (Game.GameTime < _nextFlickerAt) { return; }

            bool turnOn = !_lightsOn;

            // Short bursts of light, long stretches of dark. The asymmetry is the whole effect:
            // an even blink reads as a broken script, a half-second of light every ten seconds
            // reads as a substation losing the argument.
            int hold = turnOn
                ? _random.Next(120, 400)
                : _config.GetInt("features.spectacle.blackoutFlickerMs", 9000) + _random.Next(0, 6000);

            _nextFlickerAt = Game.GameTime + hold;
            SetLights(turnOn);
        }

        private void SetLights(bool on)
        {
            if (_lightsOn == on) { return; }

            try
            {
                Function.Call(Hash.SET_ARTIFICIAL_LIGHTS_STATE, !on);
                _lightsOn = on;
            }
            catch (Exception ex)
            {
                Log.Error("Could not switch the city lights", ex);
            }
        }

        private void RestoreLights()
        {
            if (!_blackedOut) { return; }

            try
            {
                Function.Call(Hash.SET_ARTIFICIAL_LIGHTS_STATE, false);
                Function.Call((Hash)ArtificialLightsAffectVehicles, false);
                Log.Info("Spectacle: blackout off.");
            }
            catch (Exception ex)
            {
                Log.Error("Could not restore the city lights", ex);
            }

            _blackedOut = false;
            _lightsOn = true;
        }

        /// <summary>
        /// A line of street furniture dragged across a road, usually on fire.
        ///
        /// Built on road nodes so it sits across the carriageway rather than beside it, and
        /// placed perpendicular to the way the road runs. Cars can still smash through - that is
        /// the point of them being physics props rather than walls.
        /// </summary>
        private void UpdateBarricades(Vector3 centre, bool allowed)
        {
            if (!allowed || !_config.GetBool("features.spectacle.barricades", true))
            {
                if (_barricades.Count > 0) { ClearBarricades(); }
                return;
            }

            if (Game.GameTime < _nextBarricadeAt) { return; }
            _nextBarricadeAt = Game.GameTime + _config.GetInt("features.spectacle.barricadeIntervalMs", 20000);

            if (_barricades.Count >= _config.GetInt("features.spectacle.maxBarricades", 5)) { return; }

            if (_props == null)
            {
                _props = _models.ResolveAll(BarricadeProps);

                if (_props.Count == 0 && !_reportedNoProps)
                {
                    _reportedNoProps = true;
                    Log.Warn("No barricade props are installed - the streets will stay clear.");
                }
            }

            if (_props.Count == 0) { return; }

            Build(centre);
        }

        private void Build(Vector3 centre)
        {
            float radius = _config.GetFloat("features.spectacle.barricadeRadius", 150f);
            double angle = _random.NextDouble() * Math.PI * 2.0;
            float distance = 40f + (float)_random.NextDouble() * radius;

            var candidate = new Vector3(
                centre.X + (float)Math.Cos(angle) * distance,
                centre.Y + (float)Math.Sin(angle) * distance,
                centre.Z);

            var position = new OutputArgument();
            var heading = new OutputArgument();

            if (!Function.Call<bool>(Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING,
                    candidate.X, candidate.Y, candidate.Z, position, heading, 1, 3f, 0))
            {
                return;
            }

            Vector3 spot = position.GetResult<Vector3>();
            float roadHeading = heading.GetResult<float>();

            // Across the road, not along it.
            double across = (roadHeading + 90.0) * Math.PI / 180.0;
            var barricade = new Barricade { Centre = spot };

            int width = _config.GetInt("features.spectacle.barricadeWidth", 5);

            for (int i = 0; i < width; i++)
            {
                float offset = (i - (width - 1) / 2f) * 1.6f;

                var at = new Vector3(
                    spot.X + (float)Math.Cos(across) * offset,
                    spot.Y + (float)Math.Sin(across) * offset,
                    spot.Z);

                Model model = _props[_random.Next(_props.Count)];
                if (!_models.Load(model)) { continue; }

                Prop prop = World.CreateProp(model, at, true, true);
                if (prop == null || !prop.Exists()) { continue; }

                try
                {
                    prop.IsPersistent = true;
                    prop.Heading = roadHeading + (float)(_random.NextDouble() * 40.0 - 20.0);
                }
                catch (Exception ex)
                {
                    Log.Error("Could not place a barricade prop", ex);
                }

                barricade.Props.Add(prop);
            }

            if (barricade.Props.Count == 0) { return; }

            if (_config.GetBool("features.spectacle.burningBarricades", true) &&
                _random.NextDouble() < _config.GetFloat("features.spectacle.burningChance", 0.7f))
            {
                barricade.Fire = Function.Call<int>(Hash.START_SCRIPT_FIRE, spot.X, spot.Y, spot.Z, 25, false);
            }

            _barricades.Add(barricade);
            Log.Info("Spectacle: barricade of " + barricade.Props.Count + " across a road" +
                     (barricade.Fire != 0 ? ", alight." : "."));
        }

        private void ClearBarricades()
        {
            foreach (Barricade barricade in _barricades)
            {
                try
                {
                    if (barricade.Fire != 0) { Function.Call(Hash.REMOVE_SCRIPT_FIRE, barricade.Fire); }
                    foreach (Prop prop in barricade.Props)
                    {
                        if (prop != null && prop.Exists()) { prop.Delete(); }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Could not clear a barricade", ex);
                }
            }
            _barricades.Clear();
        }

        /// <summary>
        /// Columns of smoke over the barricades, which is what makes a riot visible from the
        /// other side of the map rather than only from inside it.
        /// </summary>
        private void UpdateSmoke(Vector3 centre)
        {
            if (!_config.GetBool("features.spectacle.smoke", true)) { return; }
            if (_barricades.Count == 0) { return; }
            if (Game.GameTime < _nextSmokeAt) { return; }

            _nextSmokeAt = Game.GameTime + 5000;

            if (_smoke.Count >= _config.GetInt("features.spectacle.maxSmoke", 6)) { return; }
            if (!EnsureParticleAsset()) { return; }

            Barricade source = _barricades[_random.Next(_barricades.Count)];
            if (source.Fire == 0) { return; }

            try
            {
                Function.Call(Hash.USE_PARTICLE_FX_ASSET, _config.GetString("features.spectacle.ptfxAsset", "core"));

                int handle = Function.Call<int>(Hash.START_PARTICLE_FX_LOOPED_AT_COORD,
                    _config.GetString("features.spectacle.ptfxName", "exp_grd_bzgas_smoke"),
                    source.Centre.X, source.Centre.Y, source.Centre.Z,
                    0f, 0f, 0f,
                    _config.GetFloat("features.spectacle.smokeScale", 6f),
                    false, false, false, false);

                if (handle != 0) { _smoke.Add(handle); }
            }
            catch (Exception ex)
            {
                Log.Error("Could not start a smoke column", ex);
            }
        }

        /// <summary>
        /// Particle assets stream like models do. Asking every frame is wasteful and never
        /// asking means the effect silently does nothing.
        /// </summary>
        private bool EnsureParticleAsset()
        {
            string asset = _config.GetString("features.spectacle.ptfxAsset", "core");

            if (Function.Call<bool>(Hash.HAS_NAMED_PTFX_ASSET_LOADED, asset)) { return true; }

            if (!_ptfxRequested)
            {
                _ptfxRequested = true;
                Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, asset);
                Log.Debug("Spectacle: requested particle asset '" + asset + "'.");
            }

            return false;
        }
    }
}
