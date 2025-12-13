using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using LoneEftDmaRadar;
using LoneEftDmaRadar.UI.ESP;
using LoneEftDmaRadar.UI.Misc;

namespace LoneEftDmaRadar.UI.Radar.ViewModels
{
    public class EspSettingsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public EspSettingsViewModel()
        {
            ToggleEspCommand = new SimpleCommand(() =>
            {
                ESPManager.ToggleESP();
            });

            StartEspCommand = new SimpleCommand(() =>
            {
                ESPManager.StartESP();
            });

            // Populate available screens
            RefreshAvailableScreens();
        }

        private void RefreshAvailableScreens()
        {
            AvailableScreens.Clear();

            var monitors = MonitorInfo.GetAllMonitors();
            foreach (var monitor in monitors.OrderBy(m => m.Index))
            {
                AvailableScreens.Add(monitor);
            }

            // Ensure a valid selection exists
            if (!AvailableScreens.Any())
            {
                var fallback = new MonitorInfo
                {
                    Index = 0,
                    Name = "Primary Display",
                    Width = (int)SystemParameters.PrimaryScreenWidth,
                    Height = (int)SystemParameters.PrimaryScreenHeight,
                    Left = 0,
                    Top = 0,
                    IsPrimary = true
                };
                AvailableScreens.Add(fallback);
            }

            if (!AvailableScreens.Any(m => m.Index == App.Config.UI.EspTargetScreen))
            {
                var primary = AvailableScreens.FirstOrDefault(m => m.IsPrimary) ?? AvailableScreens.First();
                App.Config.UI.EspTargetScreen = primary.Index;
                OnPropertyChanged(nameof(EspTargetScreen));
            }
        }

        public ObservableCollection<MonitorInfo> AvailableScreens { get; } = new ObservableCollection<MonitorInfo>();

        public ICommand ToggleEspCommand { get; }
        public ICommand StartEspCommand { get; }

        public bool ShowESP
        {
            get => App.Config.UI.ShowESP;
            set
            {
                if (App.Config.UI.ShowESP != value)
                {
                    App.Config.UI.ShowESP = value;
                    if (value) ESPManager.ShowESP(); else ESPManager.HideESP();
                    OnPropertyChanged();
                }
            }
        }

