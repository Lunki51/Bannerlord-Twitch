using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using BannerlordTwitch.Util;
using TwitchLib.Api.Core.Enums;

namespace BannerlordTwitch
{
    /// <summary>
    /// Sends messages to the Twitch Extension panel via the Extensions PubSub API.
    /// Endpoint: POST https://api.twitch.tv/helix/extensions/pubsub
    /// </summary>
    public class ExtensionPubSubService
    {
        private const string PubSubUrl = "https://api.twitch.tv/helix/extensions/pubsub";
        private const string HelixUsersUrl = "https://api.twitch.tv/helix/users";

        // Separate HttpClient for direct calls — bypasses TwitchLib's FormatOAuth helper
        // which mangles non-standard tokens like our extension JWT.
        private readonly HttpClient _directHttp = new();

        private readonly CustomTwitchHttpClient _http;
        private readonly string _extensionClientId;
        private readonly string _extensionSecret;
        private readonly string _broadcasterId;
        private readonly string _broadcasterAccessToken;
        private readonly string _broadcasterClientId;

        // username (case-insensitive) -> Twitch userId
        private readonly ConcurrentDictionary<string, string> _userIdCache =
            new(StringComparer.OrdinalIgnoreCase);

        public ExtensionPubSubService(
            CustomTwitchHttpClient http,
            string extensionClientId,
            string extensionSecret,
            string broadcasterId,
            string broadcasterAccessToken,
            string broadcasterClientId)
        {
            _http = http;
            _extensionClientId = extensionClientId;
            _extensionSecret = extensionSecret;
            _broadcasterId = broadcasterId;
            _broadcasterAccessToken = broadcasterAccessToken;
            _broadcasterClientId = broadcasterClientId;
        }

        // ── Public API ────────────────────────────────────────────────────────

        public async Task SendBroadcastAsync(string[] messages)
        {
            var inner = new ExtensionMessage { type = "broadcast", user = null, messages = messages };
            await PostAsync(new[] { "broadcast" }, inner);
        }

        public async Task SendWhisperToUserNameAsync(string userName, string[] messages)
        {
            var userId = await ResolveUserIdAsync(userName);
            if (userId == null)
            {
                // Can't identify the user — fall back to broadcast so the message
                // still appears in the panel (frontend will show it without filtering).
                Log.Trace($"[ExtensionPubSub] Could not resolve userId for '{userName}', falling back to broadcast");
                var broadcastInner = new ExtensionMessage { type = "broadcast", user = null, messages = messages };
                await PostAsync(new[] { "broadcast" }, broadcastInner);
                return;
            }

            // Always put the resolved userId in the user field so the frontend can
            // reliably compare it against auth.userId (which is also a numeric id string).
            var inner = new ExtensionMessage { type = "reply", user = userId, messages = messages };
            await PostAsync(new[] { "broadcast" }, inner);
        }

        public void RegisterUser(string userName, string userId)
        {
            if (!string.IsNullOrWhiteSpace(userName) && !string.IsNullOrWhiteSpace(userId))
                _userIdCache[userName] = userId;
        }

        // ── userId resolution ─────────────────────────────────────────────────

        private async Task<string> ResolveUserIdAsync(string userName)
        {
            if (_userIdCache.TryGetValue(userName, out var cached))
                return cached;

            try
            {
                var response = await _http.GeneralRequestAsync(
                    $"{HelixUsersUrl}?login={Uri.EscapeDataString(userName)}",
                    "GET",
                    payload: null,
                    api: ApiVersion.Helix,
                    clientId: _broadcasterClientId,
                    accessToken: _broadcasterAccessToken);

                using var doc = JsonDocument.Parse(response.Value);
                var data = doc.RootElement.GetProperty("data");
                if (data.GetArrayLength() == 0) return null;

                var userId = data[0].GetProperty("id").GetString();
                if (!string.IsNullOrEmpty(userId))
                    _userIdCache[userName] = userId;

                return userId;
            }
            catch (Exception ex)
            {
                Log.Error($"[ExtensionPubSub] Failed to resolve userId for '{userName}': {ex.Message}");
                return null;
            }
        }

        // ── PubSub POST (direct HttpClient, no TwitchLib helpers) ────────────

        private async Task PostAsync(string[] targets, ExtensionMessage inner)
        {
            try
            {
                string innerJson = JsonSerializer.Serialize(inner);
                string jwt = BuildExtensionJwt();

                if (string.IsNullOrWhiteSpace(jwt))
                    Log.Error("[ExtensionPubSub] JWT is empty");

                if (jwt.Count(c => c == '.') != 2)
                    Log.Error("[ExtensionPubSub] JWT does not have 3 parts");

                if (jwt.Any(char.IsWhiteSpace))
                    throw new Exception("JWT contains whitespace");

                Log.Info($"[ExtensionPubSub] JWT LENGTH: {jwt.Length}");

                var outer = new ExtensionPubSubPayload
                {
                    broadcaster_id = _broadcasterId,
                    target = targets,
                    message = innerJson,
                };

                string payloadJson = JsonSerializer.Serialize(outer);

                var request = new HttpRequestMessage(HttpMethod.Post, PubSubUrl);
                request.Headers.Add("Client-ID", _extensionClientId);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

                var response = await _directHttp.SendAsync(request);
                Log.Info($"[ExtensionPubSub] Response: {(int)response.StatusCode} {response.StatusCode}");
                Log.Info($"[ExtensionPubSub] Payload: {payloadJson}");
                Log.Info($"[ExtensionPubSub] Inner: {innerJson}");

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Log.Error($"[ExtensionPubSub] FAILED {response.StatusCode}: {error}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[ExtensionPubSub] EXCEPTION: {ex}");
            }
        }

        // ── JWT ───────────────────────────────────────────────────────────────

        private string BuildExtensionJwt()
        {
            if (string.IsNullOrWhiteSpace(_extensionSecret))
                throw new Exception("Extension secret is null/empty");

            byte[] secretBytes;
            try
            {
                secretBytes = Convert.FromBase64String(_extensionSecret.Trim());
            }
            catch
            {
                throw new Exception("Extension secret is not valid base64");
            }

            var now = DateTimeOffset.UtcNow;

            var header = new { alg = "HS256", typ = "JWT" };
            var payload = new
            {
                exp = now.AddMinutes(3).ToUnixTimeSeconds(),
                iat = now.AddSeconds(-30).ToUnixTimeSeconds(),
                user_id = _broadcasterId,
                role = "external",
                channel_id = _broadcasterId,
                app_id = _extensionClientId,
                pubsub_perms = new
                {
                    send = new[] { "broadcast", "whisper-*" }
                }
            };

            var headerB64 = Base64UrlEncode(JsonSerializer.Serialize(header));
            var payloadB64 = Base64UrlEncode(JsonSerializer.Serialize(payload));

            var signingInput = $"{headerB64}.{payloadB64}";

            using var hmac = new HMACSHA256(secretBytes);
            var sig = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput)));

            return $"{signingInput}.{sig}";
        }

        private static string Base64UrlEncode(string input) =>
            Base64UrlEncode(Encoding.UTF8.GetBytes(input));

        private static string Base64UrlEncode(byte[] data) =>
            Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        // ── Wire shapes ───────────────────────────────────────────────────────

        private class ExtensionPubSubPayload
        {
            public string broadcaster_id { get; set; }
            public string[] target { get; set; }
            public string message { get; set; }
        }

        private class ExtensionMessage
        {
            public string type { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string user { get; set; }
            public string[] messages { get; set; }
        }
    }
}