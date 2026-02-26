using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using HarmonyLib;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero;
using BLTAdoptAHero.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
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

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("Party Management"),
     LocDescription("Allow viewer to manage their party"),
     UsedImplicitly]
    public class PartyManagement : HeroCommandHandlerBase
    {
        [CategoryOrder("Army", 0),
         CategoryOrder("Threat", 1)]
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
             LocDescription("{=ArmyMaxReissueDesc}How many times the system silently re-issues a drifted army order before releasing it. 0 = never re-issue."),
             PropertyOrder(3), UsedImplicitly]
            public int ArmyMaxReissueAttempts { get; set; } = 5;

            [LocDisplayName("{=ArmyOrderExpiry}Order Expiry (Hours)"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("{=ArmyOrderExpiryDesc}In-game hours before an army order auto-expires. 0 = no expiry."),
             PropertyOrder(4), UsedImplicitly]
            public int ArmyOrderExpiryHours { get; set; } = 0;

            // ── King army management ─────────────────────────────────────────
            [LocDisplayName("King Army Management"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Allow kings to view, create (NPC-led), and disband any kingdom army by index."),
             PropertyOrder(5), UsedImplicitly]
            public bool KingArmyManageEnabled { get; set; } = true;

            [LocDisplayName("Create Army Gold Cost"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Gold cost for a king to commission an NPC-led army."),
             PropertyOrder(6), UsedImplicitly]
            public int CreateArmyPrice { get; set; } = 100000;

            [LocDisplayName("King Can Toggle AI Armies"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Allow a kingdom's king to block or restore AI/NPC army creation for their kingdom via '!party army allowai on/off'. Defaults to on (allowed)."),
             PropertyOrder(7), UsedImplicitly]
            public bool KingAIArmyToggleEnabled { get; set; } = true;

            [LocDisplayName("King Can Toggle BLT Armies"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Allow a kingdom's king to block or restore BLT army creation for their kingdom via '!party army allowblt on/off'. Defaults to on (allowed)."),
             PropertyOrder(8), UsedImplicitly]
            public bool KingBLTArmyToggleEnabled { get; set; } = true;

            // ── Takeover ─────────────────────────────────────────────────────
            [LocDisplayName("Takeover Enabled"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Allow a clan leader to seize command of an army already led by one of their own clan members."),
             PropertyOrder(9), UsedImplicitly]
            public bool TakeoverEnabled { get; set; } = true;

            // ── Call ─────────────────────────────────────────────────────────
            [LocDisplayName("Call Enabled"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Allow army leaders or the king to call free lord parties to join an army."),
             PropertyOrder(10), UsedImplicitly]
            public bool CallEnabled { get; set; } = true;

            [LocDisplayName("Call Base Influence Cost"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Flat influence cost paid when any call order is issued, regardless of how many parties respond."),
             PropertyOrder(11), UsedImplicitly]
            public int CallBaseInfluenceCost { get; set; } = 0;

            [LocDisplayName("Call Per-Party Influence Cost"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Additional influence cost charged for each party that actually joins the army."),
             PropertyOrder(12), UsedImplicitly]
            public int CallInfluenceCostPerParty { get; set; } = 25;

            [LocDisplayName("Call Nearby Radius"),
             LocCategory("Army", "{=ArmyCat}Army"),
             LocDescription("Map-unit radius used when scanning for parties with 'army call nearby'."),
             PropertyOrder(13), UsedImplicitly]
            public float CallNearbyRadius { get; set; } = 30f;

            // ── Threat ───────────────────────────────────────────────────────
            [LocDisplayName("{=ThreatEnabled}Threat Scan"),
             LocCategory("Threat", "{=ThreatCat}Threat"),
             LocDescription("{=ThreatEnabledDesc}Enable !party threat scan subcommand"),
             PropertyOrder(1), UsedImplicitly]
            public bool ThreatEnabled { get; set; } = true;

            [LocDisplayName("{=ThreatMaxResults}Threat Max Results"),
             LocCategory("Threat", "{=ThreatCat}Threat"),
             LocDescription("{=ThreatMaxResultsDesc}Maximum number of threats listed in the output, sorted by danger"),
             PropertyOrder(2), UsedImplicitly]
            public int ThreatMaxResults { get; set; } = 3;

            [LocDisplayName("{=ThreatRadius}Threat Scan Radius"),
             LocCategory("Threat", "{=ThreatCat}Threat"),
             LocDescription("{=ThreatRadiusDesc}Map-unit radius to scan for nearby hostile parties."),
             PropertyOrder(3), UsedImplicitly]
            public float ThreatScanRadius { get; set; } = 12f;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.Value("<strong>Commands:</strong>");
                generator.Value("!party — current party/army status");
                generator.Value("!party create — spawn a new party");
                generator.Value("!party govern [fief] — become governor of a clan fief");
                generator.Value("!party stats — detailed party stats");
                generator.Value("!party disband [index|all] — disband own party/parties");
                generator.Value("");
                generator.Value("<strong>Army subcommands:</strong> !party army [subcommand]");
                generator.Value("  siege [settlement] — besiege a named enemy settlement (or auto-pick)");
                generator.Value("  defend [settlement] — defend a named friendly settlement (or auto-pick)");
                generator.Value("  patrol [settlement] — patrol around a settlement or current position");
                generator.Value("  status — army strength, behavior, cohesion, food, active order info");
                generator.Value("  disband [index] — disband your army; king: disband any by index");
                generator.Value("  leave — leave someone else's army");
                generator.Value("  reassign [hero] — transfer army leadership to a hero in your army");
                generator.Value("  view — (king) list all kingdom armies with index numbers");
                generator.Value("  create [hero_name] — (king) commission an NPC-led army");
                generator.Value("  takeover [hero|index] — (clan leader) seize command of a clan member's army");
                generator.Value("  call nearby [army_index] — call free parties near the army to join");
                generator.Value("  call all [army_index] — call all free kingdom parties to join the army");

                if (ArmyEnabled)
                {
                    generator.Value("");
                    generator.Value("<strong>Army config:</strong>");
                    generator.Value($"  Creation cost: {ArmyPrice}{Naming.Gold}");
                    generator.Value($"  Max re-issue attempts: {ArmyMaxReissueAttempts}");
                    generator.Value(ArmyOrderExpiryHours > 0
                        ? $"  Order expiry: {ArmyOrderExpiryHours}h"
                        : "  Order expiry: none");
                    if (KingArmyManageEnabled)
                        generator.Value($"  King management: create cost {CreateArmyPrice}{Naming.Gold}");
                    if (KingAIArmyToggleEnabled)
                        generator.Value("  Kings can toggle per-kingdom AI army creation (army allowai on/off)");
                    if (TakeoverEnabled)
                        generator.Value("  Clan-leader takeover: enabled");
                    if (CallEnabled)
                        generator.Value($"  Call: base {CallBaseInfluenceCost} influence + {CallInfluenceCostPerParty}/party | nearby radius {CallNearbyRadius}");
                }
                else
                {
                    generator.Value("<strong>Army command: disabled</strong>");
                }
            }
        }

        public override Type HandlerConfigType => typeof(Settings);

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

            var partyStats = new StringBuilder();

            if (string.IsNullOrEmpty(mode))
            {
                BuildStatusString(adoptedHero, party, army, behaviorText, armyBehavior, partyStats);
                onSuccess(partyStats.ToString());
            }

            switch (mode)
            {
                case "govern": HandleGovern(adoptedHero, party, army, desiredName, onSuccess, onFailure); break;
                case "create": HandleCreate(adoptedHero, party, onSuccess, onFailure); break;
                case "stats": HandleStats(adoptedHero, party, onSuccess, onFailure); break;
                case "disband": HandlePartyDisband(adoptedHero, party, desiredName, onSuccess, onFailure); break;
                case "army": HandleArmy(settings, adoptedHero, party, army, desiredName, onSuccess, onFailure); break;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  STATUS STRING (no-arg output)
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
            {
                sb.Append($"Governor: {adoptedHero.GovernorOf.Name}");
            }
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
                    if (army.LeaderParty?.TargetParty != null || army.LeaderParty?.ShortTermTargetParty != null)
                    {
                        var tgt = army.LeaderParty.ShortTermTargetParty ?? army.LeaderParty.TargetParty;
                        sb.Append("{=9aFoBcPY}Target: {target} - ".Translate(("target", tgt.Name.ToString())));
                        sb.Append("{=D3dcUxuj}Size: {size} | ".Translate(("size", tgt?.MemberRoster?.TotalManCount ?? 0)));
                    }
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
        //  GOVERN
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

        // ─────────────────────────────────────────────────────────────────────
        //  CREATE PARTY
        // ─────────────────────────────────────────────────────────────────────

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

            // Seed nearby food/horses
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

        // ─────────────────────────────────────────────────────────────────────
        //  STATS
        // ─────────────────────────────────────────────────────────────────────

        private void HandleStats(Hero h, MobileParty party, Action<string> onSuccess, Action<string> onFailure)
        {
            if (party == null) { onFailure("You have no party"); return; }

            var comp = PartyBaseHelper.PrintRegularTroopCategories(party.MemberRoster) ?? new TextObject("Unknown");
            var roster = party.MemberRoster.GetTroopRoster();
            double tier = roster.Sum(r => r.Character.Tier * r.Number) / (double)Math.Max(1, roster.Sum(r => r.Number));
            var nav = party.IsCurrentlyAtSea ? MobileParty.NavigationType.Naval : MobileParty.NavigationType.Default;
            var near = SettlementHelper.FindNearestSettlementToMobileParty(party, nav);

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

        // ─────────────────────────────────────────────────────────────────────
        //  DISBAND PARTY (top-level "!party disband")
        // ─────────────────────────────────────────────────────────────────────

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
                else
                {
                    target.Army = null;
                    target.AttachedTo = null;
                }
            }

            string name = target.Name.ToString();
            var ldr = target.LeaderHero;
            DestroyPartyAction.Apply(null, target);
            FallbackLeaderToSettlement(ldr, h);
            onSuccess($"Disbanded {name}");
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
                onFailure("Specify: siege / defend / patrol / status / disband / leave / reassign / view / create / takeover / call / allowai");
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
                case "allowai": ArmyAllowAI(settings, h, tgtArg, onSuccess, onFailure); break;
                case "allowblt": ArmyAllowBLT(settings, h, tgtArg, onSuccess, onFailure); break;
                case "threat": ArmyThreat(settings, h, party, onSuccess, onFailure); break;
                case "siege":
                case "defend":
                case "patrol": ArmyOrder(settings, h, party, army, sub, tgtArg, onSuccess, onFailure); break;
                default:
                    onFailure("Specify: siege / defend / patrol / status / disband / leave / reassign / view / create / takeover / call / threat");
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
                sb.Append($" | Order locked ({order.ReissueAttempts}/{order.MaxReissueAttempts} re-issues)");
            onSuccess(sb.ToString());
        }

        // ── DISBAND (army-level, supports king index) ─────────────────────────

        private static void ArmyDisband(Settings settings, Hero h, MobileParty party, Army army,
            string tgtArg, Action<string> onSuccess, Action<string> onFailure)
        {
            bool isKing = settings.KingArmyManageEnabled
                       && h.Clan.Kingdom?.Leader == h;

            // King path — can disband any army by index
            if (isKing)
            {
                var kArmies = h.Clan.Kingdom.Armies.ToList();
                Army targetArmy = null;

                if (string.IsNullOrWhiteSpace(tgtArg))
                {
                    if (army != null && army.LeaderParty == party)
                        targetArmy = army;           // own army
                    else if (kArmies.Count == 1)
                        targetArmy = kArmies[0];
                    else if (kArmies.Count == 0)
                    { onFailure("No active armies to disband"); return; }
                    else
                    { onFailure($"Specify army index (1-{kArmies.Count}). Use 'army view' to list them."); return; }
                }
                else if (int.TryParse(tgtArg, out int idx) && idx >= 1 && idx <= kArmies.Count)
                    targetArmy = kArmies[idx - 1];
                else
                { onFailure($"Invalid index '{tgtArg}'. Kingdom has {kArmies.Count} armies."); return; }

                if (targetArmy.LeaderParty?.MapEvent != null)
                { onFailure($"{targetArmy.Name} is in combat"); return; }

                string aName = targetArmy.Name.ToString();
                PartyOrderBehavior.Current?.CancelOrdersForParty(targetArmy.LeaderParty?.StringId, null, false);
                DisbandArmyAction.ApplyByUnknownReason(targetArmy);
                onSuccess($"Disbanded {aName}");
                return;
            }

            // Non-king: own army only
            if (army == null || army.LeaderParty != party)
            { onFailure("You are not leading an army"); return; }
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

            float influenceBefore = h.Clan.Influence;                          // ← snapshot

            var gather = curTarget ?? newLeaderParty.CurrentSettlement ?? h.HomeSettlement;
            h.Clan.Kingdom.CreateArmy(newLeaderParty.LeaderHero, gather, armyType, remaining);

            h.Clan.Influence = influenceBefore;                                 // ← restore

            if (newLeaderParty.Army == null) { onFailure("Failed to transfer army leadership"); return; }

            if (curTarget != null)
            {
                PartyOrderBehavior.IssueOrder(newLeaderParty, curType, curTarget);
                newLeaderParty.Ai.SetDoNotMakeNewDecisions(true);
                PartyOrderBehavior.Current?.RegisterOrder(h, newLeaderParty, curType, curTarget,
                    settings.ArmyMaxReissueAttempts, settings.ArmyOrderExpiryHours);
            }
            onSuccess($"Army command transferred to {newLeaderParty.LeaderHero.Name}");
        }


        // ── VIEW (king: list all kingdom armies) ──────────────────────────────

        private static void ArmyView(Settings settings, Hero h,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.KingArmyManageEnabled) { onFailure("King army management is disabled"); return; }
            if (h.Clan.Kingdom == null) { onFailure("You are not in a kingdom"); return; }

            // Non-kings can still view for intel, but note who can act
            var armies = h.Clan.Kingdom.Armies.ToList();
            if (armies.Count == 0) { onSuccess($"{h.Clan.Kingdom.Name} has no active armies"); return; }

            var sb = new StringBuilder();
            sb.Append($"{h.Clan.Kingdom.Name} | {armies.Count} Armies: ");
            for (int i = 0; i < armies.Count; i++)
            {
                var a = armies[i];
                var ldr = a.LeaderParty?.LeaderHero;
                string behavior = a.LeaderParty?.GetBehaviorText()?.ToString() ?? "—";
                string target = a.LeaderParty?.TargetSettlement?.Name?.ToString()
                               ?? a.LeaderParty?.TargetParty?.Name?.ToString() ?? "—";
                string orderTag = PartyOrderBehavior.Current?.HasActiveOrder(a.LeaderParty?.StringId ?? "") == true ? "[order]" : "";
                sb.Append($"[{i + 1}] {a.Name} (Leader:{ldr?.Name.ToString() ?? "?"}, Clan:{a.LeaderParty?.ActualClan?.Name.ToString() ?? "?"}, Str:{(int)a.EstimatedStrength}, Parties:{a.LeaderPartyAndAttachedPartiesCount}, {behavior}→{target}{orderTag}) | ");
            }
            onSuccess(sb.ToString().TrimEnd(' ', '|'));
        }

        // ── CREATE (king: commission NPC-led army) ────────────────────────────

        private void ArmyCreate(Settings settings, Hero h, MobileParty party, Army army,
            string tgtArg, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.KingArmyManageEnabled) { onFailure("King army management is disabled"); return; }
            if (h.Clan.Kingdom?.Leader != h) { onFailure("You must be king to commission an NPC-led army"); return; }
            if (h.Clan.IsUnderMercenaryService) { onFailure("Mercenaries can't create armies"); return; }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(h) < settings.CreateArmyPrice)
            { onFailure(Naming.NotEnoughGold(settings.CreateArmyPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(h))); return; }

            // Build candidate list: non-adopted, non-player, free lord parties in the kingdom
            var candidates = h.Clan.Kingdom.AllParties
                .Where(p => p.LeaderHero != null
                    && !p.LeaderHero.IsAdopted()
                    && p.LeaderHero != Hero.MainHero
                    && p.Army == null
                    && p.AttachedTo == null
                    && p.MapEvent == null
                    && !p.IsDisbanding
                    && p.IsLordParty
                    && p.MemberRoster.TotalHealthyCount > 0)
                .ToList();

            if (candidates.Count == 0) { onFailure("No eligible NPC lords available to lead an army"); return; }

            MobileParty leaderParty;
            if (!string.IsNullOrWhiteSpace(tgtArg))
            {
                leaderParty = candidates.FirstOrDefault(p =>
                    p.LeaderHero.Name.ToString().IndexOf(tgtArg, StringComparison.OrdinalIgnoreCase) >= 0);
                if (leaderParty == null) { onFailure($"No eligible NPC lord matching '{tgtArg}' found"); return; }
            }
            else
            {
                leaderParty = candidates.GetRandomElement();
            }

            // Gather potential members from model + vassals
            var vassalClans = VassalBehavior.Current?.GetVassalClans(h.Clan) ?? new List<Clan>();
            var modelParties = Campaign.Current.Models.ArmyManagementCalculationModel
                .GetMobilePartiesToCallToArmy(leaderParty);
            var members = candidates
                .Where(p => p != leaderParty)
                .Concat(modelParties.Where(p => p != leaderParty && p != null))
                .Where(p => p.Army == null && p.AttachedTo == null && p.MapEvent == null && !p.IsDisbanding)
                .Distinct()
                .ToMBList();

            var gather = leaderParty.CurrentSettlement
                      ?? SettlementHelper.FindNearestSettlementToMobileParty(leaderParty, leaderParty.NavigationCapability)
                      ?? h.Clan.Kingdom.Settlements.FirstOrDefault(s => s.IsFortification);
            if (gather == null) { onFailure("Could not determine a gather point"); return; }

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

        // ── TAKEOVER (clan leader seizes clan member's army) ──────────────────

        private void ArmyTakeover(Settings settings, Hero h, MobileParty party, Army army,
            string tgtArg, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.TakeoverEnabled) { onFailure("Army takeover is disabled"); return; }
            if (!h.IsPartyLeader || party == null) { onFailure("You must be leading a party to take over an army"); return; }
            if (party.MapEvent != null) { onFailure("Your party is in combat"); return; }
            if (army != null && army.LeaderParty == party) { onFailure("You are already leading an army — use reassign instead"); return; }
            if (h.Clan.Kingdom == null) { onFailure("You are not in a kingdom"); return; }

            // Find armies in the kingdom led by a member of the hero's own clan (not the hero themselves)
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
                    var sb = new StringBuilder("Multiple clan armies — specify index or leader name: ");
                    for (int i = 0; i < clanArmies.Count; i++)
                        sb.Append($"[{i + 1}] {clanArmies[i].LeaderParty?.LeaderHero?.Name} | ");
                    onFailure(sb.ToString().TrimEnd(' ', '|'));
                    return;
                }
            }
            else
            {
                // Try integer index first (against the full kingdom list for consistency with 'view')
                var kArmies = h.Clan.Kingdom.Armies.ToList();
                if (int.TryParse(tgtArg, out int idx) && idx >= 1 && idx <= kArmies.Count)
                {
                    var candidate = kArmies[idx - 1];
                    if (candidate.LeaderParty?.ActualClan != h.Clan)
                    { onFailure($"Army [{idx}] is not led by a member of your clan"); return; }
                    if (candidate.LeaderParty?.LeaderHero == h)
                    { onFailure("That is your own army"); return; }
                    targetArmy = candidate;
                }
                else
                {
                    // Try name match within clan armies
                    targetArmy = clanArmies.FirstOrDefault(a =>
                        a.LeaderParty?.LeaderHero?.Name.ToString()
                            .IndexOf(tgtArg, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (targetArmy == null) { onFailure($"No clan army found matching '{tgtArg}'"); return; }
                }
            }

            if (targetArmy.LeaderParty?.MapEvent != null) { onFailure("That army is currently in combat"); return; }

            var oldLeader = targetArmy.LeaderParty;
            var curOrder = PartyOrderBehavior.Current?.GetActiveOrder(oldLeader.StringId);
            var curTarget = curOrder?.TargetSettlementId != null ? Settlement.Find(curOrder.TargetSettlementId) : null;
            var curType = curOrder?.Type ?? PartyOrderType.Patrol;
            var armyType = targetArmy.ArmyType;

            // All current members except the old leader (adoptedHero's party will become leader)
            var remaining = targetArmy.Parties
                .Where(p => p != oldLeader && p != party)
                .ToMBList();
            // Include old leader as a member unless they have no party
            if (oldLeader != null && oldLeader != party && oldLeader.LeaderHero != null)
                remaining.Add(oldLeader);

            PartyOrderBehavior.Current?.CancelOrdersForParty(oldLeader.StringId, null, false);
            DisbandArmyAction.ApplyByUnknownReason(targetArmy);

            float influenceBefore = h.Clan.Influence;                          // ← snapshot

            var gather = curTarget ?? oldLeader.CurrentSettlement ?? h.HomeSettlement;
            h.Clan.Kingdom.CreateArmy(h, gather, armyType, remaining);

            h.Clan.Influence = influenceBefore;                                 // ← restore

            if (party.Army == null) { onFailure("Failed to seize army leadership"); return; }

            if (curTarget != null)
            {
                PartyOrderBehavior.IssueOrder(party, curType, curTarget);
                party.Ai.SetDoNotMakeNewDecisions(true);
                PartyOrderBehavior.Current?.RegisterOrder(h, party, curType, curTarget,
                    settings.ArmyMaxReissueAttempts, settings.ArmyOrderExpiryHours);
            }

            onSuccess($"Took over {oldLeader.LeaderHero?.Name}'s army — {remaining.Count} parties gathering");
            Log.ShowInformation($"{h.Name} seized command of {oldLeader.LeaderHero?.Name}'s army!",
                h.CharacterObject, Log.Sound.Horns2);
        }

        // ── CALL (recruit free lord parties into an army) ─────────────────────
        // Usage: army call nearby [army_index]
        //        army call all    [army_index]

        private void ArmyCall(Settings settings, Hero h, MobileParty party, Army army,
            string tgtArg, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.CallEnabled) { onFailure("Call is disabled"); return; }
            if (h.Clan.Kingdom == null) { onFailure("You are not in a kingdom"); return; }
            if (h.Clan.IsUnderMercenaryService) { onFailure("Mercenaries cannot call armies"); return; }

            bool isKing = h.Clan.Kingdom.Leader == h;

            // Parse "nearby" | "all"  +  optional army index
            var callParts = tgtArg.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            var callType = callParts.Length > 0 ? callParts[0].ToLower() : "";
            var indexStr = callParts.Length > 1 ? callParts[1].Trim() : "";

            if (callType != "nearby" && callType != "all")
            {
                onFailure("Specify: army call nearby [army_index] | army call all [army_index]");
                return;
            }

            // ── Resolve target army ───────────────────────────────────────────
            Army targetArmy = null;

            if (army != null && army.LeaderParty == party)
            {
                targetArmy = army; // caller is leading an army
            }
            else if (isKing)
            {
                var kArmies = h.Clan.Kingdom.Armies.ToList();
                if (kArmies.Count == 0)
                { onFailure("Your kingdom has no active armies to call to. Create one first."); return; }

                if (!string.IsNullOrWhiteSpace(indexStr) && int.TryParse(indexStr, out int idx)
                    && idx >= 1 && idx <= kArmies.Count)
                {
                    targetArmy = kArmies[idx - 1];
                }
                else if (kArmies.Count == 1)
                {
                    targetArmy = kArmies[0];
                }
                else
                {
                    onFailure($"Specify army index (1-{kArmies.Count}) or lead an army yourself. Use 'army view' to list them.");
                    return;
                }
            }
            else
            {
                onFailure("You must be leading an army or be king to call parties");
                return;
            }

            var armyLdrParty = targetArmy.LeaderParty;
            if (armyLdrParty == null) { onFailure("Target army has no leader party"); return; }

            // ── Find eligible lord parties ────────────────────────────────────
            var eligible = h.Clan.Kingdom.AllParties
                .Where(p => p != armyLdrParty
                    && p.Army == null
                    && p.AttachedTo == null
                    && p.MapEvent == null
                    && !p.IsDisbanding
                    && p.IsLordParty
                    && p.LeaderHero != null
                    && !p.LeaderHero.IsPrisoner
                    && p.LeaderHero != Hero.MainHero
                    && p.MemberRoster.TotalHealthyCount > 0)
                .ToList();

            if (callType == "nearby")
            {
                var ldrPos = armyLdrParty.GetPosition2D;
                eligible = eligible
                    .Where(p => p.GetPosition2D.Distance(ldrPos) <= settings.CallNearbyRadius)
                    .ToList();
            }

            if (eligible.Count == 0)
            {
                onFailure($"No free lord parties found ({callType}){(callType == "nearby" ? $" within radius {settings.CallNearbyRadius}" : "")}");
                return;
            }

            // ── Check influence ───────────────────────────────────────────────
            float totalCost = settings.CallBaseInfluenceCost + eligible.Count * (float)settings.CallInfluenceCostPerParty;
            if (h.Clan.Influence < totalCost)
            {
                onFailure($"Not enough influence: need {totalCost:F0} (base {settings.CallBaseInfluenceCost} + {eligible.Count}×{settings.CallInfluenceCostPerParty}), have {h.Clan.Influence:F0}");
                return;
            }

            // ── Add parties to army ───────────────────────────────────────────
            float influenceBefore = h.Clan.Influence;

            int added = 0;
            foreach (var p in eligible)
            {
                try
                {
                    p.Army = targetArmy;
                    added++;
                }
                catch (Exception ex)
                {
                    Log.Error($"[BLT] ArmyCall: failed to add {p.Name}: {ex}");
                }
            }

            if (added == 0) { onFailure("Failed to add any parties to the army"); return; }

            h.Clan.Influence = influenceBefore;

            float actualCost = settings.CallBaseInfluenceCost + added * (float)settings.CallInfluenceCostPerParty;
            h.Clan.Influence -= actualCost;

            onSuccess($"Called {added} parties to {targetArmy.Name} ({callType}) | Influence cost: {actualCost:F0}");
            Log.ShowInformation($"{h.Name} called {added} parties to {targetArmy.Name}!", h.CharacterObject, Log.Sound.Horns2);
        }

        // ── ALLOW AI ARMIES (king per-kingdom toggle) ─────────────────────────

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
            {
                PartyOrderBehavior.Current.SetAIArmiesBlocked(h.Clan.Kingdom, false);
                onSuccess($"AI/NPC army creation in {h.Clan.Kingdom.Name}: allowed");
            }
            else if (arg.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                PartyOrderBehavior.Current.SetAIArmiesBlocked(h.Clan.Kingdom, true);
                onSuccess($"AI/NPC army creation in {h.Clan.Kingdom.Name}: blocked");
            }
            else
            {
                onFailure("Usage: army allowai [on|off]");
            }
        }

        // ── ALLOW BLT ARMIES (king per-kingdom toggle) ─────────────────────────

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
            {
                PartyOrderBehavior.Current.SetBLTArmiesBlocked(h.Clan.Kingdom, false);
                onSuccess($"BLT army creation in {h.Clan.Kingdom.Name}: allowed");
            }
            else if (arg.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                PartyOrderBehavior.Current.SetBLTArmiesBlocked(h.Clan.Kingdom, true);
                onSuccess($"BLT army creation in {h.Clan.Kingdom.Name}: blocked");
            }
            else
            {
                onFailure("Usage: army allowblt [on|off]");
            }
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
            foreach (var other in MobileParty.All)
            {
                if (other == party || !other.IsActive || other.IsMainParty) continue;
                if (other.MapEvent != null) continue;
                if (!other.MapFaction.IsAtWarWith(party.MapFaction)) continue;
                if (other.GetPosition2D.Distance(ourPos) > radius) continue;
                float eStr = other.GetTotalLandStrengthWithFollowers();
                if (eStr <= 0f) continue;
                float adv = ourStr / eStr;
                float atk = MBMath.ClampFloat(0.5f * (1f + adv), 0.05f, 3f);
                float avd = adv < 1f ? MBMath.ClampFloat(1f / adv, 0.05f, 3f) : 0f;
                threats.Add((other.Name?.ToString() ?? "Unknown", eStr, atk, avd, flee: avd > atk));
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
            var orderType = subCmd == "siege" ? PartyOrderType.Siege
                          : subCmd == "defend" ? PartyOrderType.Defend
                          : PartyOrderType.Patrol;

            // Resolve target settlement
            Settlement target = null;
            if (!string.IsNullOrWhiteSpace(tgtArg))
            {
                target = FindSettlementByName(tgtArg, orderType, h);
                if (target == null) { onFailure($"Settlement '{tgtArg}' not found or invalid for {subCmd}"); return; }
            }
            else
            {
                target = orderType == PartyOrderType.Siege
                    ? FindBestSettlementToTarget(party, h.Clan.Kingdom, true)
                    : orderType == PartyOrderType.Defend
                        ? FindBestSettlementToDefend(party, h.Clan.Kingdom)
                        : null;
            }

            // Siege-specific validation
            if (orderType == PartyOrderType.Siege)
            {
                if (h.Clan.Kingdom.FactionsAtWarWith.Count == 0) { onFailure("No active wars"); return; }
                if (target == null) { onFailure("No valid enemy settlement found to besiege"); return; }
                if (!target.IsFortification) { onFailure($"{target.Name} is not a fortification"); return; }
                if (target.IsUnderSiege && target.SiegeEvent?.BesiegerCamp?.LeaderParty?.MapFaction != h.Clan.Kingdom)
                { onFailure($"{target.Name} is under siege by another faction"); return; }
                if (!h.Clan.Kingdom.IsAtWarWith(target.OwnerClan?.Kingdom ?? target.OwnerClan?.MapFaction))
                { onFailure($"Not at war with {target.Name}'s owner"); return; }

                if (!PartyOrderBehavior.IsSettlementReachable(party, target))
                {
                    var fallback = FindBestSettlementToDefend(party, h.Clan.Kingdom);
                    PartyOrderBehavior.IssueOrder(party, PartyOrderType.Patrol, fallback);
                    party.Ai.SetDoNotMakeNewDecisions(true);
                    PartyOrderBehavior.Current?.RegisterOrder(h, party, PartyOrderType.Patrol, fallback,
                        settings.ArmyMaxReissueAttempts, settings.ArmyOrderExpiryHours);
                    onFailure($"{target.Name} is not reachable by land — army set to patrol instead");
                    return;
                }
            }

            if (!h.IsPartyLeader) { onFailure("You are not leading a party"); return; }
            if (party.MapEvent != null) { onFailure("Your party is in combat"); return; }

            // Redirect existing army
            if (army != null && army.LeaderParty == party)
            {
                army.ArmyType = armyType;
                if (target != null) army.AiBehaviorObject = target;
                PartyOrderBehavior.IssueOrder(party, orderType, target);
                party.Ai.SetDoNotMakeNewDecisions(true);
                PartyOrderBehavior.Current?.RegisterOrder(h, party, orderType, target,
                    settings.ArmyMaxReissueAttempts, settings.ArmyOrderExpiryHours);
                onSuccess($"Army redirected: {subCmd}" + (target != null ? $" → {target.Name}" : " (current position)"));
                return;
            }
            if (army != null && army.LeaderParty != party) { onFailure("You are in someone else's army"); return; }

            // Create new army
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(h) < settings.ArmyPrice)
            { onFailure(Naming.NotEnoughGold(settings.ArmyPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(h))); return; }

            var nav = party.IsCurrentlyAtSea ? MobileParty.NavigationType.Naval : MobileParty.NavigationType.Default;
            var near = SettlementHelper.FindNearestSettlementToMobileParty(party, nav) ?? h.HomeSettlement;
            var gather = target ?? near;

            var vassals = VassalBehavior.Current.GetVassalClans(h.Clan);
            var vassalParties = h.Clan.Kingdom.AllParties
                .Where(p => (p.ActualClan == h.Clan || vassals.Contains(p.ActualClan))
                    && p != party && p.Army == null && p.AttachedTo == null
                    && p.LeaderHero != null && p.MapEvent == null && !p.IsDisbanding)
                .ToMBList();
            var modelParties = Campaign.Current.Models.ArmyManagementCalculationModel.GetMobilePartiesToCallToArmy(party);
            var merged = vassalParties.Concat(modelParties).Where(p => p != null).Distinct().ToMBList();

            h.Clan.Influence += 200f;
            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(h, -settings.ArmyPrice, true);
            h.Clan.Kingdom.CreateArmy(h, gather, armyType, merged);
            var newArmy = party.Army;
            if (newArmy == null)
            {
                onFailure("Army creation failed");
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(h, settings.ArmyPrice, false);
                return;
            }
            if (target != null) newArmy.AiBehaviorObject = target;
            PartyOrderBehavior.IssueOrder(party, orderType, target);
            party.Ai.SetDoNotMakeNewDecisions(true);
            PartyOrderBehavior.Current?.RegisterOrder(h, party, orderType, target,
                settings.ArmyMaxReissueAttempts, settings.ArmyOrderExpiryHours);

            int mCount = newArmy.Parties.Count - 1;
            onSuccess($"Gathering {armyType} army ({mCount} joining)" + (target != null ? $" → {target.Name}" : ""));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  HELPER METHODS
        // ─────────────────────────────────────────────────────────────────────

        private Settlement FindBestSettlementToTarget(MobileParty party, Kingdom kingdom, bool forSiege)
        {
            Settlement best = null;
            float bestScore = 0f;

            foreach (var enemy in kingdom.FactionsAtWarWith)
            {
                int stance = kingdom.GetStanceWith(enemy).BehaviorPriority;
                if (stance == 1 || enemy.Settlements == null) continue;

                foreach (var s in enemy.Settlements)
                {
                    if (!s.IsFortification) continue;
                    if (s.IsUnderSiege && s.SiegeEvent?.BesiegerCamp?.LeaderParty?.MapFaction != kingdom) continue;

                    float dist = Campaign.Current.Models.MapDistanceModel.GetDistance(
                        party, s, false, MobileParty.NavigationType.Default, out _);
                    if (dist >= float.MaxValue - 1f) continue;
                    if (!PartyOrderBehavior.IsSettlementReachable(party, s)) continue;

                    float str = s.Town?.GarrisonParty?.Party.EstimatedStrength + s.Town?.Militia ?? 0f;
                    var neighbours = Campaign.Current.Models.MapDistanceModel
                        .GetNeighborsOfFortification(s.Town, MobileParty.NavigationType.Default);
                    bool direct = neighbours.Any(n => kingdom.Settlements.Contains(n));

                    float prox = 10000f / (dist + 1f);
                    float penalty = Math.Min(str * 0.05f, prox * 0.5f);
                    float score = (prox - penalty) * Math.Max(1, stance) * (direct ? 1.1f : 1f);

                    if (score > bestScore) { bestScore = score; best = s; }
                }
            }
            return best;
        }

        private Settlement FindBestSettlementToDefend(MobileParty party, Kingdom kingdom)
        {
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
            {
                match = Settlement.All
                    .Where(s => s?.Name?.ToString().IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(s => s.Name.ToString().Length)   // shortest = tightest match
                    .ThenBy(s => s.Name.ToString())           // stable tie-break
                    .FirstOrDefault();
            }

            if (match == null) return null;

            // Validate for the specific order type
            switch (orderType)
            {
                case PartyOrderType.Siege:
                    if (!match.IsFortification) return null;
                    var tf = match.OwnerClan?.Kingdom ?? match.OwnerClan?.MapFaction;
                    if (tf == null || tf == hero.Clan.Kingdom) return null;
                    if (!hero.Clan.Kingdom.IsAtWarWith(tf)) return null;
                    break;
                case PartyOrderType.Defend:
                    if (!match.IsFortification) return null;
                    break;
            }
            return match;
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
                else
                {
                    mp.Army = null;
                    mp.AttachedTo = null;
                }
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