using System.Drawing;
using System.Text;
using GTA.UI;
using TonightsTheNight.Config;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// On-screen counters. Defaults to off, but its logging half is always on: the developer
    /// needs the numbers regardless, the player should not have to look at them to supply one.
    /// </summary>
    public sealed class DebugOverlay
    {
        private readonly ConfigStore _config;

        public DebugOverlay(ConfigStore config)
        {
            _config = config;
        }

        public bool Enabled
        {
            get { return _config.GetBool("features.debugOverlay.enabled", false); }
        }

        public void Draw(Director director, int modeCount, double menuDrawMs = 0)
        {
            if (!Enabled) { return; }

            var sb = new StringBuilder();
            sb.AppendLine("~y~TONIGHT'S THE NIGHT~s~ v" + DefaultConfig.Version);

            if (_config.GetBool("features.debugOverlay.showCounts", true))
            {
                sb.AppendLine("mode: " + (director.IsRunning ? director.ActiveMode.Name : "~c~idle~s~") + "  (" + modeCount + " loaded)");
                if (director.IsRunning)
                {
                    sb.AppendLine("phase: " + director.Escalation.Current + " " + director.Escalation.CurrentName +
                                  "   kills: " + director.Kills + "   fires: " + director.Ambience.ActiveFires +
                                  (director.Purge.Active ? "   ~r~PURGE " + director.Purge.Remaining + "~s~" : ""));
                }
                sb.AppendLine("active: " + director.TrackedCount + "   recruited: " + director.RecruitedTotal +
                              "   lost: " + director.LostTotal + "   culled: " + director.CulledTotal);
                if (director.IsRunning)
                {
                    sb.AppendLine("chases: " + director.Pursuit.ActiveChases + " (" + director.Pursuit.Started +
                                  " total)   looting: " + director.Looting.Active + " (" + director.Looting.Total + " total)");
                    sb.AppendLine("engaging you: " + director.Attention.Engaged + "   sent elsewhere: " + director.Attention.Trimmed +
                                  "   craft: " + director.Craft.Active +
                                  (director.Manhunt.Active ? "   wave: " + director.Manhunt.Wave : ""));

                    if (director.Contagion.Active)
                    {
                        // The two halves separately, because they answer different questions:
                        // how well the visible spread is going, and how far gone the city is
                        // underneath it.
                        sb.AppendLine("infected: " + director.Contagion.Alive + " alive   " +
                                      director.Contagion.Converted + " by contact   " +
                                      director.Contagion.Seeded + " already had it   " +
                                      director.Contagion.Suppressed + " put down");
                        sb.AppendLine("outbreak: " + (int)(director.Contagion.Prevalence * 100) + "%   front " +
                                      (int)director.Contagion.Front + "m   here " +
                                      (int)(director.Contagion.SeedChance(GTA.Game.Player.Character.Position) * 100) + "%");
                    }

                    if (director.Hunter.Active)
                    {
                        sb.AppendLine("hunter: phase " + director.Hunter.Phase + "   resolve " +
                                      (int)director.Hunter.Resolve + "/" + (int)director.Hunter.ResolveMax +
                                      (director.Hunter.Vulnerable ? "   ~y~EXPOSED~s~" : ""));
                    }
                }
            }

            if (_config.GetBool("features.debugOverlay.showPerformance", true))
            {
                sb.AppendLine("tick: " + director.LastTickMs.ToString("F2") + "ms   peak: " + director.PeakTickMs.ToString("F2") +
                              "ms   budget: " + director.CurrentBudget + "   menu: " + menuDrawMs.ToString("F2") + "ms");
            }

            if (_config.GetBool("features.debugOverlay.showFactions", true) && director.IsRunning)
            {
                foreach (var faction in director.ActiveMode.Factions)
                {
                    sb.AppendLine("  " + faction.DisplayName + "  [" + faction.Reaction + "]");
                }
            }

            float x = _config.GetFloat("features.debugOverlay.x", 0.015f);
            float y = _config.GetFloat("features.debugOverlay.y", 0.28f);
            float scale = _config.GetFloat("features.debugOverlay.scale", 0.32f);

            var text = new TextElement(sb.ToString(), new PointF(x * Screen.Width, y * Screen.Height), scale, Color.White)
            {
                Font = GTA.UI.Font.ChaletLondon,
                Outline = true
            };
            text.Draw();
        }
    }
}
