using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using TonightsTheNight.Config;
using TonightsTheNight.Factions;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>What the hunter is doing right now.</summary>
    public enum HunterState
    {
        Absent,
        /// <summary>Coming for you on foot, fast, but recognisably a person running.</summary>
        Stalking,
        /// <summary>Planted, facing you, about to do something. The window to move.</summary>
        Winding,
        /// <summary>Crossing the gap. Collision off, physics off, a streak of light.</summary>
        Rushing,
        /// <summary>Gone from where he was and not yet where he is going.</summary>
        Blinking,
        /// <summary>Open. The only time he can really be hurt.</summary>
        Staggered,
        Dead
    }

    /// <summary>
    /// The heavy moves, all of which are announced before they land.
    ///
    /// A move exists in this list only if there is something the player can do about it, which
    /// is what the list is for: it is the fight's vocabulary, and every entry has a tell, a
    /// counter and a price for missing.
    /// </summary>
    public enum HunterMove
    {
        None,
        /// <summary>The lunge. Committed at launch, so moving off the line beats it.</summary>
        Rush,
        /// <summary>A fist into the road. Everything around him goes up. Get outside it.</summary>
        Strike,
        /// <summary>A heavy swing through the arc in front of him. Get behind him.</summary>
        Cleave,
        /// <summary>Whatever is lying in the street, thrown at your head. Break the line.</summary>
        Hurl
    }

    /// <summary>Where the chase currently is, which decides how he moves.</summary>
    public enum Traversal
    {
        Ground,
        Air,
        Water
    }

    /// <summary>
    /// One thing on the map that is coming for you specifically, and cannot be reasoned with,
    /// outrun, or shot dead in the ordinary way.
    ///
    /// The design problem with an unkillable pursuer is that "unkillable" and "beatable" have to
    /// both be true or it is not a fight, it is either a cutscene or a chore. So his real health
    /// is pinned and meaningless, and everything that hits him goes into a separate pool called
    /// Resolve at a fraction of its value — except while he is staggered, when it goes in whole.
    /// Emptying the pool is the win condition, staggering him is how you get to do it, and
    /// staggering him is a consequence of things he does rather than something you can force
    /// directly. That makes the fight a rhythm: survive the ability, punish the recovery.
    ///
    /// Two other rules keep it honest. Sustained damage fills a Break meter that staggers him
    /// on its own, so a player with no explosives and good discipline still has a route. And a
    /// single large hit always lands for more than its share, so the RPG in your boot, the car
    /// you are driving and whatever a physics mod does to him all matter.
    ///
    /// The rhythm only works if the moves can be read. Every heavy move now spends a moment
    /// planted with a ring under his feet before it fires, and the rush commits to the line it
    /// launched on rather than following you round a corner — so stepping off it is a real
    /// answer, and missing costs him a longer opening than connecting does. He is dangerous
    /// because he is fast and hits like a truck, not because there is nothing to be done.
    ///
    /// He is also not exclusively yours. Anything in reach that is not you is a body he can
    /// make, and he takes the detour when it is on his way, which is both the only breathing
    /// room in the mode and the thing that sells him as a hazard rather than a scripted duel.
    ///
    /// He crosses ground, air and water because there is no version of this that is any good if
    /// the answer is a helicopter.
    /// </summary>
    public sealed class Hunter
    {
        private const string HunterGroup = "TTN_HUNTER";

        /// <summary>Vanilla groups he should be hostile to regardless of what a mode declares.</summary>
        private static readonly string[] VanillaEnemies =
        {
            "PLAYER", "CIVMALE", "CIVFEMALE", "COP", "ARMY", "SECURITY_GUARD", "PRIVATE_SECURITY"
        };

        /// <summary>Something to throw, when a mode does not name its own.</summary>
        private static readonly string[] DefaultDebris =
        {
            "prop_barrel_02a", "prop_bin_01a", "prop_rub_wheel_01", "prop_roadcone02a"
        };

        /// <summary>One thing he has thrown, and when to stop caring about it.</summary>
        private sealed class Debris
        {
            public Prop Prop;
            public int DieAt;
            /// <summary>Set once it has hit something, so it cannot hit twice.</summary>
            public bool Spent;
        }

        private readonly ConfigStore _config;
        private readonly ModelResolver _models;
        private readonly Random _random;
        private readonly HunterFx _fx;
        private readonly List<Debris> _thrown = new List<Debris>();

        private JsonValue _declared = JsonValue.Null;
        private Ped _ped;
        private Blip _blip;
        private int _group;

        private HunterState _state = HunterState.Absent;
        private int _stateUntil;
        private int _nextAbilityAt;

        /// <summary>
        /// Separate from the ability cooldown on purpose.
        ///
        /// Sharing them meant a rush could never land: BeginRush sets the ability cooldown to
        /// six and a half seconds ahead, the rush is over in one and a half, and the strike at
        /// the end of it was checking the very timer the rush had just pushed out of reach.
        /// </summary>
        private int _nextStrikeAt;

        private int _nextTaskAt;
        private int _lastHealth;

        /// <summary>
        /// The health value we set and restore, held here rather than read back.
        ///
        /// SHVDN's <c>Ped.Health</c> and <c>Ped.MaxHealth</c> have not always agreed about
        /// whether GTA's hundred-point floor is part of the number, and this class works by
        /// subtracting one from the other every frame. A hundred-point disagreement would read
        /// as a hundred points of damage per tick and empty the Resolve pool in seconds. Both
        /// sides of the subtraction come from the same native now, and the ceiling is ours.
        /// </summary>
        private int _maxHealth;
        private int _vulnerableUntil;
        private int _announcedPhase;
        private int _leashWarnedAt;

        /// <summary>The move he is winding up, and where he planted to do it.</summary>
        private HunterMove _move;
        private Vector3 _windAt;

        /// <summary>
        /// The direction a rush committed to, held rather than recomputed.
        ///
        /// This is the whole fix for the lunge that could not be beaten. It used to re-aim at
        /// the player every frame, which meant running, driving, diving and turning all failed
        /// identically: whatever you did, the line moved with you and the only counter left was
        /// not being able to be hurt. Now it steers by a fixed rate towards where you are, so a
        /// sharp change of direction inside the last half-second is faster than he can follow.
        /// </summary>
        private Vector3 _rushAim;

        /// <summary>Who he is actually going for, which is not always you.</summary>
        private Ped _target;
        private int _targetUntil;
        private int _nextTargetAt;

        /// <summary>
        /// Whether his collision is currently off, tracked rather than re-asserted.
        ///
        /// Three things switch it off - flight, a rush and a blink - and each of those has its
        /// own way of ending. Flight does not: it ends because the player landed, which no code
        /// path here was watching, so a hunter who had once followed you into the air walked
        /// through walls and floors for the rest of the night.
        /// </summary>
        private bool _noClip;

        /// <summary>Whether ragdoll is currently permitted, so it is set only when it changes.</summary>
        private bool _ragdollAllowed;

        /// <summary>Raw damage since the last stagger. Fills the Break meter.</summary>
        private float _break;

        public Hunter(ConfigStore config, ModelResolver models, Random random)
        {
            _config = config;
            _models = models;
            _random = random;
            _fx = new HunterFx(config);
        }

        public bool Enabled { get { return _config.GetBool("features.hunter.enabled", true); } }

        /// <summary>True while he is on the map.</summary>
        public bool Active { get { return _ped != null && _ped.Exists() && _state != HunterState.Dead; } }

        /// <summary>Set on the tick he is put down, so the caller can end the mode on a win.</summary>
        public bool Defeated { get; private set; }

        public float Resolve { get; private set; }
        public float ResolveMax { get; private set; }

        /// <summary>1, 2 or 3. Rises as Resolve falls, and each step makes him worse.</summary>
        public int Phase { get; private set; }

        public string Name { get; private set; }

        public bool Vulnerable { get { return Game.GameTime < _vulnerableUntil; } }

        /// <summary>
        /// Whether a ped is the hunter. The Director asks before recruiting anybody, or the
        /// riot would cheerfully sign him up as a rioter and hand him a bat.
        /// </summary>
        public bool Owns(Ped ped)
        {
            return ped != null && _ped != null && _ped.Exists() && ped.Handle == _ped.Handle;
        }

        // ------------------------------------------------------------------ lifecycle

        public void Begin(JsonValue declared, RelationshipMatrix relationships, RiotMode mode)
        {
            Clear();

            _declared = declared == null ? JsonValue.Null : declared;
            if (!_declared.IsObject || !_declared["enabled"].AsBool(true)) { return; }

            Name = _declared["name"].AsString("The Night");
            ResolveMax = Math.Max(1f, _declared["resolve"].AsFloat(_config.GetFloat("features.hunter.resolve", 2400f)));
            Resolve = ResolveMax;
            Phase = 1;
            _announcedPhase = 0;
            Defeated = false;
            _state = HunterState.Absent;
            _break = 0f;

            try
            {
                _group = relationships.Register(HunterGroup);

                // Everything. He is not on anybody's side and nothing is on his.
                foreach (Faction faction in mode.Factions)
                {
                    relationships.SetMutual(_group, faction.GroupHash, RelationshipMatrix.Hate);
                }

                foreach (string vanilla in VanillaEnemies)
                {
                    int hash = unchecked((int)Game.GenerateHash(vanilla));
                    relationships.SetMutual(_group, hash, RelationshipMatrix.Hate);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not wire up the hunter's relationships", ex);
            }

            Log.Info("Hunter armed: '" + Name + "' with " + ResolveMax + " resolve.");
        }

        public void Clear()
        {
            _fx.Reset();
            ClearThrown();

            try
            {
                if (_blip != null && _blip.Exists()) { _blip.Delete(); }
                if (_ped != null && _ped.Exists()) { _ped.Delete(); }
            }
            catch (Exception ex)
            {
                Log.Error("Could not remove the hunter", ex);
            }

            _blip = null;
            _ped = null;
            _state = HunterState.Absent;
            _declared = JsonValue.Null;
            _vulnerableUntil = 0;
            _break = 0f;
            _move = HunterMove.None;
            _target = null;
            _targetUntil = 0;

            // Reset here rather than only in Begin. Begin returns early for a mode that declares
            // no hunter, so a win left this true for the rest of the session - and the Director
            // stops the running mode the moment it sees it. Every mode started after beating
            // Tonight's The Night ended on its first tick, announcing that you had survived it.
            Defeated = false;
        }

        // ------------------------------------------------------------------ tick

        public void Update(Vector3 centre)
        {
            if (!Enabled || !_declared.IsObject)
            {
                if (_ped != null) { Clear(); }
                return;
            }

            // First, always, and on every path out of here: an abandoned time scale would
            // outlive the mode and quietly ruin the rest of the session.
            _fx.UpdateTime();

            // Before the Defeated check. Something he threw a second before he went down is
            // still in the air, and is ours to clean up whatever else has happened.
            UpdateThrown();

            if (Defeated) { return; }

            try
            {
                Ped player = Game.Player.Character;
                if (player == null || !player.Exists()) { return; }

                if (!EnsureSpawned(centre, player)) { return; }
                if (_ped == null || !_ped.Exists()) { return; }

                if (_ped.IsDead) { Die(); return; }

                TrackDamage();
                if (Resolve <= 0f) { Die(); return; }

                UpdatePhase();

                // Dying respawns you somewhere else entirely; he should not be mid-lunge at a
                // hospital door when you get up.
                if (player.IsDead) { Idle(); return; }

                Traversal traversal = TraversalFor(player);
                Ped victim = ChooseTarget(player, traversal);
                float distance = _ped.Position.DistanceTo(victim.Position);

                Leash(player, _ped.Position.DistanceTo(player.Position));

                switch (_state)
                {
                    case HunterState.Winding: AdvanceWind(victim); break;
                    case HunterState.Rushing: AdvanceRush(victim); break;
                    case HunterState.Blinking: AdvanceBlink(player); break;
                    case HunterState.Staggered: AdvanceStagger(); break;
                    default: Stalk(victim, distance, traversal); break;
                }

                MaybeUseAbility(victim, distance, traversal);
                Present(player);
            }
            catch (Exception ex)
            {
                Log.Error("Hunter update failed", ex);
            }
        }

        // ------------------------------------------------------------------ spawning

        private bool EnsureSpawned(Vector3 centre, Ped player)
        {
            if (_ped != null && _ped.Exists()) { return true; }
            if (_ped != null) { _ped = null; }

            Model model;
            if (!_models.TryResolve(_declared["models"].AsStringList(), out model))
            {
                Log.Warn("No hunter model is installed - this mode will run without one.");
                _declared = JsonValue.Null;
                return false;
            }

            if (!_models.Load(model, 2000)) { return false; }

            float distance = _declared["spawnDistance"].AsFloat(_config.GetFloat("features.hunter.spawnDistance", 70f));
            Vector3 where = Ground.Place(centre.Around(distance));
            if (where == Vector3.Zero) { where = Ground.OnGround(centre.Around(distance * 0.6f)); }
            if (where == Vector3.Zero) { return false; }

            Ped ped = World.CreatePed(model, where);
            if (ped == null || !ped.Exists()) { return false; }

            _ped = ped;
            Condition(ped);
            Announce(player);
            return true;
        }

        /// <summary>
        /// Turns a pedestrian into the thing.
        ///
        /// Note what is *not* here: invincibility. He has to take damage for any of this to
        /// work — the Resolve pool is fed by reading how much health he lost — so instead his
        /// health is enormous and restored every tick. Nothing in the game deals five thousand
        /// damage in a single frame, so the only way he goes down is the intended one.
        /// </summary>
        private void Condition(Ped ped)
        {
            try
            {
                ped.IsPersistent = true;
                ped.BlockPermanentEvents = true;

                // Read back rather than assumed. If the engine declines to give a ped five
                // thousand health, the ceiling we pin to has to be the one it actually gave us
                // or the very first tick reads the shortfall as damage and kills him instantly.
                int wanted = Math.Max(1000, _declared["health"].AsInt(5000));
                Function.Call(Hash.SET_PED_MAX_HEALTH, ped, wanted);
                Function.Call(Hash.SET_ENTITY_HEALTH, ped, wanted);

                _maxHealth = Function.Call<int>(Hash.GET_ENTITY_HEALTH, ped);
                if (_maxHealth < 100) { _maxHealth = wanted; }
                _lastHealth = _maxHealth;

                Function.Call(Hash.SET_PED_ARMOUR, ped, 100);
                // A headshot must not be able to end him before the Resolve pool has an opinion.
                Function.Call(Hash.SET_PED_SUFFERS_CRITICAL_HITS, ped, false);
                Function.Call(Hash.SET_PED_DIES_WHEN_INJURED, ped, false);
                Function.Call(Hash.SET_PED_SEEING_RANGE, ped, 400f);

                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.AlwaysFight, true);
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.CanUseCover, false);
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.CanFightArmedPedsWhenNotArmed, true);
                Function.Call(Hash.SET_PED_COMBAT_ABILITY, ped, 2);
                Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, ped, 0, false);
                Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_HASH, ped, _group);

                _ragdollAllowed = true;
                AllowRagdoll(false);

                string weapon = _declared["weapon"].AsString(_config.GetString("features.hunter.weapon", "WEAPON_MACHETE"));
                if (!string.IsNullOrEmpty(weapon))
                {
                    uint hash = (uint)Game.GenerateHash(weapon.ToUpperInvariant());
                    Function.Call(Hash.GIVE_WEAPON_TO_PED, ped, hash, 1, false, true);
                    Function.Call(Hash.SET_CURRENT_PED_WEAPON, ped, hash, true);
                }

                if (_config.GetBool("features.hunter.blip", true))
                {
                    Blip blip = ped.AddBlip();
                    blip.Sprite = BlipSprite.Standard;
                    blip.Color = BlipColor.Red;
                    blip.Name = Name;
                    blip.Scale = 0.9f;
                    Function.Call(Hash.SET_BLIP_FLASHES, blip, true);
                    _blip = blip;
                }

                _fx.Attach(ped, _config.GetString("features.hunter.fx.trail", "core/exp_grd_bzgas_smoke"), 0.6f);
                _noClip = false;
                _state = HunterState.Stalking;
            }
            catch (Exception ex)
            {
                Log.Error("Could not condition the hunter", ex);
            }
        }

        private void Announce(Ped player)
        {
            _fx.Lightning();
            _fx.Flash(_config.GetString("features.hunter.fx.phaseEffect", "DrugsTrevorClownsFightIn"), 1200);
            _fx.Sound("HUD_FRONTEND_DEFAULT_SOUNDSET", "Lose_1st");

            GTA.UI.Screen.ShowSubtitle("~r~" + Name.ToUpperInvariant() + "~s~ knows where you are.", 5000);
            GTA.UI.Notification.Show("~r~Tonight's the night.~s~ Something is coming.");
            Log.Info("Hunter spawned " + (int)_ped.Position.DistanceTo(player.Position) + "m away.");
        }

        // ------------------------------------------------------------------ health

        /// <summary>
        /// Converts whatever just happened to his real health into Resolve, then puts the health
        /// back. Everything about how hard he is to kill lives in these few lines.
        /// </summary>
        private void TrackDamage()
        {
            int health = Function.Call<int>(Hash.GET_ENTITY_HEALTH, _ped);
            int max = _maxHealth;

            if (health < _lastHealth)
            {
                float raw = _lastHealth - health;

                // A big single hit is a rocket, a car at speed, or whatever a physics mod just
                // did to him. Those always count for more than their share - partly because
                // they should, and partly because a player who has brought an RPG to this has
                // earned an answer better than "it tickles".
                bool heavy = raw >= _config.GetFloat("features.hunter.heavyHitThreshold", 150f);

                float multiplier = Vulnerable
                    ? _config.GetFloat("features.hunter.vulnerableMultiplier", 1f)
                    : _config.GetFloat("features.hunter.armouredMultiplier", 0.2f);

                if (heavy)
                {
                    multiplier = Math.Max(multiplier, _config.GetFloat("features.hunter.heavyHitMultiplier", 0.75f));
                }

                Resolve -= raw * multiplier;
                _break += raw;

                // Sustained fire is its own route in. Without this the only way to stagger him
                // is to survive an ability, which leaves a player with no explosives and good
                // aim doing everything right and getting nowhere.
                float threshold = _config.GetFloat("features.hunter.breakThreshold", 900f);
                if (threshold > 0f && _break >= threshold)
                {
                    _break = 0f;
                    Stagger(_config.GetInt("features.hunter.breakStaggerMs", 3000), "broken through");
                }
            }

            if (health < max)
            {
                try { Function.Call(Hash.SET_ENTITY_HEALTH, _ped, max); }
                catch (Exception) { }
            }

            _lastHealth = max;
        }

        private void UpdatePhase()
        {
            float fraction = ResolveMax <= 0f ? 0f : Resolve / ResolveMax;
            int phase = fraction > 0.66f ? 1 : (fraction > 0.33f ? 2 : 3);

            if (phase <= Phase) { return; }
            Phase = phase;

            if (_announcedPhase >= phase) { return; }
            _announcedPhase = phase;

            // The reward for getting him this far: a long, obvious opening. It is also the only
            // moment in the fight that is scripted rather than reactive, which is what makes the
            // difficulty curve readable instead of just steep.
            Stagger(_config.GetInt("features.hunter.phaseStaggerMs", 4500), "reeling");

            _fx.Lightning();
            _fx.Flash(_config.GetString("features.hunter.fx.phaseEffect", "DrugsTrevorClownsFightIn"), 1500);
            _fx.SlowTime(0.4f, 900);
            _fx.Shake("LARGE_EXPLOSION_SHAKE", 0.6f);

            GTA.UI.Screen.ShowSubtitle("~r~" + Name.ToUpperInvariant() + "~s~ is angry now.", 3000);
            Log.Info("Hunter reached phase " + phase + " at " + (int)Resolve + " resolve.");
        }

        private void Die()
        {
            if (Defeated) { return; }
            Defeated = true;
            _state = HunterState.Dead;

            try
            {
                Vector3 at = _ped != null && _ped.Exists() ? _ped.Position : Game.Player.Character.Position;

                _fx.StopLooped();
                _fx.Burst(at, _config.GetString("features.hunter.fx.blink", "core/exp_grd_bzgas_smoke"), 2.5f);
                _fx.Lightning();
                _fx.Flash(_config.GetString("features.hunter.fx.deathEffect", "SwitchHUDIn"), 2000);
                // No slow-motion here. Beating him ends the mode, and the mode ending tears this
                // class down on the same frame - which restores the time scale immediately and
                // turned a two-second beat into a stutter. The flash and the shake are instant
                // and survive it.
                _fx.Shake("LARGE_EXPLOSION_SHAKE", 0.8f);
                _fx.StopScreenEffect();

                if (_ped != null && _ped.Exists())
                {
                    Function.Call(Hash.SET_PED_CAN_RAGDOLL, _ped, true);
                    Function.Call(Hash.SET_ENTITY_HEALTH, _ped, 0);
                    // Left to the game from here. A body is proof, and deleting it is not.
                    _ped.MarkAsNoLongerNeeded();
                }

                if (_blip != null && _blip.Exists()) { _blip.Delete(); }
                _blip = null;
                _ped = null;
            }
            catch (Exception ex)
            {
                Log.Error("Could not finish the hunter off cleanly", ex);
            }

            GTA.UI.Screen.ShowSubtitle("~g~It is over.~s~ " + Name + " is down.", 6000);
            GTA.UI.Notification.Show("~g~You survived the night.");
            Log.Info("Hunter defeated.");
        }

        // ------------------------------------------------------------------ who

        /// <summary>
        /// Who he is going for this tick.
        ///
        /// You, almost always. But a boss who walks past a squad of police to get to you reads
        /// as a script with one line in it, and a mode where nothing else on the street is ever
        /// in danger has no stakes outside your own health bar. So anything meaningfully closer
        /// to him than you are is a detour he will take, for a few seconds, before coming back.
        ///
        /// It is also the only breathing room in the mode. Whoever he turns on buys you the time
        /// to reload, which is a far better answer to "this is relentless" than making him slower.
        /// </summary>
        private Ped ChooseTarget(Ped player, Traversal traversal)
        {
            // Nothing else is reachable from a helicopter or a boat, and a detour mid-flight
            // would strand him over the sea.
            if (traversal != Traversal.Ground) { _target = null; return player; }

            // Committed moves keep the target they launched at, or a rush would change course
            // between frames - which is exactly the undodgeable behaviour being removed.
            if (_state == HunterState.Winding || _state == HunterState.Rushing)
            {
                if (Alive(_target)) { return _target; }
                _target = null;
                return player;
            }

            if (Alive(_target) && Game.GameTime < _targetUntil &&
                _ped.Position.DistanceTo(_target.Position) <= DetourRadius * 1.6f)
            {
                return _target;
            }

            _target = null;

            if (!_config.GetBool("features.hunter.targetsOthers", true)) { return player; }
            if (Game.GameTime < _nextTargetAt) { return player; }
            _nextTargetAt = Game.GameTime + Math.Max(500, _config.GetInt("features.hunter.detourIntervalMs", 3500));

            if (_random.NextDouble() > _config.GetFloat("features.hunter.detourChance", 0.5f)) { return player; }

            Ped bystander = NearestBystander(player);
            if (bystander == null) { return player; }

            // Only when they are genuinely between him and you. Without this he wanders off
            // after somebody behind him and the fight stops being a fight.
            float toThem = _ped.Position.DistanceTo(bystander.Position);
            float toYou = _ped.Position.DistanceTo(player.Position);
            if (toThem > toYou * _config.GetFloat("features.hunter.detourAdvantage", 0.7f)) { return player; }

            _target = bystander;
            _targetUntil = Game.GameTime + Math.Max(1000, _config.GetInt("features.hunter.detourMs", 6000));
            _nextTaskAt = 0;
            return bystander;
        }

        private float DetourRadius
        {
            get { return _config.GetFloat("features.hunter.detourRadius", 30f); }
        }

        /// <summary>The nearest thing worth killing that is not you and not him.</summary>
        private Ped NearestBystander(Ped player)
        {
            Ped best = null;
            float bestDistance = DetourRadius * DetourRadius;
            bool protectMissionPeds = _config.GetBool("compatibility.protectMissionPeds", true);

            try
            {
                foreach (Ped nearby in World.GetNearbyPeds(_ped, DetourRadius))
                {
                    if (!Alive(nearby)) { continue; }
                    if (Owns(nearby) || nearby.Handle == player.Handle) { continue; }

                    // The same line every other part of this mod holds: a story ped killed here
                    // breaks a quest in a way nobody would ever attribute back to a riot mod.
                    if (protectMissionPeds && Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, nearby)) { continue; }

                    float distance = _ped.Position.DistanceToSquared(nearby.Position);
                    if (distance >= bestDistance) { continue; }

                    bestDistance = distance;
                    best = nearby;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not look for a hunter detour", ex);
            }

            return best;
        }

        private static bool Alive(Ped ped)
        {
            return ped != null && ped.Exists() && !ped.IsDead;
        }

        // ------------------------------------------------------------------ movement

        /// <summary>The only thing that touches his collision, so it can never be left off.</summary>
        private void Collision(bool on)
        {
            bool solid = !_noClip;
            if (solid == on) { return; }

            try
            {
                Function.Call(Hash.SET_ENTITY_COLLISION, _ped, on, on);
                _noClip = !on;
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Ragdoll is off while he is fighting and on while he is open.
        ///
        /// Both halves matter. Off, he cannot be tripped by a wing mirror or thrown across the
        /// street by a stray blast in the middle of a lunge, which is what made him look broken.
        /// On during the window, a rocket into an exposed boss puts him on the floor — which is
        /// the reward the window is supposed to be, and it was not there before.
        /// </summary>
        private void AllowRagdoll(bool allowed)
        {
            if (_ragdollAllowed == allowed) { return; }

            try
            {
                Function.Call(Hash.SET_PED_CAN_RAGDOLL, _ped, allowed);
                _ragdollAllowed = allowed;
            }
            catch (Exception) { }
        }

        private Traversal TraversalFor(Ped player)
        {
            try
            {
                Vehicle ride = player.CurrentVehicle;

                if (ride != null && ride.Exists())
                {
                    if (ride.ClassType == VehicleClass.Helicopters || ride.ClassType == VehicleClass.Planes) { return Traversal.Air; }
                    if (ride.ClassType == VehicleClass.Boats) { return Traversal.Water; }
                }

                if (Function.Call<bool>(Hash.IS_ENTITY_IN_WATER, player)) { return Traversal.Water; }

                // Off a rooftop, on a parachute, or thirty metres up a crane: all the same
                // problem, which is that the ground is not where he needs to be.
                //
                // Only when the probe actually answered. Treating its "I do not know" as sea
                // level made almost all of Los Santos read as a two-hundred-metre drop, and he
                // spent entire modes flying.
                float ground;
                if (Ground.TryHeight(player.Position, out ground) && player.Position.Z - ground > 12f)
                {
                    return Traversal.Air;
                }
            }
            catch (Exception)
            {
                // Any doubt resolves to the ordinary case.
            }

            return Traversal.Ground;
        }

        private void Stalk(Ped target, float distance, Traversal traversal)
        {
            if (traversal != Traversal.Ground)
            {
                Pursue(target, traversal);
                return;
            }

            // Back on the ground and back to being a solid object. Flight ends when the player
            // lands rather than when he decides to stop, so this is the only place that notices.
            Collision(true);
            AllowRagdoll(false);

            float speed = SpeedFor();

            try
            {
                Function.Call(Hash.SET_PED_MOVE_RATE_OVERRIDE, _ped, speed);
                _fx.MotionBlur(_ped, speed > 1.4f);
            }
            catch (Exception) { }

            // Ordinary melee, in between the heavy moves. This is what makes standing next to
            // him a decision rather than a free window: the telegraphed swings are dodgeable and
            // this is not, but it is small, and it only reaches as far as his arms do.
            if (distance <= _config.GetFloat("features.hunter.strikeReach", 2.6f))
            {
                Maul(target, Traversal.Ground);
            }

            if (Game.GameTime < _nextTaskAt) { return; }
            _nextTaskAt = Game.GameTime + _config.GetInt("features.hunter.retaskMs", 1500);

            try
            {
                if (distance <= _config.GetFloat("features.hunter.meleeRange", 8f))
                {
                    Function.Call(Hash.TASK_COMBAT_PED, _ped, target, 0, 16);
                }
                else
                {
                    // Run at them. Not a combat task at range: he would stop and posture, and
                    // whatever else he is, he is not somebody who takes cover.
                    Function.Call(Hash.TASK_GO_TO_ENTITY, _ped, target, -1, 2f, 12f, 1073741824f, 0);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not task the hunter", ex);
            }
        }

        /// <summary>
        /// Off the ground, he stops being a pedestrian and is moved directly.
        ///
        /// This is the answer to the only real counter a pursuer like this has: getting in a
        /// helicopter. Water is the same problem with a different surface — he walks on it,
        /// which is exactly the note this mode wants to end on.
        /// </summary>
        private void Pursue(Ped player, Traversal traversal)
        {
            float delta = Frame();
            Vector3 here = _ped.Position;
            Vector3 there = player.Position;

            Vector3 direction = there - here;
            float length = direction.Length();
            if (length < 0.01f) { return; }

            float speed = _config.GetFloat("features.hunter.flightSpeed", 26f) * (0.8f + 0.2f * Phase);
            Vector3 step = direction / length * Math.Min(length, speed * delta);
            Vector3 next = here + step;

            if (traversal == Traversal.Water)
            {
                next.Z = SurfaceAt(next, here.Z);
            }

            Collision(false);

            try
            {
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, _ped, next.X, next.Y, next.Z, false, false, false);
                Function.Call(Hash.SET_ENTITY_HEADING, _ped, Heading(step));
                Function.Call(Hash.CLEAR_PED_TASKS, _ped);
            }
            catch (Exception) { }

            TrailAt(here);

            // Close enough to do something about it. An aircraft gets wrecked rather than
            // boarded: pulling somebody out of a moving helicopter is a mess of edge cases and
            // "the engine is on fire" makes the same point.
            if (length < 6f) { Maul(player, traversal); }
        }

        /// <summary>
        /// One blow. Everything that lands damage on somebody comes through here, so the
        /// difference between what he does to you and what he does to a bystander is in one
        /// place: you get hurt, they get killed, and neither is an explosion.
        /// </summary>
        private void Maul(Ped victim, Traversal traversal)
        {
            if (!Alive(victim)) { return; }
            if (Game.GameTime < _nextStrikeAt) { return; }
            _nextStrikeAt = Game.GameTime + _config.GetInt("features.hunter.strikeIntervalMs", 1500);

            bool isPlayer = victim.Handle == Game.Player.Character.Handle;

            try
            {
                Vehicle ride = victim.CurrentVehicle;

                if (isPlayer && ride != null && ride.Exists() && traversal != Traversal.Ground)
                {
                    ride.EngineHealth = Math.Max(-1f, ride.EngineHealth - _config.GetFloat("features.hunter.vehicleDamage", 260f));
                    ride.HealthFloat = Math.Max(1f, ride.HealthFloat - _config.GetFloat("features.hunter.vehicleDamage", 260f));
                    _fx.Shake("SMALL_EXPLOSION_SHAKE", 0.7f);
                    _fx.Burst(_ped.Position, _config.GetString("features.hunter.fx.blink", "core/exp_grd_bzgas_smoke"), 1.2f);
                    return;
                }

                int damage = isPlayer
                    ? _config.GetInt("features.hunter.strikeDamage", 45)
                    : _config.GetInt("features.hunter.bystanderDamage", 250);

                Function.Call(Hash.APPLY_DAMAGE_TO_PED, victim, damage, true, 0);

                // Bodies go where he hit them. It costs nothing, it is the clearest possible
                // signal that a blow landed, and it is why he no longer needs a silent radius
                // that deleted whoever stood near him.
                if (!isPlayer)
                {
                    Function.Call(Hash.SET_PED_TO_RAGDOLL, victim, 3000, 3000, 0, true, true, false);
                    _fx.Shove(victim, Flat(victim.Position - _ped.Position), _config.GetFloat("features.hunter.shoveStrength", 22f));
                    _fx.Burst(victim.Position, _config.GetString("features.hunter.fx.slam", "core/exp_grd_bzgas_smoke"), 0.5f);
                }

                _fx.Shake("SMALL_EXPLOSION_SHAKE", isPlayer ? 0.5f : 0.25f);
            }
            catch (Exception ex)
            {
                Log.Error("Hunter strike failed", ex);
            }
        }

        /// <summary>
        /// The water surface at a point, or the ground where there is none.
        ///
        /// Both matter, because chasing somebody in a boat usually starts inland: holding his
        /// last altitude across the beach put him inside the hill on the way.
        /// </summary>
        private float SurfaceAt(Vector3 point, float fallback)
        {
            try
            {
                var height = new OutputArgument();
                if (Function.Call<bool>(Hash.GET_WATER_HEIGHT, point.X, point.Y, point.Z, height))
                {
                    return height.GetResult<float>() + 0.4f;
                }
            }
            catch (Exception) { }

            float ground;
            return Ground.TryHeight(point, out ground) ? ground + 1f : fallback;
        }

        // ------------------------------------------------------------------ abilities

        private void MaybeUseAbility(Ped target, float distance, Traversal traversal)
        {
            if (_state != HunterState.Stalking) { return; }
            if (traversal != Traversal.Ground) { return; }
            if (Game.GameTime < _nextAbilityAt) { return; }
            if (!Alive(target)) { return; }

            float strikeRange = _config.GetFloat("features.hunter.slamRange", 9f);
            float rushRange = _config.GetFloat("features.hunter.rushRange", 55f);
            float hurlRange = _config.GetFloat("features.hunter.hurlRange", 90f);

            if (distance <= strikeRange)
            {
                // Two ways to be hit at arm's length, countered in opposite directions: the
                // ground strike wants you outside it, the cleave wants you behind him. A single
                // close-range move is a rhythm you learn once and never think about again.
                bool cleave = Phase >= 2 && _random.NextDouble() < _config.GetFloat("features.hunter.cleaveChance", 0.45f);
                BeginWind(cleave ? HunterMove.Cleave : HunterMove.Strike, target);
                return;
            }

            if (distance <= rushRange)
            {
                BeginWind(HunterMove.Rush, target);
                return;
            }

            // Ranged pressure, so backing off to a rooftop with a rifle is a different fight
            // rather than a safe one. Held back until he has been hurt: the first phase is
            // deliberately a plain melee fight you can learn the tells in.
            if (Phase >= _config.GetInt("features.hunter.hurlFromPhase", 2) && distance <= hurlRange)
            {
                BeginWind(HunterMove.Hurl, target);
                return;
            }

            BeginBlink(target);
        }

        private int Cooldown(string key, int fallback)
        {
            int baseline = _config.GetInt(key, fallback);
            // Everything comes faster the closer he is to the end of him.
            return Math.Max(400, (int)(baseline * (1f - 0.18f * (Phase - 1))));
        }

        /// <summary>
        /// The tell.
        ///
        /// He plants, turns to face whoever he has picked, and a ring appears under him for a
        /// beat before anything happens. That beat is the entire difference between a fight and
        /// a hazard: it costs him the element of surprise on every heavy move he has, and it is
        /// the reason none of them need to be survivable only by being invincible.
        ///
        /// It shortens with each phase, so the fight gets harder by giving you less time rather
        /// than by taking the answer away.
        /// </summary>
        private void BeginWind(HunterMove move, Ped target)
        {
            _move = move;
            _target = target;
            _state = HunterState.Winding;
            _windAt = _ped.Position;

            int windup = Math.Max(120, (int)(Windup(move) * (1f - 0.15f * (Phase - 1))));
            _stateUntil = Game.GameTime + windup;

            Collision(true);
            AllowRagdoll(false);

            try
            {
                Function.Call(Hash.CLEAR_PED_TASKS, _ped);
                Function.Call(Hash.SET_PED_MOVE_RATE_OVERRIDE, _ped, 0.1f);
                Function.Call(Hash.TASK_TURN_PED_TO_FACE_ENTITY, _ped, target, windup);
            }
            catch (Exception) { }

            _fx.MotionBlur(_ped, false);
            _fx.Sound("HUD_MINI_GAME_SOUNDSET", "CHECKPOINT_MISSED");
        }

        /// <summary>
        /// How long the tell lasts for a given move, before the phase makes it shorter.
        ///
        /// Per move rather than one number, because the answers take different amounts of time:
        /// stepping off a charge line is a decision, getting out of a twelve-metre ring is a
        /// sprint, and both have to be possible from a standing start.
        /// </summary>
        private int Windup(HunterMove move)
        {
            switch (move)
            {
                case HunterMove.Rush: return _config.GetInt("features.hunter.rushWindupMs", 750);
                case HunterMove.Cleave: return _config.GetInt("features.hunter.cleaveWindupMs", 600);
                case HunterMove.Hurl: return _config.GetInt("features.hunter.hurlWindupMs", 700);
                default: return _config.GetInt("features.hunter.slamWindupMs", 650);
            }
        }

        /// <summary>Draws the tell every frame, then fires the move it was telling you about.</summary>
        private void AdvanceWind(Ped target)
        {
            if (!Alive(target))
            {
                // Whoever he was about to hit is already down. Drop it rather than swinging at
                // a body, and take the ordinary cooldown so this is not a free reset.
                _state = HunterState.Stalking;
                _move = HunterMove.None;
                _nextAbilityAt = Game.GameTime + 600;
                return;
            }

            DrawTell(target);

            if (Game.GameTime < _stateUntil) { return; }

            HunterMove move = _move;
            _move = HunterMove.None;

            switch (move)
            {
                case HunterMove.Rush: BeginRush(target); break;
                case HunterMove.Cleave: Cleave(target); break;
                case HunterMove.Hurl: Hurl(target); break;
                default: Strike(); break;
            }
        }

        /// <summary>
        /// What the tell looks like, which is different for each move because the answer to
        /// each move is different. A ring means get out of it; a line means get off it.
        /// </summary>
        private void DrawTell(Ped target)
        {
            try
            {
                switch (_move)
                {
                    case HunterMove.Strike:
                        _fx.Ring(_windAt, _config.GetFloat("features.hunter.slamRadius", 12f), 220, 40, 40, 90);
                        break;

                    case HunterMove.Cleave:
                        _fx.Ring(_windAt + Facing() * _config.GetFloat("features.hunter.cleaveRange", 5f) * 0.5f,
                                 _config.GetFloat("features.hunter.cleaveRange", 5f) * 0.6f, 250, 150, 40, 110);
                        break;

                    case HunterMove.Rush:
                        // The line he is about to travel, marked at both ends. Standing off it
                        // by a couple of metres is the counter, and it has to be visible to be
                        // a counter at all.
                        _fx.Ring(_windAt, 1.6f, 250, 210, 60, 120);
                        _fx.Ring(target.Position, 2.4f, 250, 210, 60, 80);
                        break;

                    case HunterMove.Hurl:
                        _fx.Ring(_windAt, 1.4f, 200, 200, 255, 90);
                        break;
                }
            }
            catch (Exception)
            {
                // A tell that fails to draw is a harder fight, not a broken one.
            }
        }

        /// <summary>
        /// The lunge. Collision off so walls, cars and fences are not part of the conversation,
        /// and a hard stop at the end that leaves him open — which is the point of it existing.
        ///
        /// The direction is fixed here, at launch, and only nudged afterwards. That is the
        /// difference between an attack and a cutscene.
        /// </summary>
        private void BeginRush(Ped target)
        {
            _state = HunterState.Rushing;
            _stateUntil = Game.GameTime + _config.GetInt("features.hunter.rushMs", 1400);
            _nextAbilityAt = Game.GameTime + Cooldown("features.hunter.rushCooldownMs", 6500);

            Vector3 lead = target.Position;

            try
            {
                // Aimed at where they are going rather than where they are, so walking in a
                // straight line is not a counter either. Dodging means changing what you are
                // doing, which is the whole point.
                lead += Flat(target.Velocity) * _config.GetFloat("features.hunter.rushLeadSeconds", 0.35f);
            }
            catch (Exception) { }

            Vector3 aim = Flat(lead - _ped.Position);
            _rushAim = aim.Length() < 0.01f ? Facing() : Vector3.Normalize(aim);

            try { Function.Call(Hash.CLEAR_PED_TASKS, _ped); }
            catch (Exception) { }

            Collision(false);
            _fx.MotionBlur(_ped, true);
            _fx.Sound("HUD_FRONTEND_DEFAULT_SOUNDSET", "Menu_Accept");
        }

        private void AdvanceRush(Ped target)
        {
            float delta = Frame();
            Vector3 here = _ped.Position;

            if (!Alive(target)) { EndRush(false); return; }

            Vector3 toTarget = Flat(target.Position - here);
            float length = toTarget.Length();

            if (length <= _config.GetFloat("features.hunter.rushHitRadius", 2.6f))
            {
                Connect(target);
                return;
            }

            // Past them, or out of time. Both are misses, and a miss is the opening.
            if (Game.GameTime > _stateUntil || Vector3.Dot(toTarget, _rushAim) <= 0f)
            {
                EndRush(false);
                return;
            }

            // Steering, not tracking. A fixed turn rate means he corrects for someone jogging
            // and cannot correct for someone who jinks, which is exactly the trade this move
            // is supposed to offer.
            float steer = Math.Min(1f, _config.GetFloat("features.hunter.rushSteerRate", 0.9f) * delta);
            Vector3 wanted = length < 0.01f ? _rushAim : toTarget / length;
            Vector3 blended = _rushAim + (wanted - _rushAim) * steer;
            if (blended.Length() > 0.01f) { _rushAim = Vector3.Normalize(blended); }

            float speed = _config.GetFloat("features.hunter.rushSpeed", 42f) * (0.85f + 0.15f * Phase);
            Vector3 next = here + _rushAim * (speed * delta);

            // Keep his own height where the probe has nothing to say. Taking its zero as an
            // answer dropped him to sea level mid-rush, which under most of the city is
            // underground.
            float ground;
            next.Z = Ground.TryHeight(next, out ground) ? ground + 1f : here.Z;

            try
            {
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, _ped, next.X, next.Y, next.Z, false, false, false);
                Function.Call(Hash.SET_ENTITY_HEADING, _ped, Heading(_rushAim));
            }
            catch (Exception) { }

            TrailAt(here);

            // Anything he runs through on the way is hit by him running through it. He is not
            // steering around people at forty metres a second.
            Trample(target);
        }

        private void Connect(Ped target)
        {
            // A rush that lands has to be able to land. Its own strike timer is cleared here
            // rather than shared, because the ordinary melee cadence would otherwise eat the
            // one hit the whole move exists to deliver.
            _nextStrikeAt = 0;
            Maul(target, Traversal.Ground);

            if (Alive(target) && target.Handle == Game.Player.Character.Handle)
            {
                try
                {
                    Vehicle ride = target.CurrentVehicle;

                    if (ride != null && ride.Exists())
                    {
                        _fx.Shove(ride, Flat(ride.Position - _ped.Position) + new Vector3(0f, 0f, 0.35f),
                                  _config.GetFloat("features.hunter.rushShoveStrength", 45f));
                    }
                    else
                    {
                        Function.Call(Hash.SET_PED_TO_RAGDOLL, target, 1500, 1500, 0, true, true, false);
                    }
                }
                catch (Exception) { }
            }

            _fx.Shake("MEDIUM_EXPLOSION_SHAKE", 0.6f);
            EndRush(true);
        }

        /// <summary>
        /// A connected rush costs him a little; a missed one costs him a lot.
        ///
        /// That asymmetry is the reward for reading the tell. Without it, dodging is worth doing
        /// only to avoid the damage, and the fight has no forward motion.
        /// </summary>
        private void EndRush(bool connected)
        {
            Collision(true);
            _fx.MotionBlur(_ped, false);

            Stagger(connected
                ? _config.GetInt("features.hunter.rushRecoveryMs", 1600)
                : _config.GetInt("features.hunter.rushWhiffRecoveryMs", 2800),
                connected ? "off balance" : "swung at nothing");
        }

        /// <summary>Whoever he runs over on the way to whoever he was aiming at.</summary>
        private void Trample(Ped target)
        {
            try
            {
                foreach (Ped nearby in World.GetNearbyPeds(_ped, 2.2f))
                {
                    if (!Alive(nearby) || Owns(nearby)) { continue; }
                    if (target != null && target.Exists() && nearby.Handle == target.Handle) { continue; }
                    if (nearby.Handle == Game.Player.Character.Handle) { continue; }
                    if (_config.GetBool("compatibility.protectMissionPeds", true) &&
                        Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, nearby))
                    {
                        continue;
                    }

                    Function.Call(Hash.SET_PED_TO_RAGDOLL, nearby, 3000, 3000, 0, true, true, false);
                    _fx.Shove(nearby, Flat(nearby.Position - _ped.Position) + new Vector3(0f, 0f, 0.4f),
                              _config.GetFloat("features.hunter.shoveStrength", 22f));
                    Function.Call(Hash.APPLY_DAMAGE_TO_PED, nearby,
                        _config.GetInt("features.hunter.bystanderDamage", 250), true, 0);
                    return;
                }
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Crossing a distance he has no business crossing. Deliberately not instant: the fade,
        /// the puff of nothing at both ends, and a beat between them are what make it read as a
        /// power rather than as the game losing track of him.
        /// </summary>
        private void BeginBlink(Ped target)
        {
            _state = HunterState.Blinking;
            _stateUntil = Game.GameTime + _config.GetInt("features.hunter.blinkMs", 700);
            _nextAbilityAt = Game.GameTime + Cooldown("features.hunter.blinkCooldownMs", 9000);

            try
            {
                Function.Call(Hash.CLEAR_PED_TASKS, _ped);
                Function.Call(Hash.SET_ENTITY_ALPHA, _ped, 60, false);
            }
            catch (Exception) { }

            Collision(false);

            _fx.Burst(_ped.Position, _config.GetString("features.hunter.fx.blink", "core/exp_grd_bzgas_smoke"), 1.4f);
            _fx.Sound("HUD_FRONTEND_DEFAULT_SOUNDSET", "Menu_Accept");
        }

        private void AdvanceBlink(Ped player)
        {
            if (Game.GameTime < _stateUntil) { return; }

            float range = _config.GetFloat("features.hunter.blinkArrivalRange", 22f);
            Vector3 arrival = Ground.Place(player.Position.Around(range));
            if (arrival == Vector3.Zero) { arrival = Ground.OnGround(player.Position.Around(range * 0.5f)); }
            if (arrival == Vector3.Zero) { arrival = player.Position; }

            Collision(true);

            try
            {
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, _ped, arrival.X, arrival.Y, arrival.Z + 1f, false, false, false);
                Function.Call(Hash.RESET_ENTITY_ALPHA, _ped);
                Function.Call(Hash.SET_ENTITY_HEADING, _ped, Heading(player.Position - arrival));
            }
            catch (Exception) { }

            _fx.Burst(arrival, _config.GetString("features.hunter.fx.blink", "core/exp_grd_bzgas_smoke"), 1.6f);
            _fx.Lightning();

            _state = HunterState.Stalking;
            _nextTaskAt = 0;
        }

        /// <summary>
        /// A fist into the road. Everything standing in the ring goes up and comes down hurt.
        ///
        /// It used to raise an explosion for the shove, which is why he spent the fight lying
        /// on his back next to a fireball: an explosion applies its impulse to everything in
        /// radius, and he was standing in the middle of it. The shove is aimed per entity now,
        /// so it can never reach him, and nothing here sets anybody on fire.
        /// </summary>
        private void Strike()
        {
            _nextAbilityAt = Game.GameTime + Cooldown("features.hunter.slamCooldownMs", 8000);

            // Where the ring was drawn, not where he is now. The tell is a promise about where
            // the blow lands, and a shove or a stumble during the windup would otherwise move
            // the hit away from the mark the player was reading.
            Vector3 at = _windAt;
            float radius = _config.GetFloat("features.hunter.slamRadius", 12f);

            _fx.Burst(at, _config.GetString("features.hunter.fx.slam", "core/exp_grd_bzgas_smoke"), 2f);
            _fx.Shake("LARGE_EXPLOSION_SHAKE", 0.55f);
            if (Phase >= 3) { _fx.Lightning(); }

            Sweep(at, radius, 0f, _config.GetInt("features.hunter.slamDamage", 30));
            Stagger(_config.GetInt("features.hunter.slamRecoveryMs", 3200), "spent");
        }

        /// <summary>
        /// The heavy swing. Same idea as the ground strike with the opposite answer: it only
        /// reaches the arc in front of him, so the counter is to be somewhere else — which is a
        /// thing you can only do if you were told it was coming.
        /// </summary>
        private void Cleave(Ped target)
        {
            _nextAbilityAt = Game.GameTime + Cooldown("features.hunter.cleaveCooldownMs", 7000);

            // Squared up one last time, so the arc that lands is the arc that was drawn. The
            // turn task during the windup usually finishes on its own; when it does not, a
            // cleave that misses because he never finished turning reads as the game cheating
            // in the player's favour, which is its own kind of illegible.
            try { Function.Call(Hash.SET_ENTITY_HEADING, _ped, Heading(target.Position - _ped.Position)); }
            catch (Exception) { }

            Vector3 at = _windAt;
            float range = _config.GetFloat("features.hunter.cleaveRange", 5f);

            _fx.Burst(at + Facing() * range * 0.5f, _config.GetString("features.hunter.fx.slam", "core/exp_grd_bzgas_smoke"), 1.4f);
            _fx.Shake("MEDIUM_EXPLOSION_SHAKE", 0.5f);

            // A cone, expressed as how far round from straight ahead still counts.
            Sweep(at, range, _config.GetFloat("features.hunter.cleaveArc", 0.35f),
                  _config.GetInt("features.hunter.cleaveDamage", 55));

            Stagger(_config.GetInt("features.hunter.cleaveRecoveryMs", 2400), "over-committed");
        }

        /// <summary>
        /// Everything in a radius, or in an arc of one.
        ///
        /// <paramref name="minimumDot"/> of zero is the full circle; anything above it is a cone
        /// centred on where he is facing. Both are hand-applied damage and hand-applied force,
        /// which is the rule this class now holds everywhere: nothing he does is an explosion,
        /// so nothing he does can hit him.
        /// </summary>
        private void Sweep(Vector3 at, float radius, float minimumDot, int playerDamage)
        {
            Ped player = Game.Player.Character;
            Vector3 facing = Facing();
            bool protectMissionPeds = _config.GetBool("compatibility.protectMissionPeds", true);
            float strength = _config.GetFloat("features.hunter.shoveStrength", 22f);

            try
            {
                foreach (Ped nearby in World.GetNearbyPeds(_ped, radius))
                {
                    if (nearby == null || !nearby.Exists() || Owns(nearby)) { continue; }

                    Vector3 away = Flat(nearby.Position - at);
                    if (away.Length() > 0.01f && minimumDot > 0f &&
                        Vector3.Dot(Vector3.Normalize(away), facing) < minimumDot)
                    {
                        continue;
                    }

                    bool isPlayer = nearby.Handle == player.Handle;

                    if (!isPlayer && protectMissionPeds &&
                        Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, nearby))
                    {
                        continue;
                    }

                    Function.Call(Hash.APPLY_DAMAGE_TO_PED, nearby,
                        isPlayer ? playerDamage : _config.GetInt("features.hunter.bystanderDamage", 250), true, 0);

                    // Anybody sitting in a car is thrown by the car being thrown, below. Trying
                    // to ragdoll them here does nothing and would only look like it should.
                    if (nearby.IsInVehicle()) { continue; }

                    Function.Call(Hash.SET_PED_TO_RAGDOLL, nearby, isPlayer ? 1800 : 3000, 2500, 0, true, true, false);
                    _fx.Shove(nearby, away + new Vector3(0f, 0f, 0.45f), strength);
                }

                // Cars in the ring are thrown too, which is most of what makes the move read as
                // heavy rather than as a scripted health subtraction.
                foreach (Vehicle vehicle in World.GetNearbyVehicles(at, radius))
                {
                    if (vehicle == null || !vehicle.Exists()) { continue; }

                    Vector3 away = Flat(vehicle.Position - at);
                    if (away.Length() > 0.01f && minimumDot > 0f &&
                        Vector3.Dot(Vector3.Normalize(away), facing) < minimumDot)
                    {
                        continue;
                    }

                    _fx.Shove(vehicle, away + new Vector3(0f, 0f, 0.6f),
                              _config.GetFloat("features.hunter.vehicleShoveStrength", 12f));
                }
            }
            catch (Exception ex)
            {
                Log.Error("Hunter sweep failed", ex);
            }
        }

        /// <summary>
        /// Something out of the street, thrown at head height.
        ///
        /// The mode is a melee fight, and a melee fight against something that cannot cross
        /// water has one answer: stand far away. This is the answer to that answer. It is a real
        /// object with real physics, so it can be sidestepped, it can be shot out of the air, and
        /// it hits whatever it actually reaches rather than whoever it was aimed at.
        /// </summary>
        private void Hurl(Ped target)
        {
            _nextAbilityAt = Game.GameTime + Cooldown("features.hunter.hurlCooldownMs", 5500);

            try
            {
                List<string> candidates = _declared["debris"].AsStringList();
                if (candidates.Count == 0) { candidates = _config.GetStringList("features.hunter.debris"); }
                if (candidates.Count == 0) { candidates = new List<string>(DefaultDebris); }

                Model model;
                if (!_models.TryPick(candidates, _random, out model) || !_models.Load(model))
                {
                    // Nothing to throw. Fall back to closing the distance rather than standing
                    // there having spent the cooldown on nothing.
                    BeginRush(target);
                    return;
                }

                Vector3 from = _ped.Position + Facing() * 1.2f + new Vector3(0f, 0f, 1.4f);
                Prop prop = World.CreateProp(model, from, false, false);
                if (prop == null || !prop.Exists()) { return; }

                prop.IsPersistent = true;

                Vector3 lead = target.Position + new Vector3(0f, 0f, 0.5f);
                try { lead += Flat(target.Velocity) * _config.GetFloat("features.hunter.hurlLeadSeconds", 0.5f); }
                catch (Exception) { }

                Vector3 flight = lead - from;
                float span = flight.Length();
                float speed = _config.GetFloat("features.hunter.hurlSpeed", 34f);

                if (span > 0.01f)
                {
                    // A flat throw plus enough lift to arrive at head height rather than at the
                    // kerb. Not real ballistics: it only has to look thrown and land near you.
                    Vector3 velocity = flight / span * speed;
                    velocity.Z += span * _config.GetFloat("features.hunter.hurlArc", 0.09f);
                    prop.Velocity = velocity;
                }

                _thrown.Add(new Debris
                {
                    Prop = prop,
                    DieAt = Game.GameTime + Math.Max(1000, _config.GetInt("features.hunter.hurlLifeMs", 6000))
                });

                _fx.Burst(from, _config.GetString("features.hunter.fx.slam", "core/exp_grd_bzgas_smoke"), 0.8f);
                _fx.Sound("HUD_FRONTEND_DEFAULT_SOUNDSET", "Menu_Accept");
            }
            catch (Exception ex)
            {
                Log.Error("Hunter could not throw anything", ex);
            }

            Stagger(_config.GetInt("features.hunter.hurlRecoveryMs", 1400), "wide open");
        }

        /// <summary>
        /// What he threw, while it is still in the air.
        ///
        /// Runs on every path including the one where he is already dead, because a prop this
        /// class made persistent and then forgot about is litter that outlives the mode.
        /// </summary>
        private void UpdateThrown()
        {
            if (_thrown.Count == 0) { return; }

            float radius = _config.GetFloat("features.hunter.hurlHitRadius", 2.2f);
            int damage = _config.GetInt("features.hunter.hurlDamage", 40);
            Ped player = Game.Player.Character;

            for (int i = _thrown.Count - 1; i >= 0; i--)
            {
                Debris debris = _thrown[i];

                if (debris.Prop == null || !debris.Prop.Exists() || Game.GameTime > debris.DieAt)
                {
                    Discard(debris);
                    _thrown.RemoveAt(i);
                    continue;
                }

                if (debris.Spent) { continue; }

                try
                {
                    Vector3 at = debris.Prop.Position;

                    if (player != null && player.Exists() && !player.IsDead &&
                        at.DistanceToSquared(player.Position) <= radius * radius)
                    {
                        Function.Call(Hash.APPLY_DAMAGE_TO_PED, player, damage, true, 0);
                        _fx.Shake("SMALL_EXPLOSION_SHAKE", 0.4f);
                        _fx.Burst(at, _config.GetString("features.hunter.fx.slam", "core/exp_grd_bzgas_smoke"), 0.7f);
                        debris.Spent = true;
                        debris.DieAt = Math.Min(debris.DieAt, Game.GameTime + 2000);
                        continue;
                    }

                    // It hits whoever it reaches. Aimed at you, but a bystander who walks into
                    // it takes it instead, which is the same rule everything else here follows.
                    foreach (Ped nearby in World.GetNearbyPeds(at, radius))
                    {
                        if (!Alive(nearby) || Owns(nearby)) { continue; }
                        if (_config.GetBool("compatibility.protectMissionPeds", true) &&
                            Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, nearby))
                        {
                            continue;
                        }

                        Function.Call(Hash.APPLY_DAMAGE_TO_PED, nearby,
                            _config.GetInt("features.hunter.bystanderDamage", 250), true, 0);
                        Function.Call(Hash.SET_PED_TO_RAGDOLL, nearby, 3000, 3000, 0, true, true, false);
                        debris.Spent = true;
                        debris.DieAt = Math.Min(debris.DieAt, Game.GameTime + 2000);
                        break;
                    }
                }
                catch (Exception)
                {
                    debris.Spent = true;
                }
            }
        }

        private void ClearThrown()
        {
            foreach (Debris debris in _thrown) { Discard(debris); }
            _thrown.Clear();
        }

        /// <summary>
        /// Hands a thrown object back to the game rather than deleting it out from under the
        /// player's feet. Persistence is what we took; letting go of it is all we owe.
        /// </summary>
        private static void Discard(Debris debris)
        {
            try
            {
                if (debris.Prop == null || !debris.Prop.Exists()) { return; }
                debris.Prop.IsPersistent = false;
                debris.Prop.MarkAsNoLongerNeeded();
            }
            catch (Exception) { }
        }

        // ------------------------------------------------------------------ stagger

        /// <summary>
        /// The window. Everything else in this class exists to open one of these and to make
        /// closing it feel like a loss.
        ///
        /// He is not put on the floor for it any more. A ragdolled boss is indistinguishable
        /// from a boss who has fallen over by accident — which, between the ground slam's own
        /// explosion and this, is exactly what he looked like. He plants instead, and ragdoll is
        /// switched *on* for the duration so that a heavy hit landed in the window does put him
        /// down. Falling over is the player's doing now, not his.
        /// </summary>
        private void Stagger(int durationMs, string why)
        {
            if (durationMs <= 0) { return; }

            _state = HunterState.Staggered;
            _stateUntil = Game.GameTime + durationMs;
            _vulnerableUntil = _stateUntil;
            _move = HunterMove.None;

            Collision(true);

            try
            {
                Function.Call(Hash.RESET_ENTITY_ALPHA, _ped);
                Function.Call(Hash.CLEAR_PED_TASKS, _ped);
                Function.Call(Hash.SET_PED_MOVE_RATE_OVERRIDE, _ped, 0.35f);

                // Rooted for the duration, rather than merely untasked. A ped with AlwaysFight
                // set and a machete in his hand does not stand about waiting when he has nothing
                // to do - he starts a combat task of his own, and the window closes itself.
                Function.Call(Hash.TASK_STAND_STILL, _ped, durationMs);

                // Open either way; the only question is whether he is also on the floor for it.
                AllowRagdoll(true);

                if (_config.GetBool("features.hunter.staggerRagdoll", false))
                {
                    Function.Call(Hash.SET_PED_TO_RAGDOLL, _ped, durationMs, durationMs, 0, true, true, false);
                }
            }
            catch (Exception) { }

            _fx.MotionBlur(_ped, false);
            _fx.Sound("HUD_MINI_GAME_SOUNDSET", "CHECKPOINT_PERFECT");
            _fx.Burst(_ped.Position, _config.GetString("features.hunter.fx.slam", "core/exp_grd_bzgas_smoke"), 0.9f);
            Log.Debug("Hunter staggered (" + why + ") for " + durationMs + "ms at " + (int)Resolve + " resolve.");
        }

        private void AdvanceStagger()
        {
            // The window is drawn on him for its whole length, so there is never any doubt about
            // whether it is still open.
            _fx.Ring(_ped.Position, 2.2f, 250, 230, 90, 70);

            if (Game.GameTime < _stateUntil) { return; }

            AllowRagdoll(false);

            try
            {
                Function.Call(Hash.SET_PED_MOVE_RATE_OVERRIDE, _ped, SpeedFor());
            }
            catch (Exception) { }

            _state = HunterState.Stalking;
            _nextTaskAt = 0;
        }

        private void Idle()
        {
            try { Function.Call(Hash.CLEAR_PED_TASKS, _ped); }
            catch (Exception) { }

            Collision(true);
            AllowRagdoll(false);
            _state = HunterState.Stalking;
            _move = HunterMove.None;
            _target = null;
        }

        // ------------------------------------------------------------------ the rest

        /// <summary>
        /// Losing him is allowed. Losing him permanently is not — a hunter you can drive away
        /// from is a chase sequence, and this is supposed to be a night you have to survive.
        /// </summary>
        private void Leash(Ped player, float distance)
        {
            float leash = _config.GetFloat("features.hunter.leashDistance", 220f);
            if (distance < leash) { return; }

            if (Game.GameTime - _leashWarnedAt > 20000)
            {
                _leashWarnedAt = Game.GameTime;
                GTA.UI.Notification.Show("~r~" + Name + "~s~ is still coming.");
            }

            // Whoever he had wandered off after is no longer the point.
            _target = null;
            _targetUntil = 0;

            // Straight to a blink rather than a slow jog across the map.
            _nextAbilityAt = 0;
            if (_state == HunterState.Stalking) { BeginBlink(player); }
        }

        private void Present(Ped player)
        {
            float distance = _ped.Position.DistanceTo(player.Position);
            float near = _config.GetFloat("features.hunter.nearDistance", 25f);

            if (distance < near) { _fx.StartScreenEffect(_config.GetString("features.hunter.fx.nearEffect", "RaceTurbo")); }
            else { _fx.StopScreenEffect(); }

            if (!_config.GetBool("features.hunter.showBar", true)) { return; }

            float fraction = ResolveMax <= 0f ? 0f : Resolve / ResolveMax;

            Hud.Banner("~r~" + Name.ToUpperInvariant() + "~s~   " + (int)(fraction * 100) + "%" +
                       Status() + "   ~c~" + (int)distance + "m",
                       0.075f, 0.5f, System.Drawing.Color.FromArgb(235, 255, 255, 255));

            Hud.Bar(0.35f, 0.115f, 0.30f, 0.012f, fraction,
                    Vulnerable ? System.Drawing.Color.FromArgb(230, 250, 210, 60)
                               : System.Drawing.Color.FromArgb(230, 190, 30, 30),
                    System.Drawing.Color.FromArgb(150, 20, 20, 20));

            float threshold = _config.GetFloat("features.hunter.breakThreshold", 900f);
            if (threshold > 0f)
            {
                Hud.Bar(0.35f, 0.130f, 0.30f, 0.006f, _break / threshold,
                        System.Drawing.Color.FromArgb(210, 240, 240, 240),
                        System.Drawing.Color.FromArgb(120, 20, 20, 20));
            }
        }

        /// <summary>
        /// The one word on the bar that says what he is doing.
        ///
        /// The tells are on the ground where the fight is, but the ground is not always where
        /// you are looking - in a car, or behind him, or at a distance. A player who cannot see
        /// the ring still gets told the swing is coming.
        /// </summary>
        private string Status()
        {
            if (_state == HunterState.Winding)
            {
                switch (_move)
                {
                    case HunterMove.Rush: return "   ~o~CHARGING";
                    case HunterMove.Cleave: return "   ~o~SWINGING";
                    case HunterMove.Hurl: return "   ~o~THROWING";
                    default: return "   ~o~WINDING UP";
                }
            }

            if (Vulnerable) { return "   ~y~EXPOSED"; }

            if (Alive(_target) && _target.Handle != Game.Player.Character.Handle) { return "   ~c~DISTRACTED"; }

            return string.Empty;
        }

        private void TrailAt(Vector3 at)
        {
            if (_random.NextDouble() > _config.GetFloat("features.hunter.trailChance", 0.5f)) { return; }
            _fx.Burst(at, _config.GetString("features.hunter.fx.trail", "core/exp_grd_bzgas_smoke"), 0.35f);
        }

        private float SpeedFor()
        {
            float baseline = _config.GetFloat("features.hunter.moveRate", 1.55f);
            return baseline + 0.15f * (Phase - 1);
        }

        /// <summary>Where he is facing, flattened. Every cone and shove is measured against it.</summary>
        private Vector3 Facing()
        {
            try
            {
                Vector3 forward = Flat(_ped.ForwardVector);
                if (forward.Length() > 0.01f) { return Vector3.Normalize(forward); }
            }
            catch (Exception) { }

            double radians = (_ped.Heading + 90f) * Math.PI / 180.0;
            return new Vector3((float)Math.Cos(radians), (float)Math.Sin(radians), 0f);
        }

        /// <summary>
        /// A vector with the height taken out.
        ///
        /// Everything in this fight happens on a street. Letting Z into a shove direction meant
        /// a target one storey up got pushed sideways and slightly into the road surface, and a
        /// rush aimed at somebody on a balcony steered into the ground.
        /// </summary>
        private static Vector3 Flat(Vector3 vector)
        {
            return new Vector3(vector.X, vector.Y, 0f);
        }

        private static float Heading(Vector3 direction)
        {
            return (float)(Math.Atan2(direction.Y, direction.X) * 180.0 / Math.PI) - 90f;
        }

        /// <summary>Real seconds since the last frame, clamped so a hitch cannot teleport him.</summary>
        private static float Frame()
        {
            float delta = Game.LastFrameTime;
            return delta <= 0f || delta > 0.25f ? 1f / 60f : delta;
        }
    }
}
