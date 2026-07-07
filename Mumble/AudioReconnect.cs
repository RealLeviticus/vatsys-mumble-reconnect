using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using vatsys;

namespace MumbleReconnect
{
    internal static class AudioReconnect
    {
        private const int ReconnectTimeoutSeconds = 5;
        private const int ReconnectPollIntervalMs = 250;
        private const int StatusPollIntervalMs = 5000;

        // Escalating delays between automatic reconnection attempts. After the last
        // fast attempt fails we log a single give-up message and drop to the slow
        // interval indefinitely, rather than restarting the fast cycle and spamming
        // the error window / server with connection attempts.
        private static readonly int[] FastRetryDelaysSeconds = { 5, 10, 20, 40, 60 };
        private const int SlowRetryIntervalSeconds = 60;

        private static readonly object _lock = new object();
        private static readonly SemaphoreSlim _reconnectGate = new SemaphoreSlim(1, 1);
        private static volatile bool _everConnected;
        private static volatile bool _manualDisconnect;
        private static bool _outageActive;
        private static bool _gaveUp;
        private static int _retryCount;
        private static volatile bool _lastConnected;
        private static DateTime _nextRetryUtc = DateTime.MinValue;
        private static System.Threading.Timer _pollTimer;
        private static int _tickRunning;

        public static event Action<bool> StatusChanged;

        private class ReconnectResult
        {
            public bool Success { get; set; }
            public string ErrorMessage { get; set; }
        }

        private static readonly Lazy<Type> MumbleType = new Lazy<Type>(() => typeof(Network).Assembly.GetType("vatsys.Mumble"));
        private static readonly Lazy<FieldInfo> MumbleInstanceField =
            new Lazy<FieldInfo>(() => MumbleType.Value?.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic));
        private static readonly Lazy<MethodInfo> MumbleReconnectMethod =
            new Lazy<MethodInfo>(() => MumbleType.Value?.GetMethod("Reconnect", BindingFlags.Instance | BindingFlags.NonPublic));
        private static readonly Lazy<MethodInfo> MumbleConnectMethod =
            new Lazy<MethodInfo>(() => MumbleType.Value?.GetMethod("Connect", BindingFlags.Instance | BindingFlags.NonPublic));
        private static readonly Lazy<MethodInfo> MumbleIsConnectedGetter =
            new Lazy<MethodInfo>(() => MumbleType.Value?.GetProperty("IsConnected", BindingFlags.Static | BindingFlags.Public)?.GetMethod);
        private static readonly Lazy<MethodInfo> MumbleDisconnectMethod =
            new Lazy<MethodInfo>(() => MumbleType.Value?.GetMethod("Disconnect", BindingFlags.Static | BindingFlags.Public));

        internal static void Init()
        {
            try
            {
                _everConnected = IsConnected;
                _lastConnected = IsConnected;
                NotifyStatus(IsConnected);

                // Start a background timer to poll connection status and drive auto-reconnect
                _pollTimer = new System.Threading.Timer(
                    _ => TickAutoReconnect(),
                    null,
                    StatusPollIntervalMs,
                    StatusPollIntervalMs);
            }
            catch (Exception ex)
            {
                Errors.Add(ex, Plugin.DisplayName);
            }
        }

        internal static bool TryReconnect(out string errorMessage)
        {
            var result = TryReconnectCoreAsync().GetAwaiter().GetResult();
            errorMessage = result.ErrorMessage;
            return result.Success;
        }

        internal static async Task<bool> TryReconnectAsync()
        {
            var result = await TryReconnectCoreAsync().ConfigureAwait(false);
            return result.Success;
        }

        private static async Task<ReconnectResult> TryReconnectCoreAsync()
        {
            // A reconnect request (manual or automatic) always re-arms auto-reconnect.
            _manualDisconnect = false;

            // Serialise reconnect attempts - vatsys.Mumble.Connect() tears down any
            // existing connection first, so overlapping attempts kill each other.
            await _reconnectGate.WaitAsync().ConfigureAwait(false);
            try
            {
                // Re-check under the gate: another attempt (or vatSys itself) may have
                // already restored the connection, and invoking Reconnect now would
                // drop a healthy connection before dialling again.
                if (IsConnected)
                {
                    NotifyStatus(true);
                    return new ReconnectResult { Success = true };
                }

                var mumbleType = MumbleType.Value;

                if (mumbleType == null)
                {
                    throw new InvalidOperationException("Unable to locate vatsys.Mumble type.");
                }

                var instance = MumbleInstanceField.Value?.GetValue(null);

                if (instance == null)
                {
                    throw new InvalidOperationException("Audio client has not been initialised yet.");
                }

                var reconnectMethod = MumbleReconnectMethod.Value ?? MumbleConnectMethod.Value;

                if (reconnectMethod == null)
                {
                    throw new InvalidOperationException("Reconnect/connect method is not available on the vatsys Mumble client.");
                }

                var invokeResult = reconnectMethod.Invoke(instance, null);

                // The Connect() fallback returns a Task; observe it so a faulted
                // connect attempt doesn't raise an unobserved task exception.
                if (invokeResult is Task invokeTask)
                {
                    _ = invokeTask.ContinueWith(
                        t => { var _ignored = t.Exception; },
                        TaskContinuationOptions.OnlyOnFaulted);
                }

                var deadline = DateTime.UtcNow.AddSeconds(ReconnectTimeoutSeconds);
                while (DateTime.UtcNow < deadline)
                {
                    if (IsConnected)
                    {
                        NotifyStatus(true);
                        return new ReconnectResult { Success = true };
                    }
                    await Task.Delay(ReconnectPollIntervalMs).ConfigureAwait(false);
                }

                var errorMessage = $"Reconnect command sent but link did not come up within {ReconnectTimeoutSeconds} seconds.";
                NotifyStatus(false);
                return new ReconnectResult { Success = false, ErrorMessage = errorMessage };
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"Failed to reconnect audio: {ex.Message}"), Plugin.DisplayName);
                return new ReconnectResult { Success = false, ErrorMessage = ex.Message };
            }
            finally
            {
                _reconnectGate.Release();
            }
        }

        internal static bool IsConnected => IsMumbleConnected();

        internal static void TryDisconnect()
        {
            try
            {
                var disconnectMethod = MumbleDisconnectMethod.Value;

                disconnectMethod?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"Failed to disconnect audio: {ex.Message}"), Plugin.DisplayName);
            }
            finally
            {
                // Deliberate disconnect - stop the auto-reconnect loop fighting it.
                _manualDisconnect = true;
                ResetRetryState();
                NotifyStatus(false);
            }
        }

        private static bool IsMumbleConnected()
        {
            try
            {
                var getter = MumbleIsConnectedGetter.Value;

                if (getter == null) return false;

                return (bool)getter.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"Failed to check Mumble connection status: {ex.Message}"), Plugin.DisplayName);
                return false;
            }
        }

        private static void ResetRetryState()
        {
            lock (_lock)
            {
                _outageActive = false;
                _gaveUp = false;
                _retryCount = 0;
                _nextRetryUtc = DateTime.MinValue;
            }
        }

        private static void NotifyStatus(bool connected)
        {
            try
            {
                StatusChanged?.Invoke(connected);
                _lastConnected = connected;
                if (connected)
                {
                    ResetRetryState();
                }
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"Error notifying status change: {ex.Message}"), Plugin.DisplayName);
            }
        }

        internal static void TickAutoReconnect()
        {
            // Prevent reentrant calls - the timer can fire again while TryReconnect blocks
            if (Interlocked.CompareExchange(ref _tickRunning, 1, 0) != 0) return;
            try
            {
                TickAutoReconnectCore();
            }
            finally
            {
                Interlocked.Exchange(ref _tickRunning, 0);
            }
        }

        private static void TickAutoReconnectCore()
        {
            // Always poll the real Mumble state, even off-network - vatSys
            // deliberately disconnects Mumble when the VATSIM network drops
            // (Audio.VATSIM_NetworkDisconnected), and the indicator must follow.
            var connected = IsMumbleConnected();

            if (connected)
            {
                _everConnected = true;
                _manualDisconnect = false;
                if (!_lastConnected) NotifyStatus(true);
                ResetRetryState();
                _lastConnected = true;
                return;
            }

            // Don't react at all if Mumble has never connected yet -
            // the user may still be in the process of connecting.
            if (!_everConnected)
            {
                return;
            }

            // Mumble is down - reflect it in the UI on the transition.
            if (_lastConnected)
            {
                NotifyStatus(false);
                _lastConnected = false;
            }

            // No auto-reconnect (or outage logging) while off the network -
            // vatSys reconnects Mumble itself when the network comes back.
            if (!Network.IsConnected)
            {
                ResetRetryState();
                return;
            }

            // The user disconnected on purpose - don't reconnect behind their back.
            if (_manualDisconnect)
            {
                return;
            }

            bool firstDetection;
            lock (_lock)
            {
                firstDetection = !_outageActive;
                if (firstDetection)
                {
                    _outageActive = true;
                    _gaveUp = false;
                    _retryCount = 0;
                    _nextRetryUtc = DateTime.UtcNow.AddSeconds(FastRetryDelaysSeconds[0]);
                }
            }

            if (firstDetection)
            {
                var errorMsg = $"Connection to Mumble Lost. Reconnection attempt in {FastRetryDelaysSeconds[0]} seconds.";
                Errors.Add(new Exception(errorMsg), Plugin.DisplayName);
            }

            lock (_lock)
            {
                if (DateTime.UtcNow < _nextRetryUtc) return;
            }

            if (!Network.IsConnected || !Network.ValidATC || !Network.IsOfficialServer)
            {
                lock (_lock)
                {
                    _nextRetryUtc = DateTime.UtcNow.AddSeconds(SlowRetryIntervalSeconds);
                }
                return;
            }

            int attemptNumber;
            lock (_lock)
            {
                attemptNumber = ++_retryCount;
            }

            var ok = TryReconnect(out _);
            if (ok)
            {
                ResetRetryState();
                NotifyStatus(true);
                return;
            }

            bool justGaveUp;
            lock (_lock)
            {
                justGaveUp = !_gaveUp && attemptNumber >= FastRetryDelaysSeconds.Length;
                if (justGaveUp)
                {
                    _gaveUp = true;
                }

                var delaySeconds = _gaveUp
                    ? SlowRetryIntervalSeconds
                    : FastRetryDelaysSeconds[Math.Min(attemptNumber, FastRetryDelaysSeconds.Length - 1)];
                _nextRetryUtc = DateTime.UtcNow.AddSeconds(delaySeconds);
            }

            if (justGaveUp)
            {
                var errorMsg = $"Failed to reconnect to Mumble after {attemptNumber} attempts. " +
                    $"Auto-retry will continue every {SlowRetryIntervalSeconds} seconds - you can also reconnect manually from the Mumble menu.";
                Errors.Add(new Exception(errorMsg), Plugin.DisplayName);
            }
        }
    }
}
