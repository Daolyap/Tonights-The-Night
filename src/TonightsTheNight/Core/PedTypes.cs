namespace TonightsTheNight.Core
{
    /// <summary>
    /// GET_PED_TYPE return values. Only the ones we actually branch on are named.
    /// </summary>
    public static class PedTypes
    {
        public const int CivMale = 4;
        public const int CivFemale = 5;
        public const int Cop = 6;
        public const int Medic = 21;
        public const int Fireman = 22;
        public const int Criminal = 23;
        public const int Bum = 24;
        public const int Special = 26;
        public const int Mission = 27;
        public const int Swat = 28;
        public const int Animal = 29;
        public const int Army = 30;

        public static bool IsCivilian(int pedType)
        {
            return pedType == CivMale || pedType == CivFemale;
        }

        /// <summary>
        /// Peds that belong to another system rather than to the ambient crowd: police, SWAT,
        /// army and emergency services.
        ///
        /// These are protected by default because overhaul packs (LSPDFR-style police mods,
        /// wanted-system replacements, dispatch overhauls) own them. Hijacking a cop out from
        /// under one of those mods breaks it in ways that look like the other mod's fault, and
        /// leaves the player with no way to tell which mod misbehaved.
        /// </summary>
        public static bool IsProtectedService(int pedType)
        {
            return pedType == Cop || pedType == Swat || pedType == Army ||
                   pedType == Medic || pedType == Fireman;
        }
    }
}
