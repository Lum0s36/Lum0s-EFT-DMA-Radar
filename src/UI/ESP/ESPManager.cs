using System;
using System.Windows;
using LoneEftDmaRadar.DMA;
using CameraManagerNew = LoneEftDmaRadar.Tarkov.GameWorld.Camera.CameraManager;

namespace LoneEftDmaRadar.UI.ESP
{
    public static class ESPManager
    {
        private static ESPWindow _espWindow;
        private static bool _isInitialized = false;
        private static bool _raidHooked = false;

        public static void Initialize()
        {
            if (_isInitialized && _espWindow != null) return;

            _espWindow = new ESPWindow();
            _espWindow.Closed += (s, e) => 
            { 
                _espWindow = null; 
                _isInitialized = false;
                ESPWindow.ShowESP = false; // Sync state
            };
            // _espWindow.Show(); // Don't show automatically on init
            _isInitialized = true;

            if (!_raidHooked)
            {
                MemDMA.RaidStopped += MemDMA_RaidStopped;
                _raidHooked = true;
            }
        }

        private static void MemDMA_RaidStopped(object sender, EventArgs e)
        {
            // Reset ESP state on raid end so users don't need to restart the app.
            Application.Current?.Dispatcher.InvokeAsync(() => _espWindow?.OnRaidStopped());
        }

        public static void ToggleESP()
        {
            if (!_isInitialized || _espWindow == null) Initialize();
            
            ESPWindow.ShowESP = !ESPWindow.ShowESP;
            if (ESPWindow.ShowESP)
            {
                ApplyResolutionOverride();
                _espWindow?.Show();
            }
            else
            {
                _espWindow?.Hide();
            }
            _espWindow?.RefreshESP();
        }

        public static void ShowESP()
        {
            if (!_isInitialized || _espWindow == null) Initialize();
            
            ESPWindow.ShowESP = true;
            ApplyResolutionOverride();
            _espWindow?.RefreshESP();
            _espWindow?.Show();
        }

        public static void StartESP()
        {
            if (!_isInitialized || _espWindow == null) Initialize();
            
            if (_espWindow == null) return; // Safety check after Initialize
            
            ESPWindow.ShowESP = true;
            _espWindow.Show();
            // Force Fullscreen
            if (_espWindow.WindowStyle != WindowStyle.None)
            {
                _espWindow.ToggleFullscreen();
            }
            else
            {
                _espWindow.ApplyResolutionOverride();
            }
            _espWindow.RefreshESP();
        }

        public static void HideESP()
        {
            ESPWindow.ShowESP = false;
            _espWindow?.RefreshESP();
            _espWindow?.Hide();
        }

        public static void ToggleFullscreen()
        {
            if (!_isInitialized) Initialize();
            _espWindow?.ToggleFullscreen();
        }
        
        public static void CloseESP()
        {
            _espWindow?.Close();
            _espWindow = null;
            _isInitialized = false;
        }

        public static void ApplyResolutionOverride()
        {
            if (!_isInitialized || _espWindow is null) return;
            CameraManagerNew.UpdateViewportRes();
            _espWindow.ApplyResolutionOverride();
        }

        public static void ApplyFontConfig()
        {
            if (!_isInitialized || _espWindow is null) return;
            _espWindow.ApplyFontConfig();
        }

        /// <summary>
        /// Resets camera state and forces ESP refresh. Useful when ESP appears broken.
        /// </summary>
        public static void ResetCamera()
        {
            CameraManagerNew.Reset();
            _espWindow?.RefreshESP();
        }

        /// <summary>
        /// Shows a notification message in the ESP window (bottom-right corner).
        /// </summary>
        public static void ShowNotification(string message)
        {
            if (!_isInitialized || _espWindow is null)
                return;

            _espWindow.ShowNotification(message);
        }

        public static bool IsInitialized => _isInitialized;
    }
}
