using BannerlordTwitch.Util;
using JetBrains.Annotations;
using TaleWorlds.Library;

namespace BannerlordTwitch
{
    [UsedImplicitly]
    public class AuthSettings
    {
        public string AccessToken { get; set; }
        public string ClientID { get; set; }
        public string BotAccessToken { get; set; }
        public string BotMessagePrefix { get; set; }
        public bool DebugSpoofAffiliate { get; set; }

        public string NeocitiesUsername { get; set; }
        public string NeocitiesPassword { get; set; }

        public string DocsTitle { get; set; } = "{=ctAF1ghX}Bannerlord Twitch Viewer Guide".Translate();

        public string DocsIntroduction { get; set; }
            = "{=rElL5v6I}Bannerlord Twitch (BLT) is a Twitch Integration mod for Mount & Blade II: Bannerlord. <br><br> As a viewer you can use channel point rewards (if available), and chat commands to interact with the game while the streamer is playing. <br><br> The primary feature of BLT is allowing you to 'adopt' a hero in the game. Your hero can either be a random hero already inside the game world, or a newly created one. Your hero equipment can be upgraded, or replaced with custom items or tournament reward items; however you will always equip the best item you have available in each actual slot. <br><br> Some examples of things you can do with your hero include 'summoning' your hero into battles that the streamer is taking part in, joining viewer tournaments, selecting your heroes 'class', unlocking special class powers, or creating new viewer clans. Everything saves with the game, so you can join for entire campaigns, while your heroes level whenever the campaign is being played. <br><br> If you aren't sure where to start, start with !help and !adopt".Translate();

        // ── Extension PubSub ─────────────────────────────────────────────────
        // Find these in the Twitch dev console under your extension -> "Extension Settings"

        /// <summary>
        /// Client ID of the Twitch Extension itself.
        /// Found in the Twitch dev console under your extension -> "Extension Settings".
        /// This is different from the broadcaster's app ClientID.
        /// </summary>
        public string ExtensionClientId { get; set; }

        /// <summary>
        /// Base64-encoded extension secret from the Twitch dev console.
        /// Click "Show Secret" under Extension Settings to copy it.
        /// </summary>
        public string ExtensionSecret { get; set; }

        [YamlDotNet.Serialization.YamlIgnore]
        public bool ExtensionConfigured =>
            !string.IsNullOrWhiteSpace(ExtensionClientId) &&
            !string.IsNullOrWhiteSpace(ExtensionSecret);

        // ── Persistence ──────────────────────────────────────────────────────

        private static TaleWorlds.Library.PlatformFilePath AuthFilePath =>
            FileSystem.GetConfigPath("Bannerlord-Twitch-Auth.yaml");

        public static AuthSettings Load()
        {
            return !FileSystem.FileExists(AuthFilePath)
                ? null
                : YamlHelpers.Deserialize<AuthSettings>(FileSystem.GetFileContentString(AuthFilePath));
        }

        public static void Save(AuthSettings authSettings)
        {
            FileSystem.SaveFileString(AuthFilePath, YamlHelpers.Serialize(authSettings));
        }
    }
}