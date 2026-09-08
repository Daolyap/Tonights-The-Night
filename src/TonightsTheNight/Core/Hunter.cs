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
        /// <summary>Crossing the gap. Collision off, physics off, a streak of light.</summary>
        Rushing,
        /// <summary>Gone from where he was and not yet where he is going.</summary>
        Blinking,
        /// <summary>On one knee. The only time he can really be hurt.</summary>
        Staggered,
        Dead
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

        private readonly ConfigStore _config;
        private readonly ModelResolver _models;
        private readonly Random _random;
        private readonly HunterFx _fx;

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

        private int _nextCullAt;
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
        private Vector3 _rushTarget;
        private int _leashWarnedAt;

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

                float distance = _ped.Position.DistanceTo(player.Position);
                Traversal traversal = TraversalFor(player);

                Leash(player, distance);

                switch (_state)
                {
                    case HunterState.Rushing: AdvanceRush(player); break;
                    case HunterState.Blinking: AdvanceBlink(player); break;
                    case HunterState.Staggered: AdvanceStagger(); break;
                    default: Stalk(player, distance, traversal); break;
                }

                MaybeUseAbility(player, distance, traversal);
                Cull(player);
                Present(player, distance);
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
                Function.Call(Hash.SET_PED_CAN_RAGDOLL, ped, false);
                Function.Call(Hash.SET_PED_SEEING_RANGE, ped, 400f);

                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.AlwaysFight, true);
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.CanUseCover, false);
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.CanFightArmedPedsWhenNotArmed, true);
                Function.Call(Hash.SET_PED_COMBAT_ABILITY, ped, 2);
                Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, ped, 0, false);
                Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_HASH, ped, _group);

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

        // ------------------------------------------------------------------ movement

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

        private void Stalk(Ped player, float distance, Traversal traversal)
        {
            if (traversal != Traversal.Ground)
            {
                Pursue(player, traversal);
                return;
            }

            float speed = SpeedFor();

            try
            {
                Function.Call(Hash.SET_PED_MOVE_RATE_OVERRIDE, _ped, speed);
                _fx.MotionBlur(_ped, speed > 1.4f);
            }
            catch (Exception) { }

            if (Game.GameTime < _nextTaskAt) { return; }
            _nextTaskAt = Game.GameTime + _config.GetInt("features.hunter.retaskMs", 1500);

            try
            {
                if (distance <= _config.GetFloat("features.hunter.meleeRange", 8f))
                {
                    Function.Call(Hash.TASK_COMBAT_PED, _ped, player, 0, 16);
                }
                else
                {
                    // Run at them. Not a combat task at range: he would stop and posture, and
                    // whatever else he is, he is not somebody who takes cover.
                    Function.Call(Hash.TASK_GO_TO_ENTITY, _ped, player, -1, 2f, 12f, 1073741824f, 0);
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

            try
            {
                Function.Call(Hash.SET_ENTITY_COLLISION, _ped, false, false);
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

        private void Maul(Ped player, Traversal traversal)
        {
            if (Game.GameTime < _nextStrikeAt) { return; }
            _nextStrikeAt = Game.GameTime + _config.GetInt("features.hunter.strikeIntervalMs", 1500);

            try
            {
                Vehicle ride = player.CurrentVehicle;

                if (ride != null && ride.Exists() && traversal != Traversal.Ground)
                {
                    ride.EngineHealth = Math.Max(-1f, ride.EngineHealth - _config.GetFloat("features.hunter.vehicleDamage", 260f));
                    ride.HealthFloat = Math.Max(1f, ride.HealthFloat - _config.GetFloat("features.hunter.vehicleDamage", 260f));
                    _fx.Shake("SMALL_EXPLOSION_SHAKE", 0.7f);
                    _fx.Burst(_ped.Position, _config.GetString("features.hunter.fx.blink", "core/exp_grd_bzgas_smoke"), 1.2f);
                    return;
                }

                Function.Call(Hash.APPLY_DAMAGE_TO_PED, player,
                    _config.GetInt("features.hunter.strikeDamage", 45), true, 0);
                _fx.Shake("SMALL_EXPLOSION_SHAKE", 0.5f);
            }
            catch (Exception ex)
            {
                Log.Error("Hunter strike failed", ex);
            }
        }

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

            return fallback;
        }

        // ------------------------------------------------------------------ abilities

        private void MaybeUseAbility(Ped player, float distance, Traversal traversal)
        {
            if (_state != HunterState.Stalking) { return; }
            if (traversal != Traversal.Ground) { return; }
            if (Game.GameTime < _nextAbilityAt) { return; }

            float slamRange = _config.GetFloat("features.hunter.slamRange", 9f);
            float rushRange = _config.GetFloat("features.hunter.rushRange", 55f);

            if (distance <= slamRange)
            {
                Slam(player);
                return;
            }

            if (distance <= rushRange)
            {
                BeginRush(player);
                return;
            }

            BeginBlink(player);
        }

        private int Cooldown(string key, int fallback)
        {
            int baseline = _config.GetInt(key, fallback);
            // Everything comes faster the closer he is to the end of him.
            return Math.Max(400, (int)(baseline * (1f - 0.18f * (Phase - 1))));
        }

        /// <summary>
        /// The lunge. Collision off so walls, cars and fences are not part of the conversation,
        /// and a hard stop at the end that leaves him open — which is the point of it existing.
        /// </summary>
        private void BeginRush(Ped player)
        {
            _state = HunterState.Rushing;
            _stateUntil = Game.GameTime + _config.GetInt("features.hunter.rushMs", 1400);
            _rushTarget = player.Position;
            _nextAbilityAt = Game.GameTime + Cooldown("features.hunter.rushCooldownMs", 6500);

            try
            {
                Function.Call(Hash.CLEAR_PED_TASKS, _ped);
                Function.Call(Hash.SET_ENTITY_COLLISION, _ped, false, false);
            }
            catch (Exception) { }

            _fx.MotionBlur(_ped, true);
            _fx.Sound("HUD_FRONTEND_DEFAULT_SOUNDSET", "Menu_Accept");
        }

        private void AdvanceRush(Ped player)
        {
            float delta = Frame();
            Vector3 here = _ped.Position;

            // Re-aimed every frame, so running away extends the rush rather than dodging it.
            _rushTarget = player.Position;

            Vector3 direction = _rushTarget - here;
            float length = direction.Length();

            if (Game.GameTime > _stateUntil || length < 2.5f)
            {
                EndRush(length < 2.5f, player);
                return;
            }

            float speed = _config.GetFloat("features.hunter.rushSpeed", 42f) * (0.85f + 0.15f * Phase);
            Vector3 next = here + direction / length * Math.Min(length, speed * delta);

            // Keep his own height where the probe has nothing to say. Taking its zero as an
            // answer dropped him to sea level mid-rush, which under most of the city is
            // underground.
            float ground;
            next.Z = Ground.TryHeight(next, out ground) ? ground + 1f : here.Z;

            try
            {
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, _ped, next.X, next.Y, next.Z, false, false, false);
                Function.Call(Hash.SET_ENTITY_HEADING, _ped, Heading(direction));
            }
            catch (Exception) { }

            TrailAt(here);
        }

        private void EndRush(bool connected, Ped player)
        {
            try { Function.Call(Hash.SET_ENTITY_COLLISION, _ped, true, true); }
            catch (Exception) { }

            _fx.MotionBlur(_ped, false);

            if (connected)
            {
                Maul(player, Traversal.Ground);
                _fx.Shake("SMALL_EXPLOSION_SHAKE", 0.5f);
            }

            // Every big move has a price. This is where the fight is actually won.
            Stagger(_config.GetInt("features.hunter.rushRecoveryMs", 1600), "off balance");
        }

        /// <summary>
        /// Crossing a distance he has no business crossing. Deliberately not instant: the fade,
        /// the puff of nothing at both ends, and a beat between them are what make it read as a
        /// power rather than as the game losing track of him.
        /// </summary>
        private void BeginBlink(Ped player)
        {
            _state = HunterState.Blinking;
            _stateUntil = Game.GameTime + _config.GetInt("features.hunter.blinkMs", 700);
            _nextAbilityAt = Game.GameTime + Cooldown("features.hunter.blinkCooldownMs", 9000);

            try
            {
                Function.Call(Hash.CLEAR_PED_TASKS, _ped);
                Function.Call(Hash.SET_ENTITY_ALPHA, _ped, 60, false);
                Function.Call(Hash.SET_ENTITY_COLLISION, _ped, false, false);
            }
            catch (Exception) { }

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

            try
            {
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, _ped, arrival.X, arrival.Y, arrival.Z + 1f, false, false, false);
                Function.Call(Hash.SET_ENTITY_COLLISION, _ped, true, true);
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
        /// The ground slam. Nothing about it is a damage source — the explosion is invisible and
        /// scaled to nothing — it exists to throw everything nearby around and to buy the long
        /// recovery afterwards, which is the widest opening in the fight.
        /// </summary>
        private void Slam(Ped player)
        {
            _nextAbilityAt = Game.GameTime + Cooldown("features.hunter.slamCooldownMs", 8000);

            Vector3 at = _ped.Position;

            _fx.Shockwave(at, 1.4f);
            _fx.Burst(at, _config.GetString("features.hunter.fx.slam", "core/exp_grd_bzgas_smoke"), 2f);
            _fx.Shake("LARGE_EXPLOSION_SHAKE", 0.55f);
            _fx.Lightning();

            try
            {
                float radius = _config.GetFloat("features.hunter.slamRadius", 12f);

                foreach (Ped nearby in World.GetNearbyPeds(_ped, radius))
                {
                    if (nearby == null || !nearby.Exists()) { continue; }
                    if (Owns(nearby)) { continue; }

                    Function.Call(Hash.SET_PED_TO_RAGDOLL, nearby, 2500, 2500, 0, true, true, false);
                }

                if (at.DistanceTo(player.Position) <= radius)
                {
                    Function.Call(Hash.APPLY_DAMAGE_TO_PED, player,
                        _config.GetInt("features.hunter.slamDamage", 30), true, 0);
                    Function.Call(Hash.SET_PED_TO_RAGDOLL, player, 1800, 1800, 0, true, true, false);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Hunter slam failed", ex);
            }

            Stagger(_config.GetInt("features.hunter.slamRecoveryMs", 3200), "spent");
        }

        // ------------------------------------------------------------------ stagger

        /// <summary>
        /// The window. Everything else in this class exists to open one of these and to make
        /// closing it feel like a loss.
        /// </summary>
        private void Stagger(int durationMs, string why)
        {
            if (durationMs <= 0) { return; }

            _state = HunterState.Staggered;
            _stateUntil = Game.GameTime + durationMs;
            _vulnerableUntil = _stateUntil;

            try
            {
                Function.Call(Hash.SET_ENTITY_COLLISION, _ped, true, true);
                Function.Call(Hash.RESET_ENTITY_ALPHA, _ped);
                Function.Call(Hash.CLEAR_PED_TASKS, _ped);
                Function.Call(Hash.SET_PED_MOVE_RATE_OVERRIDE, _ped, 0.4f);
                Function.Call(Hash.SET_PED_CAN_RAGDOLL, _ped, true);
                Function.Call(Hash.SET_PED_TO_RAGDOLL, _ped, durationMs, durationMs, 0, true, true, false);
            }
            catch (Exception) { }

            _fx.MotionBlur(_ped, false);
            Log.Debug("Hunter staggered (" + why + ") for " + durationMs + "ms at " + (int)Resolve + " resolve.");
        }

        private void AdvanceStagger()
        {
            if (Game.GameTime < _stateUntil) { return; }

            try
            {
                Function.Call(Hash.SET_PED_CAN_RAGDOLL, _ped, false);
                Function.Call(Hash.SET_PED_MOVE_RATE_OVERRIDE, _ped, SpeedFor());
            }
            catch (Exception) { }

            _state = HunterState.Stalking;
            _nextTaskAt = 0;
        }

        private void Idle()
        {
            try
            {
                Function.Call(Hash.CLEAR_PED_TASKS, _ped);
                Function.Call(Hash.SET_ENTITY_COLLISION, _ped, true, true);
            }
            catch (Exception) { }

            _state = HunterState.Stalking;
        }

        // ------------------------------------------------------------------ the rest

        /// <summary>
        /// Anybody standing near him stops standing near him. Rate-limited hard, because the
        /// point is to leave a trail behind him rather than to depopulate the district.
        /// </summary>
        private void Cull(Ped player)
        {
            if (!_config.GetBool("features.hunter.cull", true)) { return; }
            if (Game.GameTime < _nextCullAt) { return; }
            _nextCullAt = Game.GameTime + _config.GetInt("features.hunter.cullIntervalMs", 2500);

            try
            {
                float radius = _config.GetFloat("features.hunter.cullRadius", 5f);

                foreach (Ped nearby in World.GetNearbyPeds(_ped, radius))
                {
                    if (nearby == null || !nearby.Exists() || nearby.IsDead) { continue; }
                    if (Owns(nearby) || nearby.Handle == player.Handle) { continue; }

                    // The same line every other part of this mod holds: a story ped killed here
                    // breaks a quest in a way nobody would ever attribute back to a riot mod.
                    if (_config.GetBool("compatibility.protectMissionPeds", true) &&
                        Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, nearby))
                    {
                        continue;
                    }

                    Function.Call(Hash.SET_ENTITY_HEALTH, nearby, 0);
                    Function.Call(Hash.SET_PED_TO_RAGDOLL, nearby, 4000, 4000, 0, true, true, false);
                    _fx.Burst(nearby.Position, _config.GetString("features.hunter.fx.slam", "core/exp_grd_bzgas_smoke"), 0.4f);
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Hunter cull failed", ex);
            }
        }

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

            // Straight to a blink rather than a slow jog across the map.
            _nextAbilityAt = 0;
            if (_state == HunterState.Stalking) { BeginBlink(player); }
        }

        private void Present(Ped player, float distance)
        {
            float near = _config.GetFloat("features.hunter.nearDistance", 25f);

            if (distance < near) { _fx.StartScreenEffect(_config.GetString("features.hunter.fx.nearEffect", "RaceTurbo")); }
            else { _fx.StopScreenEffect(); }

            if (!_config.GetBool("features.hunter.showBar", true)) { return; }

            float fraction = ResolveMax <= 0f ? 0f : Resolve / ResolveMax;

            Hud.Banner("~r~" + Name.ToUpperInvariant() + "~s~   " + (int)(fraction * 100) + "%" +
                       (Vulnerable ? "   ~y~EXPOSED" : "") + "   ~c~" + (int)distance + "m",
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
