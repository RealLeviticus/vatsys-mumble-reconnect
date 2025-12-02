using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using vatsys;

namespace MumbleReconnect
{
    internal static class AudioReconnect
    {
        private static bool _everConnected = AFV.IsConnected;
        private static int _promptOpen;
        private static bool _pendingRetry;
        private static int _retryCount;
        private static bool _lastConnected = false;
        private static DateTime _nextRetryUtc = DateTime.MinValue;

        public static event Action<bool> StatusChanged;

        private static readonly Lazy<Type> MumbleType = new Lazy<Type>(() => typeof(Network).Assembly.GetType("vatsys.Mumble"));
        private static readonly Lazy<FieldInfo> MumbleInstanceField =
            new Lazy<FieldInfo>(() => MumbleType.Value?.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic));
        private static readonly Lazy<MethodInfo> MumbleReconnectMethod =
            new Lazy<MethodInfo>(() => MumbleType.Value?.GetMethod("Reconnect", BindingFlags.Instance | BindingFlags.NonPublic));
        private static readonly Lazy<MethodInfo> MumbleConnectMethod =
            new Lazy<MethodInfo>(() => MumbleType.Value?.GetMethod("Connect", BindingFlags.Instance | BindingFlags.NonPublic));
        private static readonly Lazy<MethodInfo> MumbleIsConnectedGetter =
            new Lazy<MethodInfo>(() => MumbleType.Value?.GetProperty("IsConnected", BindingFlags.Static | BindingFlags.Public)?.GetMethod);
        private static readonly Lazy<FieldInfo> MumbleServerField =
            new Lazy<FieldInfo>(() => MumbleType.Value?.GetField("Server", BindingFlags.Static | BindingFlags.Public));
        private static readonly Lazy<MethodInfo> MumbleDisconnectMethod =
            new Lazy<MethodInfo>(() => MumbleType.Value?.GetMethod("Disconnect", BindingFlags.Static | BindingFlags.Public));

        internal static void Init()
        {
            try
            {
                AFV.Connected += (_, __) => OnConnected();
                AFV.Disconnected += (_, __) => OnDisconnected();

                _everConnected = IsConnected;
                _lastConnected = IsConnected;
                NotifyStatus(IsConnected);
            }
            catch (Exception ex)
            {
                Errors.Add(ex, Plugin.DisplayName);
            }
        }

        private static void OnConnected()
        {
            _everConnected = true;
            Interlocked.Exchange(ref _promptOpen, 0);
                NotifyStatus(true);
                _pendingRetry = false;
                _retryCount = 0;
        }

        private static void OnDisconnected()
        {
            if (!_everConnected) return;

            // Only prompt if vatsim network is still up.
            if (!Network.IsConnected)
            {
                NotifyStatus(false);
                return;
            }

            NotifyStatus(false);
            ShowPrompt();
        }

        private static void ShowPrompt()
        {
            if (Interlocked.CompareExchange(ref _promptOpen, 1, 0) != 0) return;

            var promptThread = new Thread(() =>
            {
                try
                {
                    var result = MessageBox.Show(
                        "Audio connection to the AFV/Mumble server was lost. Click Retry to attempt a reconnection.",
                        Plugin.DisplayName,
                        MessageBoxButtons.RetryCancel,
                        MessageBoxIcon.Warning);

                    if (result == DialogResult.Retry)
                    {
                        TryReconnect();
                    }
                }
                catch (Exception ex)
                {
                    Errors.Add(ex, Plugin.DisplayName);
                }
                finally
                {
                    Interlocked.Exchange(ref _promptOpen, 0);
                }
            })
            {
                IsBackground = true
            };

            promptThread.SetApartmentState(ApartmentState.STA);
            promptThread.Start();
        }

        internal static bool TryReconnect()
        {
            return TryReconnect(out _);
        }

        internal static bool TryReconnect(out string errorMessage)
        {
            errorMessage = null;

            try
            {
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

                reconnectMethod.Invoke(instance, null);

                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < deadline)
                {
                    if (IsConnected)
                    {
                        NotifyStatus(true);
                        return true;
                    }
                    Thread.Sleep(250);
                }

                errorMessage = "Reconnect command sent but link did not come up within 5 seconds.";
                NotifyStatus(false);
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                Errors.Add(new Exception($"Failed to reconnect audio: {ex.Message}"), Plugin.DisplayName);
                return false;
            }
        }

        internal static bool IsConnected => IsMumbleConnected();

        internal static string ServerHost
        {
            get
            {
                try
                {
                    var server = MumbleServerField.Value?.GetValue(null) as string;
                    if (!string.IsNullOrWhiteSpace(server)) return server;
                }
                catch { }

                return "Unknown";
            }
        }

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
                NotifyStatus(false);
                _pendingRetry = false;
                _retryCount = 0;
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
            catch
            {
                return false;
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
                    _pendingRetry = false;
                    _retryCount = 0;
                }
            }
            catch { }
        }

        internal static void TickAutoReconnect()
        {
            var connected = IsMumbleConnected();

            if (connected)
            {
                if (!_lastConnected) NotifyStatus(true);
                _pendingRetry = false;
                _retryCount = 0;
                _lastConnected = true;
                return;
            }

            if (!_pendingRetry)
            {
                _pendingRetry = true;
                _retryCount = 0;
                _nextRetryUtc = DateTime.UtcNow.AddSeconds(15);
                Errors.Add(new Exception("Connection to Mumble Lost. Reconnection attempt in 15 seconds."), Plugin.DisplayName);
                NotifyStatus(false);
            }

            _lastConnected = false;

            if (!_pendingRetry) return;

            if (DateTime.UtcNow < _nextRetryUtc) return;

            if (!Network.IsConnected || !Network.ValidATC || !Network.IsOfficialServer)
            {
                // push next retry window forward
                _nextRetryUtc = DateTime.UtcNow.AddSeconds(15);
                return;
            }

            _retryCount++;
            var ok = TryReconnect(out _);
            if (ok)
            {
                _pendingRetry = false;
                _retryCount = 0;
                _nextRetryUtc = DateTime.MinValue;
                NotifyStatus(true);
                return;
            }

            if (_retryCount >= 3)
            {
                _pendingRetry = false;
                _nextRetryUtc = DateTime.MinValue;
                Errors.Add(new Exception("Failed to reconnect to Mumble after multiple attempts. Please restart client."), Plugin.DisplayName);
            }
            else
            {
                _nextRetryUtc = DateTime.UtcNow.AddSeconds(15);
            }
        }
    }
}
