/*
 * Lone EFT DMA Radar
 * Brought to you by Lone (Lone DMA)
 * 
MIT License

Copyright (c) 2025 Lone DMA

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 *
*/

using LoneEftDmaRadar.UI.Hotkeys;
using LoneEftDmaRadar.UI.Misc;
using LoneEftDmaRadar.UI.Radar.ViewModels;
using LoneEftDmaRadar.UI.ESP;

namespace LoneEftDmaRadar
{
    public sealed class MainWindowViewModel
    {
        private readonly MainWindow _parent;
        //public event PropertyChangedEventHandler PropertyChanged;

        public MainWindowViewModel(MainWindow parent)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            EnsureHotkeysRegistered();
        }

        public void ToggleFullscreen(bool toFullscreen)
        {
            if (toFullscreen)
            {
                // Full‐screen
                _parent.WindowStyle = WindowStyle.None;
                _parent.ResizeMode = ResizeMode.NoResize;
                _parent.Topmost = true;
                _parent.WindowState = WindowState.Maximized;
            }
            else
            {
                _parent.WindowStyle = WindowStyle.SingleBorderWindow;
                _parent.ResizeMode = ResizeMode.CanResize;
                _parent.Topmost = false;
                _parent.WindowState = WindowState.Normal;
            }
        }

        #region Hotkey Manager

        private const int HK_ZOOMTICKAMT = 5; // amt to zoom
        private const int HK_ZOOMTICKDELAY = 120; // ms

        /// <summary>
        /// Loads Hotkey Manager resources.
        /// Only call from Primary Thread/Window (ONCE!)
        /// </summary>
        private bool _hotkeysRegistered;

        internal void EnsureHotkeysRegistered()
        {
            if (_hotkeysRegistered)
                return;
            LoadHotkeyManager();
            _hotkeysRegistered = true;
        }

