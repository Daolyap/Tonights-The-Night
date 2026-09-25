using TonightsTheNight.Config;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// Attribute IDs for SET_PED_COMBAT_ATTRIBUTES.
    ///
    /// These are community-documented rather than official, and the numbering is the single
    /// most likely thing in this mod to be quietly wrong. Since nobody developing it can run
    /// the game, every ID here is overridable from config (combat.attributeIds.*) so a wrong
    /// value can be found by experiment in one session instead of a rebuild per guess.
    /// Every applied attribute is logged with its ID at Debug level for the same reason.
    /// </summary>
    public static class CombatAttribute
    {
        public static int CanUseCover = 0;
        public static int CanUseVehicles = 1;
        public static int CanDoDrivebys = 2;
        public static int CanLeaveVehicle = 3;
        public static int CanFightArmedPedsWhenNotArmed = 5;
        public static int AlwaysFight = 46;

        public static void LoadOverrides(ConfigStore config)
        {
            CanUseCover = config.GetInt("combat.attributeIds.canUseCover", CanUseCover);
            CanUseVehicles = config.GetInt("combat.attributeIds.canUseVehicles", CanUseVehicles);
            CanDoDrivebys = config.GetInt("combat.attributeIds.canDoDrivebys", CanDoDrivebys);
            CanLeaveVehicle = config.GetInt("combat.attributeIds.canLeaveVehicle", CanLeaveVehicle);
            CanFightArmedPedsWhenNotArmed = config.GetInt("combat.attributeIds.canFightArmedWhenUnarmed", CanFightArmedPedsWhenNotArmed);
            AlwaysFight = config.GetInt("combat.attributeIds.alwaysFight", AlwaysFight);

            Log.Debug("Combat attribute IDs: alwaysFight=" + AlwaysFight +
                      " canUseCover=" + CanUseCover +
                      " canFightArmedWhenUnarmed=" + CanFightArmedPedsWhenNotArmed +
                      " canUseVehicles=" + CanUseVehicles +
                      " canDoDrivebys=" + CanDoDrivebys +
                      " canLeaveVehicle=" + CanLeaveVehicle);
        }
    }
}
