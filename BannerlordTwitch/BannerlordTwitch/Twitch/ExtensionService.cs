using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using BannerlordTwitch;
using BannerlordTwitch.Util;
using Newtonsoft.Json;

namespace BannerlordTwitch.Twitch
{
    public static class ExtensionService
    {
        private static readonly AuthSettings authSettings;
        //private static readonly string BackendUrl = $"http://localhost:{authSettings.ExtensionPort}";
        private static readonly string BltSecret = "BLT123";
        private static readonly HttpClient http = new();

        public static void SendReply(string userName, params string[] messages)
        {
            _ = PostAsync(new
            {
                type = "reply",
                user = userName,
                messages
            });
        }

        public static void SendBroadcast(params string[] messages)
        {
            _ = PostAsync(new
            {
                type = "message",
                user = (string)null,
                messages
            });
        }

        public static void RegisterUser(string userId, string userName)
        {
            _ = PostRegisterAsync(userId, userName);
        }

        private static async Task PostRegisterAsync(string userId, string userName)
        {
            try
            {
                var json = JsonConvert.SerializeObject(new { userId, username = userName });
                //var request = new HttpRequestMessage(HttpMethod.Post, $"{BackendUrl}/register-by-id");
                //request.Headers.Add("x-blt-secret", BltSecret);
                //request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                //await http.SendAsync(request);
            }
            catch (Exception e)
            {
                Log.Trace($"[ExtensionService] Failed to register user: {e.Message}");
            }
        }

        private static async Task PostAsync(object payload)
        {
            try
            {
                var json = JsonConvert.SerializeObject(payload);
                //var request = new HttpRequestMessage(HttpMethod.Post, $"{BackendUrl}/message");
                //request.Headers.Add("x-blt-secret", BltSecret);
                //request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                //var response = await http.SendAsync(request);
                //if (!response.IsSuccessStatusCode)
                //{
                //    Log.Trace($"[ExtensionService] Backend returned {response.StatusCode}");
                //}
            }
            catch (Exception e)
            {
                Log.Trace($"[ExtensionService] Failed to send message: {e.Message}");
            }
        }
    }
}