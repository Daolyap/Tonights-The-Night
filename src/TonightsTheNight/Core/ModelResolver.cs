using System;
using System.Collections.Generic;
using GTA;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// Turns a model name from config into something the game can spawn, or nothing at all.
    ///
    /// Names, never hardcoded hashes. That is what lets an add-on vehicle pack or an MP-in-SP
    /// unlocker "just work" for players who have one, while the shipped config references only
    /// vanilla models so the mod stays shareable rather than built for one machine. A name that
    /// cannot be resolved falls back to a declared vanilla substitute and is logged once —
    /// never an exception, never a crash mid-riot.
    /// </summary>
    public sealed class ModelResolver
    {
        private readonly Dictionary<string, Model> _resolved = new Dictionary<string, Model>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public int Failures { get; private set; }

        /// <summary>
        /// Picks the first name in <paramref name="candidates"/> the game actually has. Returns
        /// false when none of them exist, which is a survivable outcome everywhere it is used.
        /// </summary>
        public bool TryResolve(IEnumerable<string> candidates, out Model model)
        {
            foreach (string name in candidates)
            {
                if (string.IsNullOrEmpty(name)) { continue; }

                Model cached;
                if (_resolved.TryGetValue(name, out cached))
                {
                    if (cached.IsValid) { model = cached; return true; }
                    continue;
                }

                var candidate = new Model(name);
                bool usable = candidate.IsValid && candidate.IsInCdImage;

                _resolved[name] = usable ? candidate : default(Model);

                if (usable)
                {
                    model = candidate;
                    return true;
                }

                if (_reported.Add(name))
                {
                    Failures++;
                    Log.Warn("Model '" + name + "' is not installed - falling back. " +
                             "(Expected if the config references an add-on pack you do not have.)");
                }
            }

            model = default(Model);
            return false;
        }

        /// <summary>
        /// Requests the model and waits briefly. Streaming is asynchronous, so spawning without
        /// this produces an invisible or missing entity rather than an error.
        /// </summary>
        public bool Load(Model model, int timeoutMs = 1000)
        {
            try
            {
                if (model.IsLoaded) { return true; }
                model.Request(timeoutMs);
                return model.IsLoaded;
            }
            catch (Exception ex)
            {
                Log.Error("Could not stream a model", ex);
                return false;
            }
        }

        public void Release()
        {
            foreach (var pair in _resolved)
            {
                try
                {
                    if (pair.Value.IsValid) { pair.Value.MarkAsNoLongerNeeded(); }
                }
                catch (Exception)
                {
                    // Releasing a model that was never loaded is not worth reporting.
                }
            }
        }
    }
}
