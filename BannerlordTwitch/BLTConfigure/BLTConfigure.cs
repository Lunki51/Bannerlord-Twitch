﻿using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using JetBrains.Annotations;
using TaleWorlds.MountAndBlade;
using BannerlordTwitch.Rewards;
using BLTAdoptAHero;
using BannerlordTwitch.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.ComponentInterfaces;
using BLTConfigure.UI;
using BannerlordTwitch;

namespace BLTConfigure
{
    [UsedImplicitly]
    public class BLTConfigureModule : MBSubModuleBase
    {
        private readonly Thread thread;
        private BLTConfigureWindow wnd;
        //internal static GlobalOverlayConfig OverlayConfig { get; private set; }

        public BLTConfigureModule()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
            {
                string folderPath = Path.GetDirectoryName(typeof(BLTConfigureModule).Assembly.Location);
                string assemblyPath = Path.Combine(folderPath, new AssemblyName(args.Name).Name + ".dll");
                if (!File.Exists(assemblyPath)) return null;
                var assembly = Assembly.LoadFrom(assemblyPath);
                return assembly;
            };
            //GlobalOverlayConfig.Register();
            thread = new Thread(() =>
            {
                try
                {
                    var _ = new Application();

                    // Need to make sure these styles are set globally
                    var globalStyles = (ResourceDictionary)Application.LoadComponent(
                        new Uri("BLTConfigure;component/UI/Styles.xaml",
                            UriKind.Relative));
                    Application.Current.Resources.MergedDictionaries.Add(globalStyles);

                    wnd = new BLTConfigureWindow { ShowActivated = false };
                    wnd.Show();
                    System.Windows.Threading.Dispatcher.Run();
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Exception occurred (please report this on the discord or nexusmods):\n{e}",
                        "BLT Configure Module Crashed!");
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        protected override void OnSubModuleUnloaded()
        {
            wnd?.Dispatcher.Invoke(() =>
            {
                wnd.Close();
            });
            wnd?.Dispatcher.InvokeShutdown();
            thread.Join(TimeSpan.FromMilliseconds(500));
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            try
            {
                if (game.GameType is Campaign)
                {
                    Settings.GameStarted = true;
                    // Reload settings here so they are fresh
                    //OverlayConfig = GlobalOverlayConfig.Get();
                }
            }
            catch (Exception e)
            {
                Log.Exception(nameof(OnGameStart), e);
                MessageBox.Show($"Error in {nameof(OnGameStart)}, please report this on the discord: {e}", "Bannerlord Twitch Mod STARTUP ERROR");
            }
        }
        public override void OnGameEnd(Game game)
        {
            Settings.GameStarted = false;
        }
    }
}