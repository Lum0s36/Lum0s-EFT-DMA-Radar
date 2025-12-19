/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

namespace LoneEftDmaRadar.UI.Misc
{
    /// <summary>
    /// Handles mouse movement and button commands sent to the device.
    /// </summary>
    public static class DeviceCommands
    {
        private static readonly Random _random = new();
        private static readonly char[] MovePrefix = "km.move(".ToCharArray();
        private static readonly char[] MoveSuffix = ")\n".ToCharArray();
        private static readonly char[] IntBuffer = new char[32];

        public static void Move(int x, int y)
        {
            if (!Device.connected || DeviceConnection.Port == null || !DeviceConnection.Port.IsOpen) return;
            if ((uint)(x - short.MinValue) > ushort.MaxValue ||
                (uint)(y - short.MinValue) > ushort.MaxValue) return;

            int len = 0;
            len += FormatInt(x, IntBuffer, len);
            IntBuffer[len++] = ',';
            len += FormatInt(y, IntBuffer, len);

            try
            {
                lock (DeviceConnection.Port)
                {
                    DeviceConnection.Port.Write(MovePrefix, 0, MovePrefix.Length);
                    DeviceConnection.Port.Write(IntBuffer, 0, len);
                    DeviceConnection.Port.Write(MoveSuffix, 0, MoveSuffix.Length);
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

        public static void MoveLegacy(int x, int y)
        {
            if (!Device.connected) return;
            try
            {
                DeviceConnection.Port.Write($"km.move({x}, {y})\r");
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

        public static void MoveSmooth(int x, int y, int segments)
        {
            if (!Device.connected) return;
            DeviceConnection.Port.Write($"km.move({x}, {y}, {segments})\r");
            _ = DeviceConnection.Port.BaseStream.FlushAsync();
        }

        public static void MoveBezier(int x, int y, int segments, int ctrlX, int ctrlY)
        {
            if (!Device.connected) return;
            DeviceConnection.Port.Write($"km.move({x}, {y}, {segments}, {ctrlX}, {ctrlY})\r");
            _ = DeviceConnection.Port.BaseStream.FlushAsync();
        }

        public static void MouseWheel(int delta)
        {
            if (!Device.connected) return;
            DeviceConnection.Port.Write($"km.wheel({delta})\r");
            _ = DeviceConnection.Port.BaseStream.FlushAsync();
        }

        public static void LockAxis(string axis, int bit)
        {
            if (!Device.connected) return;
            DeviceConnection.Port.Write($"km.lock_m{axis}({bit})\r");
            _ = DeviceConnection.Port.BaseStream.FlushAsync();
        }

        public static void Click(string button, int msDelay, int clickDelay = 0)
        {
            if (!Device.connected) return;

            int time = _random.Next(DeviceControllerConstants.MinRandomPressTimeMs, DeviceControllerConstants.MaxRandomPressTimeMs);
            Thread.Sleep(clickDelay);
            DeviceConnection.Port.Write($"km.{button}(1)\r");
            Thread.Sleep(time);
            DeviceConnection.Port.Write($"km.{button}(0)\r");
            _ = DeviceConnection.Port.BaseStream.FlushAsync();
            Thread.Sleep(msDelay);
        }

        public static void Press(DeviceAimbotMouseButton button, int press)
        {
            if (!Device.connected) return;
            string cmd = $"km.{MouseButtonToString(button)}({press})\r";
            DeviceConnection.Port.Write(cmd);
            _ = DeviceConnection.Port.BaseStream.FlushAsync();
        }

        public static async void LockButton(DeviceAimbotMouseButton button, int bit)
        {
            if (!Device.connected) return;

            string cmd = button switch
            {
                DeviceAimbotMouseButton.Left => $"km.lock_ml({bit})\r",
                DeviceAimbotMouseButton.Right => $"km.lock_mr({bit})\r",
                DeviceAimbotMouseButton.Middle => $"km.lock_mm({bit})\r",
                DeviceAimbotMouseButton.mouse4 => $"km.lock_ms1({bit})\r",
                DeviceAimbotMouseButton.mouse5 => $"km.lock_ms2({bit})\r",
                _ => $"km.lock_ml({bit})\r"
            };

            await Task.Delay(1);
            DeviceConnection.Port.Write(cmd);
            await DeviceConnection.Port.BaseStream.FlushAsync();
        }

        public static void UnlockAllButtons()
        {
            if (DeviceConnection.Port?.IsOpen == true)
            {
                DeviceConnection.Port.Write("km.lock_ml(0)\r");
                DeviceConnection.Port.Write("km.lock_mr(0)\r");
                DeviceConnection.Port.Write("km.lock_mm(0)\r");
                DeviceConnection.Port.Write("km.lock_ms1(0)\r");
                DeviceConnection.Port.Write("km.lock_ms2(0)\r");
            }
        }

        public static void SetMouseSerial(string serial)
        {
            if (!Device.connected) return;
            DeviceConnection.Port.Write($"km.serial({serial})\r");
        }

        public static void ResetMouseSerial()
        {
            if (!Device.connected) return;
            DeviceConnection.Port.Write("km.serial(0)\r");
        }

        public static string MouseButtonToString(DeviceAimbotMouseButton button)
        {
            return button switch
            {
                DeviceAimbotMouseButton.Left => "left",
                DeviceAimbotMouseButton.Right => "right",
                DeviceAimbotMouseButton.Middle => "middle",
                DeviceAimbotMouseButton.mouse4 => "ms1",
                DeviceAimbotMouseButton.mouse5 => "ms2",
                _ => "left"
            };
        }

        public static int MouseButtonToInt(DeviceAimbotMouseButton button) => (int)button;
        public static DeviceAimbotMouseButton IntToMouseButton(int button) => (DeviceAimbotMouseButton)button;

        private static int FormatInt(int value, char[] buf, int offset)
        {
            return value.TryFormat(buf.AsSpan(offset), out int written)
                ? written
                : 0;
        }
    }
}
