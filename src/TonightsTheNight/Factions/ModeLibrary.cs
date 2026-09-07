using System;
using System.Collections.Generic;
using System.IO;
using TonightsTheNight.Util;

namespace TonightsTheNight.Factions
{
    /// <summary>
    /// Loads riot modes from scripts/TonightsTheNight/modes/*.json, writing the stock ones to
    /// disk first so they are visible, editable, and copyable as a template.
    /// </summary>
    public sealed class ModeLibrary
    {
        public List<RiotMode> Modes { get; private set; }

        public ModeLibrary()
        {
            Modes = new List<RiotMode>();
        }

        public void Load()
        {
            Modes.Clear();
            WriteStockModes();

            string[] files;
            try
            {
                files = Directory.Exists(Paths.ModesDir)
                    ? Directory.GetFiles(Paths.ModesDir, "*.json")
                    : new string[0];
            }
            catch (Exception ex)
            {
                Log.Error("Could not list modes directory", ex);
                return;
            }

            // Read in a stable order; the menu order is decided after parsing, from each
            // mode's own "order" field.
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                string id = Path.GetFileNameWithoutExtension(file);
                try
                {
                    JsonValue parsed;
                    string error;
                    if (!JsonValue.TryParse(File.ReadAllText(file), out parsed, out error))
                    {
                        Log.Error("Mode '" + id + "' failed to parse (" + error + "). Skipped.");
                        continue;
                    }

                    if (parsed["enabled"].AsBool(true) == false)
                    {
                        Log.Info("Mode '" + id + "' is disabled in its file. Skipped.");
                        continue;
                    }

                    Modes.Add(RiotMode.FromJson(id, parsed));
                }
                catch (Exception ex)
                {
                    Log.Error("Could not load mode '" + id + "'", ex);
                }
            }

            Modes.Sort(delegate (RiotMode a, RiotMode b)
            {
                int byOrder = a.Order.CompareTo(b.Order);
                return byOrder != 0 ? byOrder : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            Log.Info("Loaded " + Modes.Count + " mode(s): " + string.Join(", ", Names()));
        }

        public string[] Names()
        {
            var names = new string[Modes.Count];
            for (int i = 0; i < Modes.Count; i++) { names[i] = Modes[i].Name; }
            return names;
        }

        /// <summary>
        /// Stock modes are rewritten when their shipped content changes, so updates deliver
        /// balance fixes. A mode the user renamed or added is never touched.
        /// </summary>
        private static void WriteStockModes()
        {
            foreach (var pair in StockModes.All())
            {
                string file = Path.Combine(Paths.ModesDir, pair.Key + ".json");
                try
                {
                    string expected = pair.Value;
                    if (File.Exists(file) && File.ReadAllText(file) == expected) { continue; }

                    // Never clobber a file the user has edited: only write if it is ours.
                    if (File.Exists(file) && !LooksUnedited(file))
                    {
                        Log.Info("Mode '" + pair.Key + "' looks edited; leaving it alone.");
                        continue;
                    }

                    File.WriteAllText(file, expected);
                    Log.Info("Wrote stock mode " + pair.Key + ".json");
                }
                catch (Exception ex)
                {
                    Log.Error("Could not write stock mode '" + pair.Key + "'", ex);
                }
            }
        }

        /// <summary>
        /// A stock file carries a marker line. If it is gone, the user has taken ownership and
        /// we stop overwriting — losing someone's tuning to an update is unforgivable.
        /// </summary>
        private static bool LooksUnedited(string file)
        {
            try
            {
                return File.ReadAllText(file).Contains(StockModes.Marker);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
