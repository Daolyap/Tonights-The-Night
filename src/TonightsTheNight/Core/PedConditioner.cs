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
        private readonly VehicleTasking _vehicles;

        /// <summary>Weapon-name lookups are cached because a miss costs a hash and a log line.</summary>
        private readonly Dictionary<string, uint> _weaponCache = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _reportedBadWeapons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _reportedEmptyHanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public PedConditioner(ConfigStore config, RelationshipMatrix relationships, Random random)
        {
            _config = config;
            _relationships = relationships;
            _random = random;
            _vehicles = new VehicleTasking(config, random);
        }

        public void Apply(Ped ped, Faction faction, Reaction reaction, Ped threat = null,
                          TrackedPed entry = null, Ped hostile = null)
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
                // Resolved per ped rather than cached, because the preset can be changed from
                // the menu mid-riot and the next person recruited should reflect that.
                WeaponPreset preset = faction.TakesWeaponPreset ? WeaponPresets.Active(_config) : null;

                GiveWeapons(ped, faction, preset);
                ApplyDurability(ped, faction, preset);
            }
            else if (reaction == Reaction.Flee || reaction == Reaction.Cower)
            {
                // Panickers want the opposite: every ambient event should reach them.
                ped.BlockPermanentEvents = false;
                // Fleeing peds need the *opposite* conditioning, or they stand and trade blows.
                Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, ped, 0, true);
                Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, CombatAttribute.AlwaysFight, false);
            }

            IssueTask(ped, faction, reaction, threat, entry, hostile);
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

        private void ApplyDurability(Ped ped, Faction faction, WeaponPreset preset)
        {
            try
            {
                // A preset's armour is part of the preset: "armed and armoured" is not the
                // realistic riot with better guns, it is a different event.
                int armour = preset != null ? preset.Armour : faction.Armour;
                if (armour > 0) { ped.Armor = armour; }
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

        /// <summary>
        /// A selected preset replaces the faction's own loadout, but only for the factions drawn
        /// from the ambient crowd. Picking "military" arms the mob with carbines; it does not
        /// re-equip the actual army, which already has the kit its mode author gave it.
        /// </summary>
        private void GiveWeapons(Ped ped, Faction faction, WeaponPreset preset)
        {
            WeaponTable table = preset != null ? preset.Weapons : faction.Weapons;
            float armedChance = preset != null ? preset.ArmedChance : faction.ArmedChance;
            int ammo = preset != null ? preset.Ammo : faction.Ammo;

            if (table.Count == 0) { return; }
            if (_random.NextDouble() > armedChance) { return; }

            // Try the pick, then the rest of the table, then a weapon every install has.
            // A ped that ends up empty-handed is invisible as a bug - it just stands there
            // looking like the mod does nothing - so it is worth several attempts and a log
            // line naming the weapon that would not take.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (TryArm(ped, table.Pick(_random), ammo)) { return; }
            }

            if (TryArm(ped, "WEAPON_PISTOL", ammo)) { return; }

            if (_reportedEmptyHanded.Add(faction.Id))
            {
                Log.Warn("Faction '" + faction.Id + "' could not be armed from its own loadout " +
                         "or the fallback. Its members will fight unarmed.");
            }
        }

        /// <summary>
        /// Gives a weapon and confirms the ped actually has it.
        ///
        /// GIVE_WEAPON_TO_PED reports nothing, so a name that resolves to a valid hash the ped
        /// cannot hold left the ped empty-handed silently. Checking afterwards is what turns
        /// "most of them have no weapon" into a line in the log naming the weapon.
        /// </summary>
        private bool TryArm(Ped ped, string name, int ammo)
        {
            if (name == null) { return false; }

            uint hash = ResolveWeapon(name);
            if (hash == 0) { return false; }

            try
            {
                Function.Call(Hash.GIVE_WEAPON_TO_PED, ped, hash, ammo, false, true);

                if (!Function.Call<bool>(Hash.HAS_PED_GOT_WEAPON, ped, hash, false))
                {
                    if (_reportedBadWeapons.Add(name))
                    {
                        Log.Warn("'" + name + "' resolved but would not attach to a ped - skipping it.");
                    }
                    return false;
                }

                // Put it in their hands rather than leaving it holstered, or a faction that is
                // waiting for a target reads as unarmed until the moment it finds one.
                Function.Call(Hash.SET_CURRENT_PED_WEAPON, ped, hash, true);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Could not arm a ped with '" + name + "'", ex);
                return false;
            }
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

        /// <summary>
        /// Tasks one ped. <paramref name="entry"/> is optional and only needed for peds in
        /// vehicles, whose decision has to be remembered rather than re-rolled.
        /// </summary>
        public void IssueTask(Ped ped, Faction faction, Reaction reaction, Ped threat = null,
                              TrackedPed entry = null, Ped hostile = null)
        {
            try
            {
                if (TaskAsDriver(ped, reaction, entry, hostile)) { return; }

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

        /// <summary>
        /// A ped already at a wheel when the riot reaches them. Returns true when they have been
        /// given a driving task, so the caller skips the on-foot one.
        ///
        /// Passengers get handled here too: the driver is the one who decides where the car goes,
        /// so everyone else in it is tasked off the back of that decision.
        /// </summary>
        private bool TaskAsDriver(Ped ped, Reaction reaction, TrackedPed entry, Ped hostile)
        {
            if (entry == null || !_vehicles.Enabled) { return false; }
            if (reaction != Reaction.Fight && reaction != Reaction.Flee) { return false; }

            Vehicle vehicle = ped.CurrentVehicle;
            if (vehicle == null || !vehicle.Exists()) { return false; }

            // A wreck is not a vehicle. Whoever is still sitting in one gets their permission to
            // get out back, then carries on as an ordinary rioter.
            if (!Function.Call<bool>(Hash.IS_VEHICLE_DRIVEABLE, vehicle, false))
            {
                VehicleTasking.Release(ped);
                entry.VehicleRole = VehicleRole.None;
                return false;
            }

            // Passengers are tasked by whoever is driving, not on their own account.
            if (vehicle.Driver == null || vehicle.Driver.Handle != ped.Handle) { return false; }

            if (entry.VehicleRole == VehicleRole.None)
            {
                entry.VehicleRole = _vehicles.Roll(reaction);
            }

            if (!_vehicles.Apply(ped, vehicle, entry.VehicleRole, hostile)) { return false; }

            Ped target = entry.VehicleRole == VehicleRole.HuntEnemy && hostile != null && hostile.Exists()
                ? hostile
                : Game.Player.Character;

            foreach (Ped occupant in vehicle.Occupants)
            {
                if (occupant == null || !occupant.Exists() || occupant.Handle == ped.Handle) { continue; }
                if (occupant.Handle == Game.Player.Character.Handle) { continue; }

                _vehicles.TaskPassenger(occupant, target);
            }

            return true;
        }
    }
}
