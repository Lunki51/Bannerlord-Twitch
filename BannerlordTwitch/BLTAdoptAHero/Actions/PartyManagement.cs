using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using HarmonyLib;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero;
using BLTAdoptAHero.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.Core;
using Helpers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using NavalDLC.CharacterDevelopment;
using NavalDLC.CampaignBehaviors;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using BannerlordTwitch.UI;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("Party Management"),
     LocDescription("Allow viewer to manage their party"),
     UsedImplicitly]
    public class PartyManagement : HeroCommandHandlerBase
    {
        [CategoryOrder("Army", 0),
         CategoryOrder("Threat", 1),
         CategoryOrder("Training", 2),
         CategoryOrder("Party Orders", 3)]
        private class Settings : IDocumentable
        {
            // ── Army ────────────────────────────────────────────────────────────
            [LocDisplayName("{=ArmyEnabled}Army"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("{=ArmyEnabledDesc}Enable the !party army command"),
             PropertyOrder(1), UsedImplicitly]
            public bool ArmyEnabled { get; set; } = true;

            [LocDisplayName("{=ArmyPrice}Price"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("{=ArmyPriceDesc}Gold cost to create an army"),
             PropertyOrder(2), UsedImplicitly]
            public int ArmyPrice { get; set; } = 50000;

            [LocDisplayName("{=ArmyMaxReissue}Max Re-issue Attempts"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("{=ArmyMaxReissueDesc}How many times the system silently re-issues a drifted order before releasing it. 0 = never re-issue."),
             PropertyOrder(3), UsedImplicitly]
            public int ArmyMaxReissueAttempts { get; set; } = 5;

            [LocDisplayName("Party Order Expiry (Days)"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription(
                 "In-game days before a clan party order auto-expires. Fractional values are supported (e.g. 0.5 = 12 hours). " +
                 "0 = no expiry."),
             PropertyOrder(4), UsedImplicitly]
            public float PartyOrderExpiryDays { get; set; } = 0f;

            [LocDisplayName("Army Order Expiry (Days)"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription(
                 "In-game days before an army order (siege/defend/patrol/garrison) " +
                 "auto-expires. Fractional values are supported (e.g. 0.5 = 12 hours). " +
                 "0 = no expiry."),
             PropertyOrder(5), UsedImplicitly]
            public float ArmyOrderExpiryDays { get; set; } = 0f;

            // ── King army management ─────────────────────────────────────────
            [LocDisplayName("King Army Management"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Allow kings to view, create (NPC-led), and disband any kingdom army by index."),
             PropertyOrder(6), UsedImplicitly]
            public bool KingArmyManageEnabled { get; set; } = true;

            [LocDisplayName("Create Army Gold Cost"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Gold cost for a king to commission an NPC-led army."),
             PropertyOrder(7), UsedImplicitly]
            public int CreateArmyPrice { get; set; } = 100000;

            [LocDisplayName("King Can Toggle AI Armies"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Allow a kingdom's king to block or restore AI/NPC army creation via '!party army allowai on/off'."),
             PropertyOrder(8), UsedImplicitly]
            public bool KingAIArmyToggleEnabled { get; set; } = true;

            [LocDisplayName("King Can Toggle BLT Armies"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Allow a kingdom's king to block or restore BLT army creation via '!party army allowblt on/off'."),
             PropertyOrder(9), UsedImplicitly]
            public bool KingBLTArmyToggleEnabled { get; set; } = true;

            [LocDisplayName("Takeover Enabled"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Allow a clan leader to seize command of an army already led by one of their own clan members."),
             PropertyOrder(10), UsedImplicitly]
            public bool TakeoverEnabled { get; set; } = true;

            [LocDisplayName("Call Enabled"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Allow army leaders or the king to call free lord parties to join an army."),
             PropertyOrder(11), UsedImplicitly]
            public bool CallEnabled { get; set; } = true;

            [LocDisplayName("Call Base Influence Cost"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Flat influence cost paid when any call order is issued."),
             PropertyOrder(12), UsedImplicitly]
            public int CallBaseInfluenceCost { get; set; } = 0;

            [LocDisplayName("Call Per-Party Influence Cost"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Additional influence cost charged for each party that actually joins."),
             PropertyOrder(13), UsedImplicitly]
            public int CallInfluenceCostPerParty { get; set; } = 25;

            [LocDisplayName("Call Nearby Radius"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Map-unit radius used when scanning for parties with 'army call nearby'."),
             PropertyOrder(14), UsedImplicitly]
            public float CallNearbyRadius { get; set; } = 30f;

            [LocDisplayName("Join Enabled"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Allow a hero to join any kingdom army by index, bringing all free clan parties."),
             PropertyOrder(15), UsedImplicitly]
            public bool JoinEnabled { get; set; } = true;

            [LocDisplayName("Join Base Influence Cost"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Flat influence cost paid when the hero joins an army. Free for mercenaries."),
             PropertyOrder(16), UsedImplicitly]
            public int JoinBaseInfluenceCost { get; set; } = 0;

            [LocDisplayName("Join Per-Party Influence Cost"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Additional influence cost for each clan party that joins the army. Free for mercenaries."),
             PropertyOrder(17), UsedImplicitly]
            public int JoinInfluenceCostPerParty { get; set; } = 10;

            [LocDisplayName("Army Kick Enabled"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Enable '!party army kick [n]' to remove the n weakest parties from an army."),
             PropertyOrder(18), UsedImplicitly]
            public bool ArmyKickEnabled { get; set; } = true;

            [LocDisplayName("Auto Call Parties on Create"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("When creating an army, automatically call eligible nearby parties to join. Disable to spawn army with leader party only."),
             PropertyOrder(19), UsedImplicitly]
            public bool AutoCallPartiesOnCreate { get; set; } = true;

            // ── Threat ───────────────────────────────────────────────────────
            [LocDisplayName("{=ThreatEnabled}Threat Scan"),
             LocCategory("Threat", "{=ThreatCat}Threat"),
             LocDescription("{=ThreatEnabledDesc}Enable !party army threat scan subcommand"),
             PropertyOrder(1), UsedImplicitly]
            public bool ThreatEnabled { get; set; } = true;

            [LocDisplayName("{=ThreatMaxResults}Threat Max Results"),
             LocCategory("Threat", "{=ThreatCat}Threat"),
             LocDescription("{=ThreatMaxResultsDesc}Maximum number of threats listed, sorted by danger"),
             PropertyOrder(2), UsedImplicitly]
            public int ThreatMaxResults { get; set; } = 3;

            [LocDisplayName("{=ThreatRadius}Threat Scan Radius"),
             LocCategory("Threat", "{=ThreatCat}Threat"),
             LocDescription("{=ThreatRadiusDesc}Map-unit radius to scan for nearby hostile parties."),
             PropertyOrder(3), UsedImplicitly]
            public float ThreatScanRadius { get; set; } = 15f;

            // ── Training ─────────────────────────────────────────────────────
            [LocDisplayName("Train Enabled"),
             LocCategory("Training", "Training"),
             LocDescription("Enable the !party train command."),
             PropertyOrder(1), UsedImplicitly]
            public bool TrainEnabled { get; set; } = true;

            [LocDisplayName("Train Max Tier"),
             LocCategory("Training", "Training"),
             LocDescription(
                 "Training will only upgrade troops in the leader's party up to this tier. " +
                 "Once all troops in the leader's party are at or above this tier, " +
                 "daily training budget spills over to a randomly chosen free clan party " +
                 "and upgrades its troops to the same tier cap. " +
                 "0 = no cap (original behaviour, only affects the leader's party)."),
             PropertyOrder(2), UsedImplicitly]
            public int TrainMaxTier { get; set; } = 0;

            // ── Party Orders ─────────────────────────────────────────────────
            [LocDisplayName("Clan Orders Enabled"),
             LocCategory("Party Orders", "Party Orders"),
             LocDescription("Enable !party [siege/defend/patrol/raid/garrison] commands for individual or all clan parties."),
             PropertyOrder(1), UsedImplicitly]
            public bool ClanOrdersEnabled { get; set; } = true;

            [LocDisplayName("Raid Enabled"),
             LocCategory("Party Orders", "Party Orders"),
             LocDescription("Enable the !party raid command for raiding enemy villages."),
             PropertyOrder(2), UsedImplicitly]
            public bool RaidEnabled { get; set; } = true;

            [LocDisplayName("Garrison Enabled"),
             LocCategory("Party Orders", "Party Orders"),
             LocDescription("Enable !party garrison and !party army garrison to keep parties inside a friendly fortification."),
             PropertyOrder(3), UsedImplicitly]
            public bool GarrisonEnabled { get; set; } = true;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.Value("<strong>Commands:</strong>");
                generator.Value("!party — current party/army status");
                generator.Value("!party create — spawn a new party");
                generator.Value("!party govern [fief] — become governor of a clan fief");
                generator.Value("!party stats — detailed party stats");
                generator.Value("!party disband [index|all] — disband own party/parties");
                generator.Value("!party train <gold> / status / cancel — invest gold in troop training");
                generator.Value("!party release [all] — release BLT order on own party (or all free clan parties) and restore AI");
                generator.Value("");
                generator.Value("<strong>Clan party orders (append 'all' for every free clan party):</strong>");
                generator.Value("  !party siege <settlement> [all]   — siege an enemy fortification");
                generator.Value("  !party defend <settlement> [all]  — smart-guard a friendly fortification");
                generator.Value("  !party patrol <settlement> [all]  — smart-guard patrol a settlement");
                generator.Value("  !party raid <village|fort> [all]  — raid a village (fort = expand to bound villages)");
                generator.Value("  !party garrison <fort> [all]      — enter and stay in a friendly fortification");
                generator.Value("  Smart-guard logic: (1) defend if under siege by enemy, (2) protect raided village, (3) patrol");
                generator.Value("  Raid 'all' distributes parties to different villages; remainder patrol the fortification");
                generator.Value("");
                generator.Value("<strong>Army subcommands:</strong> !party army [subcommand]");
                generator.Value("  siege [settlement] — besiege a named enemy settlement (or auto-pick)");
                generator.Value("  defend [settlement] — defend a named friendly settlement (or auto-pick)");
                generator.Value("  patrol [settlement] — patrol around any named settlement (or auto-pick)");
                generator.Value("  garrison [settlement] — garrison whole army at a friendly fortification");
                generator.Value("  release — release the active army order and restore normal AI");
                generator.Value("  status — army strength, behavior, cohesion, food, active order info");
                generator.Value("  disband [index] — disband your army; king: disband any by index");
                generator.Value("  leave — leave someone else's army");
                generator.Value("  reassign [hero] — transfer army leadership to a hero in your army");
                generator.Value("  kick [n] — kick the n weakest parties from army (army leader or king w/ index)");
                generator.Value("  view — (king) list all kingdom armies with index numbers");
                generator.Value("  create [hero_name] — (king) commission an NPC-led army");
                generator.Value("  takeover [hero|index] — (clan leader) seize command of a clan member's army");
                generator.Value("  call nearby [army_index] — call free parties near the army to join");
                generator.Value("  call all [army_index] — call all free kingdom parties to join the army");
                generator.Value("  join <index> — join a kingdom army by index, bringing all free clan parties");
                generator.Value("  Allied siege joining: parties allied via the diplomacy treaty system may jointly besiege a mutual enemy.");

                if (ArmyEnabled)
                {
                    generator.Value("");
                    generator.Value("<strong>Army config:</strong>");
                    generator.Value($"  Creation cost: {ArmyPrice}{Naming.Gold}");
                    generator.Value($"  Max re-issue attempts: {ArmyMaxReissueAttempts}");
                    generator.Value(PartyOrderExpiryDays > 0f
                        ? $"  Party order expiry: {PartyOrderExpiryDays}d" : "  Party order expiry: none");
                    generator.Value(ArmyOrderExpiryDays > 0f
                        ? $"  Army order expiry: {ArmyOrderExpiryDays}d" : "  Army order expiry: none");
                    if (KingArmyManageEnabled)
                        generator.Value($"  King management: create cost {CreateArmyPrice}{Naming.Gold}");
                    if (TakeoverEnabled)
                        generator.Value("  Clan-leader takeover: enabled");
                    if (CallEnabled)
                        generator.Value($"  Call: base {CallBaseInfluenceCost} influence + {CallInfluenceCostPerParty}/party | nearby radius {CallNearbyRadius}");
                    if (JoinEnabled)
                        generator.Value($"  Join: base {JoinBaseInfluenceCost} influence + {JoinInfluenceCostPerParty}/party (free for mercenaries)");
                    if (ArmyKickEnabled)
                        generator.Value("  Army kick: enabled");
                    if (GarrisonEnabled)
                        generator.Value("  Garrison: enabled");
                }
                else
                {
                    generator.Value("<strong>Army command: disabled</strong>");
                }
            }
        }

        public override Type HandlerConfigType => typeof(Settings);

        /// <summary>
        /// If the last whitespace-delimited token of <paramref name="arg"/> is a positive
        /// integer ≤ 200, returns it as a party count and removes it from the string.
        /// Otherwise returns the original string and null.
        /// </summary>
        private static (string cleanedArg, int? count) ParseTrailingCount(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg)) return (arg ?? "", null);
            var parts = arg.TrimEnd().Split(' ');
            if (parts.Length > 0
                && int.TryParse(parts[parts.Length - 1], out int n)
                && n > 0 && n <= 200)
                return (string.Join(" ", parts.Take(parts.Length - 1)).Trim(), n);
            return (arg, null);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EXECUTE
        // ─────────────────────────────────────────────────────────────────────

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) return;

            if (adoptedHero == null) { onFailure(AdoptAHero.NoHeroMessage); return; }
            if (Mission.Current != null) { onFailure("{=MPTOZqMS}You cannot manage your party, as a mission is active!".Translate()); return; }
            if (adoptedHero.Clan == null) { onFailure("{=B86KnTcu}You are not in a clan".Translate()); return; }

            var splitArgs = context.Args.Split(' ');
            var mode = splitArgs[0];
            var desiredName = string.Join(" ", splitArgs.Skip(1)).Trim();

            MobileParty party = adoptedHero.PartyBelongedTo;
            if (party == null)
            {
                var wpc = adoptedHero.Clan.WarPartyComponents.FirstOrDefault(pc => pc?.Leader == adoptedHero);
                party = wpc?.MobileParty;
            }
            Army army = party?.Army;

            string behaviorText = party?.GetBehaviorText()?.ToString() ?? "";
            string armyBehavior = army?.LeaderParty?.GetBehaviorText()?.ToString() ?? "";

            if (string.IsNullOrEmpty(mode))
            {
                var sb = new StringBuilder();
                BuildStatusString(adoptedHero, party, army, behaviorText, armyBehavior, sb);
                onSuccess(sb.ToString());
                return;
            }

            switch (mode)
            {
                case "govern": HandleGovern(adoptedHero, party, army, desiredName, onSuccess, onFailure); break;
                case "create": HandleCreate(adoptedHero, party, onSuccess, onFailure); break;
                case "stats": HandleStats(adoptedHero, party, onSuccess, onFailure); break;
                case "disband": HandlePartyDisband(adoptedHero, party, desiredName, onSuccess, onFailure); break;
                case "train": HandleTrain(settings, adoptedHero, party, desiredName, onSuccess, onFailure); break;
                case "army": HandleArmy(settings, adoptedHero, party, army, desiredName, onSuccess, onFailure); break;
                // ── Release orders on own party ────────────────────────────────
                case "release": HandlePartyRelease(adoptedHero, party, desiredName, onSuccess, onFailure); break;
                // ── Clan party order commands ──────────────────────────────────
                case "siege":
                case "defend":
                case "patrol":
                case "raid":
                case "garrison":
                    HandleClanPartyOrder(settings, adoptedHero, party, army, mode, desiredName, onSuccess, onFailure);
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  STATUS STRING  (unchanged)
        // ─────────────────────────────────────────────────────────────────────

        private static void BuildStatusString(Hero adoptedHero, MobileParty party, Army army,
            string behaviorText, string armyBehavior, StringBuilder sb)
        {
            if (adoptedHero.HeroState == Hero.CharacterStates.Released)
                sb.Append("{=r1nJTiSA}Your hero has just been released".Translate());
            else if (adoptedHero.HeroState == Hero.CharacterStates.Traveling)
                sb.Append("{=TESTING}Your hero is travelling".Translate());
            else if (adoptedHero.HeroState == Hero.CharacterStates.Fugitive)
                sb.Append("{=TESTING}Your hero is fugitive".Translate());
            else if (adoptedHero.IsPrisoner && adoptedHero.PartyBelongedToAsPrisoner?.IsMobile == true)
            {
                int days = (int)adoptedHero.CaptivityStartTime.ElapsedDaysUntilNow;
                sb.Append($"Prisoner({days}): {adoptedHero.PartyBelongedToAsPrisoner.Name}");
                sb.Append(" | ");
                var place = adoptedHero.PartyBelongedToAsPrisoner?.LeaderHero?.LastKnownClosestSettlement?.Name?.ToString() ?? "Unknown";
                sb.Append($"Last seen near {place}");
            }
            else if (adoptedHero.IsPrisoner && adoptedHero.PartyBelongedToAsPrisoner?.IsSettlement == true)
            {
                int days = (int)adoptedHero.CaptivityStartTime.ElapsedDaysUntilNow;
                sb.Append("{=zVDODxiN}Prisoner({dur}): {prisoner}".Translate(
                    ("prisoner", adoptedHero.PartyBelongedToAsPrisoner.Settlement.Name.ToString()), ("dur", days)));
            }
            else if (adoptedHero.GovernorOf != null && adoptedHero.Clan.Fiefs.Count > 0)
                sb.Append($"Governor: {adoptedHero.GovernorOf.Name}");
            else if (party != null && party.LeaderHero == adoptedHero)
            {
                sb.Append($"Party(Strength: {(int)party.Party.EstimatedStrength} - ");
                string sizeStr = $"{party.MemberRoster.TotalHealthyCount}({party.MemberRoster.TotalWounded})/{party.Party.PartySizeLimit}";
                if (party.PrisonRoster.Count > 0)
                    sb.Append($"Size: {sizeStr} - Prisoners: {party.PrisonRoster.Count}) | ");
                else
                    sb.Append($"Size: {sizeStr}) | ");

                if (party.IsCurrentlyAtSea) sb.Append("Sailing | ");
                if (!string.IsNullOrWhiteSpace(behaviorText) && behaviorText != armyBehavior)
                    sb.Append($"Your party is: {behaviorText} | ");
                if (party.IsDisbanding) sb.Append("Disbanding");

                // Active order tag
                var activeOrder = PartyOrderBehavior.Current?.GetActiveOrder(party.StringId);
                if (activeOrder != null)
                    sb.Append($"[{activeOrder.Type} order locked] | ");

                if (party.TargetParty != null || party.ShortTermTargetParty != null)
                {
                    var al = party.Army?.LeaderParty;
                    if (party.TargetParty != al?.TargetParty || party.ShortTermTargetParty != al?.ShortTermTargetParty)
                    {
                        var tgt = party.ShortTermTargetParty ?? party.TargetParty;
                        sb.Append("{=9aFoBcPY}Target: {target} - ".Translate(("target", tgt?.Name?.ToString() ?? "Unknown")));
                        sb.Append("{=D3dcUxuj}Size: {size} | ".Translate(("size", tgt?.MemberRoster?.TotalManCount ?? 0)));
                    }
                }

                if (army != null)
                {
                    sb.Append("{=CVzSgXhT}Army: {army}".Translate(("army", army.Name?.ToString() ?? army.LeaderParty?.Name?.ToString() ?? "Unknown army")));
                    sb.Append("{=d76wc5iS}[Strength: {strength} | ".Translate(("strength", Math.Round(army.EstimatedStrength).ToString())));
                    sb.Append("{=D3dcUxuj}Size: {size} | ".Translate(("size", army.TotalHealthyMembers.ToString())));
                    sb.Append("{=7p5j5Mlx}Party nº: {count}] ".Translate(("count", army.LeaderPartyAndAttachedPartiesCount.ToString())));
                    if (!string.IsNullOrWhiteSpace(armyBehavior)) sb.Append($"Your army is: {armyBehavior} | ");
                }

                if (party.MapEvent != null)
                {
                    var me = party.MapEvent;
                    var mySide = party.MapEventSide;
                    var otherSide = mySide?.OtherSide;
                    if (mySide != null && otherSide != null)
                    {
                        string side = mySide == me.DefenderSide ? "{=c3CZCj6p}(Defending)".Translate() : "{=83Uwa9xi}(Attacking)".Translate();
                        string enemy = $"{otherSide.LeaderParty.Name}:{otherSide.TroopCount}";
                        if (me.IsFieldBattle)
                            sb.Append("{=QV6KWiVt}Field Battle {battleside} [{enemy}] | ".Translate(("battleside", side), ("enemy", enemy)));
                        else if (me.IsRaid)
                            sb.Append("{=U3NJo32u}Raid {battleside} [{enemy}] | ".Translate(("battleside", side), ("enemy", enemy)));
                        else if (me.IsSiegeAssault || me.IsSallyOut || me.IsSiegeOutside)
                            sb.Append("{=FbhijpQL}Siege {battleside} [{enemy}] | ".Translate(("battleside", side), ("enemy", enemy)));
                    }
                }
            }
            else if (party != null && !adoptedHero.IsPartyLeader)
                sb.Append($"Companion in {party.Name}'s party");
            else if (party == null && !adoptedHero.IsPartyLeader)
                sb.Append("You have no party");
            else
                sb.Append("Unknown");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GOVERN / CREATE / STATS / DISBAND / TRAIN  (unchanged from original)
        // ─────────────────────────────────────────────────────────────────────

        private void HandleGovern(Hero h, MobileParty party, Army army, string desiredName,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (h.Clan.Fiefs.Count == 0) { onFailure("Your clan has no fiefs"); return; }
            if (string.IsNullOrWhiteSpace(desiredName)) { onFailure("Specify fief"); return; }
            if (h.HeroState == Hero.CharacterStates.Released) { onFailure("Your hero has just been released"); return; }
            if (h.HeroState == Hero.CharacterStates.Traveling) { onFailure("Your hero is travelling"); return; }
            if (h.HeroState == Hero.CharacterStates.Fugitive) { onFailure("Your hero is fugitive"); return; }
            if (h.Clan.Leader.IsHumanPlayerCharacter) { onFailure("Cannot govern player towns"); return; }
            if (party?.MapEvent != null) { onFailure("Your hero is busy"); return; }
            if (h.CurrentSettlement != null && (h.CurrentSettlement.IsUnderSiege || h.CurrentSettlement.IsUnderRaid)) { onFailure("Your hero is busy"); return; }
            if (h.IsPrisoner) { onFailure("You are prisoner"); return; }
            if (army != null) { onFailure("You are in an army!"); return; }

            var desiredTown = h.Clan.Fiefs.FirstOrDefault(c => c.Name.ToString().IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0);
            if (desiredTown == null) { onFailure($"Could not find a fief with the name {desiredName}"); return; }
            if (desiredTown == h.GovernorOf) { onFailure($"Already governing {desiredTown.Name}"); return; }

            if (party != null)
            {
                bool wasLeader = party.LeaderHero == h;
                party.MemberRoster.RemoveTroop(h.CharacterObject, 1, default(UniqueTroopDescriptor), 0);
                MakeHeroFugitiveAction.Apply(h, false);
                if (wasLeader && party.IsLordParty) DisbandPartyAction.StartDisband(party);
            }
            if (h.GovernorOf != null) ChangeGovernorAction.RemoveGovernorOf(h);
            TeleportHeroAction.ApplyImmediateTeleportToSettlement(h, desiredTown.Settlement);
            ChangeGovernorAction.Apply(desiredTown, h);
            onSuccess($"Governor of {desiredTown.Name}");
        }

        private void HandleCreate(Hero h, MobileParty party, Action<string> onSuccess, Action<string> onFailure)
        {
            if (h.Clan.Leader.IsHumanPlayerCharacter) { onFailure("Cannot create party in player clan"); return; }
            if (h.HeroState == Hero.CharacterStates.Released) { onFailure("Your hero has just been released"); return; }
            if (h.HeroState == Hero.CharacterStates.Traveling) { onFailure("Your hero is travelling"); return; }
            if (h.HeroState == Hero.CharacterStates.Fugitive) { onFailure("Your hero is fugitive"); return; }
            if (party != null) { onFailure("You already have a party"); return; }
            if (h.IsPrisoner) { onFailure("You are prisoner"); return; }
            if (!h.IsClanLeader && h.Clan.WarPartyComponents.Count >= h.Clan.CommanderLimit)
            { onFailure($"Clan party limit: {h.Clan.CommanderLimit}"); return; }

            if (h.GovernorOf != null) ChangeGovernorAction.RemoveGovernorOfIfExists(h.GovernorOf);

            var spawn = SettlementHelper.GetBestSettlementToSpawnAround(h)
                        ?? h.CurrentSettlement ?? h.HomeSettlement;
            var newParty = MobilePartyHelper.SpawnLordParty(h, spawn.GatePosition,
                Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Default) / 2f);

            if (newParty == null) { onFailure("Failed to create a party. Wait some time and try again."); return; }

            if (newParty.LeaderHero != h) newParty.ChangePartyLeader(h);
            if (newParty.ActualClan != h.Clan) newParty.ActualClan = h.Clan;

            foreach (var t in BLTAdoptAHeroCampaignBehavior.Current.GetRetinue(h).ToList())
                if (t != null) newParty.MemberRoster.AddToCounts(t, 1);
            foreach (var t in BLTAdoptAHeroCampaignBehavior.Current.GetRetinue2(h).ToList())
                if (t != null) newParty.MemberRoster.AddToCounts(t, 1);

            float range = 2f * Campaign.Current.EstimatedAverageLordPartySpeed * (float)CampaignTime.HoursInDay;
            foreach (var s in Campaign.Current.Settlements.Where(s => s.IsVillage))
            {
                float dist = Campaign.Current.Models.MapDistanceModel.GetDistance(newParty, s, false, newParty.NavigationCapability, out _);
                if (dist >= range) continue;
                foreach (var (item, prod) in s.Village.VillageType.Productions)
                {
                    float weight = (item.ItemType == ItemObject.ItemTypeEnum.Horse && item.HorseComponent.IsRideable && !item.HorseComponent.IsPackAnimal) ? 7f
                                   : item.IsFood ? 0.1f : 0f;
                    float sizeF = ((float)newParty.MemberRoster.TotalManCount + 2f) / 200f;
                    int n = MBRandom.RoundRandomized(weight * prod * (1f - dist / range) * sizeF);
                    if (n > 0) newParty.ItemRoster.AddToCounts(item, n);
                }
            }
            newParty.InitializeMobilePartyAtPosition(spawn.GatePosition);
            onSuccess("Party created!");
        }

        private void HandleStats(Hero h, MobileParty party, Action<string> onSuccess, Action<string> onFailure)
        {
            if (party == null) { onFailure("You have no party"); return; }

            var comp = PartyBaseHelper.PrintRegularTroopCategories(party.MemberRoster) ?? new TextObject("Unknown");
            var roster = party.MemberRoster.GetTroopRoster();
            double tier = roster.Sum(r => r.Character.Tier * r.Number) / (double)Math.Max(1, roster.Sum(r => r.Number));
            var nav = party.IsCurrentlyAtSea ? MobileParty.NavigationType.Naval : MobileParty.NavigationType.Default;
            var near = SettlementHelper.FindNearestFortificationToMobileParty(party, nav);

            var sb = new StringBuilder();
            sb.Append($"Troops: {comp}(avg Tier {Math.Round(tier, 1)}) | ");
            sb.Append($"Speed: {Math.Round(party.Speed, 1) - UpgradeBehavior.Current.GetTotalPartySpeedBonus(party.ActualClan.Leader)} (+{UpgradeBehavior.Current.GetTotalPartySpeedBonus(party.ActualClan.Leader)}) | ");
            sb.Append($"Food: {(int)party.Food}({Math.Round(party.FoodChange, 1)}) | ");
            sb.Append($"Morale: {(int)party.Morale} | ");
            sb.Append($"Sight: {Math.Round(party.SeeingRange, 1)} | ");
            sb.Append($"Wage: {party.TotalWage}");
            if (near != null) sb.Append($" | Near: {near.Name}");
            onSuccess(sb.ToString());
        }

        private void HandlePartyDisband(Hero h, MobileParty party, string arg,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (h.Clan == null) { onFailure("You are not in a clan"); return; }
            if (h.Clan.Leader.IsHumanPlayerCharacter) { onFailure("Cannot disband parties in the player clan"); return; }

            if (arg.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var toDisband = h.Clan.WarPartyComponents
                    .Select(wpc => wpc?.MobileParty)
                    .Where(mp => mp != null && mp.LeaderHero != null && mp.IsLordParty && mp.MapEvent == null)
                    .ToList();
                if (toDisband.Count == 0) { onFailure("No eligible parties to disband"); return; }

                int count = 0;
                foreach (var mp in toDisband)
                {
                    SafeRemovePartyFromArmy(mp);
                    var leader = mp.LeaderHero;
                    DestroyPartyAction.Apply(null, mp);
                    count++;
                    FallbackLeaderToSettlement(leader, h);
                }
                onSuccess($"Disbanded {count} parties");
                return;
            }

            MobileParty target;
            if (string.IsNullOrWhiteSpace(arg))
            {
                if (party == null) { onFailure("You have no party to disband"); return; }
                target = party;
            }
            else
            {
                if (!int.TryParse(arg.Trim(), out int idx) || idx < 1)
                { onFailure("Specify a valid party index (e.g. !party disband 2)"); return; }

                int n = 0; target = null;
                foreach (var wpc in h.Clan.WarPartyComponents)
                {
                    var mp = wpc?.MobileParty;
                    if (mp == null || mp.LeaderHero == null || !mp.IsLordParty) continue;
                    if (++n == idx) { target = mp; break; }
                }
                if (target == null) { onFailure($"No party at index {idx} (clan has {n} active parties)"); return; }
            }

            if (target.MapEvent != null) { onFailure($"{target.Name} is currently in combat"); return; }

            if (target.Army != null)
            {
                if (target.Army.LeaderParty == target)
                {
                    PartyOrderBehavior.Current?.CancelOrdersForParty(target.StringId, null, false);
                    DisbandArmyAction.ApplyByUnknownReason(target.Army);
                }
                else { target.Army = null; target.AttachedTo = null; }
            }

            string name = target.Name.ToString();
            var ldr = target.LeaderHero;
            DestroyPartyAction.Apply(null, target);
            FallbackLeaderToSettlement(ldr, h);
            onSuccess($"Disbanded {name}");
        }

        private static void HandleTrain(Settings settings, Hero h, MobileParty party, string arg,
    Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.TrainEnabled) { onFailure("Training is disabled"); return; }
            if (party == null || party.LeaderHero != h) { onFailure("You must be leading a party to invest in training"); return; }
            if (TrainingBehavior.Current == null) { onFailure("Training system not initialized"); return; }

            if (arg.Equals("status", StringComparison.OrdinalIgnoreCase))
            {
                var entry = TrainingBehavior.Current.GetEntry(h);
                if (entry == null || entry.Fund <= 0) { onSuccess("No training fund active"); return; }
                int daily = TrainingBehavior.ComputeDailyBudget(entry);
                int daysEst = daily > 0 ? (int)Math.Ceiling(entry.Fund / (double)daily) : 0;
                string tierStr = entry.MaxTier > 0 ? $" | Tier cap: {entry.MaxTier}" : "";
                onSuccess($"Training fund: {entry.Fund}{Naming.Gold} | {daily}{Naming.Gold}/day | ~{daysEst} days remaining{tierStr}");
                return;
            }

            if (arg.Equals("cancel", StringComparison.OrdinalIgnoreCase))
            {
                int refund = TrainingBehavior.Current.CancelFund(h);
                if (refund <= 0) { onFailure("No training fund to cancel"); return; }
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(h, refund, false);
                onSuccess($"Training cancelled - refunded {refund}{Naming.Gold}");
                return;
            }

            if (!int.TryParse(arg, out int gold) || gold <= 0)
            { onFailure("Usage: !party train <gold> | !party train status | !party train cancel"); return; }

            int have = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(h);
            if (have < gold) { onFailure(Naming.NotEnoughGold(gold, have)); return; }

            // Pass the configured max tier through to the behavior
            TrainingBehavior.Current.AddFund(h, gold, settings.TrainMaxTier);
            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(h, -gold, true);

            var entry2 = TrainingBehavior.Current.GetEntry(h);
            int daily2 = TrainingBehavior.ComputeDailyBudget(entry2);
            string tierStr2 = settings.TrainMaxTier > 0 ? $" | Tier cap: {settings.TrainMaxTier}" : "";
            onSuccess($"Invested {gold}{Naming.Gold} in training | Total fund: {entry2.Fund}{Naming.Gold} | {daily2}{Naming.Gold}/day{tierStr2}");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  RELEASE  (!party release [all])
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Cancels the active BLT order on the hero's own party (or all free clan parties
        /// when "all" is passed) and restores normal AI decision-making.
        /// </summary>
        private static void HandlePartyRelease(Hero h, MobileParty party, string arg,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (h.Clan == null) { onFailure("You are not in a clan"); return; }
            if (h.Clan.Leader.IsHumanPlayerCharacter) { onFailure("Cannot manage orders in player clan"); return; }
            if (PartyOrderBehavior.Current == null) { onFailure("Order system not initialized"); return; }

            bool allParties = arg.Equals("all", StringComparison.OrdinalIgnoreCase);

            if (allParties)
            {
                var released = new List<string>();
                foreach (var wpc in h.Clan.WarPartyComponents)
                {
                    var mp = wpc?.MobileParty;
                    if (mp == null || mp.LeaderHero == null || !mp.IsLordParty) continue;
                    if (!PartyOrderBehavior.Current.HasActiveOrder(mp.StringId)) continue;
                    PartyOrderBehavior.Current.CancelOrdersForParty(mp.StringId, null, false);
                    try { mp.Ai.SetDoNotMakeNewDecisions(false); }
                    catch (Exception ex) { Log.Error($"[BLT] HandlePartyRelease all: AI unlock failed for {mp.Name}: {ex}"); }
                    released.Add(mp.LeaderHero.FirstName?.ToString() ?? mp.Name.ToString());
                }
                if (released.Count == 0) { onSuccess("No clan parties had active orders to release"); return; }
                onSuccess($"Released {released.Count} party order(s): {string.Join(", ", released)}");
                return;
            }

            if (party == null) { onFailure("You have no party"); return; }
            if (!PartyOrderBehavior.Current.HasActiveOrder(party.StringId))
            { onSuccess($"{party.Name} has no active order"); return; }

            PartyOrderBehavior.Current.CancelOrdersForParty(party.StringId, null, false);
            try { party.Ai.SetDoNotMakeNewDecisions(false); }
            catch (Exception ex) { Log.Error($"[BLT] HandlePartyRelease: AI unlock failed for {party.Name}: {ex}"); }
            onSuccess($"{party.Name} order released — AI restored");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CLAN PARTY ORDERS  (siege / defend / patrol / raid / garrison)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Handles !party [siege|defend|patrol|raid|garrison] &lt;target&gt; [all]
        /// Append "all" to target the entire clan's free lord parties.
        /// Smart-guard logic applies to defend and patrol when targeting a fortification.
        /// Raid on a fortification expands to its bound villages, distributed across parties.
        /// </summary>
        private void HandleClanPartyOrder(Settings settings, Hero h, MobileParty party, Army army,
            string subCmd, string args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.ArmyEnabled) { onFailure("Party orders disabled"); return; }
            if (!settings.ClanOrdersEnabled) { onFailure("Clan orders disabled"); return; }
            if (h.IsPrisoner) { onFailure("You are a prisoner"); return; }
            if (h.Clan == null) { onFailure("You are not in a clan"); return; }
            if (h.Clan.Leader.IsHumanPlayerCharacter) { onFailure("Cannot order parties in player clan"); return; }

            if (subCmd == "garrison" && !settings.GarrisonEnabled) { onFailure("Garrison is disabled"); return; }
            if (subCmd == "raid" && !settings.RaidEnabled) { onFailure("Raid is disabled"); return; }

            // ── Parse "all" suffix ───────────────────────────────────────────
            bool allParties = false;
            string settlementArg = args?.Trim() ?? "";

            if (settlementArg.EndsWith(" all", StringComparison.OrdinalIgnoreCase))
            {
                allParties = true;
                settlementArg = settlementArg.Substring(0, settlementArg.Length - 4).Trim();
            }
            else if (settlementArg.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                allParties = true;
                settlementArg = "";
            }

            // ── Determine order type ─────────────────────────────────────────
            var orderType = subCmd switch
            {
                "siege" => PartyOrderType.Siege,
                "defend" => PartyOrderType.SmartGuard,
                "patrol" => PartyOrderType.SmartGuard,
                "garrison" => PartyOrderType.Garrison,
                "raid" => PartyOrderType.Raid,
                _ => PartyOrderType.SmartGuard
            };

            // ── Collect target parties ───────────────────────────────────────
            List<MobileParty> targetParties;
            if (allParties)
            {
                targetParties = h.Clan.WarPartyComponents
                    .Select(wpc => wpc?.MobileParty)
                    .Where(mp => mp != null && mp.LeaderHero != null && mp.IsLordParty
                        && mp.MapEvent == null && !mp.IsDisbanding
                        && mp.MemberRoster.TotalHealthyCount > 0
                        && mp.Army == null)   // Only free parties
                    .ToList();

                if (targetParties.Count == 0)
                { onFailure("No free clan parties available (parties in armies are excluded)"); return; }
            }
            else
            {
                if (party == null) { onFailure("You have no party"); return; }
                if (party.LeaderHero != h) { onFailure("You must be leading your party"); return; }
                if (party.MapEvent != null) { onFailure("Your party is in combat"); return; }
                if (party.Army != null)
                { onFailure("You are in army — use !party army commands instead"); return; }
                targetParties = new List<MobileParty> { party };
            }

            var refParty = targetParties[0];

            // ── Resolve primary target settlement ────────────────────────────
            Settlement primaryTarget = null;
            if (!string.IsNullOrWhiteSpace(settlementArg))
            {
                if (orderType == PartyOrderType.Raid)
                {
                    // Try village first, then fortification (which we'll expand later)
                    primaryTarget = Settlement.All.FirstOrDefault(s =>
                        s?.IsVillage == true &&
                        s.Name.ToString().Equals(settlementArg, StringComparison.OrdinalIgnoreCase));
                    if (primaryTarget == null)
                        primaryTarget = Settlement.All
                            .Where(s => s?.IsVillage == true)
                            .OrderBy(s => s.Name.ToString().Length)
                            .FirstOrDefault(s => s.Name.ToString().IndexOf(settlementArg, StringComparison.OrdinalIgnoreCase) >= 0);
                    // If still null, check all settlements (fortification case → expand to villages later)
                    if (primaryTarget == null)
                        primaryTarget = FindSettlementByNameLoose(settlementArg);
                }
                else if (orderType == PartyOrderType.Siege)
                {
                    primaryTarget = FindSettlementByName(settlementArg, PartyOrderType.Siege, h);
                }
                else
                {
                    // defend / patrol / garrison — accept any settlement
                    primaryTarget = FindSettlementByNameLoose(settlementArg);
                }

                if (primaryTarget == null)
                { onFailure($"Could not find '{settlementArg}' for {subCmd}"); return; }
            }
            else
            {
                // Auto-pick
                switch (subCmd)
                {
                    case "siege":
                        primaryTarget = FindBestSettlementToTarget(refParty, h.Clan.MapFaction, true);
                        if (primaryTarget == null) { onFailure("No valid siege target found"); return; }
                        break;
                    case "defend":
                    case "patrol":
                        primaryTarget = FindBestSettlementToDefend(refParty, h.Clan.MapFaction);
                        break;
                    case "garrison":
                        primaryTarget = FindBestSettlementToDefend(refParty, h.Clan.MapFaction);
                        if (primaryTarget == null) { onFailure("No garrison target found"); return; }
                        break;
                    case "raid":
                        var refPos = refParty.GetPosition2D;
                        primaryTarget = Settlement.All
                            .Where(s => s.IsVillage && s.Village.Settlement?.IsUnderRaid == false
                                && h.Clan.Kingdom?.IsAtWarWith(s.MapFaction) == true)
                            .OrderBy(s => s.GetPosition2D.Distance(refPos))
                            .FirstOrDefault();
                        if (primaryTarget == null) { onFailure("No raidable village found nearby"); return; }
                        break;
                }
            }

            // ── Siege: war & reachability validation ─────────────────────────
            if (orderType == PartyOrderType.Siege)
            {
                //if (h.Clan.Kingdom == null) { onFailure("You must be in a kingdom to besiege"); return; }
                if (primaryTarget != null && !primaryTarget.IsFortification)
                { onFailure($"{primaryTarget.Name} is not a fortification"); return; }
                if (primaryTarget != null && !h.Clan.MapFaction.IsAtWarWith(primaryTarget.MapFaction))
                { onFailure($"Not at war with {primaryTarget.Name}'s owners"); return; }
                if (h.Clan.MapFaction.FactionsAtWarWith.Count == 0)
                { onFailure("No active wars"); return; }
            }

            // ── Garrison: must be friendly fortification ──────────────────────
            if (orderType == PartyOrderType.Garrison)
            {
                if (primaryTarget != null && !primaryTarget.IsFortification)
                { onFailure($"{primaryTarget.Name} is not a fortification"); return; }
                if (primaryTarget != null && h.Clan.Kingdom != null && h.Clan.Kingdom.IsAtWarWith(primaryTarget.MapFaction))
                { onFailure($"Cannot garrison in hostile settlement {primaryTarget.Name}"); return; }
            }

            // ── SmartGuard: target should be a fortification ──────────────────
            if (orderType == PartyOrderType.SmartGuard && primaryTarget != null && !primaryTarget.IsFortification)
            {
                // Non-fortification target: just use regular patrol
                orderType = PartyOrderType.Patrol;
            }

            // ── Raid on a fortification → expand to bound villages ────────────
            if (orderType == PartyOrderType.Raid && primaryTarget != null && primaryTarget.IsFortification)
            {
                var villages = primaryTarget.BoundVillages
                    .Where(v => v.Settlement?.IsUnderRaid == false && v?.Settlement != null && v.Settlement.IsActive
                        && h.Clan.Kingdom?.IsAtWarWith(v.Settlement.MapFaction) == true)
                    .OrderByDescending(v => v.Hearth)
                    .ToList();

                if (villages.Count == 0)
                { onFailure($"No raidable villages found around {primaryTarget.Name}"); return; }

                if (!allParties)
                {
                    IssueSinglePartyOrder(settings, h, refParty, PartyOrderType.Raid,
                        villages[0].Settlement, onSuccess, onFailure);
                }
                else
                {
                    var results = new List<string>();
                    var usedIds = new HashSet<string>();
                    var fortRef = primaryTarget; // keep for patrol fallback

                    foreach (var mp in targetParties)
                    {
                        var available = villages.FirstOrDefault(v => !usedIds.Contains(v.Settlement.StringId));
                        if (available != null)
                        {
                            usedIds.Add(available.Settlement.StringId);
                            IssueSinglePartyOrder(settings, h, mp, PartyOrderType.Raid, available.Settlement, null, null);
                            results.Add($"{mp.LeaderHero?.FirstName}→{available.Settlement.Name}");
                        }
                        else
                        {                     
                            // No more villages; patrol / smart-guard the fortification
                            IssueSinglePartyOrder(settings, h, mp, PartyOrderType.SmartGuard, fortRef, null, null);
                            results.Add($"{mp.LeaderHero?.FirstName}→patrol");
                        }
                    }
                    onSuccess($"Raid {primaryTarget.Name}: " + string.Join(", ", results));
                }
                return;
            }

            // ── Standard issuance ─────────────────────────────────────────────
            if (!allParties)
            {
                IssueSinglePartyOrder(settings, h, targetParties[0], orderType, primaryTarget, onSuccess, onFailure);
            }
            else
            {
                var results = new List<string>();
                foreach (var mp in targetParties)
                {
                    IssueSinglePartyOrder(settings, h, mp, orderType, primaryTarget, null, null);
                    results.Add(mp.LeaderHero?.FirstName?.ToString() ?? mp.Name.ToString());
                }
                string tName = primaryTarget?.Name?.ToString() ?? "auto";
                onSuccess($"{results.Count} parties ordered to {subCmd} → {tName}: {string.Join(", ", results)}");
            }
        }

        /// <summary>
        /// Validates and issues a single-party order, registering it with PartyOrderBehavior.
        /// Siege: checks allied-siege permission.  Raid: checks village availability.
        /// Falls back to SmartGuard patrol on reachability failure.
        /// </summary>
        private void IssueSinglePartyOrder(Settings settings, Hero h, MobileParty mp,
            PartyOrderType orderType, Settlement target,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (mp == null) return;
            if (mp.MapEvent != null) { onFailure?.Invoke($"{mp.Name} is in combat"); return; }

            // ── Siege validation ──────────────────────────────────────────────
            if (orderType == PartyOrderType.Siege && target != null)
            {
                if (!mp.MapFaction.IsAtWarWith(target.MapFaction))
                { onFailure?.Invoke($"Not at war with {target.Name}'s faction"); return; }

                if (target.IsUnderSiege)
                {
                    var besiegerFaction = target.SiegeEvent?.BesiegerCamp?.LeaderParty?.MapFaction;
                    if (besiegerFaction != null && besiegerFaction != mp.MapFaction)
                    {
                        var besiegerK = besiegerFaction as Kingdom;
                        var heroK = h.Clan.Kingdom;
                        bool allied = besiegerK != null && heroK != null
                            && BLTTreatyManager.Current?.GetAlliance(heroK, besiegerK) != null
                            && besiegerK.IsAtWarWith(target.MapFaction);
                        if (!allied)
                        { onFailure?.Invoke($"{target.Name} is under siege by a non-allied faction"); return; }
                    }
                }

                if (!PartyOrderBehavior.IsSettlementReachable(mp, target))
                {
                    // Reachability failure: fallback to smart-guard of nearest friendly fortification
                    var fallback = FindBestSettlementToDefend(mp, h.Clan.MapFaction);
                    orderType = PartyOrderType.SmartGuard;
                    target = fallback;
                    onFailure?.Invoke($"{mp.LeaderHero?.FirstName}: {target?.Name.ToString() ?? "target"} not reachable — patrolling instead");
                    // Allow fall-through to issue the fallback order
                }
            }

            // ── Raid validation ───────────────────────────────────────────────
            if (orderType == PartyOrderType.Raid && target != null)
            {
                if (!target.IsVillage)
                { onFailure?.Invoke($"{target.Name} is not a village"); return; }
                if (!mp.MapFaction.IsAtWarWith(target.MapFaction))
                { onFailure?.Invoke($"Not at war with {target.Name}'s faction"); return; }
                if (target.Village.Settlement?.IsUnderRaid == true)
                { onFailure?.Invoke($"{target.Name} is already under raid"); return; }
            }

            // ── Garrison validation ───────────────────────────────────────────
            if (orderType == PartyOrderType.Garrison && target != null)
            {
                if (!target.IsFortification)
                { onFailure?.Invoke($"{target.Name} is not a fortification"); return; }
                if (h.Clan.Kingdom != null && h.Clan.Kingdom.IsAtWarWith(target.MapFaction))
                { onFailure?.Invoke($"Cannot garrison in hostile {target.Name}"); return; }
            }

            PartyOrderBehavior.IssueOrder(mp, orderType, target);
            mp.Ai.SetDoNotMakeNewDecisions(true);
            PartyOrderBehavior.Current?.RegisterOrder(h, mp, orderType, target,
                settings.ArmyMaxReissueAttempts, settings.PartyOrderExpiryDays);

            onSuccess?.Invoke($"{mp.LeaderHero?.Name ?? mp.Name} → {orderType} {target?.Name?.ToString() ?? "auto"}");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ARMY — main dispatcher
        // ─────────────────────────────────────────────────────────────────────

        private void HandleArmy(Settings settings, Hero h, MobileParty party, Army army, string desiredName,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.ArmyEnabled) { onFailure("Army disabled"); return; }
            if (h.IsPrisoner) { onFailure("You are a prisoner!"); return; }

            var parts = desiredName.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            var sub = parts.Length > 0 ? parts[0].ToLower() : "";
            var tgtArg = parts.Length > 1 ? parts[1].Trim() : "";

            if (string.IsNullOrEmpty(sub))
            {
                onFailure("Specify: siege / defend / patrol / garrison / release / status / disband / leave / reassign / kick / view / create / takeover / call / join / threat / allowai / allowblt");
                return;
            }

            switch (sub)
            {
                case "status": ArmyStatus(h, party, army, onSuccess, onFailure); break;
                case "disband": ArmyDisband(settings, h, party, army, tgtArg, onSuccess, onFailure); break;
                case "leave": ArmyLeave(h, party, army, onSuccess, onFailure); break;
                case "reassign": ArmyReassign(settings, h, party, army, tgtArg, onSuccess, onFailure); break;
                case "view": ArmyView(settings, h, onSuccess, onFailure); break;
                case "create": ArmyCreate(settings, h, party, army, tgtArg, onSuccess, onFailure); break;
                case "takeover": ArmyTakeover(settings, h, party, army, tgtArg, onSuccess, onFailure); break;
                case "call": ArmyCall(settings, h, party, army, tgtArg, onSuccess, onFailure); break;
                case "join": ArmyJoin(settings, h, party, army, tgtArg, onSuccess, onFailure); break;
                case "kick": ArmyKick(settings, h, party, army, tgtArg, onSuccess, onFailure); break;
                case "garrison": ArmyGarrison(settings, h, party, army, tgtArg, onSuccess, onFailure); break;
                case "release": ArmyRelease(h, party, army, onSuccess, onFailure); break;
                case "allowai": ArmyAllowAI(settings, h, tgtArg, onSuccess, onFailure); break;
                case "allowblt": ArmyAllowBLT(settings, h, tgtArg, onSuccess, onFailure); break;
                case "threat": ArmyThreat(settings, h, party, onSuccess, onFailure); break;
                case "siege":
                case "defend":
                case "patrol": ArmyOrder(settings, h, party, army, sub, tgtArg, onSuccess, onFailure); break;
                default:
                    onFailure("Specify: siege / defend / patrol / garrison / release / status / disband / leave / reassign / kick / view / create / takeover / call / join / threat / allowai / allowblt");
                    break;
            }
        }

        // ── STATUS ────────────────────────────────────────────────────────────

        private static void ArmyStatus(Hero h, MobileParty party, Army army,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (army == null) { onFailure("You have no army"); return; }

            var sb = new StringBuilder();
            sb.Append($"Army: {army.Name} | Str: {(int)army.EstimatedStrength} | ");
            sb.Append($"{army.TotalHealthyMembers} troops / {army.LeaderPartyAndAttachedPartiesCount} parties | ");
            sb.Append($"Cohesion: {(int)army.Cohesion}");
            sb.Append($" | {party.DefaultBehavior} → {party.TargetSettlement?.Name?.ToString() ?? party.TargetParty?.Name?.ToString() ?? "—"}");
            sb.Append($" | Morale: {(int)army.Morale}");
            if (party.FoodChange < 0f && party.Food > 0f)
                sb.Append($" | Food: ~{(int)(party.Food / Math.Abs(party.FoodChange))}d");
            var order = PartyOrderBehavior.Current?.GetActiveOrder(party.StringId);
            if (order != null)
                sb.Append($" | Order: {order.Type} ({order.ReissueAttempts}/{order.MaxReissueAttempts} re-issues)");
            onSuccess(sb.ToString());
        }

        // ── DISBAND ───────────────────────────────────────────────────────────

        private static void ArmyDisband(Settings settings, Hero h, MobileParty party, Army army,
            string tgtArg, Action<string> onSuccess, Action<string> onFailure)
        {
            // Independent clan — disband their own clan army (no index needed, only one)
            if (h.Clan.Kingdom == null)
            {
                if (army == null || army.LeaderParty != party)
                { onFailure("You are not leading a clan army"); return; }
                if (party.MapEvent != null) { onFailure("Your army is in combat"); return; }

                string aName = army.Name.ToString();
                PartyOrderBehavior.Current?.CancelOrdersForParty(party.StringId, null, false);
                DisbandArmyAction.ApplyByUnknownReason(army);
                onSuccess($"Clan army {aName} disbanded");
                return;
            }

            bool isKing = settings.KingArmyManageEnabled && h.Clan.Kingdom?.Leader == h;

            if (isKing)
            {
                var kArmies = h.Clan.Kingdom.Armies.ToList();
                Army targetArmy = null;

                if (string.IsNullOrWhiteSpace(tgtArg))
                {
                    if (army != null && army.LeaderParty == party) targetArmy = army;
                    else if (kArmies.Count == 1) targetArmy = kArmies[0];
                    else if (kArmies.Count == 0) { onFailure("No active armies to disband"); return; }
                    else { onFailure($"Specify army index (1-{kArmies.Count}). Use 'army view'."); return; }
                }
                else if (int.TryParse(tgtArg, out int idx) && idx >= 1 && idx <= kArmies.Count)
                    targetArmy = kArmies[idx - 1];
                else { onFailure($"Invalid index '{tgtArg}'. Kingdom has {kArmies.Count} armies."); return; }

                if (targetArmy.LeaderParty?.MapEvent != null) { onFailure($"{targetArmy.Name} is in combat"); return; }

                string aName = targetArmy.Name.ToString();
                PartyOrderBehavior.Current?.CancelOrdersForParty(targetArmy.LeaderParty?.StringId, null, false);
                DisbandArmyAction.ApplyByUnknownReason(targetArmy);
                onSuccess($"Disbanded {aName}");
                return;
            }

            if (army == null || army.LeaderParty != party) { onFailure("You are not leading an army"); return; }
            if (party.MapEvent != null) { onFailure("Your army is in combat"); return; }

            PartyOrderBehavior.Current?.CancelOrdersForParty(party.StringId, null, false);
            DisbandArmyAction.ApplyByUnknownReason(army);
            onSuccess("Army disbanded");
        }

        // ── LEAVE ─────────────────────────────────────────────────────────────

        private static void ArmyLeave(Hero h, MobileParty party, Army army,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (army == null) { onFailure("You are not in an army"); return; }
            if (army.LeaderParty == party) { onFailure("Cannot leave your own army"); return; }
            if (army.LeaderParty == MobileParty.MainParty) { onFailure("Cannot leave the player's army"); return; }
            if (party.MapEvent != null) { onFailure("Your army is fighting"); return; }

            var old = army;

            PartyOrderBehavior.Current?.CancelOrdersForParty(party.StringId, null, false);
            try { party.Ai.SetDoNotMakeNewDecisions(false); }
            catch (Exception ex) { Log.Error($"[BLT] ArmyLeave: AI unlock failed: {ex}"); }

            party.Army = null;
            party.AttachedTo = null;
            onSuccess($"Left {old.Name}");
            if (old.LeaderPartyAndAttachedPartiesCount <= 1 && !old.IsWaitingForArmyMembers())
                DisbandArmyAction.ApplyByUnknownReason(old);
        }

        // ── REASSIGN ──────────────────────────────────────────────────────────

        private static void ArmyReassign(Settings settings, Hero h, MobileParty party, Army army,
            string tgtArg, Action<string> onSuccess, Action<string> onFailure)
        {
            if (string.IsNullOrWhiteSpace(tgtArg)) { onFailure("Specify a hero name"); return; }
            if (army == null || army.LeaderParty != party) { onFailure("You must be leading an army"); return; }
            if (party.MapEvent != null) { onFailure("Your army is in combat"); return; }

            var newLeaderParty = army.Parties.FirstOrDefault(p =>
                p != party && p.LeaderHero != null
                && p.LeaderHero.Name.ToString().IndexOf(tgtArg, StringComparison.OrdinalIgnoreCase) >= 0);
            if (newLeaderParty == null) { onFailure($"Could not find '{tgtArg}' in your army"); return; }
            if (newLeaderParty.MapEvent != null) { onFailure($"{newLeaderParty.LeaderHero.Name} is in combat"); return; }

            ExecuteReassign(settings, h, party, army, newLeaderParty, onSuccess, onFailure);
        }

        private static void ExecuteReassign(Settings settings, Hero h, MobileParty party, Army army,
            MobileParty newLeaderParty, Action<string> onSuccess, Action<string> onFailure)
        {
            var curOrder = PartyOrderBehavior.Current?.GetActiveOrder(party.StringId);
            var curTarget = curOrder?.TargetSettlementId != null ? Settlement.Find(curOrder.TargetSettlementId) : null;
            var curType = curOrder?.Type ?? PartyOrderType.Patrol;
            var armyType = curType == PartyOrderType.Siege ? Army.ArmyTypes.Besieger
                          : curType == PartyOrderType.Defend ? Army.ArmyTypes.Defender
                          : Army.ArmyTypes.Patrolling;

            var remaining = army.Parties.Where(p => p != party && p != newLeaderParty).ToMBList();
            PartyOrderBehavior.Current?.CancelOrdersForParty(party.StringId, null, false);
            DisbandArmyAction.ApplyByUnknownReason(army);

            float influenceBefore = h.Clan.Influence;
            var gather = curTarget ?? newLeaderParty.CurrentSettlement ?? h.HomeSettlement;
            AdoptedHeroFlags._allowBLTArmyCreation = true;
            try { h.Clan.Kingdom.CreateArmy(newLeaderParty.LeaderHero, gather, armyType, remaining); }
            finally { AdoptedHeroFlags._allowBLTArmyCreation = false; }
            h.Clan.Influence = influenceBefore;

            if (newLeaderParty.Army == null) { onFailure("Failed to transfer army leadership"); return; }

            if (curTarget != null)
            {
                PartyOrderBehavior.IssueOrder(newLeaderParty, curType, curTarget);
                newLeaderParty.Ai.SetDoNotMakeNewDecisions(true);
                PartyOrderBehavior.Current?.RegisterOrder(h, newLeaderParty, curType, curTarget,
                    settings.ArmyMaxReissueAttempts, settings.ArmyOrderExpiryDays);
            }
            onSuccess($"Army command transferred to {newLeaderParty.LeaderHero.Name}");
        }

        // ── KICK (remove n weakest parties from army) ─────────────────────────

        private static void ArmyKick(Settings settings, Hero h, MobileParty party, Army army,
            string tgtArg, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.ArmyKickEnabled) { onFailure("Army kick is disabled"); return; }

            bool isKing = settings.KingArmyManageEnabled && h.Clan.Kingdom?.Leader == h;
            Army targetArmy = null;
            string countStr = tgtArg;

            if (isKing && !string.IsNullOrWhiteSpace(tgtArg))
            {
                var tokens = tgtArg.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length >= 1 && int.TryParse(tokens[0], out int armyIdx))
                {
                    var kArmies = h.Clan.Kingdom.Armies.ToList();
                    if (armyIdx >= 1 && armyIdx <= kArmies.Count)
                    {
                        targetArmy = kArmies[armyIdx - 1];
                        countStr = tokens.Length > 1 ? tokens[1] : "";
                    }
                }
            }

            if (targetArmy == null)
            {
                if (army == null || army.LeaderParty != party)
                { onFailure("You must be leading an army (kings can specify an army index)"); return; }
                targetArmy = army;
            }

            if (targetArmy.LeaderParty?.MapEvent != null)
            { onFailure($"{targetArmy.Name} is currently in combat"); return; }

            int countToKick = 1;
            if (!string.IsNullOrWhiteSpace(countStr) && int.TryParse(countStr.Trim(), out int parsed))
                countToKick = Math.Max(1, parsed);

            var kickable = targetArmy.Parties
                .Where(p => p != targetArmy.LeaderParty && p.MapEvent == null)
                .OrderBy(p => p.Party.EstimatedStrength)
                .Take(countToKick)
                .ToList();

            if (kickable.Count == 0) { onFailure($"{targetArmy.Name} has no kickable parties"); return; }

            var kicked = new List<string>();
            foreach (var p in kickable)
            {
                string pName = p.LeaderHero?.Name?.ToString() ?? p.Name.ToString();
                PartyOrderBehavior.Current?.CancelOrdersForParty(p.StringId, null, false);
                try { p.Ai.SetDoNotMakeNewDecisions(false); }
                catch (Exception ex) { Log.Error($"[BLT] ArmyKick: AI unlock failed for {pName}: {ex}"); }
                p.Army = null;
                p.AttachedTo = null;
                kicked.Add($"{pName}({(int)p.Party.EstimatedStrength}str)");
            }

            onSuccess($"Kicked {kicked.Count} parties from {targetArmy.Name}: {string.Join(", ", kicked)}");
        }

        // ── GARRISON (army) ───────────────────────────────────────────────────

        private void ArmyGarrison(Settings settings, Hero h, MobileParty party, Army army,
            string tgtArg, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.GarrisonEnabled) { onFailure("Garrison is disabled"); return; }
            if (army == null || army.LeaderParty != party) { onFailure("You must be leading an army"); return; }
            if (party.MapEvent != null) { onFailure("Your army is in combat"); return; }

            Settlement target;
            if (!string.IsNullOrWhiteSpace(tgtArg))
            {
                target = FindSettlementByName(tgtArg, PartyOrderType.Garrison, h);
                if (target == null) { onFailure($"Could not find fortification '{tgtArg}'"); return; }
            }
            else
            {
                target = FindBestSettlementToDefend(party, h.Clan.MapFaction);
                if (target == null) { onFailure("No garrison target found"); return; }
            }

            if (h.Clan.Kingdom != null && h.Clan.Kingdom.IsAtWarWith(target.MapFaction))
            { onFailure($"Cannot garrison in hostile settlement {target.Name}"); return; }

            army.ArmyType = Army.ArmyTypes.Defender;
            PartyOrderBehavior.IssueOrder(party, PartyOrderType.Garrison, target);
            party.Ai.SetDoNotMakeNewDecisions(true);
            PartyOrderBehavior.Current?.RegisterOrder(h, party, PartyOrderType.Garrison, target,
                settings.ArmyMaxReissueAttempts, settings.ArmyOrderExpiryDays);

            onSuccess($"Army garrisoning at {target.Name}");
        }

        // ── RELEASE (army) ────────────────────────────────────────────────────

        /// <summary>
        /// Cancels the active BLT order on the army leader's party and restores normal
        /// AI decision-making, letting the army behave freely again.
        /// </summary>
        private static void ArmyRelease(Hero h, MobileParty party, Army army,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (army == null || army.LeaderParty != party)
            { onFailure("You are not leading an army"); return; }
            if (PartyOrderBehavior.Current == null)
            { onFailure("Order system not initialized"); return; }
            if (!PartyOrderBehavior.Current.HasActiveOrder(party.StringId))
            { onSuccess($"Army {army.Name} has no active order"); return; }

            PartyOrderBehavior.Current.CancelOrdersForParty(party.StringId, null, false);
            try { party.Ai.SetDoNotMakeNewDecisions(false); }
            catch (Exception ex) { Log.Error($"[BLT] ArmyRelease: AI unlock failed for {party.Name}: {ex}"); }
            onSuccess($"Army {army.Name} order released — AI restored");
        }

        // ── VIEW ──────────────────────────────────────────────────────────────

        private static void ArmyView(Settings settings, Hero h,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (h.Clan.Kingdom == null)
            {
                var ownArmies = BLTClanArmyBehavior.Current?.GetClanArmies(h.Clan) ?? new List<Army>();

                var alliedArmies = new List<(Clan owner, Army army)>();
                if (BLTClanDiplomacyBehavior.Current != null)
                {
                    foreach (var allied in BLTClanDiplomacyBehavior.Current.GetAlliedClans(h.Clan))
                    {
                        foreach (var a in BLTClanArmyBehavior.Current?.GetClanArmies(allied) ?? new List<Army>())
                            alliedArmies.Add((allied, a));
                    }
                }

                if (ownArmies.Count == 0 && alliedArmies.Count == 0)
                { onSuccess($"{h.Clan.Name} has no active clan armies or allied armies"); return; }

                var sb = new StringBuilder();

                if (ownArmies.Count > 0)
                {
                    sb.Append($"{h.Clan.Name} Armies: ");
                    foreach (var a in ownArmies)
                    {
                        var ldr = a.LeaderParty?.LeaderHero;
                        string behavior = a.LeaderParty?.GetBehaviorText()?.ToString() ?? "—";
                        string tgt = a.LeaderParty?.TargetSettlement?.Name?.ToString()
                                      ?? a.LeaderParty?.TargetParty?.Name?.ToString() ?? "—";
                        sb.Append($"[own] {a.Name} (Leader:{ldr?.Name.ToString() ?? "?"}, " +
                                  $"Str:{(int)a.EstimatedStrength}, " +
                                  $"Parties:{a.LeaderPartyAndAttachedPartiesCount}, {behavior}→{tgt}) | ");
                    }
                }

                if (alliedArmies.Count > 0)
                {
                    sb.Append("Allied Armies (!party army join [n]): ");
                    int idx = 1;
                    foreach (var (owner, a) in alliedArmies)
                    {
                        var ldr = a.LeaderParty?.LeaderHero;
                        string behavior = a.LeaderParty?.GetBehaviorText()?.ToString() ?? "—";
                        string tgt = a.LeaderParty?.TargetSettlement?.Name?.ToString()
                                      ?? a.LeaderParty?.TargetParty?.Name?.ToString() ?? "—";
                        bool inCombat = a.LeaderParty?.MapEvent != null;
                        sb.Append($"[{idx}] {a.Name} ({owner.Name}, Leader:{ldr?.Name.ToString() ?? "?"}, " +
                                  $"Str:{(int)a.EstimatedStrength}, " +
                                  $"Parties:{a.LeaderPartyAndAttachedPartiesCount}, " +
                                  $"{behavior}→{tgt}{(inCombat ? " [COMBAT]" : "")}) | ");
                        idx++;
                    }
                }

                onSuccess(sb.ToString().TrimEnd(' ', '|'));
                return;
            }

            if (!settings.KingArmyManageEnabled) { onFailure("King army management is disabled"); return; }
            if (h.Clan.Kingdom == null) { onFailure("You are not in a kingdom"); return; }

            var armies = h.Clan.Kingdom.Armies.ToList();
            if (armies.Count == 0) { onSuccess($"{h.Clan.Kingdom.Name} has no active armies"); return; }

            var sb2 = new StringBuilder($"{h.Clan.Kingdom.Name} | {armies.Count} Armies: ");
            for (int i = 0; i < armies.Count; i++)
            {
                var a = armies[i];
                var ldr = a.LeaderParty?.LeaderHero;
                string behavior = a.LeaderParty?.GetBehaviorText()?.ToString() ?? "—";
                string tgt = a.LeaderParty?.TargetSettlement?.Name?.ToString()
                                  ?? a.LeaderParty?.TargetParty?.Name?.ToString() ?? "—";
                string orderTag = PartyOrderBehavior.Current?.HasActiveOrder(a.LeaderParty?.StringId ?? "") == true
                    ? "[order]" : "";
                sb2.Append($"[{i + 1}] {a.Name} (Leader:{ldr?.Name.ToString() ?? "?"}, Clan:{a.LeaderParty?.ActualClan?.Name.ToString() ?? "?"}, " +
                           $"Str:{(int)a.EstimatedStrength}, Parties:{a.LeaderPartyAndAttachedPartiesCount}, " +
                           $"{behavior}→{tgt}{orderTag}) | ");
            }
            onSuccess(sb2.ToString().TrimEnd(' ', '|'));
        }

        // ── CREATE ────────────────────────────────────────────────────────────

        private void ArmyCreate(Settings settings, Hero h, MobileParty party, Army army,
            string tgtArg, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.KingArmyManageEnabled) { onFailure("King army management is disabled"); return; }
            if (h.Clan.Kingdom?.Leader != h) { onFailure("You must be king to commission an NPC-led army"); return; }
            if (h.Clan.IsUnderMercenaryService) { onFailure("Mercenaries can't create armies"); return; }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(h) < settings.CreateArmyPrice)
            { onFailure(Naming.NotEnoughGold(settings.CreateArmyPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(h))); return; }

            var candidates = h.Clan.Kingdom.AllParties
                .Where(p => p.LeaderHero != null
                    && !p.LeaderHero.IsAdopted()
                    && p.LeaderHero != Hero.MainHero
                    && p.Army == null && p.AttachedTo == null
                    && p.MapEvent == null && !p.IsDisbanding
                    && p.IsLordParty && p.MemberRoster.TotalHealthyCount > 0)
                .ToList();

            if (candidates.Count == 0) { onFailure("No eligible NPC lords available to lead an army"); return; }

            MobileParty leaderParty;
            if (!string.IsNullOrWhiteSpace(tgtArg))
            {
                leaderParty = candidates.FirstOrDefault(p =>
                    p.LeaderHero.Name.ToString().IndexOf(tgtArg, StringComparison.OrdinalIgnoreCase) >= 0);
                if (leaderParty == null) { onFailure($"No eligible NPC lord matching '{tgtArg}'"); return; }
            }
            else
                leaderParty = candidates.GetRandomElement();

            var vassalClans = VassalBehavior.Current?.GetVassalClans(h.Clan) ?? new List<Clan>();
            var modelParties = Campaign.Current.Models.ArmyManagementCalculationModel.GetMobilePartiesToCallToArmy(leaderParty);
            var members = candidates
                .Where(p => p != leaderParty)
                .Concat(modelParties.Where(p => p != leaderParty && p != null))
                .Where(p => p.Army == null && p.AttachedTo == null && p.MapEvent == null && !p.IsDisbanding)
                .Distinct().ToMBList();

            var gather = leaderParty.CurrentSettlement
                ?? SettlementHelper.FindNearestSettlementToMobileParty(leaderParty, leaderParty.NavigationCapability)
                ?? h.Clan.Kingdom.Settlements.FirstOrDefault(s => s.IsFortification);
            if (gather == null) { onFailure("Could not determine a gather point"); return; }

            // FIX: release any BLT order locks on parties being absorbed into the new army
            foreach (var mp in members)
            {
                if (mp?.StringId == null) continue;
                PartyOrderBehavior.Current?.CancelOrdersForParty(mp.StringId, null, false);
                try { mp.Ai.SetDoNotMakeNewDecisions(false); }
                catch (Exception ex) { Log.Error($"[BLT] ArmyCreate: AI unlock failed for {mp.Name}: {ex}"); }
            }

            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(h, -settings.CreateArmyPrice, true);
            h.Clan.Kingdom.CreateArmy(leaderParty.LeaderHero, gather, Army.ArmyTypes.Patrolling, members);

            if (leaderParty.Army == null)
            {
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(h, settings.CreateArmyPrice, false);
                onFailure("Army creation failed — refunded");
                return;
            }

            int memberCount = leaderParty.Army.Parties.Count - 1;
            onSuccess($"Commissioned army under {leaderParty.LeaderHero.Name} ({memberCount} gathering)");
            Log.ShowInformation($"{h.Name} commissioned an army under {leaderParty.LeaderHero.Name}!",
                h.CharacterObject, Log.Sound.Horns2);
        }

        // ── TAKEOVER ──────────────────────────────────────────────────────────

        private void ArmyTakeover(Settings settings, Hero h, MobileParty party, Army army,
            string tgtArg, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.TakeoverEnabled) { onFailure("Army takeover is disabled"); return; }
            if (!h.IsPartyLeader || party == null) { onFailure("You must be leading a party to take over an army"); return; }
            if (party.MapEvent != null) { onFailure("Your party is in combat"); return; }
            if (army != null && army.LeaderParty == party) { onFailure("You are already leading an army — use reassign instead"); return; }
            if (h.Clan.Kingdom == null) { onFailure("You are not in a kingdom"); return; }

            var clanArmies = h.Clan.Kingdom.Armies
                .Where(a => a.LeaderParty?.ActualClan == h.Clan && a.LeaderParty?.LeaderHero != h)
                .ToList();

            Army targetArmy = null;
            if (string.IsNullOrWhiteSpace(tgtArg))
            {
                if (clanArmies.Count == 0) { onFailure("No armies in your clan to take over"); return; }
                if (clanArmies.Count == 1) targetArmy = clanArmies[0];
                else
                {
                    var sb2 = new StringBuilder("Multiple clan armies — specify index or leader name: ");
                    for (int i = 0; i < clanArmies.Count; i++)
                        sb2.Append($"[{i + 1}] {clanArmies[i].LeaderParty?.LeaderHero?.Name} | ");
                    onFailure(sb2.ToString().TrimEnd(' ', '|'));
                    return;
                }
            }
            else
            {
                var kArmies = h.Clan.Kingdom.Armies.ToList();
                if (int.TryParse(tgtArg, out int idx) && idx >= 1 && idx <= kArmies.Count)
                {
                    var candidate = kArmies[idx - 1];
                    if (candidate.LeaderParty?.ActualClan != h.Clan) { onFailure($"Army [{idx}] is not led by a clan member"); return; }
                    if (candidate.LeaderParty?.LeaderHero == h) { onFailure("That is your own army"); return; }
                    targetArmy = candidate;
                }
                else
                {
                    targetArmy = clanArmies.FirstOrDefault(a =>
                        a.LeaderParty?.LeaderHero?.Name.ToString().IndexOf(tgtArg, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (targetArmy == null) { onFailure($"No clan army found matching '{tgtArg}'"); return; }
                }
            }

            if (targetArmy.LeaderParty?.MapEvent != null) { onFailure("That army is currently in combat"); return; }

            var oldLeader = targetArmy.LeaderParty;
            var curOrder = PartyOrderBehavior.Current?.GetActiveOrder(oldLeader.StringId);
            var curTarget = curOrder?.TargetSettlementId != null ? Settlement.Find(curOrder.TargetSettlementId) : null;
            var curType = curOrder?.Type ?? PartyOrderType.Patrol;
            var armyType = targetArmy.ArmyType;

            var remaining = targetArmy.Parties.Where(p => p != oldLeader && p != party).ToMBList();
            if (oldLeader != null && oldLeader != party && oldLeader.LeaderHero != null) remaining.Add(oldLeader);

            PartyOrderBehavior.Current?.CancelOrdersForParty(oldLeader.StringId, null, false);
            DisbandArmyAction.ApplyByUnknownReason(targetArmy);

            float influenceBefore = h.Clan.Influence;
            var gather = curTarget ?? oldLeader.CurrentSettlement ?? h.HomeSettlement;
            AdoptedHeroFlags._allowBLTArmyCreation = true;
            try { h.Clan.Kingdom.CreateArmy(h, gather, armyType, remaining); }
            finally { AdoptedHeroFlags._allowBLTArmyCreation = false; }
            h.Clan.Influence = influenceBefore;

            if (party.Army == null) { onFailure("Failed to seize army leadership"); return; }

            if (curTarget != null)
            {
                PartyOrderBehavior.IssueOrder(party, curType, curTarget);
                party.Ai.SetDoNotMakeNewDecisions(true);
                PartyOrderBehavior.Current?.RegisterOrder(h, party, curType, curTarget,
                    settings.ArmyMaxReissueAttempts, settings.ArmyOrderExpiryDays);
            }

            onSuccess($"Took over {oldLeader.LeaderHero?.Name}'s army — {remaining.Count} parties gathering");
            Log.ShowInformation($"{h.Name} seized command of {oldLeader.LeaderHero?.Name}'s army!",
                h.CharacterObject, Log.Sound.Horns2);
        }

        // ── CALL ──────────────────────────────────────────────────────────────

        private void ArmyCall(Settings settings, Hero h, MobileParty party, Army army,
            string tgtArg, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.CallEnabled) { onFailure("Call is disabled"); return; }
            if (h.Clan.IsUnderMercenaryService) { onFailure("Mercenaries cannot call armies"); return; }

            bool isKing = h.Clan.Kingdom != null && h.Clan.Kingdom.Leader == h;
            bool isClanLeader = h.Clan.Leader == h && h.Clan.Kingdom == null;

            if (h.Clan.Kingdom == null && !isClanLeader)
            { onFailure("You must be your clan's leader to call parties"); return; }
            if (h.Clan.Kingdom == null && (army == null || army.LeaderParty != party))
            { onFailure("You must be leading your clan army to call parties to it"); return; }

            var allCallTokens = tgtArg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (allCallTokens.Length == 0)
            { onFailure("Specify: army call nearby [n] | army call all [n]"); return; }

            var callType = allCallTokens[0].ToLower();
            if (callType != "nearby" && callType != "all")
            { onFailure("Specify: army call nearby [n] | army call all [n]"); return; }

            var afterCall = allCallTokens.Skip(1).ToArray();

            bool kingWithoutOwnArmy = isKing && (army == null || army.LeaderParty != party);
            int? callCount = null;
            string[] indexTokens = afterCall;

            if (afterCall.Length > 0 && int.TryParse(afterCall[afterCall.Length - 1], out int callN) && callN > 0 && callN <= 200)
            {
                if (afterCall.Length > 1)
                {
                    callCount = callN;
                    indexTokens = afterCall.Take(afterCall.Length - 1).ToArray();
                }
                else if (!kingWithoutOwnArmy)
                {
                    callCount = callN;
                    indexTokens = Array.Empty<string>();
                }
            }
            var indexStr = string.Join(" ", indexTokens);

            if (callType != "nearby" && callType != "all")
            { onFailure("Specify: army call nearby [army_index] | army call all [army_index]"); return; }

            Army targetArmy = null;

            if (h.Clan.Kingdom == null)
            {
                targetArmy = army;
            }
            else if (army != null && army.LeaderParty == party)
            {
                targetArmy = army;
            }
            else if (isKing)
            {
                var kArmies = h.Clan.Kingdom.Armies.ToList();
                if (kArmies.Count == 0) { onFailure("Your kingdom has no active armies. Create one first."); return; }
                if (!string.IsNullOrWhiteSpace(indexStr) && int.TryParse(indexStr, out int idx)
                    && idx >= 1 && idx <= kArmies.Count)
                    targetArmy = kArmies[idx - 1];
                else if (kArmies.Count == 1)
                    targetArmy = kArmies[0];
                else
                { onFailure($"Specify army index (1-{kArmies.Count}) or lead an army yourself."); return; }
            }
            else
            { onFailure("You must be leading an army or be king to call parties"); return; }

            var armyLdrParty = targetArmy.LeaderParty;
            if (armyLdrParty == null) { onFailure("Target army has no leader party"); return; }

            List<MobileParty> eligible;

            if (h.Clan.Kingdom != null)
            {
                eligible = h.Clan.Kingdom.AllParties
                    .Where(p => p != armyLdrParty && p.Army == null && p.AttachedTo == null
                        && p.MapEvent == null && !p.IsDisbanding && p.IsLordParty
                        && p.LeaderHero != null && !p.LeaderHero.IsPrisoner
                        && p.LeaderHero != Hero.MainHero && p.MemberRoster.TotalHealthyCount > 0)
                    .ToList();
            }
            else
            {
                eligible = h.Clan.WarPartyComponents
                    .Select(wpc => wpc?.MobileParty)
                    .Where(p => p != null && p != armyLdrParty && p.Army == null && p.AttachedTo == null
                        && p.MapEvent == null && !p.IsDisbanding && p.IsLordParty
                        && p.LeaderHero != null && !p.LeaderHero.IsPrisoner
                        && p.MemberRoster.TotalHealthyCount > 0)
                    .ToList();

                if (BLTClanDiplomacyBehavior.Current != null)
                {
                    foreach (var allied in BLTClanDiplomacyBehavior.Current.GetAlliedClans(h.Clan))
                    {
                        eligible.AddRange(allied.WarPartyComponents
                            .Select(wpc => wpc?.MobileParty)
                            .Where(p => p != null && p != armyLdrParty && p.Army == null
                                && p.AttachedTo == null && p.MapEvent == null && !p.IsDisbanding
                                && p.IsLordParty && p.LeaderHero != null
                                && !p.LeaderHero.IsPrisoner && p.MemberRoster.TotalHealthyCount > 0));
                    }
                }
            }

            if (callType == "nearby")
            {
                var ldrPos2 = armyLdrParty.GetPosition2D;
                eligible = eligible.OrderBy(p => p.GetPosition2D.Distance(ldrPos2)).ToList();
                if (callCount.HasValue)
                    eligible = eligible.Take(callCount.Value).ToList();
            }

            if (eligible.Count == 0)
            { onFailure($"No free parties found ({callType}){(callType == "nearby" ? $" within radius {settings.CallNearbyRadius}" : "")}"); return; }

            if (h.Clan.Kingdom != null)
            {
                float totalCost = settings.CallBaseInfluenceCost + eligible.Count * (float)settings.CallInfluenceCostPerParty;
                if (h.Clan.Influence < totalCost)
                { onFailure($"Not enough influence: need {totalCost:F0}, have {h.Clan.Influence:F0}"); return; }
            }

            float influenceBefore = h.Clan.Influence;
            int added = 0;
            foreach (var p in eligible)
            {
                try
                {
                    // FIX: release any BLT order lock before assigning the party to the army
                    PartyOrderBehavior.Current?.CancelOrdersForParty(p.StringId, null, false);
                    try { p.Ai.SetDoNotMakeNewDecisions(false); }
                    catch (Exception aiEx) { Log.Error($"[BLT] ArmyCall: AI unlock failed for {p.Name}: {aiEx}"); }
                    p.Army = targetArmy;
                    added++;
                }
                catch (Exception ex) { Log.Error($"[BLT] ArmyCall: failed to add {p.Name}: {ex}"); }
            }

            if (added == 0) { onFailure("Failed to add any parties to the army"); return; }

            if (h.Clan.Kingdom != null)
            {
                h.Clan.Influence = influenceBefore;
                float actualCost = settings.CallBaseInfluenceCost + added * (float)settings.CallInfluenceCostPerParty;
                h.Clan.Influence -= actualCost;
                onSuccess($"Called {added} parties to {targetArmy.Name} ({callType}) | Influence cost: {actualCost:F0}");
            }
            else
            {
                onSuccess($"Called {added} parties to {targetArmy.Name} ({callType})");
            }

            Log.ShowInformation($"{h.Name} called {added} parties to {targetArmy.Name}!", h.CharacterObject, Log.Sound.Horns2);
        }

        // ── JOIN ──────────────────────────────────────────────────────────────

        private void ArmyJoin(Settings settings, Hero h, MobileParty party, Army army,
            string tgtArg, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.JoinEnabled) { onFailure("Army join is disabled"); return; }
            if (!h.IsPartyLeader || party == null) { onFailure("You must be leading a party to join an army"); return; }
            if (party.MapEvent != null) { onFailure("Your party is in combat"); return; }
            if (army != null) { onFailure("You are already in an army — use 'army leave' first"); return; }

            // ── Independent clan joining a clan army ───────────────────────────────
            if (h.Clan.Kingdom == null)
            {
                if (string.IsNullOrWhiteSpace(tgtArg))
                { onFailure("Specify an allied army index. Use 'army view' to list joinable armies."); return; }

                var clanDiplomacy = BLTClanDiplomacyBehavior.Current;
                if (clanDiplomacy == null) { onFailure("Clan diplomacy system not available"); return; }

                var joinable = new List<Army>();
                foreach (var allied in clanDiplomacy.GetAlliedClans(h.Clan))
                {
                    foreach (var a in BLTClanArmyBehavior.Current?.GetClanArmies(allied) ?? new List<Army>())
                        if (a?.LeaderParty != null)
                            joinable.Add(a);
                }

                if (joinable.Count == 0)
                { onFailure("No allied clan armies are available to join. Use 'army view' to check."); return; }

                Army targetArmy = null;
                if (int.TryParse(tgtArg, out int idx) && idx >= 1 && idx <= joinable.Count)
                {
                    targetArmy = joinable[idx - 1];
                }
                else
                {
                    targetArmy = joinable.FirstOrDefault(a =>
                        a.LeaderParty?.ActualClan?.Name.ToString()
                            .IndexOf(tgtArg, StringComparison.OrdinalIgnoreCase) >= 0
                        || a.LeaderParty?.LeaderHero?.Name.ToString()
                            .IndexOf(tgtArg, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                if (targetArmy == null)
                { onFailure($"No allied clan army found matching '{tgtArg}'. Use 'army view' to see indices."); return; }

                if (targetArmy.LeaderParty?.MapEvent != null)
                { onFailure($"{targetArmy.Name} is currently in combat"); return; }

                var toJoin = new List<MobileParty> { party };
                toJoin.AddRange(h.Clan.WarPartyComponents
                    .Select(wpc => wpc?.MobileParty)
                    .Where(mp => mp != null && mp != party && mp.Army == null && mp.AttachedTo == null
                        && mp.MapEvent == null && !mp.IsDisbanding && mp.IsLordParty
                        && mp.LeaderHero != null && mp.MemberRoster.TotalHealthyCount > 0));

                int added = 0;
                foreach (var mp in toJoin)
                {
                    try
                    {
                        // FIX: release any BLT order lock before assigning to the army
                        PartyOrderBehavior.Current?.CancelOrdersForParty(mp.StringId, null, false);
                        try { mp.Ai.SetDoNotMakeNewDecisions(false); }
                        catch (Exception aiEx) { Log.Error($"[BLT] ArmyJoin (clan): AI unlock failed for {mp.Name}: {aiEx}"); }
                        mp.Army = targetArmy;
                        added++;
                    }
                    catch (Exception ex) { Log.Error($"[BLT] ArmyJoin (clan): failed to add {mp.Name}: {ex}"); }
                }

                if (added == 0) { onFailure("Failed to join the army"); return; }

                onSuccess($"Joined {targetArmy.Name} with {added} parties (no influence cost — clan alliance)");
                Log.ShowInformation($"{h.Name} joined {targetArmy.Name}!", h.CharacterObject, Log.Sound.Horns2);
                return;
            }

            // ── Kingdom army join ──────────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(tgtArg))
            { onFailure("Specify an army index. Use 'army view' to list available armies."); return; }

            var kArmies = h.Clan.Kingdom.Armies.ToList();
            if (kArmies.Count == 0) { onFailure("Your kingdom has no active armies to join"); return; }

            if (!int.TryParse(tgtArg, out int kIdx) || kIdx < 1 || kIdx > kArmies.Count)
            { onFailure($"Invalid army index '{tgtArg}'. Kingdom has {kArmies.Count} armies (use 'army view')."); return; }

            var targetKArmy = kArmies[kIdx - 1];
            var ldrParty = targetKArmy.LeaderParty;
            if (ldrParty == null) { onFailure("That army has no leader party"); return; }
            if (ldrParty == party) { onFailure("You are already leading that army"); return; }
            if (ldrParty.MapEvent != null) { onFailure($"{targetKArmy.Name} is currently in combat"); return; }

            var toJoinK = new List<MobileParty> { party };
            toJoinK.AddRange(h.Clan.WarPartyComponents
                .Select(wpc => wpc?.MobileParty)
                .Where(mp => mp != null && mp != party && mp.LeaderHero != null && mp.IsLordParty
                    && mp.Army == null && mp.AttachedTo == null && mp.MapEvent == null
                    && !mp.IsDisbanding && mp.LeaderHero != Hero.MainHero
                    && mp.MemberRoster.TotalHealthyCount > 0));

            bool isMercenary = h.Clan.IsUnderMercenaryService;
            float influenceCost = 0f;
            if (!isMercenary)
            {
                influenceCost = settings.JoinBaseInfluenceCost
                              + toJoinK.Count * (float)settings.JoinInfluenceCostPerParty;
                if (h.Clan.Influence < influenceCost)
                { onFailure($"Not enough influence: need {influenceCost:F0}, have {h.Clan.Influence:F0}"); return; }
            }

            int addedK = 0;
            foreach (var mp in toJoinK)
            {
                try
                {
                    // FIX: release any BLT order lock before assigning to the army
                    PartyOrderBehavior.Current?.CancelOrdersForParty(mp.StringId, null, false);
                    try { mp.Ai.SetDoNotMakeNewDecisions(false); }
                    catch (Exception aiEx) { Log.Error($"[BLT] ArmyJoin: AI unlock failed for {mp.Name}: {aiEx}"); }
                    mp.Army = targetKArmy;
                    addedK++;
                }
                catch (Exception ex) { Log.Error($"[BLT] ArmyJoin: failed to add {mp.Name}: {ex}"); }
            }

            if (addedK == 0) { onFailure("Failed to join the army"); return; }
            if (!isMercenary) h.Clan.Influence -= influenceCost;

            string costStr = isMercenary ? "free (mercenary)" : $"{influenceCost:F0} influence";
            onSuccess($"Joined {targetKArmy.Name} with {addedK} parties | Cost: {costStr}");
            Log.ShowInformation($"{h.Name} joined {targetKArmy.Name} with {addedK} parties!", h.CharacterObject, Log.Sound.Horns2);
        }

        // ── ALLOW AI ARMIES ───────────────────────────────────────────────────

        private static void ArmyAllowAI(Settings settings, Hero h, string arg,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.KingAIArmyToggleEnabled) { onFailure("AI army toggle is disabled in config"); return; }
            if (h.Clan.Kingdom?.Leader != h) { onFailure("You must be king to toggle AI army creation"); return; }
            if (PartyOrderBehavior.Current == null) { onFailure("Order system not initialized"); return; }

            if (string.IsNullOrWhiteSpace(arg))
            {
                bool allowed = !PartyOrderBehavior.Current.IsAIArmiesBlocked(h.Clan.Kingdom);
                onSuccess($"{h.Clan.Kingdom.Name} AI armies: {(allowed ? "allowed" : "blocked")} — use 'army allowai on/off' to change");
                return;
            }
            if (arg.Equals("on", StringComparison.OrdinalIgnoreCase))
            { PartyOrderBehavior.Current.SetAIArmiesBlocked(h.Clan.Kingdom, false); onSuccess($"AI army creation in {h.Clan.Kingdom.Name}: allowed"); }
            else if (arg.Equals("off", StringComparison.OrdinalIgnoreCase))
            { PartyOrderBehavior.Current.SetAIArmiesBlocked(h.Clan.Kingdom, true); onSuccess($"AI army creation in {h.Clan.Kingdom.Name}: blocked"); }
            else onFailure("Usage: army allowai [on|off]");
        }

        // ── ALLOW BLT ARMIES ──────────────────────────────────────────────────

        private static void ArmyAllowBLT(Settings settings, Hero h, string arg,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.KingBLTArmyToggleEnabled) { onFailure("BLT army toggle is disabled in config"); return; }
            if (h.Clan.Kingdom?.Leader != h) { onFailure("You must be king to toggle BLT army creation"); return; }
            if (PartyOrderBehavior.Current == null) { onFailure("Order system not initialized"); return; }

            if (string.IsNullOrWhiteSpace(arg))
            {
                bool allowed = !PartyOrderBehavior.Current.IsBLTArmiesBlocked(h.Clan.Kingdom);
                onSuccess($"{h.Clan.Kingdom.Name} BLT armies: {(allowed ? "allowed" : "blocked")} — use 'army allowblt on/off' to change");
                return;
            }
            if (arg.Equals("on", StringComparison.OrdinalIgnoreCase))
            { PartyOrderBehavior.Current.SetBLTArmiesBlocked(h.Clan.Kingdom, false); onSuccess($"BLT army creation in {h.Clan.Kingdom.Name}: allowed"); }
            else if (arg.Equals("off", StringComparison.OrdinalIgnoreCase))
            { PartyOrderBehavior.Current.SetBLTArmiesBlocked(h.Clan.Kingdom, true); onSuccess($"BLT army creation in {h.Clan.Kingdom.Name}: blocked"); }
            else onFailure("Usage: army allowblt [on|off]");
        }

        // ── THREAT ────────────────────────────────────────────────────────────

        private static void ArmyThreat(Settings settings, Hero h, MobileParty party,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.ThreatEnabled) { onFailure("Threat scan is disabled"); return; }
            if (party == null) { onFailure("You have no party"); return; }

            float radius = settings.ThreatScanRadius;
            float ourStr = party.GetTotalLandStrengthWithFollowers();
            var ourPos = party.GetPosition2D;

            var threats = new List<(string name, float eStr, float atkScore, float avoidScore, bool flee)>();
            foreach (MobileParty other in MobileParty.All.Where(m => m.GetPosition2D.Distance(ourPos) <= radius && m.MapFaction.IsAtWarWith(party.MapFaction)))
            {
                bool original = true;
                if (other == party || !other.IsActive || other.IsMainParty) continue;
                if (other.MapEvent != null) continue;
                if (other.Army != null)
                {
                    if (threats.Any(t => t.name == other.Name.ToString() || t.name == other.Army.Name.ToString())) continue;
                    else original = false;
                }
                else
                {
                    if (threats.Any(t => t.name == other.Name.ToString())) continue;
                }
                float eStr = other.GetTotalLandStrengthWithFollowers();
                if (eStr <= 0f) continue;
                float adv = ourStr / eStr;
                float atk = MBMath.ClampFloat(0.5f * (1f + adv), 0.05f, 3f);
                float avd = adv < 1f ? MBMath.ClampFloat(1f / adv, 0.05f, 3f) : 0f;
                threats.Add(((original ? other.Name?.ToString() : other.Army.Name.ToString()) ?? "Unknown", eStr, atk, avd, flee: avd > atk));
            }

            if (threats.Count == 0) { onSuccess("No hostile forces detected nearby"); return; }

            var top = threats
                .OrderByDescending(t => t.flee ? t.avoidScore : 0f)
                .ThenByDescending(t => !t.flee ? t.atkScore : 0f)
                .Take(settings.ThreatMaxResults)
                .Select(t => $"[{(t.flee ? "⚠ DANGER" : "→ ENGAGE")}] {t.name} (Str:{t.eStr:0} vs {ourStr:0})");

            onSuccess(string.Join(" | ", top));
        }

        // ── ORDER (siege / defend / patrol) ───────────────────────────────────

        private void ArmyOrder(Settings settings, Hero h, MobileParty party, Army army,
            string subCmd, string tgtArg, Action<string> onSuccess, Action<string> onFailure)
        {
            if (h.Clan.IsUnderMercenaryService) { onFailure("Mercenaries can't create armies"); return; }

            var armyType = subCmd == "siege" ? Army.ArmyTypes.Besieger
                          : subCmd == "defend" ? Army.ArmyTypes.Defender
                          : Army.ArmyTypes.Patrolling;

            PartyOrderType orderType = subCmd == "siege"
                ? PartyOrderType.Siege
                : PartyOrderType.SmartGuard;

            var (settlementArg, createCount) = ParseTrailingCount(tgtArg);

            Settlement target = null;
            if (!string.IsNullOrWhiteSpace(settlementArg))
            {
                target = subCmd == "siege"
                    ? FindSettlementByName(settlementArg, PartyOrderType.Siege, h)
                    : FindSettlementByNameLoose(settlementArg);

                if (target == null) { onFailure($"Settlement '{settlementArg}' not found or invalid for {subCmd}"); return; }
                if (orderType == PartyOrderType.SmartGuard && !target.IsFortification)
                    orderType = PartyOrderType.Patrol;
            }
            else
            {
                target = subCmd == "siege"
                    ? FindBestSettlementToTarget(party, h.Clan.MapFaction, true)
                    : FindBestSettlementToDefend(party, h.Clan.MapFaction);
            }

            // ── Siege-specific validation ──────────────────────────────────────────
            if (subCmd == "siege")
            {
                if (h.Clan.Kingdom != null && h.Clan.Kingdom.FactionsAtWarWith.Count == 0)
                { onFailure("No active wars"); return; }
                if (target == null) { onFailure("No valid enemy settlement found to besiege"); return; }
                if (!target.IsFortification) { onFailure($"{target.Name} is not a fortification"); return; }
                if (!h.MapFaction.IsAtWarWith(target.OwnerClan?.Kingdom ?? target.OwnerClan?.MapFaction))
                { onFailure($"Not at war with {target.Name}'s owner"); return; }

                if (target.IsUnderSiege)
                {
                    var besiegerFaction = target.SiegeEvent?.BesiegerCamp?.LeaderParty?.MapFaction;
                    if (besiegerFaction != null && besiegerFaction != h.MapFaction)
                    {
                        var besiegerK = besiegerFaction as Kingdom;
                        var heroK = h.Clan.Kingdom;
                        bool allied = besiegerK != null && heroK != null
                            && BLTTreatyManager.Current?.GetAlliance(heroK, besiegerK) != null
                            && besiegerK.IsAtWarWith(target.MapFaction);
                        if (!allied)
                        { onFailure($"{target.Name} is already under siege by a non-allied faction"); return; }
                    }
                }

                if (!PartyOrderBehavior.IsSettlementReachable(party, target))
                {
                    var fallback = FindBestSettlementToDefend(party, h.Clan.MapFaction);
                    PartyOrderBehavior.IssueOrder(party, PartyOrderType.SmartGuard, fallback);
                    party.Ai.SetDoNotMakeNewDecisions(true);
                    PartyOrderBehavior.Current?.RegisterOrder(h, party, PartyOrderType.SmartGuard, fallback,
                        settings.ArmyMaxReissueAttempts, settings.ArmyOrderExpiryDays);
                    onFailure($"{target.Name} is not reachable by land — army set to patrol instead");
                    return;
                }
            }

            if (!h.IsPartyLeader) { onFailure("You are not leading a party"); return; }
            if (party.MapEvent != null) { onFailure("Your party is in combat"); return; }

            // ── Redirect existing army ─────────────────────────────────────────────
            if (army != null && army.LeaderParty == party)
            {
                army.ArmyType = armyType;
                if (target != null) army.AiBehaviorObject = target;
                PartyOrderBehavior.IssueOrder(party, orderType, target);
                party.Ai.SetDoNotMakeNewDecisions(true);
                PartyOrderBehavior.Current?.RegisterOrder(h, party, orderType, target,
                    settings.ArmyMaxReissueAttempts, settings.ArmyOrderExpiryDays);
                onSuccess($"Army redirected: {subCmd}" + (target != null ? $" → {target.Name}" : ""));
                return;
            }
            if (army != null && army.LeaderParty != party)
            { onFailure("You are in someone else's army"); return; }

            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(h) < settings.ArmyPrice)
            { onFailure(Naming.NotEnoughGold(settings.ArmyPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(h))); return; }

            var nav = party.IsCurrentlyAtSea ? MobileParty.NavigationType.Naval : MobileParty.NavigationType.Default;
            var near = SettlementHelper.FindNearestSettlementToMobileParty(party, nav) ?? h.HomeSettlement;
            var gather = target ?? near;

            Army newArmy;

            // ── Kingdom army creation ──────────────────────────────────────────────
            if (h.Clan.Kingdom != null)
            {
                var vassals = VassalBehavior.Current.GetVassalClans(h.Clan);
                MBList<MobileParty> merged;
                if (settings.AutoCallPartiesOnCreate)
                {
                    var vassalParties = h.Clan.Kingdom.AllParties
                        .Where(p => (p.ActualClan == h.Clan || vassals.Contains(p.ActualClan))
                            && p != party && p.Army == null && p.AttachedTo == null
                            && p.LeaderHero != null && p.MapEvent == null && !p.IsDisbanding)
                        .ToList();
                    var modelParties = Campaign.Current.Models.ArmyManagementCalculationModel
                        .GetMobilePartiesToCallToArmy(party)
                        .Where(p => p != null);
                    var ldrPos = party.GetPosition2D;
                    var sorted = vassalParties.Concat(modelParties).Distinct()
                        .OrderBy(p => p.GetPosition2D.Distance(ldrPos));
                    merged = (createCount.HasValue ? sorted.Take(createCount.Value) : sorted).ToMBList();
                }
                else
                {
                    merged = new MBList<MobileParty>();
                }

                // FIX: release any BLT order locks on parties being absorbed into the new army
                foreach (var mp in merged)
                {
                    if (mp?.StringId == null) continue;
                    PartyOrderBehavior.Current?.CancelOrdersForParty(mp.StringId, null, false);
                    try { mp.Ai.SetDoNotMakeNewDecisions(false); }
                    catch (Exception ex) { Log.Error($"[BLT] ArmyOrder (kingdom): AI unlock failed for {mp.Name}: {ex}"); }
                }

                h.Clan.Influence += 200f;
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(h, -settings.ArmyPrice, true);
                AdoptedHeroFlags._allowBLTArmyCreation = true;
                try { h.Clan.Kingdom.CreateArmy(h, gather, armyType, merged); }
                finally { AdoptedHeroFlags._allowBLTArmyCreation = false; }
                newArmy = party.Army;

                if (newArmy == null)
                {
                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(h, settings.ArmyPrice, false);
                    onFailure("Army creation failed");
                    return;
                }
                int mCount = newArmy.Parties.Count - 1;
                onSuccess($"Gathering {armyType} army ({mCount} joining)" + (target != null ? $" → {target.Name}" : ""));
            }
            // ── Independent clan army creation ────────────────────────────────────
            else
            {
                MBList<MobileParty> merged;
                if (settings.AutoCallPartiesOnCreate)
                {
                    var ownCandidates = h.Clan.WarPartyComponents
                        .Select(wpc => wpc?.MobileParty)
                        .Where(mp => mp != null && mp != party && mp.Army == null && mp.AttachedTo == null
                            && mp.LeaderHero != null && mp.MapEvent == null && !mp.IsDisbanding
                            && mp.IsLordParty && mp.MemberRoster.TotalHealthyCount > 0)
                        .ToList<MobileParty>();
                    var allyCandidates = new List<MobileParty>();
                    if (BLTClanDiplomacyBehavior.Current != null)
                        foreach (var allied in BLTClanDiplomacyBehavior.Current.GetAlliedClans(h.Clan))
                            allyCandidates.AddRange(allied.WarPartyComponents
                                .Select(wpc => wpc?.MobileParty)
                                .Where(mp => mp != null && mp.Army == null && mp.AttachedTo == null
                                    && mp.LeaderHero != null && mp.MapEvent == null && !mp.IsDisbanding
                                    && mp.IsLordParty && mp.MemberRoster.TotalHealthyCount > 0));
                    var ldrPos = party.GetPosition2D;
                    var sorted = ownCandidates.Concat(allyCandidates).Distinct()
                        .OrderBy(p => p.GetPosition2D.Distance(ldrPos));
                    merged = (createCount.HasValue ? sorted.Take(createCount.Value) : sorted).ToMBList();
                }
                else
                {
                    merged = new MBList<MobileParty>();
                }

                // FIX: release any BLT order locks on parties being absorbed into the new army
                foreach (var mp in merged)
                {
                    if (mp?.StringId == null) continue;
                    PartyOrderBehavior.Current?.CancelOrdersForParty(mp.StringId, null, false);
                    try { mp.Ai.SetDoNotMakeNewDecisions(false); }
                    catch (Exception ex) { Log.Error($"[BLT] ArmyOrder (clan): AI unlock failed for {mp.Name}: {ex}"); }
                }

                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(h, -settings.ArmyPrice, true);
                newArmy = BLTClanArmyBehavior.CreateClanArmy(h, gather, armyType, merged);

                if (newArmy == null)
                {
                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(h, settings.ArmyPrice, false);
                    onFailure("Clan army creation failed");
                    return;
                }
                int mCount = newArmy.Parties.Count - 1;
                onSuccess($"Gathering clan {armyType} army ({mCount} joining)" + (target != null ? $" → {target.Name}" : ""));
            }

            // ── Shared post-creation setup ─────────────────────────────────────────
            if (target != null) newArmy.AiBehaviorObject = target;
            PartyOrderBehavior.IssueOrder(party, orderType, target);
            party.Ai.SetDoNotMakeNewDecisions(true);
            PartyOrderBehavior.Current?.RegisterOrder(h, party, orderType, target,
                settings.ArmyMaxReissueAttempts, settings.ArmyOrderExpiryDays);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  HELPER METHODS
        // ─────────────────────────────────────────────────────────────────────

        private Settlement FindBestSettlementToTarget(MobileParty party, IFaction faction, bool forSiege)
        {
            Settlement best = null;
            float bestScore = 0f;

            var kingdom = faction as Kingdom;
            var clan = faction as Clan;
            int stance = 0;
            if (kingdom != null)
            {               
                foreach (var enemy in kingdom.FactionsAtWarWith)
                {
                    stance = faction.GetStanceWith(enemy).BehaviorPriority;
                    if (stance == 1 || enemy.Settlements == null) continue;
                    
                    foreach (var s in enemy.Settlements)
                    {
                        if (!s.IsFortification) continue;
                        if (s.IsUnderSiege && s.SiegeEvent?.BesiegerCamp?.LeaderParty?.MapFaction != faction)
                        {

                            var besiegerFaction = s.SiegeEvent?.BesiegerCamp?.LeaderParty?.MapFaction as Kingdom;
                            bool allied = besiegerFaction != null
                                && BLTTreatyManager.Current?.GetAlliance(kingdom, besiegerFaction) != null
                                && besiegerFaction.IsAtWarWith(s.MapFaction);
                            if (!allied) continue;
                            
                        }

                        float dist = Campaign.Current.Models.MapDistanceModel.GetDistance(
                            party, s, false, MobileParty.NavigationType.Default, out _);
                        if (dist >= float.MaxValue - 1f) continue;
                        if (!PartyOrderBehavior.IsSettlementReachable(party, s)) continue;

                        float str = s.Town?.GarrisonParty?.Party.EstimatedStrength + s.Town?.Militia ?? 0f;
                        var neighbours = Campaign.Current.Models.MapDistanceModel
                            .GetNeighborsOfFortification(s.Town, MobileParty.NavigationType.Default);
                        bool direct = neighbours.Any(n => faction.Settlements.Contains(n));

                        float prox = 10000f / (dist + 1f);
                        float penalty = Math.Min(str * 0.05f, prox * 0.5f);
                        float score = (prox - penalty) * Math.Max(1, stance) * (direct ? 1.1f : 1f);

                        if (score > bestScore) { bestScore = score; best = s; }
                    }
                }
            }
            else
            {
                foreach (var enemy in clan.FactionsAtWarWith)
                {
                    foreach (var s in enemy.Settlements)
                    {
                        if (!s.IsFortification) continue;
                        if (s.IsUnderSiege && s.SiegeEvent?.BesiegerCamp?.LeaderParty?.MapFaction != faction)
                        {                          
                            var besiegerC = s.SiegeEvent?.BesiegerCamp?.LeaderParty?.MapFaction as Clan;
                            bool allied = besiegerC != null && BLTClanDiplomacyBehavior.Current?.HasAlliance(clan, besiegerC) == true && besiegerC.IsAtWarWith(s.MapFaction);
                            if (!allied) continue;
                        }

                        float dist = Campaign.Current.Models.MapDistanceModel.GetDistance(
                            party, s, false, MobileParty.NavigationType.Default, out _);
                        if (dist >= float.MaxValue - 1f) continue;
                        if (!PartyOrderBehavior.IsSettlementReachable(party, s)) continue;

                        float str = s.Town?.GarrisonParty?.Party.EstimatedStrength + s.Town?.Militia ?? 0f;
                        var neighbours = Campaign.Current.Models.MapDistanceModel
                            .GetNeighborsOfFortification(s.Town, MobileParty.NavigationType.Default);
                        bool direct = neighbours.Any(n => faction.Settlements.Contains(n));

                        float prox = 10000f / (dist + 1f);
                        float penalty = Math.Min(str * 0.05f, prox * 0.5f);
                        float score = (prox - penalty) * Math.Max(1, stance) * (direct ? 1.1f : 1f);

                        if (score > bestScore) { bestScore = score; best = s; }
                    }
                }
            }         
            return best;
        }

        public Settlement FindBestSettlementToDefend(MobileParty party, IFaction kingdom)
        {
            if (kingdom == null) return null;
            Settlement best = null;
            float bestScore = 0f;

            foreach (var s in kingdom.Settlements)
            {
                if (!s.IsFortification) continue;
                bool threat = s.IsUnderSiege || (s.LastAttackerParty != null && s.LastAttackerParty.IsActive);
                float dist = Campaign.Current.Models.MapDistanceModel.GetDistance(party, s, false, party.NavigationCapability, out _);
                float score = (1000f / (dist + 1f)) * (threat ? 10f : 1f);
                if (score > bestScore) { bestScore = score; best = s; }
            }
            return best ?? kingdom.Settlements.FirstOrDefault(s => s.IsFortification);
        }

        private Settlement FindSettlementByName(string name, PartyOrderType orderType, Hero hero)
        {
            var match = Settlement.All.FirstOrDefault(s =>
                s?.Name?.ToString().Equals(name, StringComparison.OrdinalIgnoreCase) == true);

            if (match == null)
                match = Settlement.All
                    .Where(s => s?.Name?.ToString().IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(s => s.Name.ToString().Length)
                    .ThenBy(s => s.Name.ToString())
                    .FirstOrDefault();

            if (match == null) return null;

            switch (orderType)
            {
                case PartyOrderType.Siege:
                    if (!match.IsFortification) return null;
                    var tf = match.OwnerClan?.Kingdom ?? match.OwnerClan?.MapFaction;
                    if (tf == null || tf == hero.Clan.Kingdom) return null;
                    if (!hero.Clan.MapFaction.IsAtWarWith(tf)) return null;
                    break;
                case PartyOrderType.Garrison:
                    if (!match.IsFortification) return null;
                    break;
                case PartyOrderType.Defend:
                    if (!match.IsFortification) return null;
                    break;
            }
            return match;
        }

        /// <summary>Loose name lookup — no order-type filtering. Used for defend/patrol/garrison auto-targeting.</summary>
        private static Settlement FindSettlementByNameLoose(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return Settlement.All.FirstOrDefault(s =>
                       s?.Name?.ToString().Equals(name, StringComparison.OrdinalIgnoreCase) == true)
                ?? Settlement.All
                    .Where(s => s?.Name?.ToString().IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(s => s.Name.ToString().Length)
                    .ThenBy(s => s.Name.ToString())
                    .FirstOrDefault();
        }

        private static void SafeRemovePartyFromArmy(MobileParty mp)
        {
            try
            {
                if (mp?.Army == null) return;
                if (mp.Army.LeaderParty == mp)
                {
                    PartyOrderBehavior.Current?.CancelOrdersForParty(mp.StringId, null, false);
                    DisbandArmyAction.ApplyByUnknownReason(mp.Army);
                }
                else { mp.Army = null; mp.AttachedTo = null; }
            }
            catch (Exception ex) { Log.Error($"[BLT] SafeRemovePartyFromArmy error: {ex}"); }
        }

        private static void FallbackLeaderToSettlement(Hero leader, Hero requester)
        {
            if (leader == null || leader == requester) return;
            if (leader.PartyBelongedTo != null || leader.CurrentSettlement != null) return;
            var fallback = leader.HomeSettlement ?? Settlement.All.Where(s => s.IsTown).SelectRandom();
            if (fallback != null) EnterSettlementAction.ApplyForCharacterOnly(leader, fallback);
        }
    }
}