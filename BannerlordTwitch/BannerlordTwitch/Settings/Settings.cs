﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using TaleWorlds.Library;
using YamlDotNet.Serialization;

#if DEBUG
using System.Runtime.CompilerServices;
#endif

// ReSharper disable MemberCanBePrivate.Global
#pragma warning disable 649

namespace BannerlordTwitch
{
    // Docs here https://dev.twitch.tv/docs/api/reference#create-custom-rewards

    public class Settings : IDocumentable, IUpdateFromDefault
    {
        public ObservableCollection<Reward> Rewards { get; set; } = new();
        [YamlIgnore]
        public IEnumerable<Reward> EnabledRewards => Rewards.Where(r => r.Enabled);
        public ObservableCollection<Command> Commands { get; set; } = new();
        [YamlIgnore]
        public IEnumerable<Command> EnabledCommands => Commands.Where(r => r.Enabled);
        public ObservableCollection<GlobalConfig> GlobalConfigs { get; set; } = new();
        public SimTestingConfig SimTesting { get; set; }
        [YamlIgnore, Browsable(false)]
        public IEnumerable<ActionBase> AllActions => Rewards.Cast<ActionBase>().Concat(Commands);

        public bool DisableAutomaticFulfillment { get; set; }

        public Command GetCommand(string id) => EnabledCommands.FirstOrDefault(c =>
            string.Equals(c.Name.ToString(), id, StringComparison.CurrentCultureIgnoreCase));

        public T GetGlobalConfig<T>(string id) => (T)GlobalConfigs.First(c => c.Id == id).Config;

        private static string DefaultSettingsFileName
            => Path.Combine(Path.GetDirectoryName(typeof(Settings).Assembly.Location) ?? ".",
                "..", "..", "Bannerlord-Twitch-v4.yaml");

        public static Settings DefaultSettings { get; private set; }
        public static int ActiveProfile { get; set; } = 1;
        public static bool GameStarted { get; set; } = false;

#if DEBUG
        private static string ProjectRootDir([CallerFilePath]string file = "") => Path.Combine(Path.GetDirectoryName(file) ?? ".", "..");
        private static string SaveFilePath => Path.Combine(ProjectRootDir(), "_Module", "Bannerlord-Twitch-v4.yaml");
        public static Settings Load()
        {
            
            var settings = YamlHelpers.Deserialize<Settings>(File.ReadAllText(SaveFilePath));
            if (settings == null)
                throw new Exception($"Couldn't load the mod settings from {SaveFilePath}");

            SettingsPostLoad(settings);
            
            return settings;
        }

        public static void Save(Settings settings)
        {
            SettingsPreSave(settings);
            File.WriteAllText(SaveFilePath, YamlHelpers.Serialize(settings));
        }
        public static void ChangeProfile(int Profile)
        {
            ActiveProfile = Profile;
        }
#else

