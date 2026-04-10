using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using BannerlordTwitch;
using BannerlordTwitch.Util;
using TaleWorlds.Library;

namespace BLTConfigure.UI
{
    public class ConfigurationRootViewModel : INotifyPropertyChanged
    {
        public Settings EditedSettings { get; set; }

        public AuthSettings EditedAuthSettings { get; set; }

        public ConfigurationRootViewModel()
        {
            Load();
            UpdateLastSavedLoop();
        }

        public bool AffiliateSpoofing
        {
            get => EditedAuthSettings.DebugSpoofAffiliate;
            set
            {
                EditedAuthSettings.DebugSpoofAffiliate = value;
                SaveAuth();
            }
        }

        public bool DisableAutomaticFulfillment
        {
            get => EditedSettings.DisableAutomaticFulfillment;
            set
            {
                EditedSettings.DisableAutomaticFulfillment = value;
                SaveSettings();
            }
        }

        public string DocsTitle
        {
            get => EditedAuthSettings.DocsTitle;
            set
            {
                EditedAuthSettings.DocsTitle = value;
                SaveAuth();
            }
        }

        public string DocsIntroduction
        {
            get => EditedAuthSettings.DocsIntroduction;
            set
            {
                EditedAuthSettings.DocsIntroduction = value;
                SaveAuth();
            }
        }

        // ── Extension ─────────────────────────────────────────────────────────

        /// <summary>
        /// Client ID of the Twitch Extension (from dev console → Extension Settings).
        /// </summary>
        public string ExtensionClientId
        {
            get => EditedAuthSettings.ExtensionClientId;
            set
            {
                EditedAuthSettings.ExtensionClientId = value;
                SaveAuth();
            }
        }

        /// <summary>
        /// Base64-encoded extension secret (from dev console → Extension Settings → Show Secret).
        /// </summary>
        public string ExtensionSecret
        {
            get => EditedAuthSettings.ExtensionSecret;
            set
            {
                EditedAuthSettings.ExtensionSecret = value;
                SaveAuth();
            }
        }

        // ── Actions ───────────────────────────────────────────────────────────

        public void RefreshActionList()
        {
            if (EditedSettings != null)
            {
                ActionFilterView = CollectionViewSource.GetDefaultView(
                    EditedSettings.GlobalConfigs.Cast<object>()
                        .Concat(EditedSettings.Rewards)
                        .Concat(EditedSettings.Commands)
                        .Concat(EditedSettings.SimTesting.Yield())
                );
                ActionFilterView.GroupDescriptions.Add(new BLTConfigureWindow.TypeGroupDescription());
            }
        }

        // ── Load / Save ───────────────────────────────────────────────────────

        private void Load()
        {
            ProfileChanged(1);

            try
            {
                EditedAuthSettings = AuthSettings.Load();
            }
            catch (Exception ex)
            {
                Log.Exception($"BLTConfigureWindow.Reload", ex);
            }

            EditedAuthSettings ??= new AuthSettings
            {
                ClientID = TwitchAuthHelper.ClientID,
                BotMessagePrefix = "░BLT░ ",
            };
        }

        private DateTime lastSaved = DateTime.MinValue;
        public string LastSavedMessage => lastSaved == DateTime.MinValue || DateTime.Now - lastSaved > TimeSpan.FromSeconds(5)
            ? string.Empty
            : $"Saved {(DateTime.Now - lastSaved).TotalSeconds:0} seconds ago. " +
              $"Reload save to apply changes.";
        public string ActiveProfile => $"Profile: {Settings.ActiveProfile}";

        private async void UpdateLastSavedLoop()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                PropertyChanged?.Invoke(this, new(nameof(LastSavedMessage)));
            }
        }

        public void SaveSettings()
        {
            if (EditedSettings == null)
                return;
            try
            {
                Settings.Save(EditedSettings);
                lastSaved = DateTime.Now;
            }
            catch (Exception ex)
            {
                Log.Exception($"BLTConfigureWindow.SaveSettings", ex);
            }
        }

        public void SaveAuth()
        {
            if (EditedAuthSettings == null)
                return;
            try
            {
                AuthSettings.Save(EditedAuthSettings);
            }
            catch (Exception ex)
            {
                Log.Exception($"BLTConfigureWindow.SaveAuth", ex);
            }
        }

        public void ProfileChanged(int Profile)
        {
            if (!Settings.GameStarted)
            {
                try
                {
                    Settings.ChangeProfile(Profile);
                    PropertyChanged?.Invoke(this, new(nameof(ActiveProfile)));
                    EditedSettings = Settings.Load();
                }
                catch (Exception ex)
                {
                    Log.Exception($"BLTConfigureWindow.Reload", ex);
                }

                EditedSettings ??= new Settings();
                ConfigureContext.CurrentlyEditedSettings = EditedSettings;

                RefreshActionList();
            }
            else
                Log.Error("Cannot change profile while game is running");
        }

        public ICollectionView ActionFilterView { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}