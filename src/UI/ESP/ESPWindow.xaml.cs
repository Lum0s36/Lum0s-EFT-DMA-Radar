using LoneEftDmaRadar.Misc;
using LoneEftDmaRadar.Tarkov.GameWorld;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Camera;
using LoneEftDmaRadar.UI.ESP.Rendering;
using LoneEftDmaRadar.UI.Misc;
using LoneEftDmaRadar.DMA;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Forms.Integration;
using WinForms = System.Windows.Forms;
using SkiaSharp;
using DxColor = SharpDX.Mathematics.Interop.RawColorBGRA;
using CameraManagerNew = LoneEftDmaRadar.Tarkov.GameWorld.Camera.CameraManager;

namespace LoneEftDmaRadar.UI.ESP
{
    public partial class ESPWindow : Window
    {
        #region Fields/Properties

        public static bool ShowESP { get; set; } = true;
        private bool _dxInitFailed;

        private readonly System.Diagnostics.Stopwatch _fpsSw = new();
        private int _fpsCounter;
        private int _fps;
        private long _lastFrameTicks;
        private Timer _highFrequencyTimer;
        private int _renderPending;

        // Render surface
        private Dx9OverlayControl _dxOverlay;
        private WindowsFormsHost _dxHost;
        private bool _isClosing;
        private bool _isFullscreen;
        private bool _lastInRaidState;

        // Renderers (separated concerns)
        private readonly EspPlayerRenderer _playerRenderer = new();
        private readonly EspLootRenderer _lootRenderer = new();
        private readonly EspWorldRenderer _worldRenderer = new();
        private readonly EspOverlayRenderer _overlayRenderer = new();
        private readonly EspDebugRenderer _debugRenderer = new();

        // Cached paints for crosshair
        private readonly SKPaint _crosshairPaint;

        #endregion

        #region Constructor

