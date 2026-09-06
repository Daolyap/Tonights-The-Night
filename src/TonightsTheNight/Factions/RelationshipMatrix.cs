using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using TonightsTheNight.Util;

namespace TonightsTheNight.Factions
{
    /// <summary>
    /// Registers relationship groups and wires up who hates whom.
    ///
    /// The whole police design rests on this rather than on the wanted system: wanted levels are
    /// player-centric and fight us, while relationship groups let any faction be hostile to any
    /// other with the player as just another participant.
    /// </summary>
    public sealed class RelationshipMatrix
    {
        // Vanilla scale, confirmed against the native docs.
        public const int Companion = 0;
        public const int Respect = 1;
        public const int Like = 2;
        public const int Neutral = 3;
        public const int Dislike = 4;
        public const int Hate = 5;

        public const string PlayerGroupName = "TTN_PLAYER";

        private readonly List<int> _registered = new List<int>();
        private readonly Dictionary<string, int> _byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public int PlayerGroup { get; private set; }

        /// <summary>The player's own group, so they can be a target, an ally, or press.</summary>
        public int Register(string name)
        {
            int existing;
            if (_byName.TryGetValue(name, out existing)) { return existing; }

            var output = new OutputArgument();
            Function.Call(Hash.ADD_RELATIONSHIP_GROUP, name, output);
            int hash = output.GetResult<int>();

            // The native occasionally hands back 0; the name hash is what the game uses anyway.
            if (hash == 0)
            {
                hash = unchecked((int)Game.GenerateHash(name));
                Log.Warn("ADD_RELATIONSHIP_GROUP returned 0 for '" + name + "', falling back to the name hash.");
            }

            _registered.Add(hash);
            _byName[name] = hash;
            Log.Debug("Registered relationship group " + name + " (" + hash + ").");
            return hash;
        }

        public void RegisterPlayerGroup()
        {
            PlayerGroup = Register(PlayerGroupName);
        }

        /// <summary>
        /// Sets a relationship in one direction only. The natives are directional, so mutual
        /// hostility needs both calls — a very easy bug to write and a confusing one to debug.
        /// </summary>
        public void Set(int fromGroup, int toGroup, int relationship)
        {
            Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, relationship, fromGroup, toGroup);
        }

        public void SetMutual(int groupA, int groupB, int relationship)
        {
            Set(groupA, groupB, relationship);
            Set(groupB, groupA, relationship);
        }

        public void ApplyToPed(Ped ped, int group)
        {
            Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_HASH, ped, group);
        }

        /// <summary>
        /// Puts a ped back where the game expects it. Without this, converted peds keep our
        /// group after the riot stops and the world stays subtly wrong until a reload.
        /// </summary>
        public static void RestorePed(Ped ped, string vanillaGroup)
        {
            Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_HASH, ped, Game.GenerateHash(vanillaGroup));
        }

        public void Clear()
        {
            foreach (int hash in _registered)
            {
                try
                {
                    Function.Call(Hash.REMOVE_RELATIONSHIP_GROUP, hash);
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to remove relationship group " + hash, ex);
                }
            }

            Log.Debug("Removed " + _registered.Count + " relationship group(s).");
            _registered.Clear();
            _byName.Clear();
            PlayerGroup = 0;
        }
    }
}
