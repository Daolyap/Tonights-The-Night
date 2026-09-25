using System;
using System.Drawing;
using GTA.UI;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>
    /// The small amount of screen this mod is allowed to own.
    ///
    /// Three things genuinely need to be visible rather than logged: how long is left of a
    /// purge, which wave of a chase you are on, and how much is left of something that is
    /// hunting you. Each of those answers a question the player would otherwise ask out loud —
    /// "is this ever going to end" being the loudest of them, and the reason the purge now has
    /// a clock on screen instead of only in the log file.
    ///
    /// Deliberately drawn rather than posted as notifications: a countdown that arrives as a
    /// feed message is both too slow and impossible to glance at.
    /// </summary>
    public static class Hud
    {
        /// <summary>Centred text, high on the screen, out of the way of the reticle.</summary>
        public static void Banner(string text, float y, float scale, Color colour)
        {
            Draw(text, 0.5f, y, scale, colour, Alignment.Center);
        }

        public static void Draw(string text, float x, float y, float scale, Color colour, Alignment alignment)
        {
            if (string.IsNullOrEmpty(text)) { return; }

            try
            {
                var element = new TextElement(text, new PointF(x * Screen.Width, y * Screen.Height), scale, colour, GTA.UI.Font.ChaletComprimeCologne, alignment)
                {
                    Outline = true
                };
                element.Draw();
            }
            catch (Exception ex)
            {
                Log.Error("Could not draw a HUD element", ex);
            }
        }

        /// <summary>
        /// A horizontal meter. Used for the hunter, whose whole difficulty rests on the player
        /// being able to see that they are getting somewhere.
        /// </summary>
        public static void Bar(float x, float y, float width, float height, float fraction, Color fill, Color backing)
        {
            try
            {
                if (fraction < 0f) { fraction = 0f; }
                if (fraction > 1f) { fraction = 1f; }

                float left = x * Screen.Width;
                float top = y * Screen.Height;
                float full = width * Screen.Width;
                float tall = height * Screen.Height;

                new ContainerElement(new PointF(left, top), new SizeF(full, tall), backing).Draw();

                if (fraction > 0f)
                {
                    new ContainerElement(new PointF(left, top), new SizeF(full * fraction, tall), fill).Draw();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not draw a HUD bar", ex);
            }
        }

        /// <summary>Minutes and seconds, for anything counting down.</summary>
        public static string Clock(int totalSeconds)
        {
            if (totalSeconds < 0) { totalSeconds = 0; }
            return (totalSeconds / 60) + ":" + (totalSeconds % 60).ToString("00");
        }
    }
}
