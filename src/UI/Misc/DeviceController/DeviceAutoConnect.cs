/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using System.IO.Ports;
using System.Management;

namespace LoneEftDmaRadar.UI.Misc
{
    /// <summary>
    /// Handles auto-detection and enumeration of serial devices.
    /// </summary>
    public static class DeviceAutoConnect
    {
        public static bool AutoConnectMakcu()
        {
            try
            {
                Device.DeviceKind = KmDeviceKind.Unknown;
                string com =
                    TryGetComByVidPid(DeviceControllerConstants.MakcuVid, DeviceControllerConstants.MakcuPid, DeviceControllerConstants.MakcuSerialFragment)
                    ?? TryGetComByFriendlyName(DeviceControllerConstants.MakcuFriendlyName, DeviceControllerConstants.MakcuSerialFragment);

                if (string.IsNullOrEmpty(com))
                {
                    DebugLogger.LogDebug("[-] Makcu device not found via VID/PID or friendly name.");
                    return false;
                }

                DeviceConnection.Connect(com);

                if (!Device.connected)
                {
                    DebugLogger.LogDebug("[-] Failed to open Makcu serial port.");
                    Device.DeviceKind = KmDeviceKind.Unknown;
                    return false;
                }

                if (!DeviceConnection.ValidateMakcuSignature())
                {
                    DebugLogger.LogDebug($"[-] Device did not return expected signature ({DeviceControllerConstants.MakcuExpectSignature}).");
                    DeviceConnection.Disconnect();
                    Device.DeviceKind = KmDeviceKind.Unknown;
                    return false;
                }

                Device.DeviceKind = KmDeviceKind.Makcu;
                DebugLogger.LogDebug("[+] Makcu connected and verified.");
                return true;
            }
            catch (Exception ex)
            {
                Device.DeviceKind = KmDeviceKind.Unknown;
                DebugLogger.LogDebug($"[-] AutoConnectMakcu error: {ex}");
                return false;
            }
        }

        public static bool AutoConnectGenericKm()
        {
            // Try VID/PID for CH340
            string com = TryGetComByVidPid(DeviceControllerConstants.MakcuVid, DeviceControllerConstants.Ch340Pid, null)
                      ?? TryGetComByFriendlyName(DeviceControllerConstants.Ch340FriendlyName, null);
            if (!string.IsNullOrEmpty(com))
            {
                DebugLogger.LogDebug($"[GenericKM] AutoConnect: trying {com} (CH340)");
                if (DeviceConnection.ConnectGenericKm(com))
                    return true;
            }

            // Try VID/PID for CH9102
            com = TryGetComByVidPid(DeviceControllerConstants.MakcuVid, DeviceControllerConstants.Ch9102Pid, null)
               ?? TryGetComByFriendlyName(DeviceControllerConstants.Ch9102FriendlyName, null);
            if (!string.IsNullOrEmpty(com))
            {
                DebugLogger.LogDebug($"[GenericKM] AutoConnect: trying {com} (CH9102)");
                if (DeviceConnection.ConnectGenericKm(com))
                    return true;
            }

            // Try fallback friendly names
            foreach (var friendly in DeviceControllerConstants.GenericFriendlyFallbacks)
            {
                com = TryGetComByFriendlyName(friendly);
                if (string.IsNullOrEmpty(com))
                    continue;

                DebugLogger.LogDebug($"[GenericKM] AutoConnect fallback: trying {com} ({friendly})");
                if (DeviceConnection.ConnectGenericKm(com))
                    return true;
            }

            return false;
        }

