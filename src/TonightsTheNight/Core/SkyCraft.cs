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

            /// <summary>Position in the formation. Decides its lane, its altitude and its phase.</summary>
            public int Slot;

            public double Angle;

            /// <summary>The mode's declared orbit, before this craft's lane and tier are applied.</summary>
            public float BaseRadius;
            public float BaseHeight;

            public float Radius;
            public float Height;
            public float Bob;

            /// <summary>Where it is heading this frame, before separation is applied.</summary>
            public Vector3 Desired;

            /// <summary>Where it actually is, held here so movement can be smoothed towards Desired.</summary>
            public Vector3 Current;

            /// <summary>False until the first move, so a new craft starts where it was created.</summary>
            public bool Placed;
        }

        private readonly ConfigStore _config;
        private readonly ModelResolver _models;
        private readonly Random _random;
        private readonly List<Craft> _craft = new List<Craft>();

        private List<Model> _resolved;
        private int _nextUpdateAt;
        private bool _reportedNone;

        /// <summary>
        /// Shared phase for the whole formation, advanced in real time rather than per update.
        ///
        /// This used to be a per-craft angle stepped by a fixed amount inside the throttled
        /// update, which meant every ship jumped four times a second regardless of frame rate —
        /// the "choppy" part. Time drives it now, and the props are moved every tick.
        /// </summary>
        private double _phase;

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
            _phase = 0;
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
        ///
        /// Falls back to the ground under the ship when the navmesh has nothing to offer, which
        /// is the normal case out in the desert and was previously the reason an invasion in a
        /// quiet area produced no aliens at all.
        /// </summary>
        public Vector3 DropPoint()
        {
            if (_craft.Count == 0) { return Vector3.Zero; }

            Craft craft = _craft[_random.Next(_craft.Count)];
            if (craft.Prop == null || !craft.Prop.Exists()) { return Vector3.Zero; }

            Vector3 under = craft.Prop.Position;
            float scatter = _config.GetFloat("features.craft.dropScatter", 20f);

            var candidate = new Vector3(
                under.X + (float)(_random.NextDouble() * 2 - 1) * scatter,
                under.Y + (float)(_random.NextDouble() * 2 - 1) * scatter,
                under.Z);

            return Ground.Place(candidate);
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

            // Streaming and blip work is throttled; moving is not. A fleet that only updates
            // four times a second visibly steps rather than flies.
            if (Game.GameTime >= _nextUpdateAt)
            {
                _nextUpdateAt = Game.GameTime + _config.GetInt("features.craft.updateIntervalMs", 250);
                Prune();
                TopUp(declared, centre);
                Reslot();
            }

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
                Slot = _craft.Count,
                BaseRadius = declared["orbitRadius"].AsFloat(90f),
                BaseHeight = declared["height"].AsFloat(110f),
                Bob = (float)_random.NextDouble() * 6f
            };

            _craft.Add(craft);
            Reslot();
            ApplyFormation(craft, centre);

            Vector3 position = craft.Desired;
            craft.Current = position;
            craft.Placed = true;

            Prop prop = World.CreateProp(model, position, false, false);
            if (prop == null || !prop.Exists())
            {
                _craft.Remove(craft);
                return;
            }

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

            Log.Info("Craft on station: " + _craft.Count + " overhead, slot " + craft.Slot + ".");
        }

        /// <summary>
        /// Renumbers the formation after an addition or a loss.
        ///
        /// Slots are what stop the fleet occupying the same piece of sky. Each craft owns one
        /// share of the orbit, one lane and one altitude band, so two of them cannot converge
        /// however long they fly. Before this they all shared a radius, a height and an angular
        /// speed, and were given random starting angles — so two that happened to start close
        /// together stayed inside each other for the whole invasion.
        /// </summary>
        private void Reslot()
        {
            for (int i = 0; i < _craft.Count; i++) { _craft[i].Slot = i; }
        }

        /// <summary>Where a craft's slot says it should be, before separation and smoothing.</summary>
        private void ApplyFormation(Craft craft, Vector3 centre)
        {
            int total = Math.Max(1, _craft.Count);

            // Alternating lanes, so an odd fleet does not put every ship on the same circle.
            float lane = ((craft.Slot % 3) - 1) * _config.GetFloat("features.craft.radiusSpread", 25f);
            // Stacked altitudes. Two ships directly above one another still never touch.
            float tier = craft.Slot * _config.GetFloat("features.craft.heightSpread", 18f);

            craft.Radius = Math.Max(20f, craft.BaseRadius + lane);
            craft.Height = craft.BaseHeight + tier;
            craft.Angle = _phase + craft.Slot * (Math.PI * 2.0 / total);

            craft.Desired = new Vector3(
                centre.X + (float)Math.Cos(craft.Angle) * craft.Radius,
                centre.Y + (float)Math.Sin(craft.Angle) * craft.Radius,
                centre.Z + craft.Height + (float)Math.Sin(craft.Bob) * 3f);
        }

        private void Drift(Vector3 centre)
        {
            if (_craft.Count == 0) { return; }

            // Real seconds, so the fleet moves at the same speed at 30fps and at 120.
            float delta = Game.LastFrameTime;
            if (delta <= 0f || delta > 0.5f) { delta = 1f / 60f; }

            _phase += _config.GetFloat("features.craft.orbitSpeed", 0.06f) * delta;

            foreach (Craft craft in _craft)
            {
                craft.Bob += delta * 0.6f;
                ApplyFormation(craft, centre);
            }

            Separate();

            float smoothing = _config.GetFloat("features.craft.smoothing", 6f);
            float blend = smoothing <= 0f ? 1f : Math.Min(1f, smoothing * delta);

            foreach (Craft craft in _craft)
            {
                if (craft.Prop == null || !craft.Prop.Exists()) { continue; }

                if (!craft.Placed)
                {
                    craft.Current = craft.Desired;
                    craft.Placed = true;
                }
                else
                {
                    craft.Current += (craft.Desired - craft.Current) * blend;
                }

                try
                {
                    // Frozen, so it is moved rather than flown. Anything else and it falls.
                    craft.Prop.Position = craft.Current;
                    craft.Prop.Heading = (float)(craft.Angle * 180.0 / Math.PI) + 90f;
                }
                catch (Exception ex)
                {
                    Log.Error("Could not move a craft", ex);
                }
            }
        }

        /// <summary>
        /// Pushes any two ships that have ended up too close apart along the line between them.
        ///
        /// The slots make convergence very unlikely on their own, but the orbit centre moves
        /// with the player, a fleet can lose a member mid-flight and renumber, and a mode is
        /// free to declare a tiny orbit radius. This is the guarantee rather than the plan.
        /// </summary>
        private void Separate()
        {
            float minimum = _config.GetFloat("features.craft.minSeparation", 45f);
            if (minimum <= 0f || _craft.Count < 2) { return; }

            float minimumSquared = minimum * minimum;

            for (int i = 0; i < _craft.Count; i++)
            {
                for (int j = i + 1; j < _craft.Count; j++)
                {
                    Vector3 offset = _craft[j].Desired - _craft[i].Desired;
                    float distanceSquared = offset.LengthSquared();

                    if (distanceSquared >= minimumSquared) { continue; }

                    // Exactly coincident has no direction to push along, so invent one from
                    // the slot numbers rather than dividing by zero.
                    Vector3 push = distanceSquared < 0.01f
                        ? new Vector3((float)Math.Cos(i * 2.4), (float)Math.Sin(i * 2.4), 0.35f)
                        : offset.Normalized;

                    float correction = (minimum - (float)Math.Sqrt(distanceSquared)) * 0.5f;

                    _craft[i].Desired -= push * correction;
                    _craft[j].Desired += push * correction;
                }
            }
        }
    }
}
