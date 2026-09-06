using System;
using System.Collections.Generic;
using System.Diagnostics;
using GTA;
using GTA.Native;
using TonightsTheNight.Config;
using TonightsTheNight.Factions;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// Runs the active riot: recruits peds into factions, keeps them tasked, and puts the world
    /// back when it stops.
    ///
    /// Two rules shape everything here. First, convert rather than spawn wherever possible -
    /// the engine has already paid to stream the ambient crowd, and the ped pool is the hard
    /// ceiling every riot mod eventually hits. Second, never touch every ped every tick: work
    /// is round-robined under a budget that shrinks when frame time suffers.
    /// </summary>
    public sealed class Director
    {
        private readonly ConfigStore _config;
        private readonly EntityRegistry _registry = new EntityRegistry();
        private readonly RelationshipMatrix _relationships = new RelationshipMatrix();
        private readonly PedConditioner _conditioner;
        private readonly Random _random = new Random();
        private readonly Stopwatch _stopwatch = new Stopwatch();

        private int _cursor;            // round-robin position in the tracked list
        private int _recruitRotation;   // spreads recruits evenly across factions
        private int _budget;

        public RiotMode ActiveMode { get; private set; }
        public bool IsRunning { get { return ActiveMode != null; } }

        // Live numbers for the debug overlay and, more importantly, for the log.
        public int TrackedCount { get { return _registry.Count; } }
        public int RecruitedTotal { get; private set; }
        public int CulledTotal { get; private set; }
        public double LastTickMs { get; private set; }
        public double PeakTickMs { get; private set; }
        public int CurrentBudget { get { return _budget; } }

        public Director(ConfigStore config)
        {
            _config = config;
            _conditioner = new PedConditioner(config, _relationships, _random);
            _budget = config.GetInt("engine.pedsPerTick", 12);
        }

        public void Start(RiotMode mode)
        {
            if (IsRunning) { Stop(); }

            Log.Info("Starting mode '" + mode.Id + "' (" + mode.Name + ") with " + mode.Factions.Count + " faction(s).");

            _config.SetModeOverrides(mode.Overrides);
            CombatAttribute.LoadOverrides(_config);

            _relationships.RegisterPlayerGroup();
            foreach (Faction faction in mode.Factions)
            {
                faction.GroupHash = _relationships.Register(faction.RelationshipGroupName);
            }

            ApplyRelations(mode);
            ApplyPlayerSide(mode);

            ActiveMode = mode;
            RecruitedTotal = 0;
            CulledTotal = 0;
            PeakTickMs = 0;
            _cursor = 0;
            _budget = _config.GetInt("engine.pedsPerTick", 12);

            GTA.UI.Notification.Show("~r~Tonight's The Night~s~: " + mode.Name);
        }

        public void Stop()
        {
            if (!IsRunning) { return; }

            string id = ActiveMode.Id;
            Log.Info("Stopping mode '" + id + "'. Recruited " + RecruitedTotal + ", culled " + CulledTotal + ", peak tick " + PeakTickMs.ToString("F2") + "ms.");

            if (_config.GetBool("riot.restoreWorldOnStop", true))
            {
                _registry.RestoreAll();
            }

            _relationships.Clear();
            _config.ClearModeOverrides();
            ActiveMode = null;

            GTA.UI.Notification.Show("~g~Tonight's The Night~s~: stopped");
        }

        /// <summary>Called from the script's Aborted handler. Must not throw.</summary>
        public void EmergencyCleanup()
        {
            try
            {
                _registry.RestoreAll();
                _relationships.Clear();
                ActiveMode = null;
            }
            catch (Exception ex)
            {
                Log.Error("Emergency cleanup failed", ex);
            }
        }

        public void Tick()
        {
            if (!IsRunning) { return; }

            _stopwatch.Restart();
            try
            {
                ApplyDensity();
                Recruit();
                ProcessTracked();
            }
            catch (Exception ex)
            {
                Log.Error("Director tick failed", ex);
            }
            finally
            {
                _stopwatch.Stop();
                LastTickMs = _stopwatch.Elapsed.TotalMilliseconds;
                if (LastTickMs > PeakTickMs) { PeakTickMs = LastTickMs; }
                AdjustBudget();
            }
        }

        /// <summary>
        /// Shrinks the per-tick workload when we are costing too much and grows it back when
        /// we are cheap. Keeps a busy riot from turning into a slideshow on its own.
        /// </summary>
        private void AdjustBudget()
        {
            if (!_config.GetBool("engine.adaptiveBudget", true)) { return; }

            int configured = _config.GetInt("engine.pedsPerTick", 12);
            double target = _config.GetFloat("engine.targetTickMs", 1.5f);

            if (LastTickMs > target * 1.5 && _budget > 2)
            {
                _budget--;
            }
            else if (LastTickMs < target * 0.5 && _budget < configured)
            {
                _budget++;
            }
        }

        /// <summary>Density natives are per-frame, so they are reapplied every tick or not at all.</summary>
        private void ApplyDensity()
        {
            if (!_config.GetBool("density.enabled", true)) { return; }

            float peds = _config.GetFloat("density.pedMultiplier", 1.5f);
            float vehicles = _config.GetFloat("density.vehicleMultiplier", 0.8f);
            float scenario = _config.GetFloat("density.scenarioMultiplier", 0.4f);

            Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, peds);
            Function.Call(Hash.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME, scenario, scenario);
            Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, vehicles);
            Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, vehicles);
        }

        private void Recruit()
        {
            if (!_config.GetBool("riot.convertAmbientPeds", true)) { return; }
            if (_registry.Count >= _config.GetInt("engine.maxTrackedPeds", 120)) { return; }

            var eligible = new List<Faction>();
            foreach (Faction faction in ActiveMode.Factions)
            {
                if (!string.Equals(faction.Recruits, "none", StringComparison.OrdinalIgnoreCase))
                {
                    eligible.Add(faction);
                }
            }
            if (eligible.Count == 0) { return; }

            Ped player = Game.Player.Character;
            float radius = _config.GetFloat("riot.recruitRadius", 180f);
            double chance = _config.GetFloat("riot.conversionChance", 0.85f);

            Ped[] nearby = World.GetNearbyPeds(player, radius);
            int converted = 0;

            foreach (Ped ped in nearby)
            {
                if (converted >= _budget) { break; }
                if (!IsRecruitable(ped, player)) { continue; }
                if (_random.NextDouble() > chance) { continue; }

                // Round-robin rather than random keeps faction sizes even, which matters when
                // one side being outnumbered three to one just looks like a bug.
                Faction faction = eligible[_recruitRotation++ % eligible.Count];
                if (!string.Equals(faction.Recruits, "any", StringComparison.OrdinalIgnoreCase) && !MatchesFilter(ped, faction.Recruits))
                {
                    continue;
                }

                Reaction reaction = faction.ResolveReaction(_random);
                TrackedPed entry = _registry.Add(ped, faction, reaction, false);
                _conditioner.Apply(ped, faction, reaction);
                entry.LastTaskedAt = Game.GameTime;

                AttachBlip(entry);

                converted++;
                RecruitedTotal++;
            }
        }

        private bool IsRecruitable(Ped ped, Ped player)
        {
            if (ped == null || !ped.Exists()) { return false; }
            if (ped.Handle == player.Handle) { return false; }
            if (ped.IsDead || !ped.IsAlive) { return false; }
            if (_registry.Contains(ped)) { return false; }
            if (Function.Call<bool>(Hash.IS_PED_A_PLAYER, ped)) { return false; }
            if (!Function.Call<bool>(Hash.IS_PED_HUMAN, ped)) { return false; }

            // Mission peds belong to the story; hijacking them breaks quests in ways that are
            // very hard to attribute back to this mod.
            if (Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, ped)) { return false; }

            return true;
        }

        private static bool MatchesFilter(Ped ped, string filter)
        {
            int pedType = Function.Call<int>(Hash.GET_PED_TYPE, ped);
            const int CivMale = 4;
            const int CivFemale = 5;

            switch ((filter ?? string.Empty).ToLowerInvariant())
            {
                case "civilian": return pedType == CivMale || pedType == CivFemale;
                case "male": return pedType == CivMale;
                case "female": return pedType == CivFemale;
                case "any": return true;
                default: return false;
            }
        }

        /// <summary>
        /// Round-robins over the tracked list: prunes the dead, releases anyone who has wandered
        /// out of range, and re-tasks idle fighters. Only <see cref="_budget"/> peds per tick.
        /// </summary>
        private void ProcessTracked()
        {
            _registry.PruneDead();

            IReadOnlyList<TrackedPed> tracked = _registry.Tracked;
            if (tracked.Count == 0) { return; }

            Ped player = Game.Player.Character;
            GTA.Math.Vector3 origin = player.Position;

            // Compared squared to keep a square root out of the per-ped path.
            float cull = _config.GetFloat("engine.cullDistance", 450f);
            float cullSquared = cull * cull;
            int retaskAfterMs = 4000;
            int examined = 0;

            while (examined < _budget && tracked.Count > 0)
            {
                if (_cursor >= tracked.Count) { _cursor = 0; }

                TrackedPed entry = tracked[_cursor];
                examined++;

                if (!entry.IsUsable)
                {
                    _registry.Remove(entry, false);
                    continue;
                }

                float rangeSquared = origin.DistanceToSquared(entry.Ped.Position);

                if (rangeSquared > cullSquared)
                {
                    _registry.Remove(entry, true);
                    CulledTotal++;
                    continue;
                }

                if (entry.Reaction == Reaction.Fight &&
                    !entry.Ped.IsInCombat &&
                    Game.GameTime - entry.LastTaskedAt > retaskAfterMs)
                {
                    _conditioner.IssueTask(entry.Ped, entry.Faction, entry.Reaction);
                    entry.LastTaskedAt = Game.GameTime;
                }

                UpdateBlip(entry, rangeSquared);
                _cursor++;
            }
        }

        private void AttachBlip(TrackedPed entry)
        {
            if (!_config.GetBool("blips.enabled", true)) { return; }
            if (!entry.Faction.BlipEnabled) { return; }
            if (CountBlips() >= _config.GetInt("blips.maxBlips", 40)) { return; }

            try
            {
                Blip blip = entry.Ped.AddBlip();
                blip.Sprite = entry.Faction.BlipSprite;
                blip.Color = entry.Faction.BlipColor;
                blip.Scale = _config.GetFloat("blips.scale", 0.6f);
                blip.Name = entry.Faction.DisplayName;
                blip.IsShortRange = true;
                entry.Blip = blip;
            }
            catch (Exception ex)
            {
                Log.Error("Could not add a blip", ex);
            }
        }

        private void UpdateBlip(TrackedPed entry, float rangeSquared)
        {
            if (entry.Blip == null || !entry.Blip.Exists()) { return; }
            if (!_config.GetBool("blips.showOnlyWhenNear", true)) { return; }

            float maxDistance = _config.GetFloat("blips.maxDistance", 200f);
            bool visible = rangeSquared <= maxDistance * maxDistance;

            if (entry.Blip.Alpha != (visible ? 255 : 0))
            {
                entry.Blip.Alpha = visible ? 255 : 0;
            }
        }

        private int CountBlips()
        {
            int count = 0;
            foreach (TrackedPed entry in _registry.Tracked)
            {
                if (entry.Blip != null) { count++; }
            }
            return count;
        }

        private void ApplyRelations(RiotMode mode)
        {
            foreach (Relation relation in mode.Relations)
            {
                Faction from = mode.Find(relation.From);
                Faction to = mode.Find(relation.To);

                if (from == null || to == null)
                {
                    Log.Warn("Relation " + relation.From + " -> " + relation.To + " names a faction this mode does not define. Skipped.");
                    continue;
                }

                if (relation.Mutual)
                {
                    _relationships.SetMutual(from.GroupHash, to.GroupHash, relation.Value);
                }
                else
                {
                    _relationships.Set(from.GroupHash, to.GroupHash, relation.Value);
                }
            }
        }

        /// <summary>
        /// The player is just another relationship group, which is what makes "walk through it
        /// as press" and "join a side" the same mechanism rather than two features.
        /// </summary>
        private void ApplyPlayerSide(RiotMode mode)
        {
            string side = _config.GetString("player.side", "neutral");
            bool everyoneHostile = _config.GetBool("player.everyoneHatesPlayer", false);

            Faction joined = string.Equals(side, "neutral", StringComparison.OrdinalIgnoreCase) ? null : mode.Find(side);
            int playerGroup = joined != null ? joined.GroupHash : _relationships.PlayerGroup;

            _relationships.ApplyToPed(Game.Player.Character, playerGroup);

            if (joined != null)
            {
                Log.Info("Player joined faction '" + joined.Id + "'.");
                return;
            }

            int stance = everyoneHostile ? RelationshipMatrix.Hate : mode.PlayerRelationship;
            foreach (Faction faction in mode.Factions)
            {
                _relationships.Set(faction.GroupHash, _relationships.PlayerGroup, stance);
                _relationships.Set(_relationships.PlayerGroup, faction.GroupHash, RelationshipMatrix.Neutral);
            }

            Log.Info("Player is neutral (factions treat them as " + stance + ").");
        }
    }
}
