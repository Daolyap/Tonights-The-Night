using GTA.Native;
using TonightsTheNight.Config;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// The single place allowed to touch GTA's wanted system.
    ///
    /// Riot police are built from relationship groups, not wanted levels, precisely so that
    /// police and wanted-system overhauls keep working untouched. That is a design decision,
    /// not an implementation detail: the wanted system is player-centric and fights us, and it
    /// is exactly what an overhaul pack replaces.
    ///
    /// Everything routes through here so the guarantee is enforced in one readable place
    /// rather than relying on nobody ever calling SET_MAX_WANTED_LEVEL in a later drop.
    /// </summary>
    public static class Compatibility
    {
        private static bool _refusalLogged;

        public static bool MayManageWantedSystem(ConfigStore config)
        {
            if (config.GetBool("compatibility.manageWantedSystem", false)) { return true; }

            if (!_refusalLogged)
            {
                Log.Info("Wanted system left alone (compatibility.manageWantedSystem is off). " +
                         "Any police or wanted overhaul you have installed keeps full control.");
                _refusalLogged = true;
            }
            return false;
        }

        public static void SetMaxWantedLevel(ConfigStore config, int level)
        {
            if (!MayManageWantedSystem(config)) { return; }
            Function.Call(Hash.SET_MAX_WANTED_LEVEL, level);
        }

        public static void SetPoliceIgnorePlayer(ConfigStore config, bool ignore)
        {
            if (!MayManageWantedSystem(config)) { return; }
            Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, GTA.Game.Player, ignore);
        }

        /// <summary>Resets the flag so a reload re-reports the current stance once.</summary>
        public static void Reset() { _refusalLogged = false; }
    }
}
