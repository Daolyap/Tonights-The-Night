using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using TonightsTheNight.Config;
using TonightsTheNight.Factions;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// Turns an ordinary ped into a member of a faction: relationship group, loadout, combat
    /// conditioning and an opening task.
    ///
    /// We are steering the vanilla ped brain here, not replacing it. Everything below is a
    /// nudge to AI that already exists, which is why the result will read as GTA behaviour
    /// rather than as a crowd simulation.
    /// </summary>
    public sealed class PedConditioner
    {
        private readonly ConfigStore _config;
        private readonly RelationshipMatrix _relationships;
        private readonly Random _random;

        /// <summary>Weapon-name lookups are cached because a miss costs a hash and a log line.</summary>
        private readonly Dictionary<string, uint> _weaponCache = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _reportedBadWeapons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public PedConditioner(ConfigStore config, RelationshipMatrix relationships, Random random)
        {
            _config = config;
            _relationships = relationships;
            _random = random;
        }

        public void Apply(Ped ped, Faction faction, Reaction reaction, Ped threat = null)
        {
            _relationships.ApplyToPed(ped, faction.GroupHash);

            if (reaction == Reaction.Fight)
            {
                ApplyCombatConditioning(ped);

                // A faction can override marksmanship: soldiers should shoot straight while
                // rioters flail, and one global number cannot express both.
                if (faction.Accuracy >= 0)
                {
                    Function.Call(Hash.SET_PED_ACCURACY, ped, faction.Accuracy);
                }
                GiveWeapons(ped, faction);
                ApplyDurability(ped, faction);
            }
            else if (reaction == Reaction.Flee || reaction == Reaction.Cower)
            {
                // Panickers want the opposite: every ambient event should reach them.
                ped.BlockPermanentEvents = false;
                // Fleeing peds need the *opposite* conditioning, or they stand and trade blows.
                Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, ped, 0, true);
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.AlwaysFight, false);
            }

            IssueTask(ped, faction, reaction, threat);
        }

        private void ApplyCombatConditioning(Ped ped)
        {
            bool alwaysFight = _config.GetBool("combat.alwaysFight", true);
            bool neverFlee = _config.GetBool("combat.neverFlee", true);

            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.AlwaysFight, alwaysFight);
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.CanUseCover, _config.GetBool("combat.canUseCover", true));
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.CanFightArmedPedsWhenNotArmed, _config.GetBool("combat.canFightArmedWhenUnarmed", true));
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.CanUseVehicles, _config.GetBool("combat.canUseVehicles", false));

            if (neverFlee)
            {
                // Second argument 0 with 'false' clears the flee flags entirely.
                Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, ped, 0, false);
            }

            if (_config.GetBool("combat.blockPanicEvents", true))
            {
                // Clearing the flee attributes is not enough on its own. Ambient events - a
                // gunshot, a scream, a car mounting the pavement - fire constantly during a
                // riot and each one can pull a ped out of the fight task and into a panic run.
                // Blocking them is the difference between a crowd that fights and a crowd that
                // scatters the moment the first shot goes off.
                ped.BlockPermanentEvents = true;
            }

            Function.Call(Hash.SET_PED_COMBAT_ABILITY, ped, _config.GetInt("combat.combatAbility", 1));
            Function.Call(Hash.SET_PED_ACCURACY, ped, _config.GetInt("combat.accuracy", 20));
            Function.Call(Hash.SET_PED_SEEING_RANGE, ped, _config.GetFloat("combat.seeingRange", 60f));
            Function.Call(Hash.SET_PED_HEARING_RANGE, ped, _config.GetFloat("combat.hearingRange", 60f));

            // Deliberately NOT SET_PED_AS_ENEMY. That native makes a ped hostile to the player
            // specifically, which overrode the relationship matrix and had rioters shooting a
            // player the mode declared neutral. Who hates whom is the matrix's job alone.
        }

        private void ApplyDurability(Ped ped, Faction faction)
        {
            try
            {
                if (faction.Armour > 0) { ped.Armor = faction.Armour; }
                if (faction.Health > 0)
                {
                    ped.MaxHealth = faction.Health;
                    ped.Health = faction.Health;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not set durability on a " + faction.Id + " ped", ex);
            }
        }

        private void GiveWeapons(Ped ped, Faction faction)
        {
            if (faction.Weapons.Count == 0) { return; }
            if (_random.NextDouble() > faction.ArmedChance) { return; }

            string name = faction.PickWeapon(_random);
            if (name == null) { return; }

            uint hash = ResolveWeapon(name);
            if (hash == 0) { return; }

            Function.Call(Hash.GIVE_WEAPON_TO_PED, ped, hash, faction.Ammo, false, true);
        }

        /// <summary>
        /// Accepts "Pistol", "WEAPON_PISTOL" or a raw hash. Unknown names are logged once and
        /// skipped rather than throwing, so one typo in a config cannot break a whole faction.
        /// </summary>
        private uint ResolveWeapon(string name)
        {
            uint cached;
            if (_weaponCache.TryGetValue(name, out cached)) { return cached; }

            uint hash = 0;

            WeaponHash parsed;
            if (Enum.TryParse(name, true, out parsed))
            {
                hash = (uint)parsed;
            }
            else
            {
                string native = name.StartsWith("WEAPON_", StringComparison.OrdinalIgnoreCase) ? name : "WEAPON_" + name;
                hash = (uint)Game.GenerateHash(native.ToUpperInvariant());

                if (!Function.Call<bool>(Hash.IS_WEAPON_VALID, hash))
                {
                    if (_reportedBadWeapons.Add(name))
                    {
                        Log.Warn("Unknown weapon '" + name + "' - skipped. Use a WeaponHash name such as 'Pistol' or 'WEAPON_PISTOL'.");
                    }
                    hash = 0;
                }
            }

            _weaponCache[name] = hash;
            return hash;
        }

        public void IssueTask(Ped ped, Faction faction, Reaction reaction, Ped threat = null)
        {
            try
            {
                switch (reaction)
                {
                    case Reaction.Fight:
                        // Registering hated targets first makes the ped pick a real enemy
                        // instead of standing still looking for one.
                        Function.Call(Hash.REGISTER_HATED_TARGETS_AROUND_PED, ped, _config.GetFloat("combat.seeingRange", 60f));
                        ped.Task.FightAgainstHatedTargets(_config.GetFloat("combat.seeingRange", 60f));
                        break;

                    case Reaction.Flee:
                        // Run from an actual rioter where we know of one. Falling back to the
                        // player is a poor second - it reads as "everyone hates you" rather
                        // than "everyone is scared of the riot".
                        Ped source = threat != null && threat.Exists() && threat.Handle != ped.Handle
                            ? threat
                            : Game.Player.Character;
                        ped.Task.ReactAndFlee(source);
                        break;

                    case Reaction.Cower:
                        Function.Call(Hash.TASK_COWER, ped, -1);
                        break;

                    case Reaction.Bystander:
                        Function.Call(Hash.TASK_STAND_STILL, ped, -1);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not task a " + faction.Id + " ped", ex);
            }
        }
    }
}
