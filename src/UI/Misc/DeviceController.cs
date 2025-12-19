/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

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
        Makcu = 1,
        Generic = 2
    }

    /// <summary>
    /// Facade class for device communication. Delegates to specialized classes.
    /// </summary>
    public class Device
    {
        #region State

        public static KmDeviceKind DeviceKind { get; internal set; } = KmDeviceKind.Unknown;
        public static bool connected = false;
        public static string CurrentPortName => DeviceConnection.Port?.PortName;
        public static string version = "";
        public static Dictionary<int, bool> bState { get; private set; }

        internal static void InitButtonState()
        {
            bState = new Dictionary<int, bool>();
            for (int i = 1; i <= DeviceControllerConstants.MouseButtonCount; i++)
                bState[i] = false;
        }

        #endregion

        #region Connection (delegate to DeviceConnection)

        public static void connect(string com) => DeviceConnection.Connect(com);
        public static void disconnect() => DeviceConnection.Disconnect();
        public static void reconnect_device(string com) => DeviceConnection.Reconnect(com);
        public static void SetBaud(int baud) => DeviceConnection.SetBaud(baud);
        public static string GetVersion() => DeviceConnection.GetVersion();
        public static bool TryConnectMakcuOnPort(string com) => DeviceConnection.TryConnectMakcuOnPort(com);
        public static bool ConnectGenericKm(string com) => DeviceConnection.ConnectGenericKm(com);
        public static bool ConnectAuto(string com) => DeviceConnection.ConnectAuto(com);

        #endregion

        #region Auto-Connect (delegate to DeviceAutoConnect)

        public static bool AutoConnectMakcu() => DeviceAutoConnect.AutoConnectMakcu();
        public static bool TryAutoConnect(string lastComPort = null) => DeviceAutoConnect.TryAutoConnect(lastComPort);
        public static string TryGetComByVidPid(string vidHex, string pidHex, string serialContains = null)
            => DeviceAutoConnect.TryGetComByVidPid(vidHex, pidHex, serialContains);
        public static string TryGetComByFriendlyName(string friendlyContains, string serialContains = null)
            => DeviceAutoConnect.TryGetComByFriendlyName(friendlyContains, serialContains);
        public static List<SerialDeviceInfo> EnumerateSerialDevices() => DeviceAutoConnect.EnumerateSerialDevices();

        #endregion

        #region Commands (delegate to DeviceCommands)

        public static void move(int x, int y) => DeviceCommands.MoveLegacy(x, y);
        public static void Move(int x, int y) => DeviceCommands.Move(x, y);
        public static void move_smooth(int x, int y, int segments) => DeviceCommands.MoveSmooth(x, y, segments);
        public static void move_bezier(int x, int y, int segments, int ctrl_x, int ctrl_y)
            => DeviceCommands.MoveBezier(x, y, segments, ctrl_x, ctrl_y);
        public static void mouse_wheel(int delta) => DeviceCommands.MouseWheel(delta);
        public static void lock_axis(string axis, int bit) => DeviceCommands.LockAxis(axis, bit);
        public static void click(string button, int ms_delay, int click_delay = 0)
            => DeviceCommands.Click(button, ms_delay, click_delay);
        public static void press(DeviceAimbotMouseButton button, int press)
            => DeviceCommands.Press(button, press);
        public static void lock_button(DeviceAimbotMouseButton button, int bit)
            => DeviceCommands.LockButton(button, bit);
        public static void unlock_all_buttons() => DeviceCommands.UnlockAllButtons();
        public static void setMouseSerial(string serial) => DeviceCommands.SetMouseSerial(serial);
        public static void resetMouseSerial() => DeviceCommands.ResetMouseSerial();
        public static int MouseButtonToInt(DeviceAimbotMouseButton button) => DeviceCommands.MouseButtonToInt(button);
        public static DeviceAimbotMouseButton IntToMouseButton(int button) => DeviceCommands.IntToMouseButton(button);
        public static string MouseButtonToString(DeviceAimbotMouseButton button) => DeviceCommands.MouseButtonToString(button);

        #endregion

        #region Button Listener (delegate to DeviceButtonListener)

        public static void start_listening() => DeviceButtonListener.StartListening();
        public static bool button_pressed(DeviceAimbotMouseButton button) => DeviceButtonListener.IsButtonPressed(button);

        #endregion
    }
}