        public ESPWindow()
        {
            InitializeComponent();
            InitializeRenderSurface();

            // Initial sizes
            this.Width = ESPConstants.DefaultWindowWidth;
            this.Height = ESPConstants.DefaultWindowHeight;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            _crosshairPaint = new SKPaint
            {
                Color = SKColors.White,
                StrokeWidth = ESPConstants.CrosshairStrokeWidth,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            _fpsSw.Start();
            _lastFrameTicks = System.Diagnostics.Stopwatch.GetTimestamp();

            _highFrequencyTimer = new System.Threading.Timer(
                callback: HighFrequencyRenderCallback,
                state: null,
                dueTime: 0,
                period: ESPConstants.HighFrequencyTimerIntervalMs);
        }

        private void InitializeRenderSurface()
        {
            RenderRoot.Children.Clear();

            _dxOverlay = new Dx9OverlayControl
            {
                Dock = WinForms.DockStyle.Fill
            };

            ApplyDxFontConfig();
            _dxOverlay.RenderFrame = RenderSurface;
            _dxOverlay.DeviceInitFailed += Overlay_DeviceInitFailed;
            _dxOverlay.MouseDown += GlControl_MouseDown;
            _dxOverlay.DoubleClick += GlControl_DoubleClick;
            _dxOverlay.KeyDown += GlControl_KeyDown;

            _dxHost = new WindowsFormsHost
            {
                Child = _dxOverlay
            };

            RenderRoot.Children.Add(_dxHost);
        }

        #endregion

        #region Render Loop

        private void HighFrequencyRenderCallback(object state)
        {
            try
            {
                if (_isClosing || _dxOverlay == null)
                    return;

                int maxFPS = App.Config.UI.EspMaxFPS;
                long currentTicks = System.Diagnostics.Stopwatch.GetTimestamp();

                // FPS limiting
                if (maxFPS > 0)
                {
                    double elapsedMs = (currentTicks - _lastFrameTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    double targetMs = 1000.0 / maxFPS;
                    if (elapsedMs < targetMs)
                        return;
                }

                _lastFrameTicks = currentTicks;

                if (System.Threading.Interlocked.CompareExchange(ref _renderPending, 1, 0) == 0)
                {
                    try
                    {
                        _dxOverlay.Render();
                    }
                    finally
                    {
                        System.Threading.Interlocked.Exchange(ref _renderPending, 0);
                    }
                }
            }
            catch { /* Ignore errors during shutdown */ }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void SetFPS()
        {
            if (_fpsSw.ElapsedMilliseconds >= 1000)
            {
                _fps = System.Threading.Interlocked.Exchange(ref _fpsCounter, 0);
                _fpsSw.Restart();
            }
            else
            {
                _fpsCounter++;
            }
        }

        /// <summary>
        /// Main ESP Render Event - now delegates to specialized renderers.
        /// Uses EspRenderContext for accessing game state via IGameStateProvider.
        /// </summary>
        private void RenderSurface(Dx9RenderContext ctx)
        {
            if (_dxInitFailed)
                return;

            float screenWidth = ctx.Width;
            float screenHeight = ctx.Height;

            SetFPS();
            ctx.Clear(new DxColor(0, 0, 0, 255));

            try
            {
                // Create render context first - it provides InRaid check
                var gameState = MemoryGameStateProvider.Instance;
                
                // Detect raid state changes
                if (_lastInRaidState && !gameState.InRaid)
                {
                    CameraManagerNew.Reset();
                    EspPlayerRenderer.ClearCache();
                    DebugLogger.LogInfo("ESP: Detected raid end - reset all state");
                }
                _lastInRaidState = gameState.InRaid;

                if (!gameState.InRaid)
                    return;

                var localPlayer = gameState.LocalPlayer;
                var allPlayers = gameState.Players;

                if (localPlayer == null || allPlayers == null)
                    return;

                // Create render context with game state provider
                var context = new EspRenderContext(ctx, screenWidth, screenHeight, localPlayer, gameState);

                if (!ShowESP)
                {
                    _overlayRenderer.DrawHiddenMessage(context);
                    return;
                }

                ApplyResolutionOverrideIfNeeded();

                // Render loot (background layer)
                if (ShouldRenderLoot())
                {
                    _lootRenderer.Draw(context);
                }

                // Render containers
                _lootRenderer.DrawContainers(context);

                // Render exfils - use context.Exits
                _worldRenderer.DrawExfils(context, context.Exits);

                // Render tripwires - use context.Explosives
                _worldRenderer.DrawTripwires(context, context.Explosives);

                // Render grenades - use context.Explosives
                _worldRenderer.DrawGrenades(context, context.Explosives);

                // Render players - use context.AllPlayers
                _playerRenderer.Draw(context, context.AllPlayers);

                // DeviceAimbot overlays
                _overlayRenderer.DrawDeviceAimbotTargetLine(context);

                if (App.Config.Device.Enabled)
                {
                    _overlayRenderer.DrawDeviceAimbotFovCircle(context);
                }

                // Crosshair
                _overlayRenderer.DrawCrosshair(context, _crosshairPaint.StrokeWidth);

                // Debug overlays
                _overlayRenderer.DrawDeviceAimbotDebugOverlay(context);
                _overlayRenderer.DrawFPS(context, _fps);
                _overlayRenderer.DrawNotification(context);
                _overlayRenderer.DrawNearestPlayerInfo(context, context.AllPlayers);
                _debugRenderer.DrawLootDebugOverlay(context);
            }
            catch (System.Exception ex)
            {
                DebugLogger.LogDebug($"ESP RENDER ERROR: {ex}");
            }
        }

        private static bool ShouldRenderLoot()
        {
            return App.Config.UI.ShowESP &&
                   (App.Config.UI.EspLootFilterOnly ||
                    App.Config.UI.EspLoot ||
                    App.Config.UI.EspFood ||
                    App.Config.UI.EspMeds ||
                    App.Config.UI.EspBackpacks ||
                    App.Config.UI.EspQuestLoot ||
                    App.Config.UI.EspCorpses ||
                    App.Config.UI.EspShowWishlisted);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows a notification message in the ESP window.
        /// </summary>
        public void ShowNotification(string message)
        {
            if (_isClosing)
                return;

            _overlayRenderer.ShowNotification(message);
            RefreshESP();
        }

        /// <summary>
        /// Resets ESP state when a raid ends.
        /// </summary>
        public void OnRaidStopped()
        {
            _lastInRaidState = false;
            EspPlayerRenderer.ClearCache();
            CameraManagerNew.Reset();
            RefreshESP();
            DebugLogger.LogInfo("ESP: RaidStopped -> state reset");
        }

        /// <summary>
        /// Force refresh the ESP.
        /// </summary>
        public void RefreshESP()
        {
            if (_isClosing)
                return;

            try
            {
                _dxOverlay?.Render();
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"ESP Refresh error: {ex}");
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _renderPending, 0);
            }
        }

        public void ApplyFontConfig()
        {
            ApplyDxFontConfig();
            RefreshESP();
        }

        public void ApplyResolutionOverride()
        {
            if (!_isFullscreen)
                return;

            var monitor = GetTargetMonitor();
            var (width, height) = GetConfiguredResolution(monitor);
            this.Left = monitor?.Left ?? 0;
            this.Top = monitor?.Top ?? 0;
            this.Width = width;
            this.Height = height;
            this.RefreshESP();
        }

        public void ToggleFullscreen()
        {
            if (_isFullscreen)
            {
                this.WindowState = WindowState.Normal;
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.Topmost = false;
                this.ResizeMode = ResizeMode.CanResize;
                this.Width = ESPConstants.DefaultWindowWidth;
                this.Height = ESPConstants.DefaultWindowHeight;
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                _isFullscreen = false;
            }
            else
            {
                this.WindowStyle = WindowStyle.None;
                this.ResizeMode = ResizeMode.NoResize;
                this.Topmost = true;
                this.WindowState = WindowState.Normal;

                var monitor = GetTargetMonitor();
                var (width, height) = GetConfiguredResolution(monitor);

                this.Left = monitor?.Left ?? 0;
                this.Top = monitor?.Top ?? 0;
                this.Width = width;
                this.Height = height;
                _isFullscreen = true;
            }

            this.RefreshESP();
        }

        #endregion

        #region Private Methods

        private void ApplyDxFontConfig()
        {
            var ui = App.Config.UI;
            _dxOverlay?.SetFontConfig(
                ui.EspFontFamily,
                ui.EspFontSizeSmall,
                ui.EspFontSizeMedium,
                ui.EspFontSizeLarge);
        }

        private void ApplyResolutionOverrideIfNeeded()
        {
            if (!_isFullscreen)
                return;

            if (App.Config.UI.EspScreenWidth <= 0 && App.Config.UI.EspScreenHeight <= 0)
                return;

            var monitor = GetTargetMonitor();
            var target = GetConfiguredResolution(monitor);
            if (Math.Abs(Width - target.width) > 0.5 || Math.Abs(Height - target.height) > 0.5)
            {
                Width = target.width;
                Height = target.height;
                Left = monitor?.Left ?? 0;
                Top = monitor?.Top ?? 0;
            }
        }

        private (double width, double height) GetConfiguredResolution(MonitorInfo monitor)
        {
            double width = App.Config.UI.EspScreenWidth > 0
                ? App.Config.UI.EspScreenWidth
                : monitor?.Width ?? SystemParameters.PrimaryScreenWidth;
            double height = App.Config.UI.EspScreenHeight > 0
                ? App.Config.UI.EspScreenHeight
                : monitor?.Height ?? SystemParameters.PrimaryScreenHeight;
            return (width, height);
        }

        private MonitorInfo GetTargetMonitor()
        {
            return MonitorInfo.GetMonitor(App.Config.UI.EspTargetScreen);
        }

        private void ForceReleaseCursorAndHide()
        {
            try
            {
                Mouse.Capture(null);
                WinForms.Cursor.Current = WinForms.Cursors.Default;
                this.Cursor = System.Windows.Input.Cursors.Arrow;
                Mouse.OverrideCursor = null;
                if (_dxOverlay != null)
                {
                    _dxOverlay.Capture = false;
                }
                this.Topmost = false;
                ShowESP = false;
                Hide();
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"ESP: ForceReleaseCursor failed: {ex}");
            }
        }

        #endregion

        #region Event Handlers

        private void Overlay_DeviceInitFailed(Exception ex)
        {
            _dxInitFailed = true;
            DebugLogger.LogDebug($"ESP DX init failed: {ex}");

            Dispatcher.BeginInvoke(new Action(() =>
            {
                RenderRoot.Children.Clear();
                RenderRoot.Children.Add(new TextBlock
                {
                    Text = "DX overlay init failed. See log for details.",
                    Foreground = System.Windows.Media.Brushes.White,
                    Background = System.Windows.Media.Brushes.Black,
                    Margin = new Thickness(12)
                });
            }), DispatcherPriority.Send);
        }

        private void GlControl_MouseDown(object sender, WinForms.MouseEventArgs e)
        {
            if (e.Button == WinForms.MouseButtons.Left)
            {
                try { this.DragMove(); } catch { /* ignore dragging errors */ }
            }
        }

        private void GlControl_DoubleClick(object sender, EventArgs e)
        {
            ToggleFullscreen();
        }

        private void GlControl_KeyDown(object sender, WinForms.KeyEventArgs e)
        {
            if (e.KeyCode == WinForms.Keys.F12)
            {
                ForceReleaseCursorAndHide();
                return;
            }

            if (e.KeyCode == WinForms.Keys.Escape && this.WindowState == WindowState.Maximized)
            {
                ToggleFullscreen();
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ToggleFullscreen();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F12)
            {
                ForceReleaseCursorAndHide();
                return;
            }

            if (e.Key == Key.Escape && this.WindowState == WindowState.Maximized)
            {
                ToggleFullscreen();
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _isClosing = true;
            try
            {
                _highFrequencyTimer?.Dispose();
                _dxOverlay?.Dispose();
                _crosshairPaint.Dispose();
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"ESP: OnClosed cleanup error: {ex}");
            }
            finally
            {
                base.OnClosed(e);
            }
        }

        #endregion
    }
}