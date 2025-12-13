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

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using LoneEftDmaRadar.Tarkov;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot;
using LoneEftDmaRadar.UI.ColorPicker;
using LoneEftDmaRadar.UI.Misc;
using LoneEftDmaRadar.UI.Radar.Views;
using LoneEftDmaRadar.UI.Skia;

namespace LoneEftDmaRadar.UI.Radar.ViewModels
{
    public sealed class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly SettingsTab _parent;
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public SettingsViewModel(SettingsTab parent)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            RestartRadarCommand = new SimpleCommand(OnRestartRadar);
            OpenColorPickerCommand = new SimpleCommand(OnOpenColorPicker);
            BackupConfigCommand = new SimpleCommand(OnBackupConfig);
            OpenConfigCommand = new SimpleCommand(OnOpenConfig);
            SetContainersGlobalCommand = new SimpleCommand(OnSetContainersGlobal);
            SetContainerDistanceGlobalCommand = new SimpleCommand(OnSetContainerDistanceGlobal);
            SetScaleValues(UIScale);
        }

        #region General Settings

        public ICommand RestartRadarCommand { get; }
        private void OnRestartRadar() =>
            Memory.RestartRadar();

        private bool _colorPickerIsEnabled = true;
        public bool ColorPickerIsEnabled
        {
            get => _colorPickerIsEnabled;
            set
            {
                if (_colorPickerIsEnabled != value)
                {
                    _colorPickerIsEnabled = value;
                    OnPropertyChanged(nameof(ColorPickerIsEnabled));
                }
            }
        }
        public ICommand OpenColorPickerCommand { get; }
        private void OnOpenColorPicker()
        {
            ColorPickerIsEnabled = false;
            try
            {
                var wnd = new ColorPickerWindow()
                {
                    Owner = MainWindow.Instance
                };
                wnd.ShowDialog();
            }
            finally
            {
                ColorPickerIsEnabled = true;
            }
        }

        public ICommand BackupConfigCommand { get; }
        private async void OnBackupConfig()
        {
            try
            {
                var backupFile = Path.Combine(App.ConfigPath.FullName, $"{EftDmaConfig.Filename}.userbak");
                if (File.Exists(backupFile) &&
                    MessageBox.Show(MainWindow.Instance, "Overwrite backup?", "Backup Config", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;

                await File.WriteAllTextAsync(backupFile, JsonSerializer.Serialize(App.Config, new JsonSerializerOptions { WriteIndented = true }));
                MessageBox.Show(MainWindow.Instance, $"Backed up to {backupFile}", "Backup Config");
            }
            catch (Exception ex)
            {
                MessageBox.Show(MainWindow.Instance, $"Error: {ex.Message}", "Backup Config", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public ICommand OpenConfigCommand { get; }
        private async void OnOpenConfig()
        {
            try
            {
                Process.Start(new ProcessStartInfo(App.ConfigPath.FullName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(MainWindow.Instance, $"Error: {ex.Message}", "Save Config", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public int AimlineLength
        {
            get => App.Config.UI.AimLineLength;
            set
            {
                if (App.Config.UI.AimLineLength != value)
                {
                    App.Config.UI.AimLineLength = value;
                    OnPropertyChanged(nameof(AimlineLength));
                }
            }
        }

        public int TeammateAimlineLength
        {
            get => App.Config.UI.TeammateAimlineLength;
            set
            {
                if (App.Config.UI.TeammateAimlineLength != value)
                {
                    App.Config.UI.TeammateAimlineLength = value;
                    OnPropertyChanged(nameof(TeammateAimlineLength));
                }
            }
        }

        public int MaxDistance
        {
            get => (int)Math.Round(App.Config.UI.MaxDistance);
            set
            {
                if (App.Config.UI.MaxDistance != value)
                {
                    App.Config.UI.MaxDistance = value;
                    OnPropertyChanged(nameof(MaxDistance));
                }
            }
        }

        public float UIScale
        {
            get => App.Config.UI.UIScale;
            set
            {
                if (App.Config.UI.UIScale == value)
                    return;
                App.Config.UI.UIScale = value;
                SetScaleValues(value);
                OnPropertyChanged(nameof(UIScale));
            }
        }

        private static void SetScaleValues(float newScale)
        {
            // Update Widgets
            MainWindow.Instance?.Radar?.ViewModel?.AimviewWidget?.SetScaleFactor(newScale);
            MainWindow.Instance?.Radar?.ViewModel?.InfoWidget?.SetScaleFactor(newScale);

            #region UpdatePaints

            /// Outlines
            SKPaints.TextOutline.StrokeWidth = 2f * newScale;
            // Shape Outline is computed before usage due to different stroke widths

            SKPaints.PaintConnectorGroup.StrokeWidth = 2.25f * newScale;
            SKPaints.PaintMouseoverGroup.StrokeWidth = 1.66f * newScale;
            SKPaints.PaintLocalPlayer.StrokeWidth = 1.66f * newScale;
            SKPaints.PaintTeammate.StrokeWidth = 1.66f * newScale;
            SKPaints.PaintPMC.StrokeWidth = 1.66f * newScale;
            SKPaints.PaintWatchlist.StrokeWidth = 1.66f * newScale;
            SKPaints.PaintStreamer.StrokeWidth = 1.66f * newScale;
            SKPaints.PaintScav.StrokeWidth = 1.66f * newScale;
            SKPaints.PaintRaider.StrokeWidth = 1.66f * newScale;
            SKPaints.PaintBoss.StrokeWidth = 1.66f * newScale;
            SKPaints.PaintFocused.StrokeWidth = 1.66f * newScale;
            SKPaints.PaintPScav.StrokeWidth = 1.66f * newScale;
            SKPaints.PaintCorpse.StrokeWidth = 3 * newScale;
            SKPaints.PaintMeds.StrokeWidth = 3 * newScale;
            SKPaints.PaintFood.StrokeWidth = 3 * newScale;
            SKPaints.PaintBackpacks.StrokeWidth = 3 * newScale;
            SKPaints.PaintQuestItem.StrokeWidth = 3 * newScale;
            SKPaints.PaintWishlistItem.StrokeWidth = 3 * newScale;
            SKPaints.QuestHelperPaint.StrokeWidth = 3 * newScale;
            SKPaints.PaintDeathMarker.StrokeWidth = 3 * newScale;
            SKPaints.PaintLoot.StrokeWidth = 3 * newScale;
            SKPaints.PaintImportantLoot.StrokeWidth = 3 * newScale;
            SKPaints.PaintContainerLoot.StrokeWidth = 3 * newScale;
            SKPaints.PaintTransparentBacker.StrokeWidth = 1 * newScale;
            SKPaints.PaintExplosives.StrokeWidth = 3 * newScale;
            SKPaints.PaintExfilOpen.StrokeWidth = 1 * newScale;
            SKPaints.PaintExfilTransit.StrokeWidth = 1 * newScale;
            // Fonts
            SKFonts.UIRegular.Size = 12f * newScale;
            SKFonts.UILarge.Size = 48f * newScale;
            SKFonts.InfoWidgetFont.Size = 12f * newScale;
            // Loot Paints
            LootItem.ScaleLootPaints(newScale);

            #endregion
        }

        private bool _showMapSetupHelper;
        public bool ShowMapSetupHelper
        {
            get => _showMapSetupHelper;
            set
            {
                if (_showMapSetupHelper != value)
                {
                    _showMapSetupHelper = value;
                    if (MainWindow.Instance?.Radar?.MapSetupHelper?.ViewModel is MapSetupHelperViewModel vm)
                    {
                        vm.IsVisible = value;
                    }
                    OnPropertyChanged(nameof(ShowMapSetupHelper));
                }
            }
        }

        public bool AimviewWidget
        {
            get => App.Config.AimviewWidget.Enabled;
            set
            {
                if (App.Config.AimviewWidget.Enabled != value)
                {
                    App.Config.AimviewWidget.Enabled = value;
                    OnPropertyChanged(nameof(AimviewWidget));
                }
            }
        }

        public bool PlayerInfoWidget
        {
            get => App.Config.InfoWidget.Enabled;
            set
            {
                if (App.Config.InfoWidget.Enabled != value)
                {
                    App.Config.InfoWidget.Enabled = value;
                    OnPropertyChanged(nameof(PlayerInfoWidget));
                }
            }
        }

        public bool LootInfoWidget
        {
            get => App.Config.LootInfoWidget.Enabled;
            set
            {
                if (App.Config.LootInfoWidget.Enabled != value)
                {
                    App.Config.LootInfoWidget.Enabled = value;
                    OnPropertyChanged(nameof(LootInfoWidget));
                }
            }
        }

        public bool ConnectGroups
        {
            get => App.Config.UI.ConnectGroups;
            set
            {
                if (App.Config.UI.ConnectGroups != value)
                {
                    App.Config.UI.ConnectGroups = value;
                    OnPropertyChanged(nameof(ConnectGroups));
                }
            }
        }

        public bool HideNames
        {
            get => App.Config.UI.HideNames;
            set
            {
                if (App.Config.UI.HideNames != value)
                {
                    App.Config.UI.HideNames = value;
                    OnPropertyChanged(nameof(HideNames));
                }
            }
        }

        public bool ShowMines
        {
            get => App.Config.UI.ShowMines;
            set
            {
                if (App.Config.UI.ShowMines != value)
                {
                    App.Config.UI.ShowMines = value;
                    OnPropertyChanged(nameof(ShowMines));
                }
            }
        }

        public bool TeammateAimlines
        {
            get => App.Config.UI.TeammateAimlines;
            set
            {
                if (App.Config.UI.TeammateAimlines != value)
                {
                    App.Config.UI.TeammateAimlines = value;
                    OnPropertyChanged(nameof(TeammateAimlines));
                }
            }
        }

        public bool AIAimlines
        {
            get => App.Config.UI.AIAimlines;
            set
            {
                if (App.Config.UI.AIAimlines != value)
                {
                    App.Config.UI.AIAimlines = value;
                    OnPropertyChanged(nameof(AIAimlines));
                }
            }
        }

        public bool MarkSusPlayers
        {
            get => App.Config.UI.MarkSusPlayers;
            set
            {
                if (App.Config.UI.MarkSusPlayers != value)
                {
                    App.Config.UI.MarkSusPlayers = value;
                    OnPropertyChanged(nameof(MarkSusPlayers));
                }
            }
        }
        
        public bool ShowESP
        {
            get => UI.ESP.ESPWindow.ShowESP;
            set
            {
                if (UI.ESP.ESPWindow.ShowESP != value)
                {
                    if (value)
                        UI.ESP.ESPManager.ShowESP();
                    else
                        UI.ESP.ESPManager.HideESP();
                    
                    OnPropertyChanged(nameof(ShowESP));
                }
            }
        }

        public bool ShowLoot
        {
            get => App.Config.Loot.Enabled;
            set
            {
                if (App.Config.Loot.Enabled != value)
                {
                    App.Config.Loot.Enabled = value;
                    if (MainWindow.Instance?.Radar?.Overlay?.ViewModel is RadarOverlayViewModel vm)
                    {
                        vm.IsLootButtonVisible = value;
                    }
                    OnPropertyChanged(nameof(ShowLoot));
                }
            }
        }

        public bool EspQuestLoot
        {
            get => App.Config.UI.EspQuestLoot;
            set
            {
                if (App.Config.UI.EspQuestLoot != value)
                {
                    App.Config.UI.EspQuestLoot = value;
                    OnPropertyChanged(nameof(EspQuestLoot));
                }
            }
        }

        public int RadarMaxFPS
        {
            get => App.Config.UI.RadarMaxFPS;
            set
            {
                if (App.Config.UI.RadarMaxFPS != value)
                {
                    App.Config.UI.RadarMaxFPS = value;
                    OnPropertyChanged(nameof(RadarMaxFPS));
                }
            }
        }

        #endregion

        #region Loot

        public bool ShowStaticContainers
        {
            get => App.Config.Containers.Enabled;
            set
            {
                if (App.Config.Containers.Enabled != value)
                {
                    App.Config.Containers.Enabled = value;
                    OnPropertyChanged(nameof(ShowStaticContainers));
                }
            }
        }

        public ICommand SetContainersGlobalCommand { get; }
        private void OnSetContainersGlobal()
        {
            // Set current Radar value to Aimview and ESP
            bool value = App.Config.Containers.Enabled;
            
            // Update Aimview (same ViewModel - direct property update)
            App.Config.AimviewWidget.ShowContainers = value;
            OnPropertyChanged(nameof(AimviewShowContainers));
            
            // Update ESP (different ViewModel - need to notify)
            App.Config.UI.EspContainers = value;
            MainWindow.Instance?.EspSettings?.ViewModel?.NotifyPropertyChanged(nameof(EspSettingsViewModel.EspContainers));
        }

        public int ContainerDistance
        {
            get => (int)Math.Round(App.Config.Containers.DrawDistance);
            set
            {
                if (App.Config.Containers.DrawDistance != value)
                {
                    App.Config.Containers.DrawDistance = value;
                    OnPropertyChanged(nameof(ContainerDistance));
                }
            }
        }

        public ICommand SetContainerDistanceGlobalCommand { get; }
        private void OnSetContainerDistanceGlobal()
        {
            // Set current Radar Container Distance to Aimview and ESP
            float value = App.Config.Containers.DrawDistance;
            
            // Update Aimview (same ViewModel - direct property update)
            App.Config.AimviewWidget.ContainerDistance = value;
            OnPropertyChanged(nameof(AimviewContainerDistance));
            
            // Update ESP (different ViewModel - need to notify)
            App.Config.Containers.EspDrawDistance = value;
            MainWindow.Instance?.EspSettings?.ViewModel?.NotifyPropertyChanged(nameof(EspSettingsViewModel.EspContainerDistance));
        }

        public bool ShowCorpseMarkers
        {
            get => App.Config.Loot.ShowCorpseMarkers;
            set
            {
                if (App.Config.Loot.ShowCorpseMarkers != value)
                {
                    App.Config.Loot.ShowCorpseMarkers = value;
                    OnPropertyChanged(nameof(ShowCorpseMarkers));
                }
            }
        }

        #endregion

        #region Aimview ESP

        public bool AimviewShowLoot
        {
            get => App.Config.AimviewWidget.ShowLoot;
            set
            {
                if (App.Config.AimviewWidget.ShowLoot != value)
                {
                    App.Config.AimviewWidget.ShowLoot = value;
                    OnPropertyChanged(nameof(AimviewShowLoot));
                }
            }
        }

        public bool AimviewShowQuestItems
        {
            get => App.Config.AimviewWidget.ShowQuestItems;
            set
            {
                if (App.Config.AimviewWidget.ShowQuestItems != value)
                {
                    App.Config.AimviewWidget.ShowQuestItems = value;
                    OnPropertyChanged(nameof(AimviewShowQuestItems));
                }
            }
        }

        public bool AimviewShowAI
        {
            get => App.Config.AimviewWidget.ShowAI;
            set
            {
                if (App.Config.AimviewWidget.ShowAI != value)
                {
                    App.Config.AimviewWidget.ShowAI = value;
                    OnPropertyChanged(nameof(AimviewShowAI));
                }
            }
        }

        public bool AimviewShowEnemyPlayers
        {
            get => App.Config.AimviewWidget.ShowEnemyPlayers;
            set
            {
                if (App.Config.AimviewWidget.ShowEnemyPlayers != value)
                {
                    App.Config.AimviewWidget.ShowEnemyPlayers = value;
                    OnPropertyChanged(nameof(AimviewShowEnemyPlayers));
                }
            }
        }

        public bool AimviewShowWishlisted
        {
            get => App.Config.AimviewWidget.ShowWishlisted;
            set
            {
                if (App.Config.AimviewWidget.ShowWishlisted != value)
                {
                    App.Config.AimviewWidget.ShowWishlisted = value;
                    OnPropertyChanged(nameof(AimviewShowWishlisted));
                }
            }
        }

        public bool AimviewShowContainers
        {
            get => App.Config.AimviewWidget.ShowContainers;
            set
            {
                if (App.Config.AimviewWidget.ShowContainers != value)
                {
                    App.Config.AimviewWidget.ShowContainers = value;
                    OnPropertyChanged(nameof(AimviewShowContainers));
                }
            }
        }

        public float AimviewLootRenderDistance
        {
            get => App.Config.UI.AimviewLootRenderDistance;
            set
            {
                if (Math.Abs(App.Config.UI.AimviewLootRenderDistance - value) > float.Epsilon)
                {
                    App.Config.UI.AimviewLootRenderDistance = value;
                    OnPropertyChanged(nameof(AimviewLootRenderDistance));
                }
            }
        }

        public float AimviewContainerDistance
        {
            get => App.Config.AimviewWidget.ContainerDistance;
            set
            {
                if (Math.Abs(App.Config.AimviewWidget.ContainerDistance - value) > float.Epsilon)
                {
                    App.Config.AimviewWidget.ContainerDistance = value;
                    OnPropertyChanged(nameof(AimviewContainerDistance));
                }
            }
        }

        public bool AimviewShowHeadCircle
        {
            get => App.Config.AimviewWidget.ShowHeadCircle;
            set
            {
                if (App.Config.AimviewWidget.ShowHeadCircle != value)
                {
                    App.Config.AimviewWidget.ShowHeadCircle = value;
                    OnPropertyChanged(nameof(AimviewShowHeadCircle));
                }
            }
        }

        public bool AimviewShowExfils
        {
            get => App.Config.AimviewWidget.ShowExfils;
            set
            {
                if (App.Config.AimviewWidget.ShowExfils != value)
                {
                    App.Config.AimviewWidget.ShowExfils = value;
                    OnPropertyChanged(nameof(AimviewShowExfils));
                }
            }
        }

        #endregion

    }
}
