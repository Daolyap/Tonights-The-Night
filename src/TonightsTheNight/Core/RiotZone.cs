using GTA;
using GTA.Math;
using TonightsTheNight.Config;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// Where the riot is.
    ///
    /// Confining it does double duty: it is the cheapest performance control there is, and it
    /// produces the best moments — one district under martial law while the rest of the map
    /// carries on normally is far more interesting than uniform chaos everywhere.
    /// </summary>
    public sealed class RiotZone
    {
        private readonly ConfigStore _config;
        private Vector3 _anchor;
        private Blip _blip;

        public RiotZone(ConfigStore config)
        {
            _config = config;
        }

        public bool Citywide
        {
            get { return string.Equals(_config.GetString("zone.mode", "radius"), "citywide", System.StringComparison.OrdinalIgnoreCase); }
        }

        public float Radius { get { return _config.GetFloat("zone.radius", 300f); } }

        public bool FollowsPlayer { get { return _config.GetBool("zone.followPlayer", true); } }

        public Vector3 Centre
        {
            get { return FollowsPlayer ? Game.Player.Character.Position : _anchor; }
        }

        public void Begin()
        {
            _anchor = Game.Player.Character.Position;
            RefreshBlip();
        }

        public bool Contains(Vector3 point)
        {
            if (Citywide) { return true; }
            return Centre.DistanceToSquared(point) <= Radius * Radius;
        }

        /// <summary>The zone marker follows the same rules as the zone, or there is no marker.</summary>
        public void RefreshBlip()
        {
            bool wanted = !Citywide && _config.GetBool("zone.showOnMap", true);

            if (!wanted)
            {
                Clear();
                return;
            }

            try
            {
                if (_blip == null || !_blip.Exists())
                {
                    _blip = World.CreateBlip(Centre, Radius);
                    _blip.Color = BlipColor.Red;
                    _blip.Alpha = 80;
                    _blip.Name = "Riot Zone";
                }
                else if (FollowsPlayer)
                {
                    _blip.Position = Centre;
                }
            }
            catch (System.Exception ex)
            {
                Log.Error("Could not update the riot zone blip", ex);
            }
        }

        public void Clear()
        {
            try
            {
                if (_blip != null && _blip.Exists()) { _blip.Delete(); }
            }
            catch (System.Exception)
            {
                // A blip that is already gone needs no eulogy.
            }
            _blip = null;
        }
    }
}