        private void LoadHotkeyManager()
        {
            var zoomIn = new HotkeyActionController("Zoom In");
            zoomIn.Delay = HK_ZOOMTICKDELAY;
            zoomIn.HotkeyDelayElapsed += ZoomIn_HotkeyDelayElapsed;
            var zoomOut = new HotkeyActionController("Zoom Out");
            zoomOut.Delay = HK_ZOOMTICKDELAY;
            zoomOut.HotkeyDelayElapsed += ZoomOut_HotkeyDelayElapsed;
            var toggleLoot = new HotkeyActionController("Toggle Loot");
            toggleLoot.HotkeyStateChanged += ToggleLoot_HotkeyStateChanged;
            var toggleAimviewWidget = new HotkeyActionController("Toggle Aimview Widget");
            toggleAimviewWidget.HotkeyStateChanged += ToggleAimviewWidget_HotkeyStateChanged;
            var toggleNames = new HotkeyActionController("Toggle Player Names");
            toggleNames.HotkeyStateChanged += ToggleNames_HotkeyStateChanged;
            var toggleInfo = new HotkeyActionController("Toggle Game Info Tab");
            toggleInfo.HotkeyStateChanged += ToggleInfo_HotkeyStateChanged;
            var toggleShowFood = new HotkeyActionController("Toggle Show Food");
            toggleShowFood.HotkeyStateChanged += ToggleShowFood_HotkeyStateChanged;
            var toggleShowMeds = new HotkeyActionController("Toggle Show Meds");
            toggleShowMeds.HotkeyStateChanged += ToggleShowMeds_HotkeyStateChanged;
            var toggleShowQuestItems = new HotkeyActionController("Toggle Show Quest Items");
            toggleShowQuestItems.HotkeyStateChanged += ToggleShowQuestItems_HotkeyStateChanged;
            var engageAimbotDeviceAimbot = new HotkeyActionController("Engage Aimbot");
            engageAimbotDeviceAimbot.HotkeyStateChanged += EngageAimbotDeviceAimbot_HotkeyStateChanged;
            var toggleDeviceAimbotEnabled = new HotkeyActionController("Toggle Device Aimbot");
            toggleDeviceAimbotEnabled.HotkeyStateChanged += ToggleDeviceAimbotEnabled_HotkeyStateChanged;
            
            var toggleESP = new HotkeyActionController("Toggle ESP Overlay");
            toggleESP.HotkeyStateChanged += ToggleESP_HotkeyStateChanged;
            var toggleESPPlayers = new HotkeyActionController("Toggle ESP Players");
            toggleESPPlayers.HotkeyStateChanged += ToggleESPPlayers_HotkeyStateChanged;
            var toggleESPScavs = new HotkeyActionController("Toggle ESP Scavs/AI");
            toggleESPScavs.HotkeyStateChanged += ToggleESPScavs_HotkeyStateChanged;
            var toggleESPLoot = new HotkeyActionController("Toggle ESP Loot");
            toggleESPLoot.HotkeyStateChanged += ToggleESPLoot_HotkeyStateChanged;
            var toggleESPExfils = new HotkeyActionController("Toggle ESP Exfils");
            toggleESPExfils.HotkeyStateChanged += ToggleESPExfils_HotkeyStateChanged;
            var toggleESPContainers = new HotkeyActionController("Toggle ESP Containers");
            toggleESPContainers.HotkeyStateChanged += ToggleESPContainers_HotkeyStateChanged;
            var toggleStaticContainers = new HotkeyActionController("Toggle Static Containers");
            toggleStaticContainers.HotkeyStateChanged += ToggleStaticContainers_HotkeyStateChanged;
            var toggleESPTripwires = new HotkeyActionController("Toggle ESP Tripwires");
            toggleESPTripwires.HotkeyStateChanged += ToggleESPTripwires_HotkeyStateChanged;
            var toggleESPGrenades = new HotkeyActionController("Toggle ESP Grenades");
            toggleESPGrenades.HotkeyStateChanged += ToggleESPGrenades_HotkeyStateChanged;
            var toggleESPCrosshair = new HotkeyActionController("Toggle ESP Crosshair");
            toggleESPCrosshair.HotkeyStateChanged += ToggleESPCrosshair_HotkeyStateChanged;
            var toggleESPQuestLoot = new HotkeyActionController("Toggle ESP Quest Loot");
            toggleESPQuestLoot.HotkeyStateChanged += ToggleESPQuestLoot_HotkeyStateChanged;
            var toggleESPFood = new HotkeyActionController("Toggle ESP Food");
            toggleESPFood.HotkeyStateChanged += ToggleESPFood_HotkeyStateChanged;
            var toggleESPMeds = new HotkeyActionController("Toggle ESP Meds");
            toggleESPMeds.HotkeyStateChanged += ToggleESPMeds_HotkeyStateChanged;
            var toggleESPBackpacks = new HotkeyActionController("Toggle ESP Backpacks");
            toggleESPBackpacks.HotkeyStateChanged += ToggleESPBackpacks_HotkeyStateChanged;
            var toggleESPCorpses = new HotkeyActionController("Toggle ESP Corpses");
            toggleESPCorpses.HotkeyStateChanged += ToggleESPCorpses_HotkeyStateChanged;
            var toggleESPPlayerHealth = new HotkeyActionController("Toggle ESP Player Health");
            toggleESPPlayerHealth.HotkeyStateChanged += ToggleESPPlayerHealth_HotkeyStateChanged;
            var toggleESPAIHealth = new HotkeyActionController("Toggle ESP AI Health");
            toggleESPAIHealth.HotkeyStateChanged += ToggleESPAIHealth_HotkeyStateChanged;
            var toggleESPGroupIds = new HotkeyActionController("Toggle ESP Group IDs");
            toggleESPGroupIds.HotkeyStateChanged += ToggleESPGroupIds_HotkeyStateChanged;
            var toggleESPAIGroupIds = new HotkeyActionController("Toggle ESP AI Group IDs");
            toggleESPAIGroupIds.HotkeyStateChanged += ToggleESPAIGroupIds_HotkeyStateChanged;
            var toggleESPHeadCirclePlayers = new HotkeyActionController("Toggle ESP Head Circle Players");
            toggleESPHeadCirclePlayers.HotkeyStateChanged += ToggleESPHeadCirclePlayers_HotkeyStateChanged;
            var toggleESPHeadCircleAI = new HotkeyActionController("Toggle ESP Head Circle AI");
            toggleESPHeadCircleAI.HotkeyStateChanged += ToggleESPHeadCircleAI_HotkeyStateChanged;
            var toggleESPNearestPlayerInfo = new HotkeyActionController("Toggle ESP Nearest Player Info");
            toggleESPNearestPlayerInfo.HotkeyStateChanged += ToggleESPNearestPlayerInfo_HotkeyStateChanged;
            var toggleESPLootFilterOnly = new HotkeyActionController("Toggle ESP Loot Filter Only");
            toggleESPLootFilterOnly.HotkeyStateChanged += ToggleESPLootFilterOnly_HotkeyStateChanged;
            var toggleESPShowWishlisted = new HotkeyActionController("Toggle ESP Show Wishlisted");
            toggleESPShowWishlisted.HotkeyStateChanged += ToggleESPShowWishlisted_HotkeyStateChanged;
            var espResetCamera = new HotkeyActionController("ESP Reset Camera");
            espResetCamera.HotkeyStateChanged += ESPResetCamera_HotkeyStateChanged;

            // Add to Static Collection:
            HotkeyAction.RegisterController(zoomIn);
            HotkeyAction.RegisterController(zoomOut);
            HotkeyAction.RegisterController(toggleLoot);
            HotkeyAction.RegisterController(toggleAimviewWidget);
            HotkeyAction.RegisterController(toggleNames);
            HotkeyAction.RegisterController(toggleInfo);
            HotkeyAction.RegisterController(toggleShowFood);
            HotkeyAction.RegisterController(toggleShowMeds);
            HotkeyAction.RegisterController(toggleShowQuestItems);
            HotkeyAction.RegisterController(toggleESP);
            HotkeyAction.RegisterController(toggleESPPlayers);
            HotkeyAction.RegisterController(toggleESPScavs);
            HotkeyAction.RegisterController(toggleESPLoot);
            HotkeyAction.RegisterController(toggleESPExfils);
            HotkeyAction.RegisterController(toggleESPContainers);
            HotkeyAction.RegisterController(toggleStaticContainers);
            HotkeyAction.RegisterController(toggleESPTripwires);
            HotkeyAction.RegisterController(toggleESPGrenades);
            HotkeyAction.RegisterController(toggleESPCrosshair);
            HotkeyAction.RegisterController(toggleESPQuestLoot);
            HotkeyAction.RegisterController(toggleESPFood);
            HotkeyAction.RegisterController(toggleESPMeds);
            HotkeyAction.RegisterController(toggleESPBackpacks);
            HotkeyAction.RegisterController(toggleESPCorpses);
            HotkeyAction.RegisterController(toggleESPPlayerHealth);
            HotkeyAction.RegisterController(toggleESPAIHealth);
            HotkeyAction.RegisterController(toggleESPGroupIds);
            HotkeyAction.RegisterController(toggleESPAIGroupIds);
            HotkeyAction.RegisterController(toggleESPHeadCirclePlayers);
            HotkeyAction.RegisterController(toggleESPHeadCircleAI);
            HotkeyAction.RegisterController(toggleESPNearestPlayerInfo);
            HotkeyAction.RegisterController(toggleESPLootFilterOnly);
            HotkeyAction.RegisterController(toggleESPShowWishlisted);
            HotkeyAction.RegisterController(espResetCamera);
            HotkeyAction.RegisterController(engageAimbotDeviceAimbot);
            HotkeyAction.RegisterController(toggleDeviceAimbotEnabled);
            HotkeyManagerViewModel.NotifyControllersRegistered();
        }

