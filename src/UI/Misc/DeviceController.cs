using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Management; // <-- NuGet: System.Management
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LoneEftDmaRadar.UI.Misc
{
    public enum DeviceAimbotMouseButton : int
    {
        Left = 1,
        Right = 2,
        Middle = 3,
        mouse4 = 4,
        mouse5 = 5
    }

    /// <summary>
    /// Type of km.* device currently connected.
    /// </summary>
    public enum KmDeviceKind
    {
        Unknown = 0,
        Makcu   = 1,
        Generic = 2   // KMBox / CH340 / any km.* at 115200
    }

    // NOTE: keep the class name 'device' to avoid breaking existing references
    public class Device
    {
        // --- gating for device smooth/bezier so we don't overlap commands ---
        private long _makcuBusyUntilTicks;             // when it's safe to send the next smooth/bezier
        private static readonly double _ticksPerMs = (double)Stopwatch.Frequency / 1000.0;

        // You can tune this if your firmware runs ~2-4ms per segment
        private const int MakcuSegmentMsDefault = 3;

        // Optional: expose via config if you want
        private static int GetSegmentMs() => MakcuSegmentMsDefault;

        // Optional: enable Bezier from config if you add a toggle later.
        // For now: try Bezier when segments > 1.
        private static bool UseBezierForSmoothing => true;

        #region Makcu Identity (edit if needed)
        private const string Makcu_FRIENDLY_NAME = "USB-Enhanced-SERIAL CH343";
        private const string Makcu_VID = "1A86";
        private const string Makcu_PID = "55D3";
        private const string Makcu_SERIAL_FRAGMENT = "58A6074578"; // optional; helps if multiple adapters
        private const string Makcu_EXPECT_SIGNATURE = "km.MAKCU";
        private static readonly string[] MakcuSignatures = new[]
        {
            Makcu_EXPECT_SIGNATURE,
            "km.net",          // KMBox NET / KMNET firmware
            "kmbox.net",
            "km.kmboxnet",
            "kmnet"
        };
        #endregion

        #region Generic Serial Identity
        // CH340/CH341/CH9102 are common for slower km.* firmware variants at 115200 baud.
        private static readonly (string Vid, string Pid, string Friendly, string SerialFragment)[] GenericSerialCandidates =
        {
            (Makcu_VID, "7523", "USB-SERIAL CH340", null),
            (Makcu_VID, "5523", "USB-SERIAL CH9102", null)
        };

        private static readonly string[] GenericFriendlyFallbacks = new[]
        {
            "USB-SERIAL", "CH340", "CH341", "CH9102"
        };
        #endregion

        #region Fields / State

        /// <summary>
        /// What kind of km.* device is currently connected (Makcu vs Generic).
        /// </summary>
        public static KmDeviceKind DeviceKind { get; private set; } = KmDeviceKind.Unknown;

        private static readonly byte[] change_cmd = { 0xDE, 0xAD, 0x05, 0x00, 0xA5, 0x00, 0x09, 0x3D, 0x00 };

        public static bool connected = false;
        private static SerialPort port = null;
        public static string CurrentPortName => port?.PortName;
        private static Thread button_inputs;
        public static string version = "";
        private static bool runReader = false;

        public static Dictionary<int, bool> bState { get; private set; }

        private static readonly HashSet<byte> validBytes = new HashSet<byte>
        {
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
            0x08, 0x09, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15,
            0x16, 0x17, 0x19, 0x1F
        };

        private static readonly Random r = new Random();
        private const int DEFAULT_OPEN_BAUD = 115200;   // initial open
        private const int HIGH_BAUD = 4000000;          // Makcu high speed
        #endregion

        #region Auto-Connect Helpers (VID/PID/Name + Signature Check)
        /// <summary>
        /// One-shot: find COM port by VID/PID or friendly name (with optional serial fragment),
        /// connect via your existing flow, and verify "km.MAKCU".
        /// </summary>
        public static bool AutoConnectMakcu()
        {
            try
            {
                DeviceKind = KmDeviceKind.Unknown;
                string com =
                    TryGetComByVidPid(Makcu_VID, Makcu_PID, Makcu_SERIAL_FRAGMENT)
                    ?? TryGetComByFriendlyName(Makcu_FRIENDLY_NAME, Makcu_SERIAL_FRAGMENT);

                if (string.IsNullOrEmpty(com))
                {
                    DebugLogger.LogDebug("[-] Makcu device not found via VID/PID or friendly name.");
                    return false;
                }

                connect(com); // use your original method to keep behavior identical

                if (!connected)
                {
                    DebugLogger.LogDebug("[-] Failed to open Makcu serial port.");
                    DeviceKind = KmDeviceKind.Unknown;
                    return false;
                }

                if (!ValidateMakcuSignature())
                {
                    DebugLogger.LogDebug("[-] Device did not return expected signature (km.MAKCU).");
                    disconnect();
                    DeviceKind = KmDeviceKind.Unknown;
                    return false;
                }

                DeviceKind = KmDeviceKind.Makcu;
                DebugLogger.LogDebug("[+] Makcu connected and verified.");
                return true;
            }
            catch (Exception ex)
            {
                DeviceKind = KmDeviceKind.Unknown;
                DebugLogger.LogDebug($"[-] AutoConnectMakcu error: {ex}");
                return false;
            }
        }

        private static bool ValidateMakcuSignature(int timeoutMs = 800)
        {
            try
            {
                if (port == null || !port.IsOpen) return false;

                // make sure we don't read stale bytes
                port.DiscardInBuffer();

                // Send probe
                port.Write("km.version()\r");

                // Temporarily set a read timeout for the probe
                int oldTimeout = port.ReadTimeout;
                port.ReadTimeout = timeoutMs;

                string line = port.ReadLine()?.Trim();
                port.ReadTimeout = oldTimeout;

                DebugLogger.LogDebug($"[ValidateMakcu] Response: '{line}'");

                if (string.IsNullOrEmpty(line))
                {
                    DebugLogger.LogDebug($"[ValidateMakcu] Empty response");
                    return false;
                }

                // Accept starts-with or contains to be tolerant of echoes
                bool ok = line.StartsWith(Makcu_EXPECT_SIGNATURE, StringComparison.OrdinalIgnoreCase)
                       || line.Contains(Makcu_EXPECT_SIGNATURE, StringComparison.OrdinalIgnoreCase);

                if (ok)
                {
                    version = line; // keep your cache in sync
                    DebugLogger.LogDebug($"[ValidateMakcu] Signature VALID");
                }
                else
                {
                    DebugLogger.LogDebug($"[ValidateMakcu] Signature INVALID (expected '{Makcu_EXPECT_SIGNATURE}')");
                }

                return ok;
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[ValidateMakcu] Exception: {ex.Message}");
                return false;
            }
        }

        private static bool IsMakcuSignatureLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return false;

            return MakcuSignatures.Any(sig => line.Contains(sig, StringComparison.OrdinalIgnoreCase))
                   || line.StartsWith("km.", StringComparison.OrdinalIgnoreCase)
                   || line.Contains("km.", StringComparison.OrdinalIgnoreCase);
        }

        public static string TryGetComByVidPid(string vidHex, string pidHex, string serialContains = null)
        {
            string vidPattern = $"VID_{vidHex.Trim().ToUpper()}";
            string pidPattern = $"PID_{pidHex.Trim().ToUpper()}";

            // Direct: Serial ports
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

                    return (portObj["DeviceID"] as string); // "COMx"
                }
            }

            // Fallback: PnP entities; extract (COMx) from Friendly Name
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

        /// <summary>
        /// Enumerate all serial devices (COM ports) with friendly name and PNP ID.
        /// </summary>
        public static List<SerialDeviceInfo> EnumerateSerialDevices()
        {
            var devices = new List<SerialDeviceInfo>();

            try
            {
                // Get all COM ports
                var portNames = SerialPort.GetPortNames();

                foreach (var portName in portNames)
                {
                    string description = "Serial Port";

                    try
                    {
                        // Try to get device description from registry
                        using (var searcher = new ManagementObjectSearcher(
                            $"SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%({portName})%'"))
                        {
                            foreach (var device in searcher.Get())
                            {
                                var name = device["Name"]?.ToString();
                                if (!string.IsNullOrEmpty(name))
                                {
                                    // Extract description (remove port name)
                                    description = name.Replace($"({portName})", "").Trim();
                                    break;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // If registry lookup fails, use generic description
                    }

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

        /// <summary>
        /// Best-effort auto-connect using (in order):
        /// 1) Previously saved COM port
        /// 2) VID/PID friendly-name probe (Makcu)
        /// 3) VID/PID/friendly probe for generic km.* serial devices
        /// 4) Iterate all COM ports (generic fallback)
        /// </summary>
        public static bool TryAutoConnect(string lastComPort = null)
        {
            if (connected)
                return true;

            try
            {
                if (!string.IsNullOrWhiteSpace(lastComPort))
                {
                    DebugLogger.LogDebug($"[Device] AutoConnect: trying saved port {lastComPort}");
                    if (ConnectAuto(lastComPort))
                        return true;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[Device] AutoConnect saved port failed: {ex.Message}");
            }

            // Try VID/PID detection
            if (AutoConnectMakcu())
                return true;

            // Try known CH340/CH341/CH9102-style adapters at 115200
            if (AutoConnectGenericKm())
                return true;

            // Try all enumerated COM ports as a last resort
            foreach (var dev in EnumerateSerialDevices())
            {
                try
                {
                    DebugLogger.LogDebug($"[Device] AutoConnect: probing {dev.Port} ({dev.Description})");
                    if (ConnectAuto(dev.Port))
                        return true;
                }
                catch { /* ignore and continue */ }
            }

            return false;
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

                    return (portObj["DeviceID"] as string); // "COMx"
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

        private static bool AutoConnectGenericKm()
        {
            foreach (var candidate in GenericSerialCandidates)
            {
                string com =
                    TryGetComByVidPid(candidate.Vid, candidate.Pid, candidate.SerialFragment)
                    ?? (string.IsNullOrEmpty(candidate.Friendly)
                        ? null
                        : TryGetComByFriendlyName(candidate.Friendly, candidate.SerialFragment));

                if (string.IsNullOrEmpty(com))
                    continue;

                DebugLogger.LogDebug($"[GenericKM] AutoConnect: trying {com} ({candidate.Vid}:{candidate.Pid} / {candidate.Friendly})");
                if (ConnectGenericKm(com))
                    return true;
            }

            foreach (var friendly in GenericFriendlyFallbacks)
            {
                string com = TryGetComByFriendlyName(friendly);
                if (string.IsNullOrEmpty(com))
                    continue;

                DebugLogger.LogDebug($"[GenericKM] AutoConnect fallback: trying {com} ({friendly})");
                if (ConnectGenericKm(com))
                    return true;
            }

            return false;
        }

        private static string ExtractComFromFriendlyName(string name)
        {
            // Examples:
            //  "USB-Enhanced-SERIAL CH343 (COM7)"
            //  "Prolific USB-to-Serial Comm Port (COM5)"
            int open = name.LastIndexOf("(COM", StringComparison.OrdinalIgnoreCase);
            if (open >= 0)
            {
                int close = name.IndexOf(')', open);
                if (close > open)
                {
                    string inner = name.Substring(open + 1, close - open - 1); // "COM7"
                    if (inner.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                        return inner.ToUpper();
                }
            }
            return null;
        }
        #endregion

        #region Connect / Disconnect / Reconnect

        /// <summary>
        /// Original Makcu-specific connect (high-speed km.* adapter).
        /// IMPORTANT: left unchanged, so Makcu path stays working.
        /// </summary>
        public static void connect(string com)
        {
            try
            {
                DeviceKind = KmDeviceKind.Unknown;

                if (port == null)
                {
                    port = new SerialPort(com, DEFAULT_OPEN_BAUD, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout = 500,
                        WriteTimeout = 500,
                        Encoding = Encoding.ASCII,
                        NewLine = "\n" // for ReadLine()
                    };
                }
                else
                {
                    if (port.IsOpen) port.Close();
                    port.PortName = com;
                    port.BaudRate = DEFAULT_OPEN_BAUD;
                }

                port.Open();
                if (!port.IsOpen)
                    return;

                // small wait before mode switch
                Thread.Sleep(150);
                port.Write(change_cmd, 0, change_cmd.Length);
                port.BaseStream.Flush();

                // switch to high speed
                //port.BaudRate = HIGH_BAUD;
                SetBaud(HIGH_BAUD);
                GetVersion();
                Thread.Sleep(150);

                DebugLogger.LogDebug($"[+] Device connected to {port.PortName} at {port.BaudRate} baudrate");

                // enable button stream + disable echo
                port.Write("km.buttons(1)\r\n");
                port.Write("km.echo(0)\r\n");
                port.DiscardInBuffer();

                // NOTE: Don't start listener yet - caller will start it after validation
                // start_listening();

                bState = new Dictionary<int, bool>();
                for (int i = 1; i <= 5; i++)
                    bState[i] = false;

                connected = true;
            }
            catch (Exception ex)
            {
                connected = false;
                DeviceKind = KmDeviceKind.Unknown;
                DebugLogger.LogDebug($"[-] Device failed to connect. {ex}");
            }
        }

        public static void SetBaud(int baud)
        {
            var cmd = new byte[] {
                0xDE, 0xAD, 0x05, 0x00, 0xA5,
                (byte)(baud & 0xFF),
                (byte)((baud >> 8) & 0xFF),
                (byte)((baud >> 16) & 0xFF),
                (byte)((baud >> 24) & 0xFF)
            };
            port.Write(cmd, 0, cmd.Length);
            port.BaseStream.Flush();
            Thread.Sleep(50);
            port.BaudRate = baud;
        }

        public static void disconnect()
        {
            if (!connected || port == null)
                return;

            try
            {
                DebugLogger.LogDebug("[!] Closing port...");
                runReader = false;

                // try to disable buttons to quiet stream
                if (port.IsOpen)
                {
                    try
                    {
                        port.Write("km.buttons(0)\r\n");
                        Thread.Sleep(10);
                        port.BaseStream.Flush();
                    }
                    catch { }
                }

                port.Close();
                if (!port.IsOpen)
                    DebugLogger.LogDebug("[!] Port terminated successfully");
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[!] Port close error: {ex}");
            }
            finally
            {
                connected = false;
                DeviceKind = KmDeviceKind.Unknown;
            }
        }

        public static async void reconnect_device(string com)
        {
            disconnect();
            await Task.Delay(200);
            try
            {
                if (port != null && !port.IsOpen)
                    port.Open();

                DebugLogger.LogDebug("[+] Reconnected to device.");
                connected = port?.IsOpen == true;
            }
            catch (Exception ex)
            {
                connected = false;
                DebugLogger.LogDebug($"[-] Reconnect failed: {ex}");
            }
        }

        /// <summary>
        /// Try Makcu handshake on a specific COM.
        /// Uses your existing connect(), then validates signature.
        /// </summary>
        public static bool TryConnectMakcuOnPort(string com)
        {
            DeviceKind = KmDeviceKind.Unknown;
            connected = false;

            connect(com);

            if (!connected)
                return false;

            // Validate signature BEFORE starting listener
            if (!ValidateMakcuSignature())
            {
                DebugLogger.LogDebug($"[-] {com}: not a Makcu (km.MAKCU signature missing).");
                disconnect();
                return false;
            }

            DeviceKind = KmDeviceKind.Makcu;
            DebugLogger.LogDebug($"[+] {com}: Makcu validated via km.MAKCU signature.");

            // Now start listener after successful validation
            start_listening();

            return true;
        }

        /// <summary>
        /// Connect a generic KM device (KMBox, CH340, etc.) that speaks km.*
        /// at 115200 with NO change_cmd / baud change.
        /// </summary>
        public static bool ConnectGenericKm(string com)
        {
            DeviceKind = KmDeviceKind.Unknown;
            connected = false;

            try
            {
                if (port == null)
                {
                    port = new SerialPort(com, DEFAULT_OPEN_BAUD, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout  = 500,
                        WriteTimeout = 500,
                        Encoding     = Encoding.ASCII,
                        NewLine      = "\n"
                    };
                }
                else
                {
                    if (port.IsOpen) port.Close();
                    port.PortName = com;
                    port.BaudRate = DEFAULT_OPEN_BAUD;
                }

                port.Open();
                if (!port.IsOpen)
                    return false;

                Thread.Sleep(150);

                // Best-effort: ask for version, but don't require anything specific
                try
                {
                    port.DiscardInBuffer();
                    port.Write("km.version()\r");
                    Thread.Sleep(100);
                    version = port.ReadLine();
                    DebugLogger.LogDebug($"[GenericKM] {com} km.version(): {version}");
                }
                catch
                {
                    version = string.Empty;
                }

                // Enable button stream + disable echo (same as Makcu/high-speed mode)
                try
                {
                    port.Write("km.buttons(1)\r\n");
                    port.Write("km.echo(0)\r\n");
                    port.DiscardInBuffer();
                }
                catch (Exception ex)
                {
                    DebugLogger.LogDebug($"[GenericKM] Warning: km.buttons/km.echo failed on {com}: {ex}");
                }

                start_listening();

                bState = new Dictionary<int, bool>();
                for (int i = 1; i <= 5; i++)
                    bState[i] = false;

                connected = true;
                DeviceKind = KmDeviceKind.Generic;

                DebugLogger.LogDebug($"[+] Generic KM device connected to {port.PortName} at {port.BaudRate} baudrate");
                return true;
            }
            catch (Exception ex)
            {
                connected = false;
                DeviceKind = KmDeviceKind.Unknown;
                DebugLogger.LogDebug($"[-] Generic KM device failed to connect on {com}. {ex}");
                try
                {
                    if (port?.IsOpen == true)
                        port.Close();
                }
                catch { }
                return false;
            }
        }

        /// <summary>
        /// Auto connect on a specific COM:
        /// 1) Try Makcu handshake (change_cmd + 4M + km.MAKCU signature)
        /// 2) If that fails, fall back to generic km.* at 115200
        /// </summary>
        public static bool ConnectAuto(string com)
        {
            // First try Makcu, which uses your existing connect()
            if (TryConnectMakcuOnPort(com))
                return true;

            // If that fails, try generic km.* (KMBox, etc.)
            return ConnectGenericKm(com);
        }

        #endregion

        #region Version / Commands
        public static string GetVersion()
        {
            if (port == null || !port.IsOpen) return version = $"Port Null or Closed : {port?.PortName} {port?.IsOpen} {port?.BaudRate} ";

            try
            {
                port.DiscardInBuffer();
                port.Write("km.version()\r");
                Thread.Sleep(100);
                version = port.ReadLine();
                DebugLogger.LogDebug($"[GetVersion] Response: '{version}'");
                return version;
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[GetVersion] Error: {ex.Message}");
                return version = "";
            }
        }

        public static void move(int x, int y)
        {
            if (!connected) return;
            try
            {
                port.Write($"km.move({x}, {y})\r");
                //_ = port.BaseStream.FlushAsync();
            }
            catch (TimeoutException tex)
            {
                DebugLogger.LogDebug($"[Device] move timeout: {tex.Message}");
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[Device] move error: {ex}");
            }
        }

        private static readonly char[] MovePrefix = "km.move(".ToCharArray();
        private static readonly char[] MoveSuffix = ")\n".ToCharArray();
        private static readonly char[] IntBuffer = new char[32]; // enough for two ints

        public static void Move(int x, int y)
        {
            if (!connected || port == null || !port.IsOpen) return;
            if ((uint)(x - short.MinValue) > ushort.MaxValue ||
                (uint)(y - short.MinValue) > ushort.MaxValue) return;

            // Fast int formatting into IntBuffer
            int len = 0;
            len += FormatInt(x, IntBuffer, len);
            IntBuffer[len++] = ',';
            len += FormatInt(y, IntBuffer, len);

            try
            {
                lock (port) // prevent races
                {
                    port.Write(MovePrefix, 0, MovePrefix.Length);
                    port.Write(IntBuffer, 0, len);
                    port.Write(MoveSuffix, 0, MoveSuffix.Length);
                }
            }
            catch (TimeoutException tex)
            {
                DebugLogger.LogDebug($"[Device] Move timeout: {tex.Message}");
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[Device] Move error: {ex}");
            }
        }

        // ultra-fast int to char[]
        private static int FormatInt(int value, char[] buf, int offset)
        {
            return value.TryFormat(buf.AsSpan(offset), out int written)
                ? written
                : 0;
        }

        public static void move_smooth(int x, int y, int segments)
        {
            if (!connected) return;
            port.Write($"km.move({x}, {y}, {segments})\r");
            _ = port.BaseStream.FlushAsync();
        }

        public static void move_bezier(int x, int y, int segments, int ctrl_x, int ctrl_y)
        {
            if (!connected) return;
            port.Write($"km.move({x}, {y}, {segments}, {ctrl_x}, {ctrl_y})\r");
            _ = port.BaseStream.FlushAsync();
        }

        public static void mouse_wheel(int delta)
        {
            if (!connected) return;
            port.Write($"km.wheel({delta})\r");
            _ = port.BaseStream.FlushAsync();
        }

        public static void lock_axis(string axis, int bit)
        {
            if (!connected) return;
            port.Write($"km.lock_m{axis}({bit})\r");
            _ = port.BaseStream.FlushAsync();
        }

        public static void click(string button, int ms_delay, int click_delay = 0)
        {
            if (!connected) return;

            int time = r.Next(10, 100); // randomized press time
            Thread.Sleep(click_delay);
            port.Write($"km.{button}(1)\r");
            Thread.Sleep(time);
            port.Write($"km.{button}(0)\r");
            _ = port.BaseStream.FlushAsync();
            Thread.Sleep(ms_delay);
        }

        public static void press(DeviceAimbotMouseButton button, int press)
        {
            if (!connected) return;
            string cmd = $"km.{MouseButtonToString(button)}({press})\r";
            port.Write(cmd);
            _ = port.BaseStream.FlushAsync();
        }

        #endregion

        #region Button Stream (Listener)
        public static void start_listening()
        {
            if (button_inputs != null && button_inputs.IsAlive)
            {
                // already listening
                return;
            }

            Thread.Sleep(500); // allow time for cleanup
            runReader = true;
            button_inputs = new Thread(read_buttons)
            {
                IsBackground = true,
                Name = "MakcuButtonListener"
            };
            button_inputs.Start();
        }

        public static async void read_buttons()
        {
            await Task.Run(() =>
            {
                DebugLogger.LogDebug("[+] Listening to device.");
                while (runReader)
                {
                    if (!connected || port == null)
                    {
                        Thread.Sleep(250);
                        connected = port?.IsOpen == true;
                        continue;
                    }

                    try
                    {
                        if (port.BytesToRead > 0)
                        {
                            int data = port.ReadByte();
                            if (!validBytes.Contains((byte)data))
                                continue;

                            byte b = (byte)data;

                            // bits 0..4 -> buttons 1..5
                            for (int i = 1; i <= 5; i++)
                                bState[i] = (b & (1 << (i - 1))) != 0;

                            port.DiscardInBuffer();
                        }
                        else
                        {
                            // avoid hot-loop when idle
                            Thread.Sleep(1);
                        }
                    }
                    catch
                    {
                        connected = false;
                        Thread.Sleep(50);
                    }
                }
            });
        }
        #endregion

        #region Button Helpers / Locks
        public static bool button_pressed(DeviceAimbotMouseButton button)
        {
            if (!connected || bState == null) return false;
            return bState.TryGetValue((int)button, out bool state) && state;
        }

        public static async void lock_button(DeviceAimbotMouseButton button, int bit)
        {
            if (!connected) return;

            string cmd = button switch
            {
                DeviceAimbotMouseButton.Left   => $"km.lock_ml({bit})\r",
                DeviceAimbotMouseButton.Right  => $"km.lock_mr({bit})\r",
                DeviceAimbotMouseButton.Middle => $"km.lock_mm({bit})\r",
                DeviceAimbotMouseButton.mouse4 => $"km.lock_ms1({bit})\r",
                DeviceAimbotMouseButton.mouse5 => $"km.lock_ms2({bit})\r",
                _ => $"km.lock_ml({bit})\r"
            };

            await Task.Delay(1);
            port.Write(cmd);
            await port.BaseStream.FlushAsync();
        }

        public static int MouseButtonToInt(DeviceAimbotMouseButton button) => (int)button;
        public static DeviceAimbotMouseButton IntToMouseButton(int button) => (DeviceAimbotMouseButton)button;

        public static string MouseButtonToString(DeviceAimbotMouseButton button)
        {
            return button switch
            {
                DeviceAimbotMouseButton.Left   => "left",
                DeviceAimbotMouseButton.Right  => "right",
                DeviceAimbotMouseButton.Middle => "middle",
                DeviceAimbotMouseButton.mouse4 => "ms1",
                DeviceAimbotMouseButton.mouse5 => "ms2",
                _ => "left"
            };
        }

        public static void setMouseSerial(string serial)
        {
            if (!connected) return;
            port.Write($"km.serial({serial})\r");
        }

        public static void resetMouseSerial()
        {
            if (!connected) return;
            port.Write("km.serial(0)\r");
        }

        public static void unlock_all_buttons()
        {
            if (port?.IsOpen == true)
            {
                port.Write("km.lock_ml(0)\r");
                port.Write("km.lock_mr(0)\r");
                port.Write("km.lock_mm(0)\r");
                port.Write("km.lock_ms1(0)\r");
                port.Write("km.lock_ms2(0)\r");
            }
        }
        #endregion
    }
}
