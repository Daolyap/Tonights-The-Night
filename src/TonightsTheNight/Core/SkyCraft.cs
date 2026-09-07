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
    /// Something in the sky that the invasion arrived in.
    ///
    /// GTA V's UFOs are props, not vehicles — there is nothing to fly and nobody to put in them —
    /// so this holds them at altitude over the riot and drifts them, and the aliens on the ground
    /// spawn underneath rather than at random points around you. That is the whole trick: a
    /// hovering craft plus arrivals beneath it reads as a landing, and neither half reads as
    /// anything on its own.
    ///
    /// Every model is resolved by name with fallback. If none of the candidates exist in your
    /// install this does nothing at all and the invasion carries on without it, which is the
    /// correct outcome rather than a crash.
    /// </summary>
    public sealed class SkyCraft
    {
        private sealed class Craft
        {
            public Prop Prop;
            public Blip Blip;
            public double Angle;
            public float Radius;
            public float Height;
            public float Bob;
        }

        private readonly ConfigStore _config;
        private readonly ModelResolver _models;
        private readonly Random _random;
        private readonly List<Craft> _craft = new List<Craft>();

        private List<Model> _resolved;
        private int _nextUpdateAt;
        private bool _reportedNone;

        public int Active { get { return _craft.Count; } }

        public SkyCraft(ConfigStore config, ModelResolver models, Random random)
        {
            _config = config;
            _models = models;
            _random = random;
        }

        public bool Enabled { get { return _config.GetBool("features.craft.enabled", true); } }

        public void Reset()
        {
            Clear();
            _resolved = null;
            _reportedNone = false;
            _nextUpdateAt = 0;
        }

        public void Clear()
        {
            foreach (Craft craft in _craft)
            {
                try
                {
                    if (craft.Blip != null && craft.Blip.Exists()) { craft.Blip.Delete(); }
                    if (craft.Prop != null && craft.Prop.Exists()) { craft.Prop.Delete(); }
                }
                catch (Exception ex)
                {
                    Log.Error("Could not remove a craft", ex);
                }
            }
            _craft.Clear();
        }

        /// <summary>
        /// A point underneath one of the craft, or Vector3.Zero when nothing is overhead. The
        /// spawner uses this so arrivals happen below the ship rather than behind a hedge.
        /// </summary>
        public Vector3 DropPoint()
        {
            if (_craft.Count == 0) { return Vector3.Zero; }

            Craft craft = _craft[_random.Next(_craft.Count)];
            if (craft.Prop == null || !craft.Prop.Exists()) { return Vector3.Zero; }

            Vector3 under = craft.Prop.Position;
            float scatter = _config.GetFloat("features.craft.dropScatter", 20f);

            return World.GetSafeCoordForPed(new Vector3(
                under.X + (float)(_random.NextDouble() * 2 - 1) * scatter,
                under.Y + (float)(_random.NextDouble() * 2 - 1) * scatter,
                under.Z));
        }

        /// <summary>
        /// <paramref name="declared"/> is the mode's own craft block. A mode that declares none
        /// gets none, which is why only the invasion has ships.
        /// </summary>
        public void Update(JsonValue declared, Vector3 centre, bool allowed)
        {
            if (!Enabled || !declared.IsObject || !allowed)
            {
                if (_craft.Count > 0) { Clear(); }
                return;
            }

            if (Game.GameTime < _nextUpdateAt) { return; }
            _nextUpdateAt = Game.GameTime + _config.GetInt("features.craft.updateIntervalMs", 250);

            Prune();
            TopUp(declared, centre);
            Drift(centre);
        }

        private void Prune()
        {
            for (int i = _craft.Count - 1; i >= 0; i--)
            {
                if (_craft[i].Prop != null && _craft[i].Prop.Exists()) { continue; }

                if (_craft[i].Blip != null && _craft[i].Blip.Exists()) { _craft[i].Blip.Delete(); }
                _craft.RemoveAt(i);
            }
        }

        private void TopUp(JsonValue declared, Vector3 centre)
        {
            int wanted = Math.Min(declared["count"].AsInt(2), _config.GetInt("features.craft.maxActive", 4));
            if (_craft.Count >= wanted) { return; }

            if (_resolved == null)
            {
                _resolved = _models.ResolveAll(declared["models"].AsStringList());

                if (_resolved.Count == 0 && !_reportedNone)
                {
                    _reportedNone = true;
                    Log.Warn("No craft models are installed - the invasion will arrive without ships.");
                }
            }

            if (_resolved.Count == 0) { return; }

            Model model = _resolved[_random.Next(_resolved.Count)];
            if (!_models.Load(model)) { return; }

            var craft = new Craft
            {
                Angle = _random.NextDouble() * Math.PI * 2.0,
                Radius = declared["orbitRadius"].AsFloat(90f),
                Height = declared["height"].AsFloat(110f),
                Bob = (float)_random.NextDouble() * 6f
            };

            Vector3 position = PositionOf(craft, centre);

            Prop prop = World.CreateProp(model, position, false, false);
            if (prop == null || !prop.Exists()) { return; }

            craft.Prop = prop;

            try
            {
                prop.IsPersistent = true;
                // A prop this size with collision on would swat aircraft and land on rooftops.
                Function.Call(Hash.SET_ENTITY_COLLISION, prop, false, false);
                Function.Call(Hash.FREEZE_ENTITY_POSITION, prop, true);
                Function.Call(Hash.SET_ENTITY_LOD_DIST, prop, 2000);

                if (_config.GetBool("features.craft.blip", true))
                {
                    Blip blip = prop.AddBlip();
                    blip.Sprite = BlipSprite.Standard;
                    blip.Color = BlipColor.GreenDark;
                    blip.Name = declared["name"].AsString("Craft");
                    craft.Blip = blip;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not prepare a craft", ex);
            }

            _craft.Add(craft);
            Log.Info("Craft on station: " + _craft.Count + " overhead.");
        }

        private void Drift(Vector3 centre)
        {
            float speed = _config.GetFloat("features.craft.orbitSpeed", 0.06f);

            foreach (Craft craft in _craft)
            {
                if (craft.Prop == null || !craft.Prop.Exists()) { continue; }

                craft.Angle += speed * 0.25;
                craft.Bob += 0.08f;

                try
                {
                    // Frozen, so it is moved rather than flown. Anything else and it falls.
                    craft.Prop.Position = PositionOf(craft, centre);
                    craft.Prop.Heading = (float)(craft.Angle * 180.0 / Math.PI) + 90f;
                }
                catch (Exception ex)
                {
                    Log.Error("Could not move a craft", ex);
                }
            }
        }

        private Vector3 PositionOf(Craft craft, Vector3 centre)
        {
            return new Vector3(
                centre.X + (float)Math.Cos(craft.Angle) * craft.Radius,
                centre.Y + (float)Math.Sin(craft.Angle) * craft.Radius,
                centre.Z + craft.Height + (float)Math.Sin(craft.Bob) * 3f);
        }
    }
}
