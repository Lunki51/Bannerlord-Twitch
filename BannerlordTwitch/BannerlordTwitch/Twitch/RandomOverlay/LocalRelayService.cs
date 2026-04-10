using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BannerlordTwitch.Util;

namespace BannerlordTwitch
{
    /// <summary>
    /// Connects the game mod to the local BLT relay server (ws://localhost:3000/ws).
    /// Forwards game state to browser overlays and receives commands from them.
    /// Works alongside or as a replacement for ExtensionPubSubService.
    /// </summary>
    public class LocalRelayService : IDisposable
    {
        private const string ServerUrl = "ws://localhost:3000/ws";
        private const int ReconnectMs = 5000;
        private const int SendTimeoutMs = 3000;

        private ClientWebSocket _ws;
        private CancellationTokenSource _cts = new();
        private bool _disposed;

        /// <summary>Fired on the calling thread when a command arrives from the overlay.</summary>
        public event Action<string /*command*/, string /*userName*/> OnCommandReceived;

        public bool IsConnected => _ws?.State == WebSocketState.Open;

        public LocalRelayService()
        {
            _ = RunAsync();
        }

        // ── Connection loop ────────────────────────────────────────────────

        private async Task RunAsync()
        {
            while (!_disposed)
            {
                try
                {
                    _ws?.Dispose();
                    _ws = new ClientWebSocket();
                    // Identify as the game so the server routes correctly
                    _ws.Options.SetRequestHeader("x-client-type", "game");

                    Log.Info($"[LocalRelay] Connecting to {ServerUrl}…");
                    await _ws.ConnectAsync(new Uri(ServerUrl), _cts.Token);
                    Log.LogFeedSystem("[LocalRelay] Connected to overlay server");

                    await ReceiveLoopAsync();
                }
                catch (OperationCanceledException)
                {
                    break;  // Disposed — exit cleanly
                }
                catch (Exception ex)
                {
                    Log.Error($"[LocalRelay] Connection lost: {ex.Message}");
                }

                if (_disposed) break;
                Log.Info($"[LocalRelay] Reconnecting in {ReconnectMs / 1000}s…");
                try { await Task.Delay(ReconnectMs, _cts.Token); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task ReceiveLoopAsync()
        {
            var buf = new byte[65536];
            var sb = new StringBuilder();

            while (_ws.State == WebSocketState.Open && !_disposed)
            {
                sb.Clear();
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    sb.Append(Encoding.UTF8.GetString(buf, 0, result.Count));
                } while (!result.EndOfMessage);

                HandleIncoming(sb.ToString());
            }
        }

        private void HandleIncoming(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
                if (type != "command") return;

                var command = root.TryGetProperty("command", out var c) ? c.GetString() : null;
                var userName = root.TryGetProperty("userName", out var u) ? u.GetString() : null;

                if (string.IsNullOrWhiteSpace(command)) return;

                Log.Trace($"[LocalRelay] ← cmd '{command}' from '{userName}'");
                OnCommandReceived?.Invoke(command, userName);

                // Route directly into the game on the main thread
                MainThreadSync.Run(() =>
                {
                    BLTModule.TwitchService?.ExecuteOverlayRaw(command, userName);
                });
            }
            catch (Exception ex)
            {
                Log.Error($"[LocalRelay] Failed to parse incoming message: {ex.Message}");
            }
        }

        // ── Send helpers ───────────────────────────────────────────────────

        /// <summary>Send any JSON-serialisable object to all overlay clients.</summary>
        public async Task SendAsync(object payload)
        {
            if (_ws?.State != WebSocketState.Open) return;
            try
            {
                var json = JsonSerializer.Serialize(payload);
                var bytes = Encoding.UTF8.GetBytes(json);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                cts.CancelAfter(SendTimeoutMs);
                await _ws.SendAsync(new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text, true, cts.Token);
            }
            catch (OperationCanceledException) { /* timed out or disposed */ }
            catch (Exception ex)
            {
                Log.Error($"[LocalRelay] Send error: {ex.Message}");
            }
        }

        /// <summary>Push a full or partial state snapshot to the overlay.</summary>
        public Task SendStateAsync(object stateData) =>
            SendAsync(new { type = "state", data = stateData });

        /// <summary>Push a text reply to a specific user (or broadcast if userName is null).</summary>
        public Task SendReplyAsync(string userName, string[] messages) =>
            SendAsync(new { type = userName != null ? "reply" : "message", user = userName, messages });

        // ── Dispose ────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            try { _ws?.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutdown", CancellationToken.None).Wait(1000); }
            catch { /* ignore */ }
            _ws?.Dispose();
            _cts.Dispose();
        }
    }
}