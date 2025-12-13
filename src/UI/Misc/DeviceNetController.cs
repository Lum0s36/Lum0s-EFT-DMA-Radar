using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace LoneEftDmaRadar.UI.Misc
{
    /// <summary>
    /// Lightweight KMBox NET wrapper for mouse movement.
    /// </summary>
    internal static class DeviceNetController
    {
        private static KmBoxNetClient _client;
        private static readonly object _lock = new();
        private static CancellationTokenSource _cts;

        public static bool Connected { get; private set; }

        public static async Task<bool> ConnectAsync(string ip, int port, string macHex, int timeoutMs = 3000)
        {
            try
            {
                lock (_lock)
                {
                    Disconnect();

                    _cts = new CancellationTokenSource(timeoutMs);
                    if (!IPAddress.TryParse(ip, out var address))
                    {
                        DebugLogger.LogDebug($"[KMBoxNet] Invalid IP: {ip}");
                        return false;
                    }

                    _client = new KmBoxNetClient(address, port, macHex);
                }

                var ok = await _client.ConnectAsync(_cts.Token).ConfigureAwait(false);
                Connected = ok;
                DebugLogger.LogDebug(ok
                    ? "[KMBoxNet] Connected"
                    : "[KMBoxNet] Connection failed");
                return ok;
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[KMBoxNet] Connect error: {ex}");
                Connected = false;
                return false;
            }
        }

        /// <summary>
        /// Synchronous convenience wrapper for callers that are not async.
        /// </summary>
        public static bool Connect(string ip, int port, string macHex, int timeoutMs = 3000)
        {
            return ConnectAsync(ip, port, macHex, timeoutMs).GetAwaiter().GetResult();
        }

        public static void Disconnect()
        {
            lock (_lock)
            {
                Connected = false;
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
                _client?.Dispose();
                _client = null;
            }
        }

        public static void Move(int dx, int dy)
        {
            if (!Connected || _client == null)
                return;

            try
            {
                _client.MouseMoveAsync((short)dx, (short)dy).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[KMBoxNet] Move error: {ex}");
                Connected = false;
            }
        }
    }
}