        internal static void EnsureHotkeysRegisteredStatic()
        {
            MainWindow.Instance?.ViewModel?.EnsureHotkeysRegistered();
        }

        private void ToggleAimviewWidget_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State && _parent.Settings?.ViewModel is SettingsViewModel vm)
                vm.AimviewWidget = !vm.AimviewWidget;
        }

        private void ToggleShowMeds_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State && _parent.Radar?.Overlay?.ViewModel is RadarOverlayViewModel vm)
            {
                vm.ShowMeds = !vm.ShowMeds;
            }
        }

        private void ToggleShowQuestItems_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State && _parent.Radar?.Overlay?.ViewModel is RadarOverlayViewModel vm)
            {
                vm.ShowQuestItems = !vm.ShowQuestItems;
            }
        }

        private void EngageAimbotDeviceAimbot_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            DebugLogger.LogDebug($"[Hotkey] Engage Aimbot: State changed to {e.State}");
            
            if (_parent.DeviceAimbot?.ViewModel is DeviceAimbotViewModel DeviceAimbotAim)
            {
                DeviceAimbotAim.IsEngaged = e.State;
                DebugLogger.LogDebug($"[Hotkey] DeviceAimbot.IsEngaged set to {e.State}");
            }
            else
            {
                DebugLogger.LogDebug("[Hotkey] DeviceAimbot or ViewModel is null");
            }
        }

        private void ToggleDeviceAimbotEnabled_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (!e.State)
                return;

            if (_parent.DeviceAimbot?.ViewModel is DeviceAimbotViewModel vm)
            {
                vm.Enabled = !vm.Enabled;
            }
        }

        private void ToggleShowFood_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State && _parent.Radar?.Overlay?.ViewModel is RadarOverlayViewModel vm)
            {
                vm.ShowFood = !vm.ShowFood;
            }
        }

        private void ToggleInfo_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State && _parent.Settings?.ViewModel is SettingsViewModel vm)
                vm.PlayerInfoWidget = !vm.PlayerInfoWidget;
        }

        private void ToggleNames_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State && _parent.Settings?.ViewModel is SettingsViewModel vm)
                vm.HideNames = !vm.HideNames;
        }

        private void ToggleLoot_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State && _parent.Settings?.ViewModel is SettingsViewModel vm)
                vm.ShowLoot = !vm.ShowLoot;
        }

        private void ZoomOut_HotkeyDelayElapsed(object sender, EventArgs e)
        {
            _parent.Radar?.ViewModel?.ZoomOut(HK_ZOOMTICKAMT);
        }

        private void ZoomIn_HotkeyDelayElapsed(object sender, EventArgs e)
        {
            _parent.Radar?.ViewModel?.ZoomIn(HK_ZOOMTICKAMT);
        }

        private void ToggleESP_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                ESPManager.ToggleESP();
                ESPManager.ShowNotification($"ESP Overlay: {(ESPWindow.ShowESP ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPPlayers_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                bool newState = !App.Config.UI.EspPlayerSkeletons;
                App.Config.UI.EspPlayerSkeletons = newState;
                App.Config.UI.EspPlayerBoxes = newState;
                App.Config.UI.EspPlayerNames = newState;
                App.Config.UI.EspPlayerDistance = newState;
                NotifyEspSettingsChanged(
                    nameof(EspSettingsViewModel.EspPlayerSkeletons),
                    nameof(EspSettingsViewModel.EspPlayerBoxes),
                    nameof(EspSettingsViewModel.EspPlayerNames),
                    nameof(EspSettingsViewModel.EspPlayerDistance));
                ESPManager.ShowNotification($"ESP Players: {(newState ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPScavs_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                bool newState = !App.Config.UI.EspAISkeletons;
                App.Config.UI.EspAISkeletons = newState;
                App.Config.UI.EspAIBoxes = newState;
                App.Config.UI.EspAINames = newState;
                App.Config.UI.EspAIDistance = newState;
                NotifyEspSettingsChanged(
                    nameof(EspSettingsViewModel.EspAISkeletons),
                    nameof(EspSettingsViewModel.EspAIBoxes),
                    nameof(EspSettingsViewModel.EspAINames),
                    nameof(EspSettingsViewModel.EspAIDistance));
                ESPManager.ShowNotification($"ESP Scavs/AI: {(newState ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPLoot_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                App.Config.UI.EspLoot = !App.Config.UI.EspLoot;
                NotifyEspSettingsChanged(nameof(EspSettingsViewModel.EspLoot));
                ESPManager.ShowNotification($"ESP Loot: {(App.Config.UI.EspLoot ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPExfils_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                App.Config.UI.EspExfils = !App.Config.UI.EspExfils;
                NotifyEspSettingsChanged(nameof(EspSettingsViewModel.EspExfils));
                ESPManager.ShowNotification($"ESP Exfils: {(App.Config.UI.EspExfils ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPContainers_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                App.Config.UI.EspContainers = !App.Config.UI.EspContainers;
                NotifyEspSettingsChanged(nameof(EspSettingsViewModel.EspContainers));
                ESPManager.ShowNotification($"ESP Containers: {(App.Config.UI.EspContainers ? "ON" : "OFF")}");
            }
        }

        private void ToggleStaticContainers_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State && _parent.Settings?.ViewModel is SettingsViewModel vm)
            {
                vm.ShowStaticContainers = !vm.ShowStaticContainers;
            }
        }

        private void ToggleESPTripwires_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                App.Config.UI.EspTripwires = !App.Config.UI.EspTripwires;
                NotifyEspSettingsChanged(nameof(EspSettingsViewModel.EspTripwires));
                ESPManager.ShowNotification($"ESP Tripwires: {(App.Config.UI.EspTripwires ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPGrenades_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                App.Config.UI.EspGrenades = !App.Config.UI.EspGrenades;
                NotifyEspSettingsChanged(nameof(EspSettingsViewModel.EspGrenades));
                ESPManager.ShowNotification($"ESP Grenades: {(App.Config.UI.EspGrenades ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPCrosshair_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                App.Config.UI.EspCrosshair = !App.Config.UI.EspCrosshair;
                NotifyEspSettingsChanged(nameof(EspSettingsViewModel.EspCrosshair));
                ESPManager.ShowNotification($"ESP Crosshair: {(App.Config.UI.EspCrosshair ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPQuestLoot_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                App.Config.UI.EspQuestLoot = !App.Config.UI.EspQuestLoot;
                NotifyEspSettingsChanged(nameof(EspSettingsViewModel.EspQuestLoot));
                ESPManager.ShowNotification($"ESP Quest Loot: {(App.Config.UI.EspQuestLoot ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPFood_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                App.Config.UI.EspFood = !App.Config.UI.EspFood;
                NotifyEspSettingsChanged(nameof(EspSettingsViewModel.EspFood));
                ESPManager.ShowNotification($"ESP Food: {(App.Config.UI.EspFood ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPMeds_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                App.Config.UI.EspMeds = !App.Config.UI.EspMeds;
                NotifyEspSettingsChanged(nameof(EspSettingsViewModel.EspMeds));
                ESPManager.ShowNotification($"ESP Meds: {(App.Config.UI.EspMeds ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPBackpacks_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                App.Config.UI.EspBackpacks = !App.Config.UI.EspBackpacks;
                NotifyEspSettingsChanged(nameof(EspSettingsViewModel.EspBackpacks));
                ESPManager.ShowNotification($"ESP Backpacks: {(App.Config.UI.EspBackpacks ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPCorpses_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                App.Config.UI.EspCorpses = !App.Config.UI.EspCorpses;
                NotifyEspSettingsChanged(nameof(EspSettingsViewModel.EspCorpses));
                ESPManager.ShowNotification($"ESP Corpses: {(App.Config.UI.EspCorpses ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPPlayerHealth_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                App.Config.UI.EspPlayerHealth = !App.Config.UI.EspPlayerHealth;
                NotifyEspSettingsChanged(nameof(EspSettingsViewModel.EspPlayerHealth));
                ESPManager.ShowNotification($"ESP Player Health: {(App.Config.UI.EspPlayerHealth ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPAIHealth_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                App.Config.UI.EspAIHealth = !App.Config.UI.EspAIHealth;
                NotifyEspSettingsChanged(nameof(EspSettingsViewModel.EspAIHealth));
                ESPManager.ShowNotification($"ESP AI Health: {(App.Config.UI.EspAIHealth ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPGroupIds_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                App.Config.UI.EspGroupIds = !App.Config.UI.EspGroupIds;
                NotifyEspSettingsChanged(nameof(EspSettingsViewModel.EspGroupIds));
                ESPManager.ShowNotification($"ESP Group IDs: {(App.Config.UI.EspGroupIds ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPAIGroupIds_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                App.Config.UI.EspAIGroupIds = !App.Config.UI.EspAIGroupIds;
                NotifyEspSettingsChanged(nameof(EspSettingsViewModel.EspAIGroupIds));
                ESPManager.ShowNotification($"ESP AI Group IDs: {(App.Config.UI.EspAIGroupIds ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPHeadCirclePlayers_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                App.Config.UI.EspHeadCirclePlayers = !App.Config.UI.EspHeadCirclePlayers;
                NotifyEspSettingsChanged(nameof(EspSettingsViewModel.EspHeadCirclePlayers));
                ESPManager.ShowNotification($"ESP Head Circle Players: {(App.Config.UI.EspHeadCirclePlayers ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPHeadCircleAI_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                App.Config.UI.EspHeadCircleAI = !App.Config.UI.EspHeadCircleAI;
                NotifyEspSettingsChanged(nameof(EspSettingsViewModel.EspHeadCircleAI));
                ESPManager.ShowNotification($"ESP Head Circle AI: {(App.Config.UI.EspHeadCircleAI ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPNearestPlayerInfo_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                App.Config.UI.EspNearestPlayerInfo = !App.Config.UI.EspNearestPlayerInfo;
                NotifyEspSettingsChanged(nameof(EspSettingsViewModel.EspNearestPlayerInfo));
                ESPManager.ShowNotification($"ESP Nearest Player Info: {(App.Config.UI.EspNearestPlayerInfo ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPLootFilterOnly_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                App.Config.UI.EspLootFilterOnly = !App.Config.UI.EspLootFilterOnly;
                NotifyEspSettingsChanged(nameof(EspSettingsViewModel.EspLootFilterOnly));
                ESPManager.ShowNotification($"ESP Loot Filter Only: {(App.Config.UI.EspLootFilterOnly ? "ON" : "OFF")}");
            }
        }

        private void ToggleESPShowWishlisted_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                App.Config.UI.EspShowWishlisted = !App.Config.UI.EspShowWishlisted;
                NotifyEspSettingsChanged(nameof(EspSettingsViewModel.EspShowWishlisted));
                ESPManager.ShowNotification($"ESP Show Wishlisted: {(App.Config.UI.EspShowWishlisted ? "ON" : "OFF")}");
            }
        }

        private void ESPResetCamera_HotkeyStateChanged(object sender, HotkeyEventArgs e)
        {
            if (e.State)
            {
                ESPManager.ResetCamera();
                ESPManager.ShowNotification("ESP Camera Reset");
            }
        }

        /// <summary>
        /// Helper method to notify EspSettingsViewModel when config values are changed via hotkeys.
        /// This ensures the UI checkboxes stay in sync with the actual config values.
        /// </summary>
        private void NotifyEspSettingsChanged(params string[] propertyNames)
        {
            var espViewModel = MainWindow.Instance?.EspSettings?.ViewModel;
            if (espViewModel != null)
            {
                foreach (var propertyName in propertyNames)
                {
                    espViewModel.NotifyPropertyChanged(propertyName);
                }
            }
        }

        #endregion
    }
}
