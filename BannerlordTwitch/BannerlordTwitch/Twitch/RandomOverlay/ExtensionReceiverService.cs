using System;
using System.Threading;
using Newtonsoft.Json;
using static BannerlordTwitch.BLTModule;
using BLTOverlay;

namespace BLTOverlay
{
    public class ExtensionReceiverService
    {
        private readonly string channelId;
        private readonly string accessToken;
        private Timer pollTimer;

        public event Action<OverlayCommandMessage> OnMessageReceived;
        public class OverlayCommandMessage
        {
            public string Command { get; set; }
            public string UserId { get; set; }
            public string UserName { get; set; }
        }
        public ExtensionReceiverService(string channelId, string accessToken)
        {
            this.channelId = channelId;
            this.accessToken = accessToken;
        }

        public void Start()
        {
            pollTimer = new Timer(Poll, null, 0, 1000); // 1s polling
        }

        private async void Poll(object state)
        {
            try
            {
                // TODO: Replace with your actual backend call
                string json = await FetchNextMessage();

                if (string.IsNullOrEmpty(json))
                    return;

                var msg = JsonConvert.DeserializeObject<OverlayCommandMessage>(json);

                OnMessageReceived?.Invoke(msg);
            }
            catch (Exception ex)
            {
                // swallow or log
            }
        }

        private async System.Threading.Tasks.Task<string> FetchNextMessage()
        {
            // placeholder
            return null;
        }

        public void Stop()
        {
            pollTimer?.Dispose();
        }
    }
}