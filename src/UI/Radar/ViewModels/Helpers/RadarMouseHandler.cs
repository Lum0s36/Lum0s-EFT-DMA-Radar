/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.Tarkov.GameWorld.Exits;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Quests;
using LoneEftDmaRadar.UI.Loot;
using LoneEftDmaRadar.UI.Misc;
using LoneEftDmaRadar.UI.Skia;
using SkiaSharp.Views.WPF;
using System.Windows.Input;

namespace LoneEftDmaRadar.UI.Radar.ViewModels.Helpers
{
    /// <summary>
    /// Handles mouse input events for the radar.
    /// </summary>
    public sealed class RadarMouseHandler
    {
        private readonly SKGLElement _radar;
        private readonly Func<bool> _inRaidCheck;
        private readonly Func<IEnumerable<IMouseoverEntity>> _getMouseOverItems;
        private readonly Action _clearWidgetFocus;

        private bool _mouseDown;
        private Vector2 _lastMousePosition;
        private float _dpiScaleX = 1f;
        private float _dpiScaleY = 1f;

        /// <summary>
        /// Currently moused-over item.
        /// </summary>
        public IMouseoverEntity MouseOverItem { get; private set; }

        /// <summary>
        /// Currently moused-over group ID.
        /// </summary>
        public int? MouseoverGroup { get; private set; }

        /// <summary>
        /// Current map pan position for free-map mode.
        /// </summary>
        public Vector2 MapPanPosition { get; set; }

        /// <summary>
        /// Whether mouse is currently pressed.
        /// </summary>
        public bool IsMouseDown => _mouseDown;

        /// <summary>
        /// Last mouse position (for panning).
        /// </summary>
        public Vector2 LastMousePosition => _lastMousePosition;

        public RadarMouseHandler(
            SKGLElement radar,
            Func<bool> inRaidCheck,
            Func<IEnumerable<IMouseoverEntity>> getMouseOverItems,
            Action clearWidgetFocus)
        {
            _radar = radar ?? throw new ArgumentNullException(nameof(radar));
            _inRaidCheck = inRaidCheck ?? throw new ArgumentNullException(nameof(inRaidCheck));
            _getMouseOverItems = getMouseOverItems ?? throw new ArgumentNullException(nameof(getMouseOverItems));
            _clearWidgetFocus = clearWidgetFocus ?? throw new ArgumentNullException(nameof(clearWidgetFocus));

            // Subscribe to events
            _radar.MouseMove += Radar_MouseMove;
            _radar.MouseDown += Radar_MouseDown;
            _radar.MouseUp += Radar_MouseUp;
            _radar.MouseLeave += Radar_MouseLeave;
            _radar.SizeChanged += Radar_SizeChanged;

            UpdateDpiScaleFactors();
        }

        /// <summary>
        /// Updates DPI scale factors based on current radar size.
        /// </summary>
        public void UpdateDpiScaleFactors()
        {
            try
            {
                double actualWidth = _radar.ActualWidth;
                double actualHeight = _radar.ActualHeight;
                var canvasSize = _radar.CanvasSize;

                if (actualWidth > 0 && actualHeight > 0 && canvasSize.Width > 0 && canvasSize.Height > 0)
                {
                    _dpiScaleX = (float)(canvasSize.Width / actualWidth);
                    _dpiScaleY = (float)(canvasSize.Height / actualHeight);
                }
                else
                {
                    _dpiScaleX = 1f;
                    _dpiScaleY = 1f;
                }
            }
            catch
            {
                _dpiScaleX = 1f;
                _dpiScaleY = 1f;
            }
        }

        /// <summary>
        /// Clears the current mouseover references.
        /// </summary>
        public void ClearMouseover()
        {
            MouseOverItem = null;
            MouseoverGroup = null;
        }

        #region Event Handlers

        private void Radar_MouseLeave(object sender, MouseEventArgs e)
        {
            _mouseDown = false;
        }

        private void Radar_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _mouseDown = false;
        }

        private void Radar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateDpiScaleFactors();
        }

        private void Radar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as IInputElement;
            var pt = e.GetPosition(element);
            var mouseX = (float)pt.X * _dpiScaleX;
            var mouseY = (float)pt.Y * _dpiScaleY;
            var mouse = new Vector2(mouseX, mouseY);

            _clearWidgetFocus();

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _lastMousePosition = mouse;
                _mouseDown = true;

                // Handle double-click on streaming player
                if (e.ClickCount >= 2 && MouseOverItem is ObservedPlayer observed)
                {
                    if (_inRaidCheck() && observed.IsStreaming)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = observed.TwitchChannelURL,
                            UseShellExecute = true
                        });
                    }
                }
            }

            if (e.RightButton == MouseButtonState.Pressed)
            {
                if (MouseOverItem is AbstractPlayer player)
                {
                    player.IsFocused = !player.IsFocused;
                }
            }

            // Hide loot overlay on mouse down
            if (MainWindow.Instance?.Radar?.Overlay?.ViewModel is RadarOverlayViewModel vm && vm.IsLootOverlayVisible)
            {
                vm.IsLootOverlayVisible = false;
            }
        }

        private void Radar_MouseMove(object sender, MouseEventArgs e)
        {
            var element = sender as IInputElement;
            var pt = e.GetPosition(element);
            var mouseX = (float)pt.X * _dpiScaleX;
            var mouseY = (float)pt.Y * _dpiScaleY;
            var mouse = new Vector2(mouseX, mouseY);

            bool isMapFree = MainWindow.Instance?.Radar?.Overlay?.ViewModel?.IsMapFreeEnabled ?? false;

            if (_mouseDown && isMapFree)
            {
                // Panning
                var deltaX = -(mouseX - _lastMousePosition.X);
                var deltaY = -(mouseY - _lastMousePosition.Y);
                MapPanPosition = new Vector2(MapPanPosition.X + deltaX, MapPanPosition.Y + deltaY);
                _lastMousePosition = mouse;
            }
            else
            {
                if (!_inRaidCheck())
                {
                    ClearMouseover();
                    return;
                }

                var items = _getMouseOverItems();
                if (items?.Any() != true)
                {
                    ClearMouseover();
                    return;
                }

                // Find closest item
                var closest = items.Aggregate(
                    (x1, x2) => Vector2.Distance(x1.MouseoverPosition, mouse)
                             < Vector2.Distance(x2.MouseoverPosition, mouse)
                        ? x1 : x2);

                float mouseoverThreshold = 12f * _dpiScaleX;
                if (Vector2.Distance(closest.MouseoverPosition, mouse) >= mouseoverThreshold)
                {
                    ClearMouseover();
                    return;
                }

                ProcessMouseoverItem(closest);
            }
        }

        private void ProcessMouseoverItem(IMouseoverEntity closest)
        {
            switch (closest)
            {
                case AbstractPlayer player:
                    MouseOverItem = player;
                    MouseoverGroup = (player.IsHumanHostile && player.GroupID != -1)
                        ? player.GroupID
                        : null;
                    break;

                case LootCorpse corpseObj:
                    MouseOverItem = corpseObj;
                    var corpse = corpseObj.Player;
                    MouseoverGroup = (corpse?.IsHumanHostile == true && corpse.GroupID != -1)
                        ? corpse.GroupID
                        : null;
                    break;

                case StaticLootContainer:
                case LootAirdrop:
                case IExitPoint:
                case LootItem:
                case QuestLocation:
                    MouseOverItem = closest;
                    MouseoverGroup = null;
                    break;

                default:
                    ClearMouseover();
                    break;
            }
        }

        #endregion
    }
}