        public static Settings Load()
        {
            Settings settings = null;
            // Try loading settings from the active profile
            try
            {
                PlatformFilePath ProfileFilePath = FileSystem.GetConfigPath($"Bannerlord-Twitch-v4-p{ActiveProfile}.yaml");
                if (FileSystem.FileExists(ProfileFilePath))
                {
                    try
                    {
                        settings = YamlHelpers.Deserialize<Settings>(FileSystem.GetFileContentString(ProfileFilePath));
                        Log.Info($"Settings loaded from {ProfileFilePath}");
                    }
                    catch (Exception ex)
                    {
                        Log.Exception($"Exception loading settings from {ProfileFilePath}: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception($"Failed to load profile {ActiveProfile}", ex);
            }

            // If we failed to load anything then load defaults
            if (settings == null)
            {
                Log.Info($"Couldn't find existing settings, loading defaults from internal {DefaultSettingsFileName}.");
                settings = YamlHelpers.Deserialize<Settings>(File.ReadAllText(DefaultSettingsFileName));
                if (settings != null)
                    Save(settings);
            }

            // If we STILL haven't loaded anything, then the mod install must be broken
            if (settings == null)
            {
                throw new Exception($"Couldn't load the settings, check the mod is installed correctly!");
            }

            SettingsPostLoad(settings);

            SettingsHelpers.CallInDepth<IUpdateFromDefault>(settings,
                config => config.OnUpdateFromDefault(settings));

            Log.Info($"Settings succesfully applied");

            return settings;
        }

        //public static void ImportOld()
        //{
        //    Settings settings = null;
        //    // Try loading settings from the active profile
        //    try
        //    {
        //        PlatformFilePath ProfileFilePath = FileSystem.GetConfigPath($"Bannerlord-Twitch-v3.yaml");
        //        PlatformFilePath ProfileFilePath2 = FileSystem.GetConfigPath($"Bannerlord-Twitch-v4.yaml");
        //        if (FileSystem.FileExists(ProfileFilePath))
        //        {
        //            try
        //            {
        //                settings = YamlHelpers.Deserialize<Settings>(FileSystem.GetFileContentString(ProfileFilePath));
        //                Log.Info($"Settings imported from {ProfileFilePath}");
        //            }
        //            catch (Exception ex)
        //            {
        //                Log.Exception($"Exception loading settings from {ProfileFilePath}: {ex.Message}", ex);
        //            }
        //        }
        //        else if (FileSystem.FileExists(ProfileFilePath2))
        //        {
        //            try
        //            {
        //                settings = YamlHelpers.Deserialize<Settings>(FileSystem.GetFileContentString(ProfileFilePath2));
        //                Log.Info($"Settings imported from {ProfileFilePath2}");
        //            }
        //            catch (Exception ex)
        //            {
        //                Log.Exception($"Exception loading settings from {ProfileFilePath2}: {ex.Message}", ex);
        //            }
        //        }
        //        else
        //            Log.Info($"No settings found at {ProfileFilePath} or {ProfileFilePath2}");
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Exception($"Failed to import settings", ex);
        //    }

        //    SettingsHelpers.CallInDepth<IUpdateFromDefault>(settings,
        //        config => config.OnUpdateFromDefault(settings));

        //    SettingsPostLoad(settings);

        //    Log.Info($"Settings succesfully imported");

        //}

        public static void Save(Settings settings)
        {
            SettingsPreSave(settings);
            FileSystem.SaveFileString(FileSystem.GetConfigPath($"Bannerlord-Twitch-v4-p{ActiveProfile}.yaml"), YamlHelpers.Serialize(settings));
            Log.Info($"Settings Saved to Profile {ActiveProfile} at Bannerlord-Twitch-v4-p{ActiveProfile}.yaml");
        }
        public static void ChangeProfile(int Profile)
        {
            ActiveProfile = Profile;
        }
#endif

        private static void SettingsPostLoad(Settings settings)
        {
            settings.Commands ??= new();
            settings.Rewards ??= new();
            settings.GlobalConfigs ??= new();
            settings.SimTesting ??= new();

            ActionManager.ConvertSettings(settings.Commands);
            ActionManager.ConvertSettings(settings.Rewards);
            ActionManager.EnsureGlobalSettings(settings.GlobalConfigs);

            SettingsHelpers.CallInDepth<ILoaded>(settings, config => config.OnLoaded(settings));
        }

        private static void SettingsPreSave(Settings settings)
        {
            SettingsHelpers.CallInDepth<ISaving>(settings, config => config.OnSaving());
        }

        public void GenerateDocumentation(IDocumentationGenerator generator)
        {
            generator.Div("commands", () =>
            {
                generator.H1("{=JlFpeaxe}Commands".Translate());
                generator.Table(() =>
                {
                    generator.TR(() => generator
                        .TH("{=15umM0Xo}Command".Translate())
                        .TH("{=J6daarYb}Description".Translate())
                        .TH("{=e2Fu7JYS}Settings".Translate()));
                    foreach (var d in Commands.Where(c => c.Enabled))
                    {
                        generator.TR(() =>
                        {
                            generator.TD(d.Name.ToString());
                            generator.TD(string.IsNullOrEmpty(d.Documentation.ToString())
                                ? d.Help.ToString()
                                : d.Documentation.ToString());
                            generator.TD(() =>
                            {
                                if (d.HandlerConfig is IDocumentable doc)
                                {
                                    doc.GenerateDocumentation(generator);
                                }
                                else if (d.HandlerConfig != null)
                                {
                                    DocumentationHelpers.AutoDocument(generator, d.HandlerConfig);
                                }
                            });
                        });
                    }
                });
            });
            generator.Br();
            generator.Div("rewards", () =>
            {
                generator.H1("{=u6xsREDY}Channel Point Rewards".Translate());
                generator.Table(() =>
                {
                    generator.TR(() => generator
                        .TH("{=15umM0Xo}Command".Translate())
                        .TH("{=J6daarYb}Description".Translate())
                        .TH("{=e2Fu7JYS}Settings".Translate()));
                    foreach (var r in Rewards.Where(r => r.Enabled))
                    {
                        generator.TR(() =>
                        {
                            generator.TD(r.RewardSpec.Title.ToString());
                            generator.TD(string.IsNullOrEmpty(r.Documentation.ToString())
                                ? r.RewardSpec.Prompt?.ToString() : r.Documentation.ToString());
                            generator.TD(() =>
                            {
                                if (r.HandlerConfig is IDocumentable doc)
                                {
                                    doc.GenerateDocumentation(generator);
                                }
                                else if (r.HandlerConfig != null)
                                {
                                    DocumentationHelpers.AutoDocument(generator, r.HandlerConfig);
                                }
                            });
                        });
                    }
                });
            });
            generator.Br();
            generator.Div("global-configs", () =>
            {
                foreach (var g in GlobalConfigs.Select(c => c.Config).OfType<IDocumentable>())
                {
                    g.GenerateDocumentation(generator);
                }
            });
        }

        #region IUpdateFromDefault
        public void OnUpdateFromDefault(Settings defaultSettings)
        {
            // merge missing actions / rewards / global configs from template
            SettingsHelpers.MergeCollectionsSorted(
                Commands,
                defaultSettings.Commands,
                (s, s2) => s.ID == s2.ID || s.ToString() == s2.ToString(),
                (a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.CurrentCulture)
            );
            SettingsHelpers.MergeCollectionsSorted(
                Rewards,
                defaultSettings.Rewards,
                (s, s2) => s.ID == s2.ID || s.ToString() == s2.ToString(),
                (a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.CurrentCulture)
            );
            SettingsHelpers.MergeCollectionsSorted(
                GlobalConfigs,
                defaultSettings.GlobalConfigs,
                (s, s2) => s.Id == s2.Id,
                (a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.CurrentCulture)
            );
        }
        #endregion
    }
}
