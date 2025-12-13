/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.UI.Misc;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LoneEftDmaRadar.DMA;
using VmmSharpEx;
using System.Threading.Tasks;

namespace LoneEftDmaRadar.UI.Radar.ViewModels
{
    public sealed class DeviceAimbotViewModel : INotifyPropertyChanged
    {
        private bool _isConnected;
        private string _deviceVersion = "Not Connected";
        private List<Device.SerialDeviceInfo> _availableDevices;
        private Device.SerialDeviceInfo _selectedDevice;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand RefreshDevicesCommand { get; }
        public ICommand TestMoveCommand { get; }

        // Connection Status
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                _isConnected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectionStatus));
            }
        }

        public string ConnectionStatus => IsConnected ? "Connected" : "Disconnected";

        public string DeviceVersion
        {
            get => _deviceVersion;
            set
            {
                _deviceVersion = value;
                OnPropertyChanged();
            }
        }

        public List<Device.SerialDeviceInfo> AvailableDevices
        {
            get => _availableDevices;
            set
            {
                _availableDevices = value;
                OnPropertyChanged();
            }
        }

        public Device.SerialDeviceInfo SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                _selectedDevice = value;
                OnPropertyChanged();
            }
        }

        // KMBox NET
        public bool UseKmBoxNet
        {
            get => App.Config.Device.UseKmBoxNet;
            set { App.Config.Device.UseKmBoxNet = value; OnPropertyChanged(); }
        }

        public string KmBoxNetIp
        {
            get => App.Config.Device.KmBoxNetIp;
            set { App.Config.Device.KmBoxNetIp = value; OnPropertyChanged(); }
        }

        public int KmBoxNetPort
        {
            get => App.Config.Device.KmBoxNetPort;
            set { App.Config.Device.KmBoxNetPort = value; OnPropertyChanged(); }
        }

        public string KmBoxNetMac
        {
            get => App.Config.Device.KmBoxNetMac;
            set { App.Config.Device.KmBoxNetMac = value; OnPropertyChanged(); }
        }

        // Settings
        public bool AutoConnect
        {
            get => App.Config.Device.AutoConnect;
            set { App.Config.Device.AutoConnect = value; OnPropertyChanged(); }
        }

        public float Smoothing
        {
            get => App.Config.Device.Smoothing;
            set { App.Config.Device.Smoothing = value; OnPropertyChanged(); }
        }

        public bool Enabled
        {
            get => App.Config.Device.Enabled;
            set { App.Config.Device.Enabled = value; OnPropertyChanged(); }
        }

        public List<Bones> AvailableBones { get; } = new List<Bones>
        {
            Bones.HumanHead,
            Bones.HumanNeck,
            Bones.HumanSpine3,
            Bones.HumanSpine2,
            Bones.HumanPelvis,
            Bones.Closest
        };

        public Bones TargetBone
        {
            get => App.Config.Device.TargetBone;
            set { App.Config.Device.TargetBone = value; OnPropertyChanged(); }
        }

        public bool MemWritesEnabled
        {
            get => App.Config.MemWrites.Enabled;
            set 
            { 
                // Show warning when enabling
                if (value && !App.Config.MemWrites.Enabled)
                {
                    var result = System.Windows.MessageBox.Show(
                        "?? WARNING ??\n\n" +
                        "Memory writes directly modify game memory and are HIGHLY DETECTABLE.\n\n" +
                        "This includes features like:\n" +
                        "  ? No Recoil\n" +
                        "  ? No Sway\n" +
                        "  ? Other memory modifications\n\n" +
                        "Using memory writes significantly increases your risk of detection and account ban.\n\n" +
                        "ARE YOU SURE YOU WANT TO ENABLE MEMORY WRITES?",
                        "?? CRITICAL WARNING - Memory Writes ??",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning,
                        System.Windows.MessageBoxResult.No);
        
                    if (result != System.Windows.MessageBoxResult.Yes)
                    {
                        OnPropertyChanged(); // Refresh UI to uncheck
                        return;
                    }
                }
        
                App.Config.MemWrites.Enabled = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsMemWritesEnabled)); // Update dependent UI
                
                // Log the change
                DebugLogger.LogDebug($"[DeviceAimbot] MemWrites {(value ? "ENABLED" : "DISABLED")}");
            }
        }
        
        // Helper property for UI binding
        public bool IsMemWritesEnabled => App.Config.MemWrites.Enabled;

        public float FOV
        {
            get => App.Config.Device.FOV;
            set { App.Config.Device.FOV = value; OnPropertyChanged(); }
        }

        public float MaxDistance
        {
            get => App.Config.Device.MaxDistance;
            set { App.Config.Device.MaxDistance = value; OnPropertyChanged(); }
        }

        public int TargetingMode
        {
            get => (int)App.Config.Device.Targeting;
            set { App.Config.Device.Targeting = (DeviceAimbotConfig.TargetingMode)value; OnPropertyChanged(); }
        }

        public bool ShowDebug
        {
            get => App.Config.Device.ShowDebug;
            set { App.Config.Device.ShowDebug = value; OnPropertyChanged(); }
        }

        public bool EnablePrediction
        {
            get => App.Config.Device.EnablePrediction;
            set { App.Config.Device.EnablePrediction = value; OnPropertyChanged(); }
        }

        public bool NoRecoilEnabled
        {
            get => App.Config.MemWrites.NoRecoilEnabled;
            set { App.Config.MemWrites.NoRecoilEnabled = value; OnPropertyChanged(); }
        }

        public float NoRecoilAmount
        {
            get => App.Config.MemWrites.NoRecoilAmount;
            set { App.Config.MemWrites.NoRecoilAmount = value; OnPropertyChanged(); }
        }

        public float NoSwayAmount
        {
            get => App.Config.MemWrites.NoSwayAmount;
            set { App.Config.MemWrites.NoSwayAmount = value; OnPropertyChanged(); }
        }        

        /// <summary>
        /// True while Device Aimbot is actively engaged (aim-key/ hotkey).
        /// </summary>
        public bool IsEngaged
        {
            get => MemDMA.DeviceAimbot?.IsEngaged ?? false;
            set
            {
                if (MemDMA.DeviceAimbot != null)
                {
                    MemDMA.DeviceAimbot.IsEngaged = value;
                    OnPropertyChanged();
                }
            }
        }

        // Target Filters
        public bool TargetPMC
        {
            get => App.Config.Device.TargetPMC;
            set { App.Config.Device.TargetPMC = value; OnPropertyChanged(); }
        }

        public bool TargetPlayerScav
        {
            get => App.Config.Device.TargetPlayerScav;
            set { App.Config.Device.TargetPlayerScav = value; OnPropertyChanged(); }
        }

        public bool TargetAIScav
        {
            get => App.Config.Device.TargetAIScav;
            set { App.Config.Device.TargetAIScav = value; OnPropertyChanged(); }
        }

        public bool TargetBoss
        {
            get => App.Config.Device.TargetBoss;
            set { App.Config.Device.TargetBoss = value; OnPropertyChanged(); }
        }

        public bool TargetRaider
        {
            get => App.Config.Device.TargetRaider;
            set { App.Config.Device.TargetRaider = value; OnPropertyChanged(); }
        }

        // FOV Circle Display
        public bool ShowFovCircle
        {
            get => App.Config.Device.ShowFovCircle;
            set { App.Config.Device.ShowFovCircle = value; OnPropertyChanged(); }
        }

        public string FovCircleColorEngaged
        {
            get => App.Config.Device.FovCircleColorEngaged;
            set { App.Config.Device.FovCircleColorEngaged = value; OnPropertyChanged(); }
        }

        public string FovCircleColorIdle
        {
            get => App.Config.Device.FovCircleColorIdle;
            set { App.Config.Device.FovCircleColorIdle = value; OnPropertyChanged(); }
        }

        private bool _isTesting;
        public bool IsTesting
        {
            get => _isTesting;
            set
            {
                _isTesting = value;
                OnPropertyChanged();
            }
        }

        public DeviceAimbotViewModel()
        {
            ConnectCommand = new SimpleCommand(Connect);
            DisconnectCommand = new SimpleCommand(Disconnect);
            RefreshDevicesCommand = new SimpleCommand(RefreshDevices);
            TestMoveCommand = new SimpleCommand(TestMove);

            RefreshDevices();

            // Auto-connect if enabled (Device Aimbot VID/PID-based)
            if (App.Config.Device.AutoConnect)
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    System.Threading.Thread.Sleep(1000);
                    if (!App.Config.Device.UseKmBoxNet && Device.TryAutoConnect(App.Config.Device.LastComPort))
                    {
                        UpdateConnectionStatus();
                        if (!string.IsNullOrWhiteSpace(App.Config.Device.LastComPort))
                            return;

                        try
                        {
                            // If we found a port through detection, remember it.
                            App.Config.Device.LastComPort = Device.CurrentPortName;
                        }
                        catch { /* ignore */ }
                    }
                    else if (App.Config.Device.UseKmBoxNet)
                    {
                        DeviceNetController.ConnectAsync(App.Config.Device.KmBoxNetIp, App.Config.Device.KmBoxNetPort, App.Config.Device.KmBoxNetMac)
                            .ContinueWith(t =>
                            {
                                if (t.Result)
                                {
                                    UpdateConnectionStatus();
                                }
                            });
                    }
                });
            }
        }

        private void RefreshDevices()
        {
            try
            {
                AvailableDevices = Device.EnumerateSerialDevices();
                
                // Try to select last used device
                if (!string.IsNullOrEmpty(App.Config.Device.LastComPort))
                {
                    SelectedDevice = AvailableDevices.FirstOrDefault(d => d.Port == App.Config.Device.LastComPort)
                                     ?? AvailableDevices.FirstOrDefault();
                }
                else
                {
                    SelectedDevice = AvailableDevices.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"Error refreshing devices: {ex}");
            }
        }

        private async void Connect()
        {
            try
            {
                if (App.Config.Device.UseKmBoxNet)
                {
                    bool okNet = await DeviceNetController.ConnectAsync(App.Config.Device.KmBoxNetIp, App.Config.Device.KmBoxNetPort, App.Config.Device.KmBoxNetMac);
                    UpdateConnectionStatus();
                    if (!okNet)
                    {
                        System.Windows.MessageBox.Show(
                            "Failed to connect to KMBox NET.",
                            "KMBox NET",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                    }
                    return;
                }

                if (SelectedDevice == null)
                {
                    System.Windows.MessageBox.Show(
                        "Please select a device first.",
                        "Device Aimbot",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // Auto device-type connect:
                // 1) Try Device Aimbot (4M baud + change_cmd + km.DeviceAimbot)
                // 2) Fall back to generic km.* device (KMBox/CH340 at 115200)
                bool ok = Device.ConnectAuto(SelectedDevice.Port);

                if (ok && Device.connected)
                {
                    App.Config.Device.LastComPort = SelectedDevice.Port;
                    UpdateConnectionStatus();
                }
                else
                {
                    UpdateConnectionStatus(); // make sure UI shows disconnected
                    System.Windows.MessageBox.Show(
                        "Failed to connect to device.\n\n" +
                        "If you are using a KMBox / CH340-based device, make sure it is in km.* mode.",
                        "Device Aimbot / KM Device",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Connection error: {ex.Message}",
                    "Device Aimbot",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void Disconnect()
        {
            try
            {
                if (App.Config.Device.UseKmBoxNet)
                {
                    DeviceNetController.Disconnect();
                }

                Device.disconnect();
                UpdateConnectionStatus();
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"Disconnect error: {ex}");
            }
        }

        private void UpdateConnectionStatus()
        {
            IsConnected = Device.connected || DeviceNetController.Connected;

            if (DeviceNetController.Connected)
            {
                DeviceVersion = "KMBox NET";
            }
            else if (Device.connected)
            {
                var kind = Device.DeviceKind.ToString();
                if (string.IsNullOrWhiteSpace(Device.version))
                {
                    DeviceVersion = $"Connected ({kind})";
                }
                else
                {
                    DeviceVersion = $"{Device.version} ({kind})";
                }
            }
            else
            {
                DeviceVersion = "Not Connected";
            }
        }

        private async void TestMove()
        {
            if (!Device.connected && !DeviceNetController.Connected)
            {
                System.Windows.MessageBox.Show(
                    "Please connect to a device first.",
                    "Test Move",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (IsTesting)
            {
                System.Windows.MessageBox.Show(
                    "Test already in progress!",
                    "Test Move",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            IsTesting = true;

            try
            {
                // Run test pattern in background
                await System.Threading.Tasks.Task.Run(() =>
                {
                    // Test pattern: small square
                    int step = 50;  // pixels
                    int delay = 200; // ms

                    DebugLogger.LogDebug("[DeviceAimbotTest] Starting movement test");

                    // Right
                    SendTestMove(step, 0);
                    System.Threading.Thread.Sleep(delay);

                    // Down
                    SendTestMove(0, step);
                    System.Threading.Thread.Sleep(delay);

                    // Left
                    SendTestMove(-step, 0);
                    System.Threading.Thread.Sleep(delay);

                    // Up
                    SendTestMove(0, -step);
                    System.Threading.Thread.Sleep(delay);

                    // Small circle pattern (8 points)
                    for (int i = 0; i < 8; i++)
                    {
                        double angle = i * Math.PI / 4; // 45 degree increments
                        int x = (int)(Math.Cos(angle) * 30);
                        int y = (int)(Math.Sin(angle) * 30);
                        Device.Move(x, y);
                        System.Threading.Thread.Sleep(100);
                    }

                    DebugLogger.LogDebug("[DeviceAimbotTest] Movement test complete");
                });

                System.Windows.MessageBox.Show(
                    "Test complete! Your mouse should have moved in a square pattern, then a circle.\n\n" +
                    "If the mouse didn't move, check:\n" +
                    "? Device connection\n" +
                    "? COM port selection\n" +
                    "? Device firmware / mode",
                    "Test Move",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Test failed: {ex.Message}",
                    "Test Move",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                DebugLogger.LogDebug($"[DeviceAimbotTest] Error: {ex}");
            }
            finally
            {
                IsTesting = false;
            }
        }

        private static void SendTestMove(int dx, int dy)
        {
            try
            {
                if (App.Config.Device.UseKmBoxNet && DeviceNetController.Connected)
                {
                    DeviceNetController.Move(dx, dy);
                    return;
                }

                Device.move(dx, dy);
            }
            catch (TimeoutException tex)
            {
                DebugLogger.LogDebug($"[DeviceAimbotTest] Move timeout: {tex.Message}");
                // swallow here so the test UI doesn't show a scary popup if the device already moved
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[DeviceAimbotTest] Move error: {ex}");
                throw;
            }
        }
    }
}
