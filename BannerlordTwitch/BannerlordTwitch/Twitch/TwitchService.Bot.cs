﻿using System;
using System.Collections.Generic;
using System.Linq;
using BannerlordTwitch.Util;
using BLTOverlay;
using Microsoft.AspNet.SignalR.Messaging;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Moderation.GetModerators;
using TwitchLib.Api.Helix.Models.Soundtrack;
using TwitchLib.Api.Helix;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Client.Models.Builders;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Models;
using TwitchLib.Client.Models.Internal;
using TwitchLib.Client.Enums;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace BannerlordTwitch
{
    internal partial class TwitchService
    {
        private class Bot : IDisposable
        {
            private readonly string channel;
            private TwitchClient client;
            private readonly AuthSettings authSettings;
            private string botUserName;

            public Bot(string channel, AuthSettings authSettings)
            {
                this.authSettings = authSettings;
                this.channel = channel;

                var api = new TwitchAPI();

                //api.Settings.Secret = SECRET;
                api.Settings.ClientId = authSettings.ClientID;
                api.Settings.AccessToken = authSettings.BotAccessToken;

                // Get the bot username
                api.Helix.Users.GetUsersAsync(accessToken: authSettings.BotAccessToken).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        Log.LogFeedSystem($"Bot connection failed: {t.Exception?.Message}");
                        return;
                    }
                    var user = t.Result.Users.First();

                    Log.Info($"Bot user is {user.Login}");
                    botUserName = user.Login;
                    Connect();
                });
            }

            private void Connect()
            {
                var credentials = new ConnectionCredentials(botUserName, authSettings.BotAccessToken, disableUsernameCheck: true);
                var clientOptions = new ClientOptions();
                var customClient = new WebSocketClient(clientOptions);
                // Double check to destroy the client
                if (client != null)
                {
                    client.OnLog -= Client_OnLog;
                    client.OnJoinedChannel -= Client_OnJoinedChannel;
                    client.OnMessageReceived -= Client_OnMessageReceived;
                    //client.OnWhisperReceived -= Client_OnWhisperReceived;
                    client.OnConnected -= Client_OnConnected;
                    client.OnDisconnected -= Client_OnDisconnected;
                    client.Disconnect();
                }
                client = new TwitchClient(customClient);
                client.Initialize(credentials, channel);

                client.OnLog += Client_OnLog;
                client.OnJoinedChannel += Client_OnJoinedChannel;
                client.OnMessageReceived += Client_OnMessageReceived;
                client.OnConnected += Client_OnConnected;
                client.OnDisconnected += Client_OnDisconnected;
                //client.OnWhisperReceived += Client_OnWhisperReceived;

                client.Connect();
            }

            private static IEnumerable<string> FormatMessage(params string[] msg)
            {
                const string space = "{=U9DzsTDv} ░ "; // " ░▓█▓░ ";// " ▄▓▄▓▄ ";
                var parts = new List<string>();
                string currPart = msg.First();
                foreach (string msgPart in msg.Skip(1))
                {
                    if (currPart.Length + space.Length + msgPart.Length > 450)
                    {
                        parts.Add(currPart);
                        currPart = msgPart;
                    }
                    else
                    {
                        currPart += space + msgPart;
                    }
                }
                parts.Add(currPart);
                return parts;  // string.Join(space, msg);
            }

            private string BotPrefix => authSettings.BotMessagePrefix ?? "{=b4tJSNJG}[BLT] ".Translate();

            public void SendChat(params string[] msg)
            {
                if (client.IsConnected)
                {
                    try
                    {
                        var parts = FormatMessage(msg);
                        foreach (string part in parts)
                        {
                            client.SendMessage(channel, BotPrefix + part);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to send chat: {e.Message}");
                    }
                }
            }

            public void SendChatReply(string userName, params string[] msg)
            {
                if (client.IsConnected)
                {
                    try
                    {
                        var parts = FormatMessage(msg);
                        foreach (string part in parts)
                        {
                            client.SendMessage(channel, $"{BotPrefix}@{userName} {part}");
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to send reply: {e.Message}");
                    }
                }
            }

            public void SendReply(string replyId, params string[] msg)
            {
                if (client.IsConnected)
                {
                    try
                    {
                        var parts = FormatMessage(msg);
                        foreach (string part in parts)
                        {
                            client.SendReply(channel, replyId, BotPrefix + part);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to send reply: {e.Message}");
                    }
                }
            }

            //public void SendWhisper(string userName, params string[] msg)
            //{
            //    if (client.IsConnected)
            //    {
            //        try
            //        {
            //            var parts = FormatMessage(msg);
            //            foreach (string part in parts)
            //            {
            //                client.SendWhisper(userName, "Command heard");
            //            }
            //        }
            //        catch (Exception e)
            //        {
            //            Log.Error($"Failed to send reply: {e.Message}");
            //        }
            //    }
            //}

            private void Client_OnLog(object sender, OnLogArgs e)
            {
                Log.Trace($"{e.DateTime}: {e.BotUsername} - {e.Data}");
            }

            private bool autoReconnect = true;

            private void Client_OnConnected(object sender, OnConnectedArgs e)
            {
                Log.LogFeedSystem("{=DYWDiBCl}Bot connected".Translate());

                // disconnectCts = new CancellationTokenSource();
                // Task.Factory.StartNew(() => {
                //     while (!disconnectCts.IsCancellationRequested)
                //     {
                //         MainThreadSync.Run(() =>
                //         {
                //             if (!client.IsConnected || client.JoinedChannels.Count == 0)
                //             {
                //                 client.Disconnect();
                //                 disconnectCts.Cancel();
                //                 Connect();
                //             }
                //         });
                //         Task.Delay(TimeSpan.FromSeconds(15), disconnectCts.Token).Wait();
                //     }
                // }, TaskCreationOptions.LongRunning);
            }

            private void Client_OnDisconnected(object sender, OnDisconnectedEventArgs e)
            {
                Log.LogFeedSystem("{=thucznzJ}Bot disconnected".Translate());
                if (autoReconnect)
                {
                    Connect();
                }
            }


            private void Client_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
            {
                Log.LogFeedSystem("{=Hd6Q51eb}@{BotUsername} has joined channel {Channel}".Translate(
                    ("BotUsername", e.BotUsername), ("Channel", e.Channel)));
                SendChat("{=SbufvVIR}bot reporting for duty!".Translate(), "{=vBtkF25N}Type !help for command list".Translate());
            }

            private HashSet<string> handledMessages = new();
            private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
            {
                // Double check we didn't already handle this message, as sometimes bot can be receiving them twice
                if (!handledMessages.Add(e.ChatMessage.Id))
                    return;

                // Register the user info always and before doing anything else, so it is appropriately up to date in
                // case a bot command is being issued
                TwitchHub.AddUser(e.ChatMessage.DisplayName, e.ChatMessage.ColorHex);

                string msg = e.ChatMessage.Message;
                if (msg.StartsWith("!"))
                {
                    HandleChatBoxMessage(msg.TrimStart('!'), e.ChatMessage);
                }
            }

            //private void Client_OnWhisperReceived(object sender, OnWhisperReceivedArgs e)
            //{
            //    // Double check we didn't already handle this message, as sometimes bot can be receiving them twice
            //    if (!handledMessages.Add(e.WhisperMessage.MessageId))
            //        return;

            //    // Register the user info always and before doing anything else, so it is appropriately up to date in
            //    // case a bot command is being issued
            //    TwitchHub.AddUser(e.WhisperMessage.DisplayName, e.WhisperMessage.ColorHex);

            //    string msg = e.WhisperMessage.Message;
            //    if (msg.StartsWith("!"))
            //    {
            //        HandleWhisperMessage(msg.TrimStart('!'), e.WhisperMessage);
            //    }
            //}

            //private void HandleWhisperMessage(string msg, WhisperMessage whisperMessage)
            //{
            //    string[] parts = msg.Split(' ');
            //    if (parts[0] == "help")
            //    {
            //        BLTModule.TwitchService?.ShowCommandHelp();
            //    }
            //    else
            //    {
            //        string cmdName = parts[0];
            //        string args = msg.Substring(cmdName.Length).Trim();
            //        BLTModule.TwitchService?.ExecuteCommandFromWhisper(cmdName, whisperMessage, args);
            //    }
            //}

            private void HandleChatBoxMessage(string msg, ChatMessage chatMessage)
            {
                string[] parts = msg.Split(' ');
                if (parts[0] == "help")
                {
                    BLTModule.TwitchService?.ShowCommandHelp();
                }
                else
                {
                    string cmdName = parts[0];
                    string args = msg.Substring(cmdName.Length).Trim();
                    BLTModule.TwitchService?.ExecuteCommand(cmdName, chatMessage, args);
                }
            }

            private void ReleaseUnmanagedResources()
            {
                autoReconnect = false;
                client?.Disconnect();
                client = null;
            }

            public void Dispose()
            {
                ReleaseUnmanagedResources();
                GC.SuppressFinalize(this);
            }

            ~Bot()
            {
                ReleaseUnmanagedResources();
            }
        }
    }
}
