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
        [CategoryOrder("Army", 0)]
        private class Settings : IDocumentable
        {
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
             LocDescription("{=ThreatRadiusDesc}Map-unit radius to scan for nearby hostile parties. Default 12 covers roughly the same area the engine uses for encounter detection."),
             PropertyOrder(3), UsedImplicitly]
            public float ThreatScanRadius { get; set; } = 12f;
            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.Value("<strong>Commands:</strong>");
                generator.Value("!party — current party/army status");
                generator.Value("!party create — spawn a new party");
                generator.Value("!party govern [fief] — become governor of a clan fief");
                generator.Value("!party stats — detailed party stats");
                generator.Value("");
                generator.Value("<strong>Army subcommands:</strong> !party army [subcommand]");
                generator.Value("  siege [settlement] — besiege a named enemy settlement (or auto-pick best)");
                generator.Value("  defend [settlement] — defend a named friendly settlement (or auto-pick)");
                generator.Value("  patrol [settlement] — patrol around a settlement or current position");
                generator.Value("  status — army strength, behavior, cohesion, food, active order info");
                generator.Value("  disband — disband your army");
                generator.Value("  leave — leave someone else's army");
                generator.Value("  reassign [hero] — transfer army leadership to a hero in your army");

                if (ArmyEnabled)
                {
                    generator.Value("");
                    generator.Value("<strong>Army config:</strong>");
                    generator.Value($"  Creation cost: {ArmyPrice}{Naming.Gold}");
                    generator.Value($"  Max re-issue attempts: {ArmyMaxReissueAttempts}");
                    generator.Value(ArmyOrderExpiryHours > 0
                        ? $"  Order expiry: {ArmyOrderExpiryHours}h"
                        : "  Order expiry: none");
                }
                else
                {
                    generator.Value("<strong>Army command: disabled</strong>");
                }
            }
        }
        public override Type HandlerConfigType => typeof(Settings);
        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) return;
            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }
            if (Mission.Current != null)
            {
                onFailure("{=MPTOZqMS}You cannot manage your party, as a mission is active!".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=B86KnTcu}You are not in a clan".Translate());
                return;
            }
            var splitArgs = context.Args.Split(' ');
            var mode = splitArgs[0];
            var desiredName = string.Join(" ", splitArgs.Skip(1)).Trim();

            MobileParty party = adoptedHero.PartyBelongedTo;
            if (party == null)
            {
                var warPartyComponent = adoptedHero.Clan.WarPartyComponents.FirstOrDefault(pc => pc?.Leader == adoptedHero);
                party = warPartyComponent?.MobileParty;
            }
            Army army = party?.Army;

            string behaviorText = party?.GetBehaviorText()?.ToString() ?? "";
            string armyBehavior = army?.LeaderParty?.GetBehaviorText()?.ToString() ?? "";

            var partyStats = new StringBuilder();

            if (string.IsNullOrEmpty(mode))
            {

                if (adoptedHero.HeroState == Hero.CharacterStates.Released)
                    partyStats.Append("{=r1nJTiSA}Your hero has just been released".Translate());
                else if (adoptedHero.HeroState == Hero.CharacterStates.Traveling)
                    partyStats.Append("{=TESTING}Your hero is travelling".Translate());
                else if (adoptedHero.HeroState == Hero.CharacterStates.Fugitive)
                    partyStats.Append("{=TESTING}Your hero is fugitive".Translate());
                else if (adoptedHero.IsPrisoner && adoptedHero.PartyBelongedToAsPrisoner?.IsMobile == true)
                {
                    int prisontime = (int)adoptedHero.CaptivityStartTime.ElapsedDaysUntilNow;
                    partyStats.Append($"Prisoner({prisontime}): {adoptedHero.PartyBelongedToAsPrisoner.Name}");
                    partyStats.Append(" | ");
                    var place = adoptedHero.PartyBelongedToAsPrisoner?.LeaderHero?.LastKnownClosestSettlement?.Name?.ToString() ?? "Unknown";
                    partyStats.Append($"Last seen near {place}");
                }
                else if (adoptedHero.IsPrisoner && adoptedHero.PartyBelongedToAsPrisoner?.IsSettlement == true)
                {
                    int prisontime = (int)adoptedHero.CaptivityStartTime.ElapsedDaysUntilNow;
                    partyStats.Append("{=zVDODxiN}Prisoner({dur}): {prisoner}".Translate(("prisoner", adoptedHero.PartyBelongedToAsPrisoner.Settlement.Name.ToString()), ("dur", prisontime)));
                }
                    
                else if (adoptedHero.GovernorOf != null && adoptedHero.Clan.Fiefs.Count > 0)
                {
                    var govFief = adoptedHero.GovernorOf;
                    partyStats.Append($"Governor: { govFief.Name}");
                }
                else if (party != null && party?.LeaderHero == adoptedHero)
                {
                    partyStats.Append($"Party(Strength: {(int)party.Party.EstimatedStrength} - ");
                    string partySizeStr = $"{party.MemberRoster.TotalHealthyCount}({party.MemberRoster.TotalWounded})/{party.Party.PartySizeLimit}";
                    if (party.PrisonRoster.Count > 0)
                    {
                        partyStats.Append($"Size: {partySizeStr} - ");
                        partyStats.Append($"Prisoners: {party.PrisonRoster.Count}) | ");
                    }
                    else partyStats.Append($"Size: {partySizeStr}) | ");
                    if (party.IsCurrentlyAtSea)
                    {
                        partyStats.Append("Sailing | ");
                    }
                    if (!string.IsNullOrWhiteSpace(behaviorText) && behaviorText != armyBehavior)
                    {
                        partyStats.Append($"Your party is: {behaviorText} | ");
                    }
                    if (party.IsDisbanding)
                    {
                        partyStats.Append("Disbanding");
                    }
                    if (party != null && (party.TargetParty != null || party.ShortTermTargetParty != null))
                    {
                        var armyLeaderTarget = party.Army?.LeaderParty?.TargetParty;
                        var armyLeaderShortTarget = party.Army?.LeaderParty?.ShortTermTargetParty;

                        if (party.TargetParty != armyLeaderTarget || party.ShortTermTargetParty != armyLeaderShortTarget)
                        {
                            var target = party.ShortTermTargetParty ?? party.TargetParty;
                            partyStats.Append("{=9aFoBcPY}Target: {target} - ".Translate(("target", target?.Name?.ToString() ?? "Unknown")));
                            partyStats.Append("{=D3dcUxuj}Size: {size} | ".Translate(("size", target?.MemberRoster?.TotalManCount ?? 0)));
                        }
                    }
                    if (party?.Army != null)
                    {
                        partyStats.Append("{=CVzSgXhT}Army: {army}".Translate(("army", army?.Name?.ToString() ?? army?.LeaderParty?.Name?.ToString() ?? "Unknown army")));
                        partyStats.Append("{=d76wc5iS}[Strength: {strength} | ".Translate(("strength", Math.Round(army.EstimatedStrength).ToString())));
                        partyStats.Append("{=D3dcUxuj}Size: {size} | ".Translate(("size", army.TotalHealthyMembers.ToString())));
                        partyStats.Append("{=7p5j5Mlx}Party nº: {count}] ".Translate(("count", army.LeaderPartyAndAttachedPartiesCount.ToString())));

                        if (!string.IsNullOrWhiteSpace(armyBehavior))
                        {
                            partyStats.Append($"Your army is: {armyBehavior} | ");
                        }
                        if (army?.LeaderParty != null && (army.LeaderParty.TargetParty != null || army.LeaderParty.ShortTermTargetParty != null))
                        {
                            var target = army.LeaderParty.ShortTermTargetParty ?? army.LeaderParty.TargetParty;
                            partyStats.Append("{=9aFoBcPY}Target: {target} - ".Translate(("target", target.Name.ToString())));
                            partyStats.Append("{=D3dcUxuj}Size: {size} | ".Translate(("size", target?.MemberRoster?.TotalManCount ?? 0)));
                        }
                    }
                    if (party.MapEvent != null)
                    {
                        var mapEvent = adoptedHero.PartyBelongedTo.MapEvent;
                        var partySide = adoptedHero.PartyBelongedTo.MapEventSide;
                        var otherSide = partySide.OtherSide;

                        if (partySide != null && otherSide != null)
                        {
                            string battleSide = partySide == mapEvent.DefenderSide
                                ? "{=c3CZCj6p}(Defending)".Translate()
                                : "{=83Uwa9xi}(Attacking)".Translate();

                            string enemyName = otherSide.LeaderParty.Name.ToString();
                            int remainTroops = otherSide.TroopCount;
                            //int enemyTroops = otherSide.Parties.;

                            string enemy = $"{enemyName}:{remainTroops}";

                            if (mapEvent.IsFieldBattle)
                            {
                                partyStats.Append("{=QV6KWiVt}Field Battle {battleside} [{enemy}] | "
                                    .Translate(("battleside", battleSide), ("enemy", enemy)));
                            }
                            else if (mapEvent.IsRaid)
                            {
                                partyStats.Append("{=U3NJo32u}Raid {battleside} [{enemy}] | "
                                    .Translate(("battleside", battleSide), ("enemy", enemy)));
                            }
                            else if (mapEvent.IsSiegeAssault || mapEvent.IsSallyOut || mapEvent.IsSiegeOutside)
                            {
                                partyStats.Append("{=FbhijpQL}Siege {battleside} [{enemy}] | "
                                    .Translate(("battleside", battleSide), ("enemy", enemy)));
                            }
                        }
                    }
                }
                else if (party != null && !adoptedHero.IsPartyLeader)
                {
                    partyStats.Append($"Companion in {party.Name}'s party");
                }
                else if (party == null && !adoptedHero.IsPartyLeader) partyStats.Append("You have no party");
                else partyStats.Append("Unknown");
                onSuccess(partyStats.ToString());
            }
            switch (mode)
            {
                case "govern":
                    {
                        if (adoptedHero.Clan.Fiefs.Count == 0)
                        {
                            onFailure("You clan has no fiefs");
                            return;
                        }
                        var desiredTown = adoptedHero.Clan?.Fiefs.FirstOrDefault(c => c.Name.ToString().IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0);
                        var govFief = adoptedHero.GovernorOf;
                        if (string.IsNullOrWhiteSpace(desiredName))
                        {
                            onFailure("Specify fief");
                            return;
                        }
                        if (adoptedHero.HeroState == Hero.CharacterStates.Released)
                        {
                            onFailure("Your hero has just been released");
                            return;
                        }
                        if (adoptedHero.HeroState == Hero.CharacterStates.Traveling)
                        {
                            onFailure("Your hero is travelling");
                            return;
                        }
                        if (adoptedHero.HeroState == Hero.CharacterStates.Fugitive)
                        {
                            onFailure("Your hero is fugitive");
                            return;
                        }
                        if (adoptedHero.Clan.Leader.IsHumanPlayerCharacter)
                        {
                            onFailure("Cannot govern player towns");
                            return;
                        }
                        if (party != null && adoptedHero.PartyBelongedTo.MapEvent != null)
                        {
                            onFailure("Your hero is busy");
                            return;
                        }
                        if (adoptedHero.CurrentSettlement != null && (adoptedHero.CurrentSettlement.IsUnderSiege || adoptedHero.CurrentSettlement.IsUnderRaid))
                        {
                            onFailure("Your hero is busy");
                            return;
                        }
                        if (adoptedHero.IsPrisoner)
                        {
                            onFailure("You are prisoner");
                            return;
                        }
                        if (desiredTown == govFief)
                        {
                            onFailure($"Already governing {desiredTown.Name}");
                            return;
                        }
                        if (desiredTown == null)
                        {
                            onFailure($"Could not find a fief with the name {desiredName}");
                            return;
                        }
                        if (army != null)
                        {
                            onFailure("You are in an army!");
                            return;
                        }
                        if (party != null)
                        {
                            var oldParty = party;
                            bool wasLeader = oldParty.LeaderHero == adoptedHero;
                            oldParty.MemberRoster.RemoveTroop(adoptedHero.CharacterObject, 1, default(UniqueTroopDescriptor), 0);
                            MakeHeroFugitiveAction.Apply(adoptedHero, false);
                            if (wasLeader && oldParty.IsLordParty)
                                DisbandPartyAction.StartDisband(oldParty);
                        }
                        if (govFief != null)
                        {
                            ChangeGovernorAction.RemoveGovernorOf(adoptedHero);
                        }
                        TeleportHeroAction.ApplyImmediateTeleportToSettlement(adoptedHero, desiredTown.Settlement);
                        ChangeGovernorAction.Apply(desiredTown, adoptedHero);
                        onSuccess($"Governor of {desiredTown.Name}");                       
                        break;
                    }
                case "create":
                    {
                        if (adoptedHero.Clan.Leader.IsHumanPlayerCharacter)
                        {
                            onFailure("Cannot create party in player clan");
                            return;
                        }
                        if (adoptedHero.HeroState == Hero.CharacterStates.Released)
                        {
                            onFailure("Your hero has just been released");
                            return;
                        }
                        if (adoptedHero.HeroState == Hero.CharacterStates.Traveling)
                        {
                            onFailure("Your hero is travelling");
                            return;
                        }
                        if (adoptedHero.HeroState == Hero.CharacterStates.Fugitive)
                        {
                            onFailure("Your hero is fugitive");
                            return;
                        }
                        if (party != null)
                        {
                            onFailure("You already have a party");
                            return;
                        }
                        if (adoptedHero.IsPrisoner)
                        {
                            onFailure("You are prisoner");
                            return;
                        }                    
                        int parties = adoptedHero.Clan.WarPartyComponents.Count;
                        if (!adoptedHero.IsClanLeader && parties >= adoptedHero.Clan.CommanderLimit)
                        {
                            onFailure($"Clan party limit: {adoptedHero.Clan.CommanderLimit}");
                            return;
                        }
                        if (adoptedHero.GovernorOf != null)
                        {
                            var govFief = adoptedHero.GovernorOf;
                            ChangeGovernorAction.RemoveGovernorOfIfExists(govFief);
                        }
                        if (party == null)
                        {
                            Settlement spawnSettlement = SettlementHelper.GetBestSettlementToSpawnAround(adoptedHero) ?? adoptedHero.CurrentSettlement ?? adoptedHero.HomeSettlement;
                            MobileParty newParty = MobilePartyHelper.SpawnLordParty(adoptedHero, spawnSettlement.GatePosition, Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Default) / 2f);
                            var retinue = BLTAdoptAHeroCampaignBehavior.Current.GetRetinue(adoptedHero).ToList();
                            var retinue2 = BLTAdoptAHeroCampaignBehavior.Current.GetRetinue2(adoptedHero).ToList();
                            if (newParty != null)
                            {
                                if (newParty.LeaderHero != adoptedHero)
                                    newParty.ChangePartyLeader(adoptedHero);
                                if (newParty.ActualClan != adoptedHero.Clan)
                                    newParty.ActualClan = adoptedHero.Clan;
                                if (newParty.Owner == null)
                                    Log.Info("Party create owner null");
                                foreach (var retinueTroop in retinue)
                                {
                                    if (retinueTroop != null)
                                    {
                                        newParty.MemberRoster.AddToCounts(retinueTroop, 1);
                                    }
                                }
                                foreach (var retinue2Troop in retinue2)
                                {
                                    if (retinue2Troop != null)
                                    {
                                        newParty.MemberRoster.AddToCounts(retinue2Troop, 1);
                                    }
                                }
                                float num = 2f * Campaign.Current.EstimatedAverageLordPartySpeed * (float)CampaignTime.HoursInDay;
                                foreach (Settlement settlement in Campaign.Current.Settlements)
                                {
                                    if (settlement.IsVillage)
                                    {
                                        float num2;
                                        float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(newParty, settlement, false, newParty.NavigationCapability, out num2);
                                        if (distance < num)
                                        {
                                            foreach (ValueTuple<ItemObject, float> valueTuple in settlement.Village.VillageType.Productions)
                                            {
                                                ItemObject item = valueTuple.Item1;
                                                float item2 = valueTuple.Item2;
                                                float num3 = (item.ItemType == ItemObject.ItemTypeEnum.Horse && item.HorseComponent.IsRideable && !item.HorseComponent.IsPackAnimal) ? 7f : (item.IsFood ? 0.1f : 0f);
                                                float num4 = ((float)newParty.MemberRoster.TotalManCount + 2f) / 200f;
                                                float num5 = 1f - distance / num;
                                                int num6 = MBRandom.RoundRandomized(num3 * item2 * num5 * num4);
                                                if (num6 > 0)
                                                {
                                                    newParty.ItemRoster.AddToCounts(item, num6);
                                                }
                                            }
                                        }
                                    }
                                }
                                newParty.InitializeMobilePartyAtPosition(spawnSettlement.GatePosition);
                            }
                            else
                            {
                                onFailure("Failed to create a party. Wait some time and try again.");
                                return;
                            }
                        }
                        //PartyTemplateObject template = adoptedHero.Culture?.DefaultPartyTemplate;

                        //if (template != null)
                        //{
                        //    foreach (var stack in template.Stacks)
                        //    {
                        //        int amount = MBRandom.RandomInt(stack.MinValue, stack.MaxValue + 1);

                        //        if (amount > 0)
                        //            newParty.MemberRoster.AddToCounts(stack.Character, amount);
                        //    }
                        //}
                        onSuccess("Party created!");
                        break;
                    }
                case "stats":
                    {
                        if (party == null)
                        {
                            onFailure("You have no party");
                            return;
                        }
                        TextObject composition = PartyBaseHelper.PrintRegularTroopCategories(party.MemberRoster) ?? new TextObject("Unknown");
                        double tier = Math.Round(party.MemberRoster.GetTroopRoster().Sum(r => r.Character.Tier * r.Number) / (double)party.MemberRoster.GetTroopRoster().Sum(r => r.Number),1);

                        partyStats.Append($"Troops: {composition}(avg Tier {Math.Round(tier, 1)}) | ");
                        partyStats.Append($"Speed: {Math.Round(party.Speed, 1) - UpgradeBehavior.Current.GetTotalPartySpeedBonus(party.ActualClan.Leader)} (+{UpgradeBehavior.Current.GetTotalPartySpeedBonus(party.ActualClan.Leader)}) | ");
                        partyStats.Append($"Food: {(int)party.Food}({Math.Round(party.FoodChange, 1)}) | ");
                        partyStats.Append($"Morale: {(int)party.Morale} | ");
                        partyStats.Append($"Sight: {Math.Round(party.SeeingRange, 1)} | ");
                        partyStats.Append($"Wage: {party.TotalWage} ");
                        var nav = party.IsCurrentlyAtSea ? MobileParty.NavigationType.Naval : MobileParty.NavigationType.Default;
                        Settlement location = SettlementHelper.FindNearestSettlementToMobileParty(party, nav);
                        if (location != null)
                            partyStats.Append($"| Near: {location.Name}");
                        onSuccess(partyStats.ToString());
                        break;
                    }
                case "disband":
                    {
                        if (adoptedHero.Clan == null)
                        {
                            onFailure("You are not in a clan");
                            return;
                        }
                        if (adoptedHero.Clan.Leader.IsHumanPlayerCharacter)
                        {
                            onFailure("Cannot disband parties in the player clan");
                            return;
                        }

                        // ── DISBAND ALL ────────────────────────────────────────────────────
                        if (desiredName.Equals("all", StringComparison.OrdinalIgnoreCase))
                        {
                            var toDisband = adoptedHero.Clan.WarPartyComponents
                            .Select(wpc => wpc?.MobileParty)
                            .Where(mp => mp != null && mp.LeaderHero != null
                                      && mp.IsLordParty && mp.MapEvent == null)
                            .ToList(); // Note: no longer filters mp.Army == null

                            if (toDisband.Count == 0) { onFailure("No eligible parties to disband"); return; }

                            int disbanded = 0;
                            foreach (var mp in toDisband)
                            {
                                // Teardown any army membership before destroy
                                SafeRemovePartyFromArmy(mp);

                                var leader = mp.LeaderHero;
                                DestroyPartyAction.Apply(null, mp);
                                disbanded++;
                                FallbackLeaderToSettlement(leader, adoptedHero);
                            }
                            onSuccess($"Disbanded {disbanded} parties");
                            return;
                        }

                        // ── DISBAND (single) ───────────────────────────────────────────────
                        MobileParty targetParty;

                        if (string.IsNullOrWhiteSpace(desiredName))
                        {
                            if (party == null)
                            {
                                onFailure("You have no party to disband");
                                return;
                            }
                            targetParty = party;
                        }
                        else
                        {
                            if (!int.TryParse(desiredName.Trim(), out int idx) || idx < 1)
                            {
                                onFailure("Specify a valid party index (e.g. !party disband 2)");
                                return;
                            }

                            int count = 0;
                            targetParty = null;
                            foreach (var wpc in adoptedHero.Clan.WarPartyComponents)
                            {
                                var mp = wpc?.MobileParty;
                                if (mp == null || mp.LeaderHero == null || !mp.IsLordParty) continue;
                                if (++count == idx) { targetParty = mp; break; }
                            }

                            if (targetParty == null)
                            {
                                onFailure($"No party at index {idx} (clan has {count} active parties)");
                                return;
                            }
                        }

                        if (targetParty.MapEvent != null)
                        {
                            onFailure($"{targetParty.Name} is currently in combat");
                            return;
                        }

                        // Graceful army teardown rather than hard-fail.
                        // If something left a stale army reference we clean it here;
                        // if the army is valid we disband it first so members scatter
                        // rather than referencing a destroyed leader party.
                        if (targetParty.Army != null)
                        {
                            var existingArmy = targetParty.Army;
                            if (existingArmy.LeaderParty == targetParty)
                            {
                                // This party IS the army leader — disband the whole army
                                PartyOrderBehavior.Current?.CancelOrdersForParty(targetParty.StringId, null, false);
                                DisbandArmyAction.ApplyByUnknownReason(existingArmy);
                            }
                            else
                            {
                                // Member party — just remove it
                                targetParty.Army = null;
                                targetParty.AttachedTo = null;
                            }
                        }

                        string disbandedName = targetParty.Name.ToString();
                        Hero disbandedLeader = targetParty.LeaderHero;
                        DestroyPartyAction.Apply(null, targetParty);
                        FallbackLeaderToSettlement(disbandedLeader, adoptedHero);
                        onSuccess($"Disbanded {disbandedName}");
                        break;
                    }
                case "army":
                    {
                        if (!settings.ArmyEnabled) { onFailure("Army disabled"); return; }
                        if (adoptedHero.Clan.IsUnderMercenaryService) { onFailure("Mercenaries can't create armies"); return; }
                        if (adoptedHero.IsPrisoner) { onFailure("You are a prisoner!"); return; }

                        // Parse subcommand + optional target
                        // e.g. "army siege Pravend" → subCmd="siege", targetArg="Pravend"
                        // e.g. "army status"        → subCmd="status", targetArg=""
                        var armyParts = desiredName.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        var subCmd = armyParts.Length > 0 ? armyParts[0].ToLower() : "";
                        var targetArg = armyParts.Length > 1 ? armyParts[1].Trim() : "";

                        if (string.IsNullOrEmpty(subCmd))
                        {
                            onFailure("Specify: siege [target] / defend [target] / patrol / status / disband / leave / reassign [hero]");
                            return;
                        }

                        switch (subCmd)
                        {
                            // ── STATUS ────────────────────────────────────────────────────────
                            case "status":
                                {
                                    if (army == null) { onFailure("You have no army"); return; }

                                    var sb = new System.Text.StringBuilder();
                                    sb.Append($"Army: {army.Name} | Str: {(int)army.EstimatedStrength} | ");
                                    sb.Append($"{army.TotalHealthyMembers} troops / {army.LeaderPartyAndAttachedPartiesCount} parties | ");
                                    sb.Append($"Cohesion: {(int)army.Cohesion}");

                                    string behavior = party.DefaultBehavior.ToString();
                                    string targetName = party.TargetSettlement?.Name?.ToString()
                                                     ?? party.TargetParty?.Name?.ToString() ?? "—";
                                    sb.Append($" | {behavior} → {targetName}");
                                    sb.Append($" | Morale: {(int)army.Morale}");

                                    // Food estimate in days
                                    if (party.FoodChange < 0f && party.Food > 0f)
                                    {
                                        int foodDays = (int)(party.Food / Math.Abs(party.FoodChange));
                                        sb.Append($" | Food: ~{foodDays}d");
                                    }

                                    // Active order info
                                    var activeOrder = PartyOrderBehavior.Current?.GetActiveOrder(party.StringId);
                                    if (activeOrder != null)
                                        sb.Append($" | Order locked ({activeOrder.ReissueAttempts}/{activeOrder.MaxReissueAttempts} re-issues)");

                                    onSuccess(sb.ToString());
                                    return;
                                }

                            // ── DISBAND ───────────────────────────────────────────────────────
                            case "disband":
                                {
                                    if (army == null || army.LeaderParty != party)
                                    {
                                        onFailure("You are not leading an army");
                                        return;
                                    }
                                    if (party.MapEvent != null) { onFailure("Your army is in combat"); return; }

                                    PartyOrderBehavior.Current?.CancelOrdersForParty(party.StringId, null, false);
                                    DisbandArmyAction.ApplyByUnknownReason(army);
                                    onSuccess("Army disbanded");
                                    return;
                                }

                            // ── LEAVE ─────────────────────────────────────────────────────────
                            case "leave":
                                {
                                    if (army == null) { onFailure("You are not in an army"); return; }
                                    if (army.LeaderParty == party) { onFailure("Cannot leave your own army"); return; }
                                    if (army.LeaderParty == MobileParty.MainParty) { onFailure("Cannot leave the player's army"); return; }
                                    if (party.MapEvent != null) { onFailure("Your army is fighting"); return; }

                                    var oldArmy = army;
                                    party.Army = null;
                                    party.AttachedTo = null;
                                    onSuccess($"Left {oldArmy.Name}");
                                    if (oldArmy.LeaderPartyAndAttachedPartiesCount <= 1 && !oldArmy.IsWaitingForArmyMembers())
                                        DisbandArmyAction.ApplyByUnknownReason(oldArmy);
                                    return;
                                }

                            // ── REASSIGN ──────────────────────────────────────────────────────
                            case "reassign":
                                {
                                    if (string.IsNullOrWhiteSpace(targetArg)) { onFailure("Specify a hero name"); return; }
                                    if (army == null || army.LeaderParty != party) { onFailure("You must be leading an army"); return; }
                                    if (party.MapEvent != null) { onFailure("Your army is in combat"); return; }

                                    // Find target hero in the army
                                    var newLeaderParty = army.Parties.FirstOrDefault(p =>
                                        p != party
                                        && p.LeaderHero != null
                                        && p.LeaderHero.Name.ToString().IndexOf(targetArg, StringComparison.OrdinalIgnoreCase) >= 0);

                                    if (newLeaderParty == null)
                                    {
                                        onFailure($"Could not find '{targetArg}' in your army");
                                        return;
                                    }
                                    if (newLeaderParty.MapEvent != null)
                                    {
                                        onFailure($"{newLeaderParty.LeaderHero.Name} is in combat");
                                        return;
                                    }

                                    // Capture current order before tearing down
                                    var curOrder = PartyOrderBehavior.Current?.GetActiveOrder(party.StringId);
                                    var curTarget = curOrder?.TargetSettlementId != null
                                        ? Settlement.Find(curOrder.TargetSettlementId) : null;
                                    var curType = curOrder?.Type ?? PartyOrderType.Patrol;

                                    var armyType = curType == PartyOrderType.Siege ? Army.ArmyTypes.Besieger
                                                 : curType == PartyOrderType.Defend ? Army.ArmyTypes.Defender
                                                 : Army.ArmyTypes.Patrolling;

                                    // Capture remaining members (excluding old and new leader)
                                    var remaining = army.Parties
                                        .Where(p => p != party && p != newLeaderParty)
                                        .ToMBList();

                                    // Cancel order and disband old army
                                    PartyOrderBehavior.Current?.CancelOrdersForParty(party.StringId, null, false);
                                    DisbandArmyAction.ApplyByUnknownReason(army);

                                    // Recreate under new leader
                                    var gatherPoint = curTarget ?? newLeaderParty.CurrentSettlement
                                        ?? adoptedHero.HomeSettlement;
                                    adoptedHero.Clan.Kingdom.CreateArmy(
                                        newLeaderParty.LeaderHero, gatherPoint, armyType, remaining);

                                    var newArmy = newLeaderParty.Army;
                                    if (newArmy == null)
                                    {
                                        onFailure("Failed to transfer army leadership");
                                        return;
                                    }

                                    // Re-issue and register order for new leader if there was one
                                    if (newLeaderParty.Army == null) { onFailure("Failed to transfer army leadership"); return; }

                                    if (curTarget != null)
                                    {
                                        PartyOrderBehavior.IssueOrder(newLeaderParty, curType, curTarget);
                                        newLeaderParty.Ai.SetDoNotMakeNewDecisions(true);
                                        PartyOrderBehavior.Current?.RegisterOrder(
                                            adoptedHero, newLeaderParty, curType, curTarget,
                                            settings.ArmyMaxReissueAttempts, settings.ArmyOrderExpiryHours);
                                    }

                                    onSuccess($"Army command transferred to {newLeaderParty.LeaderHero.Name}");
                                    return;
                                }

                            // ── SIEGE / DEFEND / PATROL ───────────────────────────────────────
                            case "siege":
                            case "defend":
                            case "patrol":
                                {
                                    var armyType = subCmd == "siege" ? Army.ArmyTypes.Besieger
                                                 : subCmd == "defend" ? Army.ArmyTypes.Defender
                                                 : Army.ArmyTypes.Patrolling;

                                    var orderType = subCmd == "siege" ? PartyOrderType.Siege
                                                  : subCmd == "defend" ? PartyOrderType.Defend
                                                  : PartyOrderType.Patrol;

                                    // ── Resolve target settlement ──────────────────────────────
                                    Settlement target = null;
                                    if (!string.IsNullOrWhiteSpace(targetArg))
                                    {
                                        target = FindSettlementByName(targetArg, orderType, adoptedHero);
                                        if (target == null)
                                        {
                                            onFailure($"Settlement '{targetArg}' not found or invalid for {subCmd}");
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        target = orderType == PartyOrderType.Siege
                                            ? FindBestSettlementToTarget(adoptedHero.Clan.Kingdom, true)
                                            : orderType == PartyOrderType.Defend
                                                ? FindBestSettlementToDefend(party, adoptedHero.Clan.Kingdom)
                                                : null; // patrol can be null (patrols current position)
                                    }

                                    // ── Siege-specific validation ──────────────────────────────────
                                    if (orderType == PartyOrderType.Siege)
                                    {
                                        if (adoptedHero.Clan.Kingdom.FactionsAtWarWith.Count == 0)
                                        {
                                            onFailure("No active wars");
                                            return;
                                        }
                                        if (target == null)
                                        {
                                            onFailure("No valid enemy settlement found to besiege");
                                            return;
                                        }
                                        if (!target.IsFortification)
                                        {
                                            onFailure($"{target.Name} is not a fortification");
                                            return;
                                        }
                                        if (target.IsUnderSiege && target.SiegeEvent?.BesiegerCamp?.LeaderParty?.MapFaction != adoptedHero.Clan.Kingdom)
                                        {
                                            onFailure($"{target.Name} is under siege by another faction");
                                            return;
                                        }
                                        if (!adoptedHero.Clan.Kingdom.IsAtWarWith(
                                                target.OwnerClan?.Kingdom ?? target.OwnerClan?.MapFaction))
                                        {
                                            onFailure($"Not at war with {target.Name}'s owner");
                                            return;
                                        }

                                        if (!PartyOrderBehavior.IsSettlementReachable(party, target))
                                        {
                                            var fallbackTarget = FindBestSettlementToDefend(party, adoptedHero.Clan.Kingdom);
                                            PartyOrderBehavior.IssueOrder(party, PartyOrderType.Patrol, fallbackTarget);
                                            party.Ai.SetDoNotMakeNewDecisions(true);
                                            PartyOrderBehavior.Current?.RegisterOrder(
                                                adoptedHero, party, PartyOrderType.Patrol, fallbackTarget,
                                                settings.ArmyMaxReissueAttempts, settings.ArmyOrderExpiryHours);
                                            onFailure($"{target.Name} is not reachable by land — army set to patrol instead");
                                            return;
                                        }
                                    }

                                    if (!adoptedHero.IsPartyLeader) { onFailure("You are not leading a party"); return; }
                                    if (party.MapEvent != null) { onFailure("Your party is in combat"); return; }

                                    // ── REDIRECT existing army ─────────────────────────────────────
                                    if (army != null && army.LeaderParty == party)
                                    {
                                        army.ArmyType = armyType;
                                        if (target != null) army.AiBehaviorObject = target;

                                        PartyOrderBehavior.IssueOrder(party, orderType, target);
                                        party.Ai.SetDoNotMakeNewDecisions(true);
                                        PartyOrderBehavior.Current?.RegisterOrder(
                                            adoptedHero, party, orderType, target,
                                            settings.ArmyMaxReissueAttempts, settings.ArmyOrderExpiryHours);

                                        onSuccess($"Army redirected: {subCmd}"
                                            + (target != null ? $" → {target.Name}" : " (current position)"));
                                        return;
                                    }

                                    if (army != null && army.LeaderParty != party)
                                    {
                                        onFailure("You are in someone else's army");
                                        return;
                                    }

                                    // ── CREATE new army ────────────────────────────────────────────
                                    if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.ArmyPrice)
                                    {
                                        onFailure(Naming.NotEnoughGold(settings.ArmyPrice,
                                            BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                                        return;
                                    }

                                    var navType = party.IsCurrentlyAtSea
                                        ? MobileParty.NavigationType.Naval
                                        : MobileParty.NavigationType.Default;
                                    var nearestSettlement = SettlementHelper.FindNearestSettlementToMobileParty(party, navType)
                                        ?? adoptedHero.HomeSettlement;
                                    var gatherPoint = target ?? nearestSettlement;

                                    // Build member list (vassals + army model suggestions)
                                    var vassals = VassalBehavior.Current.GetVassalClans(adoptedHero.Clan);
                                    var vassalParties = adoptedHero.Clan.Kingdom.AllParties
                                        .Where(p => (p.ActualClan == adoptedHero.Clan || vassals.Contains(p.ActualClan))
                                                 && p != party
                                                 && p.Army == null
                                                 && p.AttachedTo == null
                                                 && p.LeaderHero != null
                                                 && p.MapEvent == null
                                                 && !p.IsDisbanding)
                                        .ToMBList();
                                    var modelParties = Campaign.Current.Models.ArmyManagementCalculationModel
                                        .GetMobilePartiesToCallToArmy(party);
                                    var mergedParties = vassalParties.Concat(modelParties)
                                        .Where(p => p != null)
                                        .Distinct()
                                        .ToMBList();

                                    // Give influence, deduct gold
                                    adoptedHero.Clan.Influence += 200f;
                                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.ArmyPrice, true);

                                    adoptedHero.Clan.Kingdom.CreateArmy(adoptedHero, gatherPoint, armyType, mergedParties);
                                    var newArmy = party.Army;

                                    if (newArmy == null)
                                    {
                                        onFailure("Army creation failed");
                                        // Refund on failure
                                        BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, settings.ArmyPrice, false);
                                        return;
                                    }

                                    if (target != null) newArmy.AiBehaviorObject = target;

                                    // Issue order immediately — don't leave the gather-phase AI in charge
                                    PartyOrderBehavior.IssueOrder(party, orderType, target);
                                    party.Ai.SetDoNotMakeNewDecisions(true);
                                    PartyOrderBehavior.Current?.RegisterOrder(
                                        adoptedHero, party, orderType, target,
                                        settings.ArmyMaxReissueAttempts, settings.ArmyOrderExpiryHours);

                                    int memberCount = newArmy.Parties.Count - 1; // exclude leader
                                    onSuccess($"Gathering {armyType} army ({memberCount} joining)"
                                        + (target != null ? $" → {target.Name}" : ""));
                                    return;
                                }

                            // ── THREAT ────────────────────────────────────────────────────────
                            case "threat":
                                {
                                    if (!settings.ThreatEnabled) { onFailure("Threat scan is disabled"); return; }
                                    if (party == null) { onFailure("You have no party"); return; }

                                    float radius = settings.ThreatScanRadius;
                                    float ourStrength = party.GetTotalLandStrengthWithFollowers();
                                    var ourPos = party.GetPosition2D;

                                    var threats = new List<(string name, float enemyStr, float attackScore, float avoidScore, bool flee)>();

                                    foreach (var other in MobileParty.All)
                                    {
                                        if (other == party || !other.IsActive || other.IsMainParty) continue;
                                        if (other.MapEvent != null) continue;
                                        if (!other.MapFaction.IsAtWarWith(party.MapFaction)) continue;
                                        if (other.GetPosition2D.Distance(ourPos) > radius) continue;

                                        float enemyStr = other.GetTotalLandStrengthWithFollowers();
                                        if (enemyStr <= 0f) continue;

                                        float adv = ourStrength / enemyStr;
                                        float attackScore = MBMath.ClampFloat(0.5f * (1f + adv), 0.05f, 3.0f);
                                        float avoidScore = adv < 1f
                                            ? MBMath.ClampFloat(1f / adv, 0.05f, 3.0f) : 0f;

                                        threats.Add((
                                            other.Name?.ToString() ?? "Unknown",
                                            enemyStr, attackScore, avoidScore,
                                            flee: avoidScore > attackScore));
                                    }

                                    if (threats.Count == 0) { onSuccess("No hostile forces detected nearby"); return; }

                                    var top = threats
                                        .OrderByDescending(t => t.flee ? t.avoidScore : 0f)
                                        .ThenByDescending(t => !t.flee ? t.attackScore : 0f)
                                        .Take(settings.ThreatMaxResults)
                                        .Select(t =>
                                        {
                                            string icon = t.flee ? "⚠ DANGER" : "→ ENGAGE";
                                            return $"[{icon}] {t.name} (Str:{t.enemyStr:0} vs {ourStrength:0})";
                                        });

                                    onSuccess(string.Join(" | ", top));
                                    return;
                                }

                            default:
                                onFailure("Specify: siege [target] / defend [target] / patrol / status / disband / leave / reassign [hero]");
                                return;
                        }
                    }
            }
        }

        private Settlement FindBestSettlementToTarget(Kingdom kingdom, bool forSiege)
        {
            var distModel = Campaign.Current.Models.MapDistanceModel;
            Settlement bestSettlement = null;
            float bestScore = 0f;
            var midSet = kingdom.FactionMidSettlement;

            foreach (var enemy in kingdom.FactionsAtWarWith)
            {
                int stance = kingdom.GetStanceWith(enemy).BehaviorPriority;
                if (stance == 1) continue;
                if (enemy.Settlements == null) continue;

                foreach (var settlement in enemy.Settlements)
                {
                    if (!settlement.IsFortification) continue;
                    if (settlement.IsUnderSiege && settlement.SiegeEvent?.BesiegerCamp?.LeaderParty?.MapFaction != kingdom) continue;

                    float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(midSet, settlement, false, false, MobileParty.NavigationType.All);
                    float strength = settlement.Town?.GarrisonParty?.Party.EstimatedStrength + settlement.Town.Militia ?? 0f;
                    var neighbours = Campaign.Current.Models.MapDistanceModel.GetNeighborsOfFortification(settlement.Town, MobileParty.NavigationType.All);
                    bool direct = neighbours.Any(n => kingdom.Settlements.Contains(n));

                    // Score based on proximity and defensive strength
                    float score = (10000f / distance - Math.Min(strength * 0.05f, 10000f / distance - 1f)) * (Math.Max(1, stance) * (direct ? 1.1f : 1f));

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestSettlement = settlement;
                    }
                }
            }

            return bestSettlement;
        }

        /// <summary>
        /// Defensively removes a party from its army (if any) before the caller
        /// calls DestroyPartyAction. Handles both leader and member cases.
        /// </summary>
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
            catch (Exception ex)
            {
                Log.Error($"[BLT] SafeRemovePartyFromArmy error: {ex}");
                // Don't rethrow — caller will still call Destroy which is the safest path
            }
        }

        private static void FallbackLeaderToSettlement(Hero leader, Hero requester)
        {
            if (leader == null || leader == requester) return;
            if (leader.PartyBelongedTo != null || leader.CurrentSettlement != null) return;

            var fallback = leader.HomeSettlement
                        ?? Settlement.All.Where(s => s.IsTown).SelectRandom();
            if (fallback != null)
                EnterSettlementAction.ApplyForCharacterOnly(leader, fallback);
        }

        private Settlement FindBestSettlementToDefend(MobileParty party, Kingdom kingdom)
        {
            Settlement bestSettlement = null;
            float bestScore = 0f;

            foreach (var settlement in kingdom.Settlements)
            {
                if (!settlement.IsFortification) continue;

                // Prioritize settlements under threat
                bool underThreat = settlement.IsUnderSiege ||
                                  settlement.LastAttackerParty != null &&
                                  settlement.LastAttackerParty.IsActive;

                float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(party, settlement, false, party.NavigationCapability, out float ratio);
                float threatMultiplier = underThreat ? 10f : 1f;

                float score = (1000f / (distance + 1f)) * threatMultiplier;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSettlement = settlement;
                }
            }

            return bestSettlement ?? kingdom.Settlements.FirstOrDefault(s => s.IsFortification);
        }

        /// <summary>
        /// Fuzzy settlement lookup by name, with order-type-appropriate validation.
        /// Returns null with no failure message — caller reports the error.
        /// </summary>
        /// <summary>
        /// Fuzzy settlement lookup by name, with order-type-appropriate validation.
        /// Returns null with no failure message — caller reports the error.
        /// </summary>
        private Settlement FindSettlementByName(string name, PartyOrderType orderType, Hero hero)
        {
            // Exact match first
            var match = Settlement.All.FirstOrDefault(s =>
                s?.Name?.ToString().Equals(name, StringComparison.OrdinalIgnoreCase) == true);

            if (match == null)
            {
                var partials = Settlement.All
                    .Where(s => s?.Name?.ToString().IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                if (partials.Count == 1) match = partials[0];
                // Ambiguous / not found → return null, caller handles it
            }

            if (match == null) return null;

            // Order-type validation
            switch (orderType)
            {
                case PartyOrderType.Siege:
                    if (!match.IsFortification) return null;
                    var targetFaction = match.OwnerClan?.Kingdom ?? match.OwnerClan?.MapFaction;
                    if (targetFaction == null) return null;
                    if (targetFaction == hero.Clan.Kingdom) return null; // own settlement
                    if (!hero.Clan.Kingdom.IsAtWarWith(targetFaction)) return null; // not at war
                    break;
                case PartyOrderType.Defend:
                    if (!match.IsFortification) return null;
                    break;
                    // Patrol: any settlement is fine
            }

            return match;
        }
    }
}
