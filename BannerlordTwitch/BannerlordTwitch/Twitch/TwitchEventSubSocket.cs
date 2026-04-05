using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets;
using Microsoft.Extensions.Hosting;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using BannerlordTwitch.Util;

namespace BannerlordTwitch.Twitch
{
    public class TwitchEventSubSocket : IHostedService
    {
        public delegate void ChannelPointsRewardEvent(object e, ChannelPointsCustomRewardRedemption args);
        public delegate void SubWebSocketConnectedEvent(object e, WebsocketConnectedArgs args);
        private readonly ILogger<TwitchEventSubSocket> _logger;
        private readonly EventSubWebsocketClient _eventSubWebsocketClient;

        public SubWebSocketConnectedEvent OnEventSubServiceConnected;
        public ChannelPointsRewardEvent OnChannelPointsRewardsRedeemed;

        public string SessionId { 
            get{
                return _eventSubWebsocketClient?.SessionId;
            } 
        }

        public TwitchEventSubSocket(ILogger<TwitchEventSubSocket> logger = null)
        {
            _logger = logger;
            _eventSubWebsocketClient = new EventSubWebsocketClient(null);

            _eventSubWebsocketClient.WebsocketConnected += OnWebsocketConnected;
            _eventSubWebsocketClient.WebsocketDisconnected += OnWebsocketDisconnected;
            _eventSubWebsocketClient.WebsocketReconnected += OnWebsocketReconnected;
            _eventSubWebsocketClient.ErrorOccurred += OnErrorOccurred;

            _eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += async (object e, ChannelPointsCustomRewardRedemptionArgs args) =>
            {
                OnChannelPointsRewardsRedeemed?.Invoke(e, args.Notification.Payload.Event);
            };

            _eventSubWebsocketClient.ChannelFollow += OnChannelFollow;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _eventSubWebsocketClient.ConnectAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _eventSubWebsocketClient.DisconnectAsync();
        }

        private async Task OnErrorOccurred(object sender, ErrorOccuredArgs e)
        {
            // Was empty — swallowing all websocket errors silently
            Log.Error($"[EventSub] Error: {e.Exception?.Message ?? "(no message)"}");
        }

        private async Task OnChannelFollow(object sender, ChannelFollowArgs e)
        {
            var eventData = e.Notification.Payload.Event;
        }

        private async Task OnWebsocketConnected(object sender, WebsocketConnectedArgs e)
        {
            if (!e.IsRequestedReconnect)
            {
                // Guard against NullReferenceException if nobody subscribed
                OnEventSubServiceConnected?.Invoke(sender, e);
            }
        }

        private async Task OnWebsocketDisconnected(object sender, EventArgs e)
        {
            Log.LogFeedSystem("EventSub disconnected, reconnecting…");
            // Add a small delay before each retry so we don't hammer Twitch
            while (!await _eventSubWebsocketClient.ReconnectAsync())
            {
                Log.Error("[EventSub] Reconnect attempt failed, retrying in 5s…");
                await Task.Delay(5000);
            }
        }

        private async Task OnWebsocketReconnected(object sender, EventArgs e)
        {
        }
    }
}
