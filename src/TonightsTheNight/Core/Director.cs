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
        private readonly ModelResolver _models = new ModelResolver();
        private readonly Spawner _spawner;
        private readonly VehicleBehaviour _vehicles;
        private readonly Random _random = new Random();
        private readonly Pursuit _pursuit;
        private readonly Looting _looting;
        private readonly Reinforcements _reinforcements;
        private readonly SkyCraft _craft;
        private readonly Perception _perception;
        private readonly Spectacle _spectacle;

        public Escalation Escalation { get; private set; }
        public RiotZone Zone { get; private set; }
        public Ambience Ambience { get; private set; }
        public PurgeClock Purge { get; private set; }

        public Pursuit Pursuit { get { return _pursuit; } }
        public Looting Looting { get { return _looting; } }
        public Reinforcements Reinforcements { get { return _reinforcements; } }
        public SkyCraft Craft { get { return _craft; } }
        public Perception Perception { get { return _perception; } }
        public Spectacle Spectacle { get { return _spectacle; } }

        /// <summary>Recruits confirmed dead, as opposed to merely despawned. Drives escalation.</summary>
        public int Kills { get; private set; }
        private readonly Stopwatch _stopwatch = new Stopwatch();

        private int _cursor;            // round-robin position in the tracked list
        private int _recruitRotation;   // spreads recruits evenly across factions
        private int _budget;
        private int _nextWorkAt;        // game time of the next heavy pass
        private Ped _lastFighter;       // a nearby threat for fleeing peds to run from

        /// <summary>Factions repeated by share, so round-robin over it honours the weights.</summary>
        private readonly List<Faction> _recruitPool = new List<Faction>();

        /// <summary>
        /// Faction pairs that dislike or hate each other, as "a|b". Needed because "drive after
        /// an enemy" has to pick a real enemy: a different faction is not the same as a hostile
        /// one, and sending a rioter to ram their own allies reads as broken.
        /// </summary>
        private readonly HashSet<string> _hostilePairs = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// The standing each faction takes towards the player once it can see them. Held rather
        /// than written straight to the matrix, because hostility is now a state that comes and
        /// goes with whether anybody has eyes on you.
        /// </summary>
        private readonly Dictionary<string, int> _hostileToPlayer = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Peds being calmed down after a stop, and the deadline for doing so.</summary>
        private readonly List<Ped> _pacifying = new List<Ped>();
        private int _pacifyUntil;
        private int _nextStatsAt;
        private int _nextVehiclePruneAt;
        private bool _playerMovedGroup;

        public RiotMode ActiveMode { get; private set; }
        public bool IsRunning { get { return ActiveMode != null; } }

        /// <summary>
        /// Set while the menu is open. The riot keeps existing - density, escalation, the purge
        /// clock - but stops recruiting, spawning and retasking, because none of that needs to
        /// happen in the few seconds you spend reading a slider, and the menu itself is not
        /// cheap to draw.
        /// </summary>
        public bool Paused { get; set; }


        // Live numbers for the debug overlay and, more importantly, for the log.
        public int TrackedCount { get { return _registry.Count; } }
        public int RecruitedTotal { get; private set; }
        public int CulledTotal { get; private set; }

        /// <summary>
        /// Recruits that left the registry because they died or despawned. The gap between this
        /// and <see cref="RecruitedTotal"/> is what tells us whether a riot is sustaining itself
        /// or burning through the local population.
        /// </summary>
        public int LostTotal { get; private set; }
        public double LastTickMs { get; private set; }
        public double PeakTickMs { get; private set; }
        public int CurrentBudget { get { return _budget; } }

        public Director(ConfigStore config)
        {
            _config = config;
            _conditioner = new PedConditioner(config, _relationships, _random);
            _reinforcements = new Reinforcements(config);
            _spawner = new Spawner(config, _models, _random, _reinforcements);
            _vehicles = new VehicleBehaviour(config);
            _craft = new SkyCraft(config, _models, _random);
            _perception = new Perception(config, _random);
            _spectacle = new Spectacle(config, _models, _random);
            _pursuit = new Pursuit(config, _random, _registry);
            _looting = new Looting(config, _models, _random, _registry);

            Escalation = new Escalation(config);
            Zone = new RiotZone(config);
            Ambience = new Ambience(config, _random);
            Purge = new PurgeClock(config);

            _budget = config.GetInt("engine.pedsPerTick", 12);
        }

        public void Start(RiotMode mode)
        {
            // Quiet, or switching modes announces a stop the player did not ask for.
            if (IsRunning) { Stop(true); }

            Log.Info("Starting mode '" + mode.Id + "' (" + mode.Name + ") with " + mode.Factions.Count + " faction(s).");

            _config.SetModeOverrides(mode.Overrides);
            CombatAttribute.LoadOverrides(_config);
            WeaponPresets.Reset();

            foreach (Faction faction in mode.Factions)
            {
                faction.GroupHash = _relationships.Register(faction.RelationshipGroupName);
            }

            ApplyRelations(mode);
            BuildHostility(mode);
            // Before the standings, which depend on whether the riot can currently see you.
            _perception.Reset();
            ApplyPlayerSide(mode);

            // Before the pool, because the pool asks the escalation which factions are allowed
            // yet - and asking an unloaded escalation got the previous mode's answer.
            Escalation.Load(mode.Escalation);
            BuildRecruitPool(mode);
            Zone.Begin();
            Ambience.Apply(mode.Ambience);
            Purge.Begin(mode.Purge);
            _spawner.Reset();
            _pursuit.Reset();
            _looting.Reset();
            _reinforcements.Reset();
            _craft.Reset();
            _spectacle.Reset();

            ActiveMode = mode;
            Kills = 0;
            RecruitedTotal = 0;
            CulledTotal = 0;
            LostTotal = 0;
            _nextStatsAt = 0;
            PeakTickMs = 0;
            _cursor = 0;
            _budget = _config.GetInt("engine.pedsPerTick", 12);

            GTA.UI.Notification.Show("~r~Tonight's The Night~s~: " + mode.Name);
        }

        public void Stop() { Stop(false); }

        public void Stop(bool quiet)
        {
            if (!IsRunning) { return; }

            string id = ActiveMode.Id;
            Log.Info("Stopping mode '" + id + "'. Recruited " + RecruitedTotal + ", lost " + LostTotal +
                     ", culled " + CulledTotal + ", still active " + _registry.Count +
                     ", peak tick " + PeakTickMs.ToString("F2") + "ms.");

            // Chases and looting hold their own tasks and props, so they are unwound before
            // the registry hands the peds back.
            _pursuit.EndAll();
            _looting.EndAll();

            bool restore = _config.GetBool("riot.restoreWorldOnStop", true);
            if (restore) { BeginPacifying(_registry.Tracked); }

            // Always, whatever the setting: this is what ends the tracking, not just what
            // undoes the conditioning.
            _registry.ReleaseAll(restore);
            RestorePlayerGroup();

            Ambience.Clear();
            Zone.Clear();
            Purge.Clear();
            _craft.Clear();
            _spectacle.Clear();
            _models.Release();

            _relationships.Clear();
            _config.ClearModeOverrides();
            ActiveMode = null;
            _lastFighter = null;

            if (!quiet) { GTA.UI.Notification.Show("~g~Tonight's The Night~s~: stopped"); }
        }

        /// <summary>
        /// Puts the player back in the vanilla PLAYER group.
        ///
        /// Joining a side moves them into a faction's group, and Clear() then deletes that group
        /// out from under them. The player was left assigned to a hash that no longer existed,
        /// which kills every vanilla system keyed off PLAYER - the police stop responding
        /// entirely - and nothing put it back until a later mode happened to start with
        /// player.side neutral. Must run before the groups are removed.
        /// </summary>
        private void RestorePlayerGroup()
        {
            if (!_playerMovedGroup) { return; }

            try
            {
                RelationshipMatrix.RestorePed(Game.Player.Character, RelationshipMatrix.VanillaPlayerGroup);
                Log.Info("Player returned to the vanilla PLAYER relationship group.");
            }
            catch (Exception ex)
            {
                Log.Error("Could not restore the player's relationship group", ex);
            }

            _playerMovedGroup = false;
        }

        /// <summary>Called from the script's Aborted handler. Must not throw.</summary>
        public void EmergencyCleanup()
        {
            try
            {
                _pursuit.EndAll();
                _looting.EndAll();
                _registry.RestoreAll();
                RestorePlayerGroup();
                Ambience.Clear();
                Zone.Clear();
                _craft.Clear();
                _spectacle.Clear();
                // Leaves the wanted ceiling at zero for the rest of the session if skipped,
                // which would look exactly like the police mod having broken.
                Purge.Clear();
                _models.Release();
                _relationships.Clear();
                ActiveMode = null;
                _lastFighter = null;
            }
            catch (Exception ex)
            {
                Log.Error("Emergency cleanup failed", ex);
            }
        }

        public void Tick()
        {
            // Runs even when stopped: peds who were mid-fight need a few seconds of calming
            // before they truly settle.
            Pacify();

            if (!IsRunning) { return; }

            _stopwatch.Restart();
            try
            {
                // Density natives are per-frame, so they run every frame or they visibly
                // stutter between values.
                ApplyDensity();

                if (Escalation.Update(Kills))
                {
                    // A faction that joins at this phase can now be recruited into.
                    BuildRecruitPool(ActiveMode);
                    AnnouncePhase();
                }

                if (Purge.Update())
                {
                    // The purge ending stops the mode: that is the event, not a side effect.
                    Stop();
                    return;
                }

                Zone.RefreshBlip();

                Phase phase = Escalation.CurrentPhase;
                bool unphased = !Escalation.Enabled || phase == null;
                Ambience.UpdateFires(Zone.Centre, unphased || phase.Fires);

                // Both throttle themselves, and both need to run while the player is driving
                // rather than only when the recruiting pass happens to come round.
                // Ships hold station whether or not you are reading a menu; they are two
                // entity moves a quarter-second, and a fleet that freezes mid-air is worse.
                _craft.Update(ActiveMode.Craft, Zone.Centre, true);

                // The city's own state: the power, the roads, the smoke. Runs whether or not a
                // menu is open, because a blackout that flickers back on while you read a
                // slider is worse than no blackout.
                _spectacle.Update(Zone.Centre, phase, unphased);

                _perception.Update(_registry.Tracked);
                if (_perception.Changed) { OnPerceptionChanged(); }

                if (!Paused)
                {
                    _pursuit.Update(_registry.Tracked, _perception.Spotted);
                    _looting.Update(_registry.Tracked, unphased || phase.Looting);
                }

                // Recruiting and retasking are the expensive half and do not need frame rate.
                if (!Paused && Game.GameTime >= _nextWorkAt)
                {
                    _nextWorkAt = Game.GameTime + _config.GetInt("engine.workIntervalMs", 50);
                    Recruit();
                    SpawnWaves();
                    ProcessTracked();
                }
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

            if (_recruitPool.Count == 0) { return; }

            Ped player = Game.Player.Character;
            float radius = _config.GetFloat("riot.recruitRadius", 180f);
            double chance = _config.GetFloat("riot.conversionChance", 0.85f);

            Ped[] nearby = World.GetNearbyPeds(player, radius);
            int converted = 0;

            foreach (Ped ped in nearby)
            {
                if (converted >= _budget) { break; }
                if (!IsRecruitable(ped, player)) { continue; }
                if (!Zone.Contains(ped.Position)) { continue; }
                if (_random.NextDouble() > chance) { continue; }

                // People sharing a car are travelling together; putting them on opposing sides
                // means they immediately shoot each other, which reads as a bug rather than as
                // a riot. Vehicle mates inherit the faction of whoever was recruited first.
                Faction faction = FactionOfVehicleMates(ped);

                if (faction == null)
                {
                    // Round-robin over the share-weighted pool: predictable proportions, and
                    // no risk of one side being randomly outnumbered in a way that reads as a bug.
                    faction = _recruitPool[_recruitRotation++ % _recruitPool.Count];
                }

                if (!string.Equals(faction.Recruits, "any", StringComparison.OrdinalIgnoreCase) && !MatchesFilter(ped, faction.Recruits))
                {
                    continue;
                }

                Reaction reaction = faction.ResolveReaction(_random);
                TrackedPed entry = _registry.Add(ped, faction, reaction, false);

                // Taken over means taken responsibility for. Left non-persistent, the ped you
                // are in the middle of a fight with is exactly the one the population manager
                // reclaims - it has no idea the fight matters. Restore hands them back.
                try { ped.IsPersistent = true; }
                catch (Exception ex) { Log.Error("Could not take ownership of a recruit", ex); }

                _conditioner.Apply(ped, faction, reaction, _lastFighter, entry, HostileFor(entry));
                entry.LastTaskedAt = Game.GameTime;

                // Recruits come from one sweep around the player, so the last fighter we made
                // is reliably close by - good enough to give panicking peds something real to
                // run from without paying for a search.
                if (reaction == Reaction.Fight) { _lastFighter = ped; }

                AttachBlip(entry);

                converted++;
                RecruitedTotal++;
            }
        }

        /// <summary>
        /// Spawns the factions that have no ambient equivalent — soldiers, aliens, SWAT waves.
        /// Gated on the escalation phase, which is what makes the army arrive late rather than
        /// turning up to the first thrown punch.
        /// </summary>
        private void SpawnWaves()
        {
            if (!_config.GetBool("riot.spawnFactions", true)) { return; }

            int ceiling = _config.GetInt("engine.maxTrackedPeds", 120);
            if (_registry.Count >= ceiling) { return; }

            _spawner.NoteAliveCounts(_registry.Tracked);

            foreach (Faction faction in ActiveMode.Factions)
            {
                // Re-checked per faction, not once per pass. Per-faction caps are not a global
                // cap: with five spawning factions all due in the same pass, one check at the
                // top let every one of them add a full wave over the ceiling.
                if (_registry.Count >= ceiling) { break; }

                if (!Escalation.Allows(faction.FromPhase)) { continue; }
                if (!_spawner.WaveDue(faction)) { continue; }

                // Aliens walk out from under their own ship rather than appearing behind a
                // hedge, which is the half that makes the craft read as an arrival.
                GTA.Math.Vector3 anchor = Zone.Centre;

                if (faction.ArrivesByCraft)
                {
                    GTA.Math.Vector3 drop = _craft.DropPoint();
                    if (drop != GTA.Math.Vector3.Zero) { anchor = drop; }
                }

                List<Ped> wave = _spawner.SpawnWave(faction, anchor);

                // Before the conditioning loop, not after it. Conditioning can throw, and the
                // catch is all the way up in Tick - which would leave these vehicles persistent
                // and unreachable from every cleanup path there is.
                foreach (Vehicle arrived in _spawner.LastWaveVehicles)
                {
                    _registry.AddVehicle(arrived);
                }

                if (wave.Count == 0) { continue; }

                Ped target = _lastFighter != null && _lastFighter.Exists() ? _lastFighter : Game.Player.Character;

                foreach (Ped ped in wave)
                {
                    Reaction reaction = faction.ResolveReaction(_random);
                    TrackedPed entry = _registry.Add(ped, faction, reaction, true);
                    _conditioner.Apply(ped, faction, reaction, _lastFighter, entry, HostileFor(entry));
                    entry.LastTaskedAt = Game.GameTime;

                    AttachBlip(entry);

                    Vehicle vehicle = ped.CurrentVehicle;
                    if (vehicle != null && vehicle.Exists() && vehicle.Driver == ped)
                    {
                        _vehicles.Apply(ped, vehicle, faction, target);
                        _vehicles.ApplyOccupants(vehicle, ped, target);
                    }
                }

                RecruitedTotal += wave.Count;
                Log.Debug("Spawned " + wave.Count + " for faction '" + faction.Id + "' in " +
                          _spawner.LastWaveVehicles.Count + " vehicle(s).");
            }
        }

        /// <summary>
        /// Menu-driven skip. Goes through here rather than straight to the Escalation so the
        /// recruit pool is rebuilt, which is what lets a faction that joins at this phase
        /// actually start being recruited.
        /// </summary>
        public bool SkipPhase()
        {
            if (!IsRunning || !Escalation.Advance()) { return false; }

            BuildRecruitPool(ActiveMode);
            AnnouncePhase();
            return true;
        }

        /// <summary>
        /// Records a casualty and says so on screen the first time a faction crosses a whole
        /// step. Without the notification the only evidence the system exists is a slow drift in
        /// numbers nobody would connect to their own body count.
        /// </summary>
        private void NoteReinforcement(Faction faction)
        {
            _reinforcements.NoteLoss(faction);

            string announcement = _reinforcements.StepUp(faction);
            if (announcement != null)
            {
                GTA.UI.Notification.Show("~o~" + announcement);
            }
        }

        private void AnnouncePhase()
        {
            GTA.UI.Notification.Show("~r~" + Escalation.CurrentName + "~s~");
            GTA.UI.Screen.ShowSubtitle("~r~" + Escalation.CurrentName.ToUpperInvariant() + "~s~", 4000);
        }

        /// <summary>
        /// A periodic line in the log, because a single total at shutdown hides the shape of
        /// the run. "Recruited 281, still active 12" only means something once you can see
        /// whether the active count was climbing, flat, or collapsing.
        /// </summary>
        private void ReportStats()
        {
            if (Game.GameTime < _nextStatsAt) { return; }
            _nextStatsAt = Game.GameTime + 30000;

            Log.Info("Riot: active " + _registry.Count + "/" + _config.GetInt("engine.maxTrackedPeds", 120) +
                     ", recruited " + RecruitedTotal + ", lost " + LostTotal + ", kills " + Kills +
                     ", culled " + CulledTotal + ", vehicles " + _registry.VehicleCount +
                     ", fires " + Ambience.ActiveFires +
                     ", barricades " + _spectacle.BarricadeCount +
                     (_spectacle.BlackedOut ? ", blackout" : "") +
                     ", chases " + _pursuit.ActiveChases + "/" + _pursuit.Started +
                     ", reinforcement " + ReinforcementSummary() +
                     ", player " + (_perception.Spotted ? "spotted" : "unseen " + _perception.SecondsSinceSeen + "s") +
                     ", looting " + _looting.Active + "/" + _looting.Total +
                     ", phase " + Escalation.Current + " '" + Escalation.CurrentName + "'" +
                     ", tick " + LastTickMs.ToString("F2") + "ms, budget " + _budget);
        }

        /// <summary>Per-faction commitment, for the log line that explains a growing response.</summary>
        private string ReinforcementSummary()
        {
            if (!_reinforcements.Enabled || ActiveMode == null) { return "off"; }

            var summary = new System.Text.StringBuilder();

            foreach (Faction faction in ActiveMode.Factions)
            {
                if (!faction.Spawn.Enabled) { continue; }

                int losses = _reinforcements.LossesFor(faction);
                if (losses == 0) { continue; }

                if (summary.Length > 0) { summary.Append(' '); }
                summary.Append(faction.Id).Append(" x")
                       .Append(_reinforcements.Commitment(faction).ToString("F1"))
                       .Append("(-").Append(losses).Append(')');
            }

            return summary.Length == 0 ? "none yet" : summary.ToString();
        }

        /// <summary>
        /// Expands each recruiting faction into slots proportional to its share, so a mode can
        /// say "a few rioters, mostly onlookers" without any special-casing at recruit time.
        /// </summary>
        private void BuildRecruitPool(RiotMode mode)
        {
            _recruitPool.Clear();
            _recruitRotation = 0;

            foreach (Faction faction in mode.Factions)
            {
                if (string.Equals(faction.Recruits, "none", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (!Escalation.Allows(faction.FromPhase)) { continue; }

                int slots = (int)Math.Round(faction.Share * 10f);
                if (slots < 1) { slots = 1; }

                for (int i = 0; i < slots; i++) { _recruitPool.Add(faction); }
            }

            // Interleave so consecutive recruits alternate rather than arriving in blocks.
            for (int i = _recruitPool.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                Faction swap = _recruitPool[i];
                _recruitPool[i] = _recruitPool[j];
                _recruitPool[j] = swap;
            }

            Log.Info("Recruit pool: " + _recruitPool.Count + " slot(s) across " + mode.Factions.Count + " faction(s).");
        }

        /// <summary>
        /// The faction of an already-recruited occupant of this ped's vehicle, or null when the
        /// ped is on foot or nobody in the car has been recruited yet.
        /// </summary>
        private Faction FactionOfVehicleMates(Ped ped)
        {
            if (!_config.GetBool("riot.groupVehicleOccupants", true)) { return null; }

            Vehicle vehicle = ped.CurrentVehicle;
            if (vehicle == null || !vehicle.Exists()) { return null; }

            foreach (Ped occupant in vehicle.Occupants)
            {
                if (occupant == null || occupant.Handle == ped.Handle) { continue; }

                TrackedPed mate = _registry.Find(occupant);
                if (mate != null) { return mate.Faction; }
            }
            return null;
        }

        /// <summary>
        /// Indexes the mode's own relations. Only dislike and hate count: allies and neutrals
        /// are not targets, however different their faction.
        /// </summary>
        private void BuildHostility(RiotMode mode)
        {
            _hostilePairs.Clear();

            foreach (Relation relation in mode.Relations)
            {
                if (relation.Value < RelationshipMatrix.Dislike) { continue; }

                _hostilePairs.Add(relation.From + "|" + relation.To);
                if (relation.Mutual) { _hostilePairs.Add(relation.To + "|" + relation.From); }
            }
        }

        private bool AreHostile(Faction from, Faction to)
        {
            // A faction declared hostile to itself is a free-for-all, which is a legitimate
            // thing for a mode to want.
            return _hostilePairs.Contains(from.Id + "|" + to.Id);
        }

        /// <summary>
        /// Only drivers need something to chase, and they are a small minority. One native beats
        /// a list walk for everyone else.
        /// </summary>
        private Ped HostileFor(TrackedPed entry)
        {
            if (!entry.IsUsable || !entry.Ped.IsInVehicle()) { return null; }
            return PickHostile(entry);
        }

        /// <summary>
        /// Someone for this ped to go after. Scans a bounded window of the tracked list rather
        /// than all of it — this runs for drivers only, and an approximate enemy nearby beats an
        /// exact one across the map.
        /// </summary>
        private Ped PickHostile(TrackedPed entry)
        {
            IReadOnlyList<TrackedPed> tracked = _registry.Tracked;
            if (tracked.Count == 0 || _hostilePairs.Count == 0) { return null; }

            int start = _random.Next(tracked.Count);
            int window = Math.Min(tracked.Count, 24);

            for (int offset = 0; offset < window; offset++)
            {
                TrackedPed other = tracked[(start + offset) % tracked.Count];

                if (other == entry || !other.IsUsable) { continue; }
                if (!AreHostile(entry.Faction, other.Faction)) { continue; }

                return other.Ped;
            }

            return null;
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
            if (_config.GetBool("compatibility.protectMissionPeds", true) &&
                Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, ped))
            {
                return false;
            }

            // Police, SWAT, army and emergency services are left alone unless a mode explicitly
            // asks for them. A police or wanted-system overhaul owns those peds, and this mod
            // taking them over would break that mod invisibly.
            if (_config.GetBool("compatibility.protectEmergencyServices", true) &&
                PedTypes.IsProtectedService(Function.Call<int>(Hash.GET_PED_TYPE, ped)))
            {
                return false;
            }

            return true;
        }

        private static bool MatchesFilter(Ped ped, string filter)
        {
            int pedType = Function.Call<int>(Hash.GET_PED_TYPE, ped);

            switch ((filter ?? string.Empty).ToLowerInvariant())
            {
                case "civilian": return PedTypes.IsCivilian(pedType);
                case "male": return pedType == PedTypes.CivMale;
                case "female": return pedType == PedTypes.CivFemale;
                case "criminal": return pedType == PedTypes.Criminal || pedType == PedTypes.Bum;
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
            LostTotal += _registry.PruneDead();
            PruneVehicles();
            ReportStats();

            IReadOnlyList<TrackedPed> tracked = _registry.Tracked;
            if (tracked.Count == 0) { return; }

            Ped player = Game.Player.Character;
            GTA.Math.Vector3 origin = player.Position;

            // Running someone over counts as provocation, and the damage is credited to the
            // car rather than to you. Read once per pass, not once per ped.
            Vehicle ride = player.CurrentVehicle;

            // Compared squared to keep a square root out of the per-ped path.
            float cull = _config.GetFloat("engine.cullDistance", 450f);
            float cullSquared = cull * cull;
            int retaskAfterMs = _config.GetInt("combat.retaskIntervalMs", 6000);
            int examined = 0;

            while (examined < _budget && tracked.Count > 0)
            {
                if (_cursor >= tracked.Count) { _cursor = 0; }

                TrackedPed entry = tracked[_cursor];
                examined++;

                if (!entry.IsUsable || !EntityRegistry.IsSameEntity(entry))
                {
                    if (entry.Ped != null && entry.Ped.Exists() && entry.Ped.IsDead)
                    {
                        Kills++;

                        // Only spawned members count: converting a pedestrian and losing them
                        // is the riot working, not a faction taking casualties.
                        if (entry.Spawned) { NoteReinforcement(entry.Faction); }

                        // Killing someone is the loudest provocation there is, and the one most
                        // likely to be followed by driving away from it. A kill by car is
                        // credited to the car, which is exactly the case worth catching.
                        int killer = Function.Call<int>(Hash.GET_PED_SOURCE_OF_DEATH, entry.Ped);
                        if (killer == player.Handle || (ride != null && ride.Exists() && killer == ride.Handle))
                        {
                            _pursuit.NoteProvoked(entry.Faction);
                        }
                    }

                    _registry.Remove(entry, false);
                    LostTotal++;
                    continue;
                }

                NoteProvocation(entry, player, ride);

                float rangeSquared = origin.DistanceToSquared(entry.Ped.Position);

                if (rangeSquared > cullSquared)
                {
                    _registry.Remove(entry, true);
                    CulledTotal++;
                    continue;
                }

                if (ShouldRetask(entry, retaskAfterMs))
                {
                    _conditioner.IssueTask(entry.Ped, entry.Faction, entry.Reaction, _lastFighter, entry, HostileFor(entry));
                    // Jittered so a crowd does not retask in lockstep every few seconds.
                    entry.LastTaskedAt = Game.GameTime + _random.Next(0, 1500);
                }

                UpdateBlip(entry, rangeSquared);
                _cursor++;
            }
        }

        /// <summary>
        /// Releases vehicles we are done with. Throttled: it walks the whole list with a native
        /// per vehicle, which does not belong on the same 50ms cadence as the ped work.
        /// </summary>
        private void PruneVehicles()
        {
            if (Game.GameTime < _nextVehiclePruneAt) { return; }
            _nextVehiclePruneAt = Game.GameTime + 2000;

            _registry.PruneVehicles(
                Game.Player.Character.Position,
                _config.GetFloat("engine.cullDistance", 450f),
                _config.GetFloat("engine.abandonedVehicleDistance", 120f),
                _config.GetInt("engine.maxTrackedVehicles", 40));
        }

        /// <summary>
        /// Notices that the player has shot, run over or otherwise hurt this ped, and tells the
        /// pursuit system its faction now has a reason to come after them.
        ///
        /// The damage flag is cleared afterwards so one shot does not keep re-provoking the
        /// same faction on every pass over the list.
        /// </summary>
        private void NoteProvocation(TrackedPed entry, Ped player, Vehicle ride)
        {
            if (!_pursuit.Enabled) { return; }

            bool hurt = Function.Call<bool>(Hash.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY, entry.Ped, player, true);

            if (!hurt && ride != null && ride.Exists())
            {
                hurt = Function.Call<bool>(Hash.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY, entry.Ped, ride, true);
            }

            if (!hurt) { return; }

            Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, entry.Ped);
            _pursuit.NoteProvoked(entry.Faction);
        }

        /// <summary>
        /// Re-issuing a fight task over the top of a ped that is already reacting to something
        /// is what produced the stutter loop: the game starts a flee, we interrupt it with a
        /// fight, the game starts another flee. Leave a ped alone while it is visibly busy.
        /// </summary>
        private bool ShouldRetask(TrackedPed entry, int retaskAfterMs)
        {
            if (entry.Reaction != Reaction.Fight) { return false; }
            // A chase or a looting run is a task of its own; re-issuing "fight whoever is
            // nearby" over the top of one is how a chase ends at the first junction.
            if (entry.InPursuit || entry.Looting) { return false; }
            if (Game.GameTime - entry.LastTaskedAt < retaskAfterMs) { return false; }

            Ped ped = entry.Ped;
            if (ped.IsInCombat || ped.IsFleeing || ped.IsRagdoll) { return false; }

            // Being aimed at is its own reaction; interrupting it causes the stutter.
            if (Function.Call<bool>(Hash.IS_PLAYER_FREE_AIMING_AT_ENTITY, Game.Player, ped)) { return false; }

            return true;
        }

        /// <summary>
        /// Clearing tasks once is not enough: two peds who have already wounded each other will
        /// re-engage from vanilla AI memory the moment they are free. Keep clearing for a few
        /// seconds so the crowd actually settles instead of flaring back up.
        /// </summary>
        private void BeginPacifying(IReadOnlyList<TrackedPed> entries)
        {
            _pacifying.Clear();
            foreach (TrackedPed entry in entries)
            {
                if (entry.IsUsable) { _pacifying.Add(entry.Ped); }
            }

            int seconds = _config.GetInt("riot.pacifySeconds", 5);
            _pacifyUntil = Game.GameTime + seconds * 1000;

            if (_pacifying.Count > 0)
            {
                Log.Info("Calming " + _pacifying.Count + " ped(s) for " + seconds + "s.");
            }
        }

        private void Pacify()
        {
            if (_pacifying.Count == 0) { return; }

            if (Game.GameTime > _pacifyUntil)
            {
                _pacifying.Clear();
                return;
            }

            foreach (Ped ped in _pacifying)
            {
                try
                {
                    if (ped == null || !ped.Exists() || ped.IsDead) { continue; }
                    if (!ped.IsInCombat) { continue; }

                    Function.Call(Hash.CLEAR_PED_TASKS_IMMEDIATELY, ped);
                }
                catch (Exception)
                {
                    // A ped that vanished mid-pass is not worth a log line every frame.
                }
            }
        }

        private void AttachBlip(TrackedPed entry)
        {
            if (!_config.GetBool("blips.enabled", true)) { return; }
            if (!entry.Faction.BlipEnabled) { return; }
            // Counted rather than recounted: walking the whole tracked list once per recruit was
            // ~1,400 iterations twenty times a second in a class whose rule is never to touch
            // every ped every tick.
            if (_registry.BlipCount >= _config.GetInt("blips.maxBlips", 40)) { return; }

            try
            {
                Blip blip = entry.Ped.AddBlip();
                blip.Sprite = entry.Faction.BlipSprite;
                blip.Color = entry.Faction.BlipColor;
                blip.Scale = _config.GetFloat("blips.scale", 0.6f);
                blip.Name = entry.Faction.DisplayName;
                blip.IsShortRange = true;
                entry.Blip = blip;
                _registry.NoteBlipAdded();
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
        /// <summary>
        /// Re-applies the player's standing. Safe to call mid-riot, so the stance can be changed
        /// from the menu without restarting the mode.
        /// </summary>
        public void RefreshPlayerStance()
        {
            if (IsRunning) { ApplyPlayerSide(ActiveMode); }
        }

        /// <summary>
        /// Points each faction's hostility at the vanilla PLAYER group rather than moving the
        /// player into a group of ours. Joining a side is the one case where the player really
        /// does change group, and it is restored on stop.
        /// </summary>
        private void ApplyPlayerSide(RiotMode mode)
        {
            string side = _config.GetString("player.side", "neutral");
            Faction joined = string.Equals(side, "neutral", StringComparison.OrdinalIgnoreCase) ? null : mode.Find(side);

            if (joined != null)
            {
                _relationships.ApplyToPed(Game.Player.Character, joined.GroupHash);
                _playerMovedGroup = true;
                Log.Info("Player joined faction '" + joined.Id + "'.");
                return;
            }

            if (_playerMovedGroup)
            {
                RelationshipMatrix.RestorePed(Game.Player.Character, RelationshipMatrix.VanillaPlayerGroup);
                _playerMovedGroup = false;
            }

            // A forced stance from the menu overrides everything; "mode" (the default) lets each
            // faction answer for itself and falls back to the mode's own setting.
            int forced = ParseStance(_config.GetString("player.stance", "mode"), -1);

            _hostileToPlayer.Clear();

            foreach (Faction faction in mode.Factions)
            {
                int stance = forced >= 0 ? forced
                    : (faction.PlayerRelationship >= 0 ? faction.PlayerRelationship : mode.PlayerRelationship);

                _hostileToPlayer[faction.Id] = stance;
                _relationships.Set(_relationships.PlayerGroup, faction.GroupHash, RelationshipMatrix.Neutral);

                Log.Debug("Faction '" + faction.Id + "' regards the player as " + stance +
                          " (0 companion .. 5 hate) once they can see them.");
            }

            // Only the factions that would actually come for you are worth spending a
            // line-of-sight trace on.
            var hostileIds = new List<string>();
            foreach (var pair in _hostileToPlayer)
            {
                if (pair.Value >= RelationshipMatrix.Dislike) { hostileIds.Add(pair.Key); }
            }
            _perception.SetHostileFactions(hostileIds);

            ApplyVisibility(mode);

            Log.Info("Player stance: " + (forced >= 0 ? forced.ToString() : "per faction") +
                     ", vanilla PLAYER group kept.");
        }

        /// <summary>
        /// Writes each faction's standing towards the player, which is their declared stance
        /// while they can see the player and neutral while they cannot.
        ///
        /// Neutral is not a truce. Their combat AI keeps working; it simply has nothing to say
        /// about someone it cannot find, and goes back to the targets it can see.
        /// </summary>
        private void ApplyVisibility(RiotMode mode)
        {
            bool spotted = _perception.Spotted;

            foreach (Faction faction in mode.Factions)
            {
                int hostile;
                if (!_hostileToPlayer.TryGetValue(faction.Id, out hostile)) { continue; }

                _relationships.Set(faction.GroupHash, _relationships.PlayerGroup,
                    spotted ? hostile : RelationshipMatrix.Neutral);
            }
        }

        /// <summary>
        /// Re-applies standing when the riot finds or loses the player.
        ///
        /// Losing you also needs the peds already fighting you to be told: a combat task that has
        /// locked onto a target does not drop it because a relationship changed underneath it, so
        /// nearby fighters are cleared and re-tasked, at which point they pick somebody they can
        /// actually see.
        /// </summary>
        private void OnPerceptionChanged()
        {
            if (ActiveMode == null) { return; }

            // Having joined a side, the player is in a faction's own group and standings are
            // group to group. Being seen or not does not enter into it.
            if (_playerMovedGroup) { return; }

            ApplyVisibility(ActiveMode);

            if (_perception.Spotted)
            {
                if (_config.GetBool("features.perception.notify", true))
                {
                    GTA.UI.Notification.Show("~r~Spotted.~s~ " + _perception.Reason + ".");
                }
                return;
            }

            ReleaseFromPlayer();

            if (_config.GetBool("features.perception.notify", true))
            {
                GTA.UI.Notification.Show("~g~They have lost you.");
            }
        }

        /// <summary>
        /// Breaks off everyone currently fighting the player, so they re-acquire. Bounded to the
        /// peds near enough to have been fighting you in the first place.
        /// </summary>
        private void ReleaseFromPlayer()
        {
            Ped player = Game.Player.Character;
            float radius = _config.GetFloat("features.perception.sightRange", 60f) * 1.5f;
            float radiusSquared = radius * radius;
            int released = 0;

            foreach (TrackedPed entry in _registry.Tracked)
            {
                if (!entry.IsUsable || entry.InPursuit) { continue; }
                if (entry.Reaction != Reaction.Fight) { continue; }
                if (player.Position.DistanceToSquared(entry.Ped.Position) > radiusSquared) { continue; }

                // Only the ones actually fighting the player. Clearing every nearby fighter
                // would stop every unrelated fight in the street at the same moment, which
                // reads as the riot pausing rather than as losing them.
                if (!IsFightingPlayer(entry.Ped, player)) { continue; }

                try
                {
                    Function.Call(Hash.CLEAR_PED_TASKS, entry.Ped);
                    // Due for a fresh target on the Director's next pass over it.
                    entry.LastTaskedAt = 0;
                    released++;
                }
                catch (Exception)
                {
                    // A ped that vanished mid-pass is not worth reporting.
                }
            }

            Log.Debug("Perception: released " + released + " ped(s) from the player.");
        }

        private static bool IsFightingPlayer(Ped ped, Ped player)
        {
            try
            {
                if (!ped.IsInCombat) { return false; }

                int target = Function.Call<int>(Hash.GET_PED_TARGET_FROM_COMBAT_PED, ped, 0);
                return target == player.Handle;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// How the factions regard the player, forced across every faction. Returns
        /// <paramref name="unforced"/> for "mode", which is the default and means each faction
        /// answers for itself.
        ///
        /// "mode" is the default rather than "target" because one answer for the whole riot is
        /// wrong for half the modes: in Martial Law the soldiers should want you and the crowd
        /// should not, and a single mode-wide "target" had civilians spending the riot punching
        /// you instead of the army that was shooting at them.
        /// </summary>
        private static int ParseStance(string text, int unforced)
        {
            switch ((text ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "ignored": return RelationshipMatrix.Neutral;
                case "disliked": return RelationshipMatrix.Dislike;
                case "target": return RelationshipMatrix.Hate;
                case "mode": return unforced;
                default:
                    Log.Warn("Unknown player stance '" + text + "', letting each faction decide.");
                    return unforced;
            }
        }

    }
}