        public bool EspPlayerSkeletons
        {
            get => App.Config.UI.EspPlayerSkeletons;
            set
            {
                if (App.Config.UI.EspPlayerSkeletons != value)
                {
                    App.Config.UI.EspPlayerSkeletons = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspPlayerBoxes
        {
            get => App.Config.UI.EspPlayerBoxes;
            set
            {
                if (App.Config.UI.EspPlayerBoxes != value)
                {
                    App.Config.UI.EspPlayerBoxes = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspPlayerNames
        {
            get => App.Config.UI.EspPlayerNames;
            set
            {
                if (App.Config.UI.EspPlayerNames != value)
                {
                    App.Config.UI.EspPlayerNames = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspGroupIds
        {
            get => App.Config.UI.EspGroupIds;
            set
            {
                if (App.Config.UI.EspGroupIds != value)
                {
                    App.Config.UI.EspGroupIds = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspGroupColors
        {
            get => App.Config.UI.EspGroupColors;
            set
            {
                if (App.Config.UI.EspGroupColors != value)
                {
                    App.Config.UI.EspGroupColors = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspFactionColors
        {
            get => App.Config.UI.EspFactionColors;
            set
            {
                if (App.Config.UI.EspFactionColors != value)
                {
                    App.Config.UI.EspFactionColors = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspPlayerFaction
        {
            get => App.Config.UI.EspPlayerFaction;
            set
            {
                if (App.Config.UI.EspPlayerFaction != value)
                {
                    App.Config.UI.EspPlayerFaction = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspPlayerHealth
        {
            get => App.Config.UI.EspPlayerHealth;
            set
            {
                if (App.Config.UI.EspPlayerHealth != value)
                {
                    App.Config.UI.EspPlayerHealth = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspPlayerDistance
        {
            get => App.Config.UI.EspPlayerDistance;
            set
            {
                if (App.Config.UI.EspPlayerDistance != value)
                {
                    App.Config.UI.EspPlayerDistance = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspAISkeletons
        {
            get => App.Config.UI.EspAISkeletons;
            set
            {
                if (App.Config.UI.EspAISkeletons != value)
                {
                    App.Config.UI.EspAISkeletons = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspAIBoxes
        {
            get => App.Config.UI.EspAIBoxes;
            set
            {
                if (App.Config.UI.EspAIBoxes != value)
                {
                    App.Config.UI.EspAIBoxes = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspAINames
        {
            get => App.Config.UI.EspAINames;
            set
            {
                if (App.Config.UI.EspAINames != value)
                {
                    App.Config.UI.EspAINames = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspAIGroupIds
        {
            get => App.Config.UI.EspAIGroupIds;
            set
            {
                if (App.Config.UI.EspAIGroupIds != value)
                {
                    App.Config.UI.EspAIGroupIds = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspAIHealth
        {
            get => App.Config.UI.EspAIHealth;
            set
            {
                if (App.Config.UI.EspAIHealth != value)
                {
                    App.Config.UI.EspAIHealth = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspAIDistance
        {
            get => App.Config.UI.EspAIDistance;
            set
            {
                if (App.Config.UI.EspAIDistance != value)
                {
                    App.Config.UI.EspAIDistance = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public bool EspLoot
        {
            get => App.Config.UI.EspLoot;
            set
            {
                if (App.Config.UI.EspLoot != value)
                {
                    App.Config.UI.EspLoot = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspLootPrice
        {
            get => App.Config.UI.EspLootPrice;
            set
            {
                if (App.Config.UI.EspLootPrice != value)
                {
                    App.Config.UI.EspLootPrice = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspLootFilterOnly
        {
            get => App.Config.UI.EspLootFilterOnly;
            set
            {
                if (App.Config.UI.EspLootFilterOnly != value)
                {
                    App.Config.UI.EspLootFilterOnly = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspLootConeEnabled
        {
            get => App.Config.UI.EspLootConeEnabled;
            set
            {
                if (App.Config.UI.EspLootConeEnabled != value)
                {
                    App.Config.UI.EspLootConeEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public float EspLootConeAngle
        {
            get => App.Config.UI.EspLootConeAngle;
            set
            {
                if (Math.Abs(App.Config.UI.EspLootConeAngle - value) > float.Epsilon)
                {
                    App.Config.UI.EspLootConeAngle = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspFood
        {
            get => App.Config.UI.EspFood;
            set
            {
                if (App.Config.UI.EspFood != value)
                {
                    App.Config.UI.EspFood = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspMeds
        {
            get => App.Config.UI.EspMeds;
            set
            {
                if (App.Config.UI.EspMeds != value)
                {
                    App.Config.UI.EspMeds = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspBackpacks
        {
            get => App.Config.UI.EspBackpacks;
            set
            {
                if (App.Config.UI.EspBackpacks != value)
                {
                    App.Config.UI.EspBackpacks = value;
                    OnPropertyChanged();
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
                    OnPropertyChanged();
                }
            }
        }

        public bool EspShowWishlisted
        {
            get => App.Config.UI.EspShowWishlisted;
            set
            {
                if (App.Config.UI.EspShowWishlisted != value)
                {
                    App.Config.UI.EspShowWishlisted = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspCorpses
        {
            get => App.Config.UI.EspCorpses;
            set
            {
                if (App.Config.UI.EspCorpses != value)
                {
                    App.Config.UI.EspCorpses = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspContainers
        {
            get => App.Config.UI.EspContainers;
            set
            {
                if (App.Config.UI.EspContainers != value)
                {
                    App.Config.UI.EspContainers = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspExfils
        {
            get => App.Config.UI.EspExfils;
            set
            {
                if (App.Config.UI.EspExfils != value)
                {
                    App.Config.UI.EspExfils = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspTripwires
        {
            get => App.Config.UI.EspTripwires;
            set
            {
                if (App.Config.UI.EspTripwires != value)
                {
                    App.Config.UI.EspTripwires = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspGrenades
        {
            get => App.Config.UI.EspGrenades;
            set
            {
                if (App.Config.UI.EspGrenades != value)
                {
                    App.Config.UI.EspGrenades = value;
                    OnPropertyChanged();
                }
            }
        }

        public Array LabelPositions { get; } = Enum.GetValues(typeof(EspLabelPosition));

        public EspLabelPosition EspLabelPosition
        {
            get => App.Config.UI.EspLabelPosition;
            set
            {
                if (App.Config.UI.EspLabelPosition != value)
                {
                    App.Config.UI.EspLabelPosition = value;
                    OnPropertyChanged();
                }
            }
        }

        public EspLabelPosition EspLabelPositionAI
        {
            get => App.Config.UI.EspLabelPositionAI;
            set
            {
                if (App.Config.UI.EspLabelPositionAI != value)
                {
                    App.Config.UI.EspLabelPositionAI = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspHeadCirclePlayers
        {
            get => App.Config.UI.EspHeadCirclePlayers;
            set
            {
                if (App.Config.UI.EspHeadCirclePlayers != value)
                {
                    App.Config.UI.EspHeadCirclePlayers = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspHeadCircleAI
        {
            get => App.Config.UI.EspHeadCircleAI;
            set
            {
                if (App.Config.UI.EspHeadCircleAI != value)
                {
                    App.Config.UI.EspHeadCircleAI = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspCrosshair
        {
            get => App.Config.UI.EspCrosshair;
            set
            {
                if (App.Config.UI.EspCrosshair != value)
                {
                    App.Config.UI.EspCrosshair = value;
                    OnPropertyChanged();
                }
            }
        }

        public float EspCrosshairLength
        {
            get => App.Config.UI.EspCrosshairLength;
            set
            {
                if (Math.Abs(App.Config.UI.EspCrosshairLength - value) > float.Epsilon)
                {
                    App.Config.UI.EspCrosshairLength = value;
                    OnPropertyChanged();
                }
            }
        }

        #region Colors
        public string EspColorPlayers
        {
            get => App.Config.UI.EspColorPlayers;
            set { App.Config.UI.EspColorPlayers = value; OnPropertyChanged(); }
        }

        public string EspColorAI
        {
            get => App.Config.UI.EspColorAI;
            set { App.Config.UI.EspColorAI = value; OnPropertyChanged(); }
        }

        public string EspColorPlayerScavs
        {
            get => App.Config.UI.EspColorPlayerScavs;
            set { App.Config.UI.EspColorPlayerScavs = value; OnPropertyChanged(); }
        }

        public string EspColorRaiders
        {
            get => App.Config.UI.EspColorRaiders;
            set { App.Config.UI.EspColorRaiders = value; OnPropertyChanged(); }
        }

        public string EspColorBosses
        {
            get => App.Config.UI.EspColorBosses;
            set { App.Config.UI.EspColorBosses = value; OnPropertyChanged(); }
        }

        public string EspColorLoot
        {
            get => App.Config.UI.EspColorLoot;
            set { App.Config.UI.EspColorLoot = value; OnPropertyChanged(); }
        }

        public string EspColorContainers
        {
            get => App.Config.UI.EspColorContainers;
            set { App.Config.UI.EspColorContainers = value; OnPropertyChanged(); }
        }

        public string EspColorExfil
        {
            get => App.Config.UI.EspColorExfil;
            set { App.Config.UI.EspColorExfil = value; OnPropertyChanged(); }
        }

        public string EspColorTripwire
        {
            get => App.Config.UI.EspColorTripwire;
            set { App.Config.UI.EspColorTripwire = value; OnPropertyChanged(); }
        }

        public string EspColorGrenade
        {
            get => App.Config.UI.EspColorGrenade;
            set { App.Config.UI.EspColorGrenade = value; OnPropertyChanged(); }
        }

        public string EspColorCrosshair
        {
            get => App.Config.UI.EspColorCrosshair;
            set { App.Config.UI.EspColorCrosshair = value; OnPropertyChanged(); }
        }

        public string EspColorFactionBear
        {
            get => App.Config.UI.EspColorFactionBear;
            set { App.Config.UI.EspColorFactionBear = value; OnPropertyChanged(); }
        }

        public string EspColorFactionUsec
        {
            get => App.Config.UI.EspColorFactionUsec;
            set { App.Config.UI.EspColorFactionUsec = value; OnPropertyChanged(); }
        }
        #endregion

        public int EspScreenWidth
        {
            get => App.Config.UI.EspScreenWidth;
            set
            {
                if (App.Config.UI.EspScreenWidth != value)
                {
                    App.Config.UI.EspScreenWidth = value;
                    ESPManager.ApplyResolutionOverride();
                    OnPropertyChanged();
                }
            }
        }

        public int EspScreenHeight
        {
            get => App.Config.UI.EspScreenHeight;
            set
            {
                if (App.Config.UI.EspScreenHeight != value)
                {
                    App.Config.UI.EspScreenHeight = value;
                    ESPManager.ApplyResolutionOverride();
                    OnPropertyChanged();
                }
            }
        }

        public int EspMaxFPS
        {
            get => App.Config.UI.EspMaxFPS;
            set
            {
                if (App.Config.UI.EspMaxFPS != value)
                {
                    App.Config.UI.EspMaxFPS = value;
                    OnPropertyChanged();
                }
            }
        }

        public float EspPlayerMaxDistance
        {
            get => App.Config.UI.EspPlayerMaxDistance;
            set
            {
                if (Math.Abs(App.Config.UI.EspPlayerMaxDistance - value) > float.Epsilon)
                {
                    App.Config.UI.EspPlayerMaxDistance = value;
                    OnPropertyChanged();
                }
            }
        }

        public float EspAIMaxDistance
        {
            get => App.Config.UI.EspAIMaxDistance;
            set
            {
                if (Math.Abs(App.Config.UI.EspAIMaxDistance - value) > float.Epsilon)
                {
                    App.Config.UI.EspAIMaxDistance = value;
                    OnPropertyChanged();
                }
            }
        }

        public float EspLootMaxDistance
        {
            get => App.Config.UI.EspLootMaxDistance;
            set
            {
                if (Math.Abs(App.Config.UI.EspLootMaxDistance - value) > float.Epsilon)
                {
                    App.Config.UI.EspLootMaxDistance = value;
                    OnPropertyChanged();
                }
            }
        }

        public float EspContainerDistance
        {
            get => App.Config.Containers.EspDrawDistance;
            set
            {
                if (Math.Abs(App.Config.Containers.EspDrawDistance - value) > float.Epsilon)
                {
                    App.Config.Containers.EspDrawDistance = value;
                    OnPropertyChanged();
                }
            }
        }

        public string EspFontFamily
        {
            get => App.Config.UI.EspFontFamily;
            set
            {
                var newVal = value ?? string.Empty;
                if (!string.Equals(App.Config.UI.EspFontFamily, newVal, StringComparison.Ordinal))
                {
                    App.Config.UI.EspFontFamily = newVal;
                    ESPManager.ApplyFontConfig();
                    OnPropertyChanged();
                }
            }
        }

        public int EspFontSizeSmall
        {
            get => App.Config.UI.EspFontSizeSmall;
            set
            {
                if (App.Config.UI.EspFontSizeSmall != value)
                {
                    App.Config.UI.EspFontSizeSmall = value;
                    ESPManager.ApplyFontConfig();
                    OnPropertyChanged();
                }
            }
        }

        public int EspFontSizeMedium
        {
            get => App.Config.UI.EspFontSizeMedium;
            set
            {
                if (App.Config.UI.EspFontSizeMedium != value)
                {
                    App.Config.UI.EspFontSizeMedium = value;
                    ESPManager.ApplyFontConfig();
                    OnPropertyChanged();
                }
            }
        }

        public int EspFontSizeLarge
        {
            get => App.Config.UI.EspFontSizeLarge;
            set
            {
                if (App.Config.UI.EspFontSizeLarge != value)
                {
                    App.Config.UI.EspFontSizeLarge = value;
                    ESPManager.ApplyFontConfig();
                    OnPropertyChanged();
                }
            }
        }

        public int EspTargetScreen
        {
            get => App.Config.UI.EspTargetScreen;
            set
            {
                if (App.Config.UI.EspTargetScreen != value)
                {
                    App.Config.UI.EspTargetScreen = value;
                    ESPManager.ApplyResolutionOverride();
                    OnPropertyChanged();
                }
            }
        }

        public bool EspNearestPlayerInfo
        {
            get => App.Config.UI.EspNearestPlayerInfo;
            set
            {
                if (App.Config.UI.EspNearestPlayerInfo != value)
                {
                    App.Config.UI.EspNearestPlayerInfo = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EspLootDebug
        {
            get => App.Config.UI.EspLootDebug;
            set
            {
                if (App.Config.UI.EspLootDebug != value)
                {
                    App.Config.UI.EspLootDebug = value;
                    App.Config.Save();
                    OnPropertyChanged();
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Public method to notify property changes from external sources (e.g., Set Global buttons).
        /// </summary>
        public void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
