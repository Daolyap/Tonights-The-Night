using System;
using System.Collections.Generic;
using TonightsTheNight.Util;

namespace TonightsTheNight.Factions
{
    /// <summary>
    /// One entry in a loadout. Weights matter more than they look: a riot fought with bats and
    /// crowbars sustains itself, while one fought with pistols kills off the local population
    /// faster than the game can replace it and fizzles out into an empty street.
    /// </summary>
    public struct WeaponChoice
    {
        public string Name;
        public float Weight;
    }

    /// <summary>
    /// A weighted pool of weapon names.
    ///
    /// Shared by factions and by the weapon presets, because "what is this crowd carrying" is
    /// the same question whether a mode author answers it or the player picks a preset from the
    /// menu. Names only - never hashes - so an add-on weapon pack works and its absence is a
    /// log line rather than a crash.
    /// </summary>
    public sealed class WeaponTable
    {
        private readonly List<WeaponChoice> _choices = new List<WeaponChoice>();
        private float _total;

        public int Count { get { return _choices.Count; } }
        public IReadOnlyList<WeaponChoice> Choices { get { return _choices; } }

        public WeaponTable Add(string name, float weight)
        {
            if (string.IsNullOrEmpty(name)) { return this; }

            _choices.Add(new WeaponChoice { Name = name, Weight = Math.Max(0.01f, weight) });
            _total = 0f;
            return this;
        }

        /// <summary>
        /// Accepts either a bare name or {"name": ..., "weight": ...}, so a mode author can
        /// write a plain list until the moment they care about proportions.
        /// </summary>
        public static WeaponTable FromJson(JsonValue node)
        {
            var table = new WeaponTable();

            foreach (JsonValue entry in node.Items)
            {
                if (entry.IsObject)
                {
                    string name = entry["name"].AsString(null);
                    if (name == null)
                    {
                        Log.Warn("Weapon entry has no 'name'. Skipped.");
                        continue;
                    }
                    table.Add(name, entry["weight"].AsFloat(1f));
                }
                else
                {
                    table.Add(entry.AsString(null), 1f);
                }
            }

            return table;
        }

        /// <summary>Weighted pick, so a loadout can be mostly melee with the odd firearm.</summary>
        public string Pick(Random random)
        {
            if (_choices.Count == 0) { return null; }

            if (_total <= 0f)
            {
                foreach (WeaponChoice choice in _choices) { _total += choice.Weight; }
            }

            double roll = random.NextDouble() * _total;
            foreach (WeaponChoice choice in _choices)
            {
                roll -= choice.Weight;
                if (roll <= 0) { return choice.Name; }
            }

            return _choices[_choices.Count - 1].Name;
        }
    }
}
