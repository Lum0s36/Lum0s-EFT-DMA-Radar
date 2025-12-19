/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

namespace LoneEftDmaRadar.UI.Misc
{
    /// <summary>
    /// Constants used by DeviceController and related classes.
    /// </summary>
    public static class DeviceControllerConstants
    {
        // Makcu VID/PID/Serial
        public const string MakcuVid = "1A86";
        public const string MakcuPid = "7523";
        public const string MakcuSerialFragment = "MAKCU";
        public const string MakcuFriendlyName = "USB-SERIAL CH340";
        public const string MakcuExpectSignature = "km.MAKCU";

        // CH340/CH9102 PIDs
        public const string Ch340Pid = "7523";
        public const string Ch9102Pid = "55D4";
        public const string Ch340FriendlyName = "CH340";
        public const string Ch9102FriendlyName = "CH9102";

        // Makcu signatures for validation
        public static readonly string[] MakcuSignatures = { "km.MAKCU", "km.Makcu", "MAKCU" };

        // Generic fallback friendly names
        public static readonly string[] GenericFriendlyFallbacks = { "USB-SERIAL", "USB Serial", "CH340", "CH9102" };

        // Serial port settings
        public const int DefaultOpenBaud = 115200;
        public const int HighBaud = 1000000;
        public const int DefaultReadTimeout = 500;
        public const int DefaultWriteTimeout = 500;

        // Timing constants
        public const int PortOpenDelayMs = 50;
        public const int BaudChangeDelayMs = 10;
        public const int ReconnectDelayMs = 1000;
        public const int ListenerStartDelayMs = 100;
        public const int ConnectionLostRetryDelayMs = 500;
        public const int ButtonReaderExceptionDelayMs = 100;
        public const int SignatureValidationTimeout = 500;

        // Click timing
        public const int MinRandomPressTimeMs = 20;
        public const int MaxRandomPressTimeMs = 40;

        // Makcu segment timing
        public const int MakcuSegmentMsDefault = 10;

        // Button count
        public const int MouseButtonCount = 5;

        // Valid button bytes (0-31 for 5 buttons bitmask)
        public static readonly byte[] ValidButtonBytes = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

        // Change mode command
        public static readonly byte[] ChangeModeCommand = { 0xDE, 0xAD, 0x02, 0x00, 0xA5, 0x00 };
    }
}
