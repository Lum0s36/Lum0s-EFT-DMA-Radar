/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using System.IO.Ports;
using System.Text;

namespace LoneEftDmaRadar.UI.Misc
{
    /// <summary>
    /// Handles serial port connection, disconnection, and reconnection.
    /// </summary>
    public static class DeviceConnection
    {
        private static SerialPort _port;
        internal static SerialPort Port => _port;

        public static void Connect(string com)
        {
            try
            {
                Device.DeviceKind = KmDeviceKind.Unknown;

                if (_port == null)
                {
                    _port = new SerialPort(com, DeviceControllerConstants.DefaultOpenBaud, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout = DeviceControllerConstants.DefaultReadTimeout,
                        WriteTimeout = DeviceControllerConstants.DefaultWriteTimeout,
                        Encoding = Encoding.ASCII,
                        NewLine = "\n"
                    };
                }
                else
                {
                    if (_port.IsOpen) _port.Close();
                    _port.PortName = com;
                    _port.BaudRate = DeviceControllerConstants.DefaultOpenBaud;
                }

                _port.Open();
                if (!_port.IsOpen)
                    return;

                Thread.Sleep(DeviceControllerConstants.PortOpenDelayMs);
                _port.Write(DeviceControllerConstants.ChangeModeCommand, 0, DeviceControllerConstants.ChangeModeCommand.Length);
                _port.BaseStream.Flush();

                SetBaud(DeviceControllerConstants.HighBaud);
                Device.version = GetVersion();
                Thread.Sleep(DeviceControllerConstants.PortOpenDelayMs);

                DebugLogger.LogDebug($"[+] Device connected to {_port.PortName} at {_port.BaudRate} baudrate");

                _port.Write("km.buttons(1)\r\n");
                _port.Write("km.echo(0)\r\n");
                _port.DiscardInBuffer();

                Device.InitButtonState();
                Device.connected = true;
            }
            catch (Exception ex)
            {
                Device.connected = false;
                Device.DeviceKind = KmDeviceKind.Unknown;
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
            _port.Write(cmd, 0, cmd.Length);
            _port.BaseStream.Flush();
            Thread.Sleep(DeviceControllerConstants.BaudChangeDelayMs);
            _port.BaudRate = baud;
        }

        public static void Disconnect()
        {
            if (!Device.connected || _port == null)
                return;

            try
            {
                DebugLogger.LogDebug("[!] Closing port...");
                DeviceButtonListener.StopListening();

                if (_port.IsOpen)
                {
                    try
                    {
                        _port.Write("km.buttons(0)\r\n");
                        Thread.Sleep(10);
                        _port.BaseStream.Flush();
                    }
                    catch { }
                }

                _port.Close();
                if (!_port.IsOpen)
                    DebugLogger.LogDebug("[!] Port terminated successfully");
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[!] Port close error: {ex}");
            }
            finally
            {
                Device.connected = false;
                Device.DeviceKind = KmDeviceKind.Unknown;
            }
        }

        public static async void Reconnect(string com)
        {
            Disconnect();
            await Task.Delay(DeviceControllerConstants.ReconnectDelayMs);
            try
            {
                if (_port != null && !_port.IsOpen)
                    _port.Open();

                DebugLogger.LogDebug("[+] Reconnected to device.");
                Device.connected = _port?.IsOpen == true;
            }
            catch (Exception ex)
            {
                Device.connected = false;
                DebugLogger.LogDebug($"[-] Reconnect failed: {ex}");
            }
        }

        public static bool TryConnectMakcuOnPort(string com)
        {
            Device.DeviceKind = KmDeviceKind.Unknown;
            Device.connected = false;

            Connect(com);

            if (!Device.connected)
                return false;

            if (!ValidateMakcuSignature())
            {
                DebugLogger.LogDebug($"[-] {com}: not a Makcu (km.MAKCU signature missing).");
                Disconnect();
                return false;
            }

            Device.DeviceKind = KmDeviceKind.Makcu;
            DebugLogger.LogDebug($"[+] {com}: Makcu validated via km.MAKCU signature.");

            DeviceButtonListener.StartListening();
            return true;
        }

        public static bool ConnectGenericKm(string com)
        {
            Device.DeviceKind = KmDeviceKind.Unknown;
            Device.connected = false;

            try
            {
                if (_port == null)
                {
                    _port = new SerialPort(com, DeviceControllerConstants.DefaultOpenBaud, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout = DeviceControllerConstants.DefaultReadTimeout,
                        WriteTimeout = DeviceControllerConstants.DefaultWriteTimeout,
                        Encoding = Encoding.ASCII,
                        NewLine = "\n"
                    };
                }
                else
                {
                    if (_port.IsOpen) _port.Close();
                    _port.PortName = com;
                    _port.BaudRate = DeviceControllerConstants.DefaultOpenBaud;
                }

                _port.Open();
                if (!_port.IsOpen)
                    return false;

                Thread.Sleep(DeviceControllerConstants.PortOpenDelayMs);

                try
                {
                    _port.DiscardInBuffer();
                    _port.Write("km.version()\r");
                    Thread.Sleep(100);
                    Device.version = _port.ReadLine();
                    DebugLogger.LogDebug($"[GenericKM] {com} km.version(): {Device.version}");
                }
                catch
                {
                    Device.version = string.Empty;
                }

                try
                {
                    _port.Write("km.buttons(1)\r\n");
                    _port.Write("km.echo(0)\r\n");
                    _port.DiscardInBuffer();
                }
                catch (Exception ex)
                {
                    DebugLogger.LogDebug($"[GenericKM] Warning: km.buttons/km.echo failed on {com}: {ex}");
                }

                DeviceButtonListener.StartListening();
                Device.InitButtonState();

                Device.connected = true;
                Device.DeviceKind = KmDeviceKind.Generic;

                DebugLogger.LogDebug($"[+] Generic KM device connected to {_port.PortName} at {_port.BaudRate} baudrate");
                return true;
            }
            catch (Exception ex)
            {
                Device.connected = false;
                Device.DeviceKind = KmDeviceKind.Unknown;
                DebugLogger.LogDebug($"[-] Generic KM device failed to connect on {com}. {ex}");
                try
                {
                    if (_port?.IsOpen == true)
                        _port.Close();
                }
                catch { }
                return false;
            }
        }

        public static bool ConnectAuto(string com)
        {
            if (TryConnectMakcuOnPort(com))
                return true;

            return ConnectGenericKm(com);
        }

        public static bool ValidateMakcuSignature(int timeoutMs = DeviceControllerConstants.SignatureValidationTimeout)
        {
            try
            {
                if (_port == null || !_port.IsOpen) return false;

                _port.DiscardInBuffer();
                _port.Write("km.version()\r");

                int oldTimeout = _port.ReadTimeout;
                _port.ReadTimeout = timeoutMs;

                string line = _port.ReadLine()?.Trim();
                _port.ReadTimeout = oldTimeout;

                DebugLogger.LogDebug($"[ValidateMakcu] Response: '{line}'");

                if (string.IsNullOrEmpty(line))
                {
                    DebugLogger.LogDebug($"[ValidateMakcu] Empty response");
                    return false;
                }

                bool ok = line.StartsWith(DeviceControllerConstants.MakcuExpectSignature, StringComparison.OrdinalIgnoreCase)
                       || line.Contains(DeviceControllerConstants.MakcuExpectSignature, StringComparison.OrdinalIgnoreCase);

                if (ok)
                {
                    Device.version = line;
                    DebugLogger.LogDebug($"[ValidateMakcu] Signature VALID");
                }
                else
                {
                    DebugLogger.LogDebug($"[ValidateMakcu] Signature INVALID (expected '{DeviceControllerConstants.MakcuExpectSignature}')");
                }

                return ok;
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[ValidateMakcu] Exception: {ex.Message}");
                return false;
            }
        }

        public static string GetVersion()
        {
            if (_port == null || !_port.IsOpen)
                return Device.version = $"Port Null or Closed : {_port?.PortName} {_port?.IsOpen} {_port?.BaudRate} ";

            try
            {
                _port.DiscardInBuffer();
                _port.Write("km.version()\r");
                Thread.Sleep(100);
                Device.version = _port.ReadLine();
                DebugLogger.LogDebug($"[GetVersion] Response: '{Device.version}'");
                return Device.version;
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[GetVersion] Error: {ex.Message}");
                return Device.version = "";
            }
        }
    }
}
