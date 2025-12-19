/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.UI.Skia;
using LoneEftDmaRadar.UI.Radar.Views;
using SkiaSharp.Views.WPF;

namespace LoneEftDmaRadar.UI.Radar.ViewModels.Helpers
{
    /// <summary>
    /// Manages radar widget initialization and lifecycle.
    /// </summary>
    public sealed class RadarWidgetManager
    {
        private readonly SKGLElement _radar;

        /// <summary>
        /// Aimview Widget Viewport.
        /// </summary>
        public AimviewWidget AimviewWidget { get; private set; }

        /// <summary>
        /// Player Info Widget Viewport.
        /// </summary>
        public PlayerInfoWidget InfoWidget { get; private set; }

        /// <summary>
        /// Loot Info Widget Viewport.
        /// </summary>
        public LootInfoWidget LootInfoWidget { get; private set; }

        /// <summary>
        /// Quest Helper Widget Viewport.
        /// </summary>
        public QuestHelperWidget QuestHelperWidget { get; private set; }

        public RadarWidgetManager(SKGLElement radar)
        {
            _radar = radar ?? throw new ArgumentNullException(nameof(radar));
        }

        /// <summary>
        /// Initializes all widgets with default or configured positions.
        /// </summary>
        public void InitializeWidgets()
        {
            var size = _radar.CanvasSize;
            var cr = new SKRect(0, 0, size.Width, size.Height);

            // Aimview Widget
            if (App.Config.AimviewWidget.Location == default)
            {
                App.Config.AimviewWidget.Location = new SKRect(cr.Left, cr.Bottom - 200, cr.Left + 200, cr.Bottom);
            }
            AimviewWidget = new AimviewWidget(_radar, App.Config.AimviewWidget.Location, 
                App.Config.AimviewWidget.Minimized, App.Config.UI.UIScale);

            // Info Widget
            if (App.Config.InfoWidget.Location == default)
            {
                App.Config.InfoWidget.Location = new SKRect(cr.Right - 1, cr.Top, cr.Right, cr.Top + 1);
            }
            InfoWidget = new PlayerInfoWidget(_radar, App.Config.InfoWidget.Location,
                App.Config.InfoWidget.Minimized, App.Config.UI.UIScale);

            // Loot Info Widget
            if (App.Config.LootInfoWidget.Location == default)
            {
                App.Config.LootInfoWidget.Location = new SKRect(cr.Left, cr.Top, cr.Left + 300, cr.Top + 400);
            }
            LootInfoWidget = new LootInfoWidget(_radar, App.Config.LootInfoWidget.Location,
                App.Config.LootInfoWidget.Minimized, App.Config.UI.UIScale);

            // Quest Helper Widget
            if (App.Config.QuestHelper.WidgetLocation == default)
            {
                App.Config.QuestHelper.WidgetLocation = new SKRect(cr.Right - 350, cr.Top, cr.Right, cr.Top + 300);
            }
            QuestHelperWidget = new QuestHelperWidget(_radar, App.Config.QuestHelper.WidgetLocation,
                App.Config.QuestHelper.WidgetMinimized, App.Config.UI.UIScale);
        }

        /// <summary>
        /// Clears focus from all widgets.
        /// </summary>
        public void ClearAllWidgetFocus()
        {
            if (AimviewWidget != null) AimviewWidget.IsFocused = false;
            if (InfoWidget != null) InfoWidget.IsFocused = false;
            if (LootInfoWidget != null) LootInfoWidget.IsFocused = false;
            if (QuestHelperWidget != null) QuestHelperWidget.IsFocused = false;
        }
    }
}
