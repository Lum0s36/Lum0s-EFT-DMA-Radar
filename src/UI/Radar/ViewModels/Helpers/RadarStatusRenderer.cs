/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.UI.Skia;
using SkiaSharp;

namespace LoneEftDmaRadar.UI.Radar.ViewModels.Helpers
{
    /// <summary>
    /// Renders status messages when not in raid.
    /// </summary>
    public sealed class RadarStatusRenderer
    {
        private int _statusOrder = 1;

        /// <summary>
        /// Gets and increments the status order for rotating animation.
        /// </summary>
        public int StatusOrder
        {
            get => _statusOrder;
            set
            {
                if (_statusOrder >= 3)
                    _statusOrder = 1;
                else
                    _statusOrder++;
            }
        }

        /// <summary>
        /// Increment status order for next animation frame.
        /// </summary>
        public void IncrementStatusOrder()
        {
            StatusOrder++;
        }

        /// <summary>
        /// Display 'Game Process Not Running!' status message.
        /// </summary>
        public void DrawGameNotRunning(SKCanvas canvas)
        {
            const string notRunning = "Game Process Not Running!";
            var bounds = canvas.LocalClipBounds;
            float textWidth = SKFonts.UILarge.MeasureText(notRunning);
            canvas.DrawText(notRunning,
                (bounds.Width / 2) - textWidth / 2f, bounds.Height / 2,
                SKTextAlign.Left,
                SKFonts.UILarge,
                SKPaints.TextRadarStatus);
        }

        /// <summary>
        /// Display 'Starting Up...' status message.
        /// </summary>
        public void DrawStartingUp(SKCanvas canvas)
        {
            const string startingUp1 = "Starting Up.";
            const string startingUp2 = "Starting Up..";
            const string startingUp3 = "Starting Up...";
            var bounds = canvas.LocalClipBounds;
            int order = _statusOrder;
            string status = order == 1 ? startingUp1 : order == 2 ? startingUp2 : startingUp3;
            float textWidth = SKFonts.UILarge.MeasureText(startingUp1);
            canvas.DrawText(status,
                (bounds.Width / 2) - textWidth / 2f, bounds.Height / 2,
                SKTextAlign.Left,
                SKFonts.UILarge,
                SKPaints.TextRadarStatus);
        }

        /// <summary>
        /// Display 'Waiting for Raid Start...' status message.
        /// </summary>
        public void DrawWaitingForRaid(SKCanvas canvas)
        {
            const string waitingFor1 = "Waiting for Raid Start.";
            const string waitingFor2 = "Waiting for Raid Start..";
            const string waitingFor3 = "Waiting for Raid Start...";
            var bounds = canvas.LocalClipBounds;
            int order = _statusOrder;
            string status = order == 1 ? waitingFor1 : order == 2 ? waitingFor2 : waitingFor3;
            float textWidth = SKFonts.UILarge.MeasureText(waitingFor1);
            canvas.DrawText(status,
                (bounds.Width / 2) - textWidth / 2f, bounds.Height / 2,
                SKTextAlign.Left,
                SKFonts.UILarge,
                SKPaints.TextRadarStatus);
        }
    }
}
