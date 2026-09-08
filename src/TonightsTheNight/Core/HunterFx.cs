using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using TonightsTheNight.Config;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// Everything the hunter looks and sounds like.
    ///
    /// Kept apart from the hunter's behaviour for one reason: none of it may ever matter. Every
    /// particle asset, screen effect and sound name here is community-documented rather than
    /// official, and an install that does not have one should get a hunter with less spectacle,
    /// never a hunter that throws. So this class fails silently by design, and the behaviour
    /// class never checks whether any of it worked.
    ///
    /// The names are all config, for the same reason they are in <c>Spectacle</c>: being wrong
    /// about one should cost an edit and a reload rather than a rebuild.
    /// </summary>
    public sealed class HunterFx
    {
        private readonly ConfigStore _config;

        private readonly List<int> _looped = new List<int>();
        private string _requestedAsset;
        private bool _screenEffectRunning;
        private bool _timeSlowed;
        private int _timeSlowUntil;

        public HunterFx(ConfigStore config)
        {
            _config = config;
        }

        public void Reset()
        {
            StopLooped();
            StopScreenEffect();
            RestoreTime();
            _requestedAsset = null;
        }

        // ---------------------------------------------------------------- particles

        /// <summary>
        /// A one-off burst at a point. Used for every teleport, every footfall of a rush and
        /// the ground slam.
        /// </summary>
        public void Burst(Vector3 at, string effectPath, float scale)
        {
            string asset = AssetFor(effectPath);
            string effect = EffectFor(effectPath);

            if (!EnsureAsset(asset)) { return; }

            try
            {
                Function.Call(Hash.USE_PARTICLE_FX_ASSET, asset);
                Function.Call(Hash.START_PARTICLE_FX_NON_LOOPED_AT_COORD, effect,
                    at.X, at.Y, at.Z, 0f, 0f, 0f, scale, false, false, false);
            }
            catch (Exception ex)
            {
                Log.Error("Could not play a hunter particle effect", ex);
            }
        }

        /// <summary>A trail that stays on him. Stopped explicitly, or it outlives the mode.</summary>
        public void Attach(Ped ped, string effectPath, float scale)
        {
            string asset = AssetFor(effectPath);
            string effect = EffectFor(effectPath);

            if (ped == null || !ped.Exists() || !EnsureAsset(asset)) { return; }

            try
            {
                Function.Call(Hash.USE_PARTICLE_FX_ASSET, asset);
                int handle = Function.Call<int>(Hash.START_PARTICLE_FX_LOOPED_ON_ENTITY, effect, ped,
                    0f, 0f, 0f, 0f, 0f, 0f, scale, false, false, false);

                if (handle != 0) { _looped.Add(handle); }
            }
            catch (Exception ex)
            {
                Log.Error("Could not attach a hunter particle effect", ex);
            }
        }

        public void StopLooped()
        {
            foreach (int handle in _looped)
            {
                try { Function.Call(Hash.STOP_PARTICLE_FX_LOOPED, handle, false); }
                catch (Exception) { }
            }
            _looped.Clear();
        }

        /// <summary>
        /// Streams a particle asset. Returns false until it has arrived, which is the whole
        /// reason nothing here is allowed to be load-bearing: the first blink of a session will
        /// usually happen before the asset is ready.
        /// </summary>
        private bool EnsureAsset(string asset)
        {
            if (string.IsNullOrEmpty(asset)) { return false; }

            try
            {
                if (Function.Call<bool>(Hash.HAS_NAMED_PTFX_ASSET_LOADED, asset)) { return true; }

                if (_requestedAsset != asset)
                {
                    _requestedAsset = asset;
                    Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, asset);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not request the particle asset '" + asset + "'", ex);
            }

            return false;
        }

        /// <summary>Effects are configured as "asset/effect", so one string names both halves.</summary>
        private static string AssetFor(string path)
        {
            int slash = path.IndexOf('/');
            return slash < 0 ? string.Empty : path.Substring(0, slash);
        }

        private static string EffectFor(string path)
        {
            int slash = path.IndexOf('/');
            return slash < 0 ? path : path.Substring(slash + 1);
        }

        // ---------------------------------------------------------------- screen

        public void StartScreenEffect(string name)
        {
            if (string.IsNullOrEmpty(name) || _screenEffectRunning) { return; }

            try
            {
                Function.Call(Hash.ANIMPOSTFX_PLAY, name, 0, true);
                _screenEffectRunning = true;
            }
            catch (Exception ex)
            {
                Log.Error("Could not start the hunter screen effect", ex);
            }
        }

        public void StopScreenEffect()
        {
            if (!_screenEffectRunning) { return; }

            try
            {
                Function.Call(Hash.ANIMPOSTFX_STOP, _config.GetString("features.hunter.fx.nearEffect", "RaceTurbo"));
            }
            catch (Exception ex)
            {
                Log.Error("Could not stop the hunter screen effect", ex);
            }

            _screenEffectRunning = false;
        }

        /// <summary>A single flash, for a phase change or a death.</summary>
        public void Flash(string name, int durationMs)
        {
            if (string.IsNullOrEmpty(name)) { return; }

            try { Function.Call(Hash.ANIMPOSTFX_PLAY, name, durationMs, false); }
            catch (Exception ex) { Log.Error("Could not flash a screen effect", ex); }
        }

        // ---------------------------------------------------------------- time

        /// <summary>
        /// The one effect that is worth the risk of touching a global: everything slowing down
        /// while something moves at full speed is the entire idea being sold here.
        ///
        /// Bounded and self-restoring. A time scale left at 0.3 by a crash would ruin a save
        /// session, so it is re-checked every tick and restored on every teardown path there is.
        /// </summary>
        public void SlowTime(float scale, int durationMs)
        {
            if (!_config.GetBool("features.hunter.timeSlow", true)) { return; }

            try
            {
                Function.Call(Hash.SET_TIME_SCALE, scale);
                _timeSlowed = true;
                _timeSlowUntil = Game.GameTime + durationMs;
            }
            catch (Exception ex)
            {
                Log.Error("Could not slow time", ex);
            }
        }

        /// <summary>Called every tick. The deadline is what makes the effect safe.</summary>
        public void UpdateTime()
        {
            if (!_timeSlowed) { return; }
            if (Game.GameTime < _timeSlowUntil) { return; }
            RestoreTime();
        }

        public void RestoreTime()
        {
            if (!_timeSlowed) { return; }

            try { Function.Call(Hash.SET_TIME_SCALE, 1f); }
            catch (Exception ex) { Log.Error("Could not restore the time scale", ex); }

            _timeSlowed = false;
        }

        // ---------------------------------------------------------------- world

        public void Lightning()
        {
            try { Function.Call(Hash.FORCE_LIGHTNING_FLASH); }
            catch (Exception) { }
        }

        public void Shake(string kind, float intensity)
        {
            try { Function.Call(Hash.SHAKE_GAMEPLAY_CAM, kind, intensity); }
            catch (Exception) { }
        }

        public void Sound(string set, string name)
        {
            if (string.IsNullOrEmpty(name)) { return; }

            try { Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, name, set, true); }
            catch (Exception) { }
        }

        /// <summary>
        /// A non-damaging explosion, used purely for the shove. The hunter's own damage is
        /// applied by hand so it cannot be turned into a stray kill on somebody's save.
        /// </summary>
        public void Shockwave(Vector3 at, float scale)
        {
            try
            {
                Function.Call(Hash.ADD_EXPLOSION, at.X, at.Y, at.Z,
                    _config.GetInt("features.hunter.fx.slamExplosion", 4), 0f, true, false, scale);
            }
            catch (Exception ex)
            {
                Log.Error("Could not raise a shockwave", ex);
            }
        }

        public void MotionBlur(Ped ped, bool on)
        {
            if (ped == null || !ped.Exists()) { return; }

            try { Function.Call(Hash.SET_ENTITY_MOTION_BLUR, ped, on); }
            catch (Exception) { }
        }
    }
}
