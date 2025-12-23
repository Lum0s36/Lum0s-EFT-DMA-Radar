/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

namespace LoneEftDmaRadar.UI.Misc
{
    /// <summary>
    /// Handles button state listening from the device.
    /// </summary>
    public static class DeviceButtonListener
    {
        private static Thread _buttonInputThread;
        private static bool _runReader = false;
        private static byte _lastButtonState = 0;

        public static void StartListening()
        {
            if (_buttonInputThread != null && _buttonInputThread.IsAlive)
                return;

            Thread.Sleep(DeviceControllerConstants.ListenerStartDelayMs);
            _runReader = true;
            _buttonInputThread = new Thread(ReadButtons)
            {
                IsBackground = true,
                Name = "DeviceButtonListener"
            };
            _buttonInputThread.Start();
        }

        public static void StopListening()
        {
            _runReader = false;
        }

        private static async void ReadButtons()
        {
            await Task.Run(() =>
            {
                DebugLogger.LogDebug("[+] Listening to device buttons.");
                while (_runReader)
                {
                    if (!Device.connected || DeviceConnection.Port == null)
                    {
                        Thread.Sleep(DeviceControllerConstants.ConnectionLostRetryDelayMs);
                        Device.connected = DeviceConnection.Port?.IsOpen == true;
                        continue;
                    }

                    try
                    {
                        if (DeviceConnection.Port.BytesToRead > 0)
                        {
                            int data = DeviceConnection.Port.ReadByte();
                            if (!DeviceControllerConstants.ValidButtonBytes.Contains((byte)data))
                                continue;

                            byte b = (byte)data;
                            
                            // Log state changes
                            if (b != _lastButtonState)
                            {
                                DebugLogger.LogDebug($"[DeviceButtonListener] Button state changed: 0x{_lastButtonState:X2} -> 0x{b:X2}");
                                _lastButtonState = b;
                            }

                            for (int i = 1; i <= DeviceControllerConstants.MouseButtonCount; i++)
                                Device.bState[i] = (b & (1 << (i - 1))) != 0;

                            DeviceConnection.Port.DiscardInBuffer();
                        }
                        else
                        {
                            Thread.Sleep(1);
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogDebug($"[DeviceButtonListener] Error: {ex.Message}");
                        Device.connected = false;
                        Thread.Sleep(DeviceControllerConstants.ButtonReaderExceptionDelayMs);
                    }
                }
            });
        }

        public static bool IsButtonPressed(DeviceAimbotMouseButton button)
        {
            if (!Device.connected || Device.bState == null) return false;
            return Device.bState.TryGetValue((int)button, out bool state) && state;
        }
    }
}
