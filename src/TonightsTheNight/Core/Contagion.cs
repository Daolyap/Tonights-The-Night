using System;
using System.Collections.Generic;
using System.Drawing;
using GTA;
using GTA.Math;
using TonightsTheNight.Config;
using TonightsTheNight.Factions;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// A riot that recruits for itself.
    ///
    /// Every other mode here has the same underlying arc, because every other mode is fed by the
    /// same two supply lines: convert the crowd that is already streamed in, and spawn what has
    /// no ambient equivalent. Both are capped, so a riot rises to a level and stays there.
    ///
    /// Contagion is a third supply line, and it is the interesting one: the riot converts people
    /// itself, by reaching them. Nothing is spawned and nothing is recruited from a radius around
    /// the player — a faction grows only where its members physically are, which means the thing
    /// spreads outward from wherever it started, thins where it is being killed, and can be
    /// contained by killing the right people rather than by a setting.
    ///
    /// That was true only of the crowd you could see, and it is the bug this class now fixes.
    /// Contact spread runs on the tracked list, and the tracked list is a couple of hundred metres
    /// wide, so an outbreak existed only where the player was standing. Drive four blocks and the
    /// infected despawned with the street; drive back and the district was clean. The outbreak was
    /// not being contained, it was being un-witnessed.
    ///
    /// So there are two layers now. Contact spread is unchanged and still does the visible work,
    /// person to person, in front of you. Underneath it an epidemic runs on the clock: an origin,
    /// a front that widens whether or not anybody is watching, and a prevalence that climbs on its
    /// own curve. Anybody the riot takes over inside that front has a prevalence-weighted chance
    /// of already being infected when you meet them — which is what makes leaving cost you rather
    /// than save you, and what makes coming back to a street you cleared an hour ago mean
    /// something.
    ///
    /// It is beatable, and beatable locally: every infected killed leaves a suppression mark on
    /// that patch of city that thins the seeding there for a while. Holding a block is a real
    /// thing you can do. Holding the county is not.
    /// </summary>
    public sealed class Contagion
    {
        /// <summary>
        /// One patch of city that has been cleared recently, and how hard.
        ///
        /// Bounded to a handful and merged by proximity, because the alternative is a spatial
        /// index for a mechanic whose entire job is to answer one float per recruited pedestrian.
        /// </summary>
        private sealed class Clearance
        {
            public Vector3 At;
            public float Strength;
            public int FadesAt;
        }

        private const int MaxClearances = 12;

        private readonly ConfigStore _config;
        private readonly Random _random;
        private readonly List<Clearance> _cleared = new List<Clearance>();

        private JsonValue _declared = JsonValue.Null;
        private int _nextPassAt;
        private int _cursor;

        private Vector3 _origin;
        private bool _anchored;
        private int _lastAdvanceAt;
        private int _announcedTenth;

        /// <summary>How many people this infection has taken by contact, in front of you.</summary>
        public int Converted { get; private set; }

        /// <summary>How many were already infected when the riot reached them.</summary>
        public int Seeded { get; private set; }

        /// <summary>How many infected have been put down.</summary>
        public int Suppressed { get; private set; }

        /// <summary>Everyone this outbreak has claimed, by either route.</summary>
        public int Total { get { return Converted + Seeded; } }

        /// <summary>How many infected are alive and tracked right now.</summary>
        public int Alive { get; private set; }

        /// <summary>What share of the crowd inside the front has it. 0 to 1.</summary>
        public float Prevalence { get; private set; }

        /// <summary>How far the outbreak has reached from where it started, in metres.</summary>
        public float Front { get; private set; }

        public Contagion(ConfigStore config, Random random)
        {
            _config = config;
            _random = random;
        }

        public bool Active
        {
            get
            {
                return _declared.IsObject && _declared["enabled"].AsBool(true) &&
                       _config.GetBool("features.contagion.enabled", true);
            }
        }

        public void Begin(JsonValue declared)
        {
            _declared = declared == null ? JsonValue.Null : declared;
            Converted = 0;
            Seeded = 0;
            Suppressed = 0;
            Alive = 0;
            _nextPassAt = 0;
            _cursor = 0;
            _cleared.Clear();
            _anchored = false;
            _announcedTenth = 0;
            _lastAdvanceAt = Game.GameTime;

            Prevalence = Math.Max(0f, Math.Min(1f, _declared["startingPrevalence"].AsFloat(
                _config.GetFloat("features.contagion.startingPrevalence", 0.03f))));
            Front = Math.Max(0f, _declared["startingFront"].AsFloat(
                _config.GetFloat("features.contagion.startingFront", 140f)));

            if (Active)
            {
                Log.Info("Contagion armed: '" + _declared["source"].AsString("?") + "' spreads to '" +
                         _declared["target"].AsString("?") + "'.");
            }
        }

        public void Clear()
        {
            _declared = JsonValue.Null;
            Converted = 0;
            Seeded = 0;
            Suppressed = 0;
            Alive = 0;
            Prevalence = 0f;
            Front = 0f;
            _cleared.Clear();
            _anchored = false;
        }

        // ------------------------------------------------------------------ the epidemic

        /// <summary>
        /// The half of this that does not need anybody to be watching.
        ///
        /// Called every tick rather than on the work throttle: it is two multiplications, and the
        /// whole point of it is that the outbreak keeps its own time. Sitting in the menu, driving
        /// to Sandy Shores and standing still are all the same to it.
        /// </summary>
        public void Advance(Vector3 playerPosition)
        {
            if (!Active) { return; }

            if (!_anchored)
            {
                // Where it started. Everything geographic is measured from here, so it is taken
                // once - from wherever the mode was begun - and never moved.
                _origin = playerPosition;
                _anchored = true;
            }

            int now = Game.GameTime;
            float seconds = (now - _lastAdvanceAt) / 1000f;
            _lastAdvanceAt = now;

            // A pause, a load or a hitch must not hand the epidemic a free hour.
            if (seconds <= 0f) { return; }
            if (seconds > 2f) { seconds = 2f; }

            // Logistic, because that is the shape an epidemic actually has: slow while it is rare,
            // fast in the middle, and flattening as it runs out of people who have not had it.
            float ceiling = Math.Max(0.05f, Math.Min(1f, _declared["maxPrevalence"].AsFloat(
                _config.GetFloat("features.contagion.maxPrevalence", 0.9f))));
            float rate = Math.Max(0f, _declared["growthPerSecond"].AsFloat(
                _config.GetFloat("features.contagion.growthPerSecond", 0.012f)));

            float floor = _config.GetFloat("features.contagion.startingPrevalence", 0.03f);
            if (Prevalence < floor * 0.5f) { Prevalence = floor * 0.5f; }

            Prevalence += rate * Prevalence * (1f - Prevalence / ceiling) * seconds;
            if (Prevalence > ceiling) { Prevalence = ceiling; }
            if (Prevalence < 0f) { Prevalence = 0f; }

            // The front widens on the clock too, faster once the thing is established. This is
            // the part that makes distance a delay rather than an escape.
            float spread = Math.Max(0f, _declared["frontMetresPerSecond"].AsFloat(
                _config.GetFloat("features.contagion.frontMetresPerSecond", 7f)));
            Front += spread * (0.4f + Prevalence) * seconds;

            FadeClearances(now);
            AnnounceProgress();
        }

        /// <summary>
        /// Whether somebody the riot is about to take over is already infected.
        ///
        /// This is the entire load-bearing idea. The crowd near you is not a fresh population that
        /// happens to be next to an outbreak; it is a sample of a district that has one. Outside
        /// the front, nobody. Inside it, a share that is highest at the origin, thinner at the
        /// edge, and thinner still anywhere you have recently been killing them.
        /// </summary>
        public bool IsAlreadyInfected(Vector3 at)
        {
            if (!Active || !_anchored) { return false; }
            return _random.NextDouble() < SeedChance(at);
        }

        /// <summary>The share of people at a point who have it. Exposed for the debug overlay.</summary>
        public float SeedChance(Vector3 at)
        {
            if (!Active || !_anchored || Front <= 1f) { return 0f; }

            float distance = at.DistanceTo(_origin);
            if (distance > Front) { return 0f; }

            // Deepest at the origin, thinnest at the rim, but never nothing at the rim - an
            // outbreak with a hard edge reads as a circle drawn on a map rather than as a thing
            // spreading through a city.
            float rim = _config.GetFloat("features.contagion.edgeShare", 0.3f);
            float depth = 1f - distance / Front;
            float chance = Prevalence * (rim + (1f - rim) * depth);

            return Math.Max(0f, chance - ClearanceAt(at));
        }

        /// <summary>
        /// Somebody put an infected down. The street they did it on gets easier for a while.
        ///
        /// Global prevalence barely moves for one kill, and it should not: an epidemic is not
        /// beaten by attrition. What a kill buys is local, temporary, and enough to hold a block
        /// while you do something about the block.
        /// </summary>
        public void NoteSuppressed(Vector3 where)
        {
            if (!Active) { return; }

            Suppressed++;

            float perKill = Math.Max(0f, _config.GetFloat("features.contagion.suppressionPerKill", 0.02f));
            Prevalence = Math.Max(0f, Prevalence - perKill * 0.1f);

            float radius = _config.GetFloat("features.contagion.clearRadius", 70f);
            int fade = Math.Max(1000, _config.GetInt("features.contagion.clearMs", 90000));
            float cap = Math.Max(0f, _config.GetFloat("features.contagion.maxSuppression", 0.55f));

            foreach (Clearance clearance in _cleared)
            {
                if (clearance.At.DistanceTo(where) > radius) { continue; }

                clearance.Strength = Math.Min(cap, clearance.Strength + perKill);
                clearance.FadesAt = Game.GameTime + fade;
                return;
            }

            if (_cleared.Count >= MaxClearances)
            {
                // Oldest out. A cleared patch nobody has been back to is the one worth losing.
                int oldest = 0;
                for (int i = 1; i < _cleared.Count; i++)
                {
                    if (_cleared[i].FadesAt < _cleared[oldest].FadesAt) { oldest = i; }
                }
                _cleared.RemoveAt(oldest);
            }

            _cleared.Add(new Clearance
            {
                At = where,
                Strength = Math.Min(cap, perKill),
                FadesAt = Game.GameTime + fade
            });
        }

        private float ClearanceAt(Vector3 at)
        {
            if (_cleared.Count == 0) { return 0f; }

            float radius = _config.GetFloat("features.contagion.clearRadius", 70f);
            float radiusSquared = radius * radius;
            float strongest = 0f;

            foreach (Clearance clearance in _cleared)
            {
                float distance = clearance.At.DistanceToSquared(at);
                if (distance > radiusSquared) { continue; }

                // Full strength at the centre, tapering to nothing at the edge, so a cleared
                // block has a shape rather than a boundary you can stand astride.
                float share = clearance.Strength * (1f - (float)Math.Sqrt(distance) / radius);
                if (share > strongest) { strongest = share; }
            }

            return strongest;
        }

        private void FadeClearances(int now)
        {
            for (int i = _cleared.Count - 1; i >= 0; i--)
            {
                if (now < _cleared[i].FadesAt) { continue; }
                _cleared.RemoveAt(i);
            }
        }

        private void AnnounceProgress()
        {
            if (!_config.GetBool("features.contagion.announce", true)) { return; }

            int tenth = (int)(Prevalence * 10f);
            if (tenth <= _announcedTenth) { return; }

            // Only the ones worth interrupting for. Every ten per cent is a notification every
            // half-minute, which is how a mechanic becomes wallpaper.
            _announcedTenth = tenth;
            if (tenth != 3 && tenth != 5 && tenth != 8) { return; }

            GTA.UI.Notification.Show("~r~Outbreak:~s~ " + (tenth * 10) + "% of the district is infected.");
            Log.Info("Contagion prevalence passed " + (tenth * 10) + "% with " + Total + " taken.");
        }

        // ------------------------------------------------------------------ contact

        /// <summary>
        /// One pass of spreading. <paramref name="convert"/> does the actual work, because
        /// changing somebody's faction means re-conditioning and re-blipping them and the
        /// Director owns both of those.
        /// </summary>
        public void Update(IReadOnlyList<TrackedPed> tracked, RiotMode mode, Action<TrackedPed, Faction> convert)
        {
            if (!Active || tracked.Count == 0) { return; }

            if (Game.GameTime < _nextPassAt) { return; }
            _nextPassAt = Game.GameTime + Math.Max(200, _declared["intervalMs"].AsInt(1200));

            Faction carrier = Carrier(mode);
            Faction victim = mode.Find(_declared["target"].AsString(null));

            if (carrier == null || victim == null)
            {
                Log.Warn("Contagion names a faction this mode does not define. Disabled for this run.");
                _declared = JsonValue.Null;
                return;
            }

            CountAlive(tracked, carrier);

            int ceiling = _declared["max"].AsInt(0);
            if (ceiling > 0 && Total >= ceiling) { return; }

            float radius = _declared["radius"].AsFloat(3.5f);
            float radiusSquared = radius * radius;
            double chance = _declared["chance"].AsFloat(0.55f);

            // Bounded like everything else that walks the tracked list. Over a few seconds this
            // covers the whole crowd, and an infection that spreads a fraction of a second later
            // than it could have is not something anybody can see.
            int window = Math.Min(tracked.Count, Math.Max(12, _declared["samplesPerPass"].AsInt(20)));
            int spread = 0;
            int perPass = Math.Max(1, _declared["perPass"].AsInt(3));

            for (int offset = 0; offset < window && spread < perPass; offset++)
            {
                TrackedPed source = tracked[(_cursor + offset) % tracked.Count];

                if (!source.IsUsable || source.Faction != carrier) { continue; }
                if (_random.NextDouble() > chance) { continue; }

                for (int i = 0; i < tracked.Count && spread < perPass; i++)
                {
                    TrackedPed near = tracked[i];

                    if (near == source || !near.IsUsable || near.Faction != victim) { continue; }
                    if (near.Ped.Position.DistanceToSquared(source.Ped.Position) > radiusSquared) { continue; }

                    convert(near, carrier);
                    Converted++;

                    // Contact spread is also evidence. Every person it takes in front of you
                    // nudges the curve the unwatched half of the city is running on.
                    Prevalence = Math.Min(1f, Prevalence +
                        _config.GetFloat("features.contagion.growthPerContact", 0.0015f));

                    spread++;

                    if (ceiling > 0 && Total >= ceiling) { return; }
                }
            }

            _cursor = (_cursor + window) % tracked.Count;
        }

        /// <summary>The faction people turn into, or null when this mode does not name one.</summary>
        public Faction Carrier(RiotMode mode)
        {
            if (!Active || mode == null) { return null; }
            return mode.Find(_declared["source"].AsString(null));
        }

        /// <summary>Whether a faction is the one people are turning into.</summary>
        public bool IsCarrier(RiotMode mode, Faction faction)
        {
            if (faction == null) { return false; }
            Faction carrier = Carrier(mode);
            return carrier != null && carrier == faction;
        }

        /// <summary>Counted here rather than by the Director, which already has enough to do.</summary>
        private void CountAlive(IReadOnlyList<TrackedPed> tracked, Faction carrier)
        {
            int alive = 0;
            foreach (TrackedPed entry in tracked)
            {
                if (entry.IsUsable && entry.Faction == carrier) { alive++; }
            }
            Alive = alive;
        }

        /// <summary>Called when a ped that arrived already infected joins the riot.</summary>
        public void NoteSeeded() { Seeded++; }

        // ------------------------------------------------------------------ screen

        /// <summary>
        /// How bad it is, on screen, where the question is actually being asked.
        ///
        /// "How many of them are there" is the only question this mode makes you ask and the only
        /// one it could not answer: the count lived in the log file. Three numbers cover it — how
        /// many are around you now, how many it has taken in total, and how much of the district
        /// is gone — and the bar underneath is the one you watch while deciding whether this
        /// street is still worth holding.
        /// </summary>
        public void Draw()
        {
            if (!Active) { return; }
            if (!_config.GetBool("features.contagion.showCount", true)) { return; }

            string line = "~r~INFECTED~s~   " + Alive + " near you   ~c~" + Total + " turned";
            if (Suppressed > 0) { line += "   ~g~" + Suppressed + " down"; }

            Hud.Banner(line, 0.035f, 0.5f, Color.FromArgb(235, 255, 255, 255));

            Hud.Banner("~c~" + (int)(Prevalence * 100) + "% of the district   " + (int)Front + "m across",
                       0.070f, 0.36f, Color.FromArgb(190, 235, 235, 235));

            Hud.Bar(0.38f, 0.093f, 0.24f, 0.008f, Prevalence,
                    Color.FromArgb(220, 120, 190, 70),
                    Color.FromArgb(140, 20, 20, 20));
        }
    }
}