        public static bool TryAutoConnect(string lastComPort = null)
        {
            if (Device.connected)
                return true;

            try
            {
                if (!string.IsNullOrWhiteSpace(lastComPort))
                {
                    DebugLogger.LogDebug($"[Device] AutoConnect: trying saved port {lastComPort}");
                    if (DeviceConnection.ConnectAuto(lastComPort))
                        return true;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[Device] AutoConnect saved port failed: {ex.Message}");
            }

            if (AutoConnectMakcu())
                return true;

            if (AutoConnectGenericKm())
                return true;

            foreach (var dev in EnumerateSerialDevices())
            {
                try
                {
                    DebugLogger.LogDebug($"[Device] AutoConnect: probing {dev.Port} ({dev.Description})");
                    if (DeviceConnection.ConnectAuto(dev.Port))
                        return true;
                }
                catch { }
            }

            return false;
        }

        public static string TryGetComByVidPid(string vidHex, string pidHex, string serialContains = null)
        {
            string vidPattern = $"VID_{vidHex.Trim().ToUpper()}";
            string pidPattern = $"PID_{pidHex.Trim().ToUpper()}";

            using (var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, PNPDeviceID, Name FROM Win32_SerialPort"))
            {
                foreach (ManagementObject portObj in searcher.Get())
                {
                    string pnp = (portObj["PNPDeviceID"] as string) ?? "";
                    if (!pnp.Contains(vidPattern) || !pnp.Contains(pidPattern))
                        continue;

                    if (!string.IsNullOrEmpty(serialContains) &&
                        !pnp.Contains(serialContains, StringComparison.OrdinalIgnoreCase))
                        continue;

                    return (portObj["DeviceID"] as string);
                }
            }

            using (var devs = new ManagementObjectSearcher(
                "SELECT Name, PNPDeviceID FROM Win32_PnPEntity WHERE PNPDeviceID LIKE 'USB\\\\VID_%'"))
            {
                foreach (ManagementObject dev in devs.Get())
                {
                    string pnp = (dev["PNPDeviceID"] as string) ?? "";
                    if (!pnp.Contains(vidPattern) || !pnp.Contains(pidPattern))
                        continue;

                    if (!string.IsNullOrEmpty(serialContains) &&
                        !pnp.Contains(serialContains, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string name = (dev["Name"] as string) ?? "";
                    var com = ExtractComFromFriendlyName(name);
                    if (!string.IsNullOrEmpty(com))
                        return com;
                }
            }

            return null;
        }

        public static string TryGetComByFriendlyName(string friendlyContains, string serialContains = null)
        {
            using (var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, PNPDeviceID, Name FROM Win32_SerialPort"))
            {
                foreach (ManagementObject portObj in searcher.Get())
                {
                    string name = (portObj["Name"] as string) ?? "";
                    string pnp = (portObj["PNPDeviceID"] as string) ?? "";

                    if (!name.Contains(friendlyContains, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!string.IsNullOrEmpty(serialContains) &&
                        !pnp.Contains(serialContains, StringComparison.OrdinalIgnoreCase))
                        continue;

                    return (portObj["DeviceID"] as string);
                }
            }

            using (var devs = new ManagementObjectSearcher(
                "SELECT Name, PNPDeviceID FROM Win32_PnPEntity WHERE Name IS NOT NULL"))
            {
                foreach (ManagementObject dev in devs.Get())
                {
                    string name = (dev["Name"] as string) ?? "";
                    if (!name.Contains(friendlyContains, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string pnp = (dev["PNPDeviceID"] as string) ?? "";
                    if (!string.IsNullOrEmpty(serialContains) &&
                        !pnp.Contains(serialContains, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var com = ExtractComFromFriendlyName(name);
                    if (!string.IsNullOrEmpty(com))
                        return com;
                }
            }

            return null;
        }

        public static List<SerialDeviceInfo> EnumerateSerialDevices()
        {
            var devices = new List<SerialDeviceInfo>();

            try
            {
                var portNames = SerialPort.GetPortNames();

                foreach (var portName in portNames)
                {
                    string description = "Serial Port";

                    try
                    {
                        using (var searcher = new ManagementObjectSearcher(
                            $"SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%({portName})%'"))
                        {
                            foreach (var device in searcher.Get())
                            {
                                var name = device["Name"]?.ToString();
                                if (!string.IsNullOrEmpty(name))
                                {
                                    description = name.Replace($"({portName})", "").Trim();
                                    break;
                                }
                            }
                        }
                    }
                    catch { }

                    devices.Add(new SerialDeviceInfo
                    {
                        Port = portName,
                        Description = description
                    });

                    DebugLogger.LogDebug($"[Device] Found: {portName} - {description}");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[Device] Error enumerating: {ex}");
            }

            return devices;
        }

        private static string ExtractComFromFriendlyName(string name)
        {
            int open = name.LastIndexOf("(COM", StringComparison.OrdinalIgnoreCase);
            if (open >= 0)
            {
                int close = name.IndexOf(')', open);
                if (close > open)
                {
                    string inner = name.Substring(open + 1, close - open - 1);
                    if (inner.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                        return inner.ToUpper();
                }
            }
            return null;
        }
    }

    public sealed class SerialDeviceInfo
    {
        public string Port { get; init; } = "";
        public string Name { get; init; } = "";
        public string Pnp { get; init; } = "";
        public string Description { get; set; }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Description))
                return $"{Port} - {Description}";
            return Port;
        }
    }
}
