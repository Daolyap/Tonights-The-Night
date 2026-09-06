using GTA;
using TonightsTheNight.Factions;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// One ped we have taken over, plus everything needed to hand it back to the game exactly
    /// as we found it. The restore data is the important half.
    /// </summary>
    public sealed class TrackedPed
    {
        public Ped Ped;
        public Faction Faction;
        public Reaction Reaction;
        public Blip Blip;

        /// <summary>The relationship group the ped had before we touched it.</summary>
        public int OriginalGroup;

        /// <summary>
        /// The model this ped had when we recruited it.
        ///
        /// Entity handles are recycled: once a rioter dies and despawns, its handle can be
        /// handed to something else entirely — which is how a seagull ends up wearing a red
        /// riot blip. Comparing the model catches that.
        /// </summary>
        public int OriginalModel;

        /// <summary>True when we created this ped, as opposed to converting an ambient one.</summary>
        public bool Spawned;

        /// <summary>Game time of the last retask, so we do not re-issue tasks every tick.</summary>
        public int LastTaskedAt;

        /// <summary>
        /// True while this ped is in a car chase. The Director leaves them alone: re-issuing a
        /// fight task over the top of a pursuit is exactly how a chase ends thirty feet down
        /// the road with everyone getting out to punch a lamppost.
        /// </summary>
        public bool InPursuit;

        /// <summary>Seat they were assigned in a chase. -1 is the driver.</summary>
        public int PursuitSeat;

        /// <summary>Prop this ped is carrying while looting, if any.</summary>
        public Prop Loot;

        public bool IsUsable
        {
            get { return Ped != null && Ped.Exists() && !Ped.IsDead; }
        }
    }
}
