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
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
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
    public class PartyManagement : HeroCommandHandlerBase
    {
        [CategoryOrder("War", 0)]
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=TESTING}Army"),
             LocCategory("Army", "{=TESTING}Army"),
             LocDescription("{=TESTING}Enable creating army command"),
             PropertyOrder(1), UsedImplicitly]
            public bool ArmyEnabled { get; set; } = true;

            [LocDisplayName("{=TESTING}Price"),
             LocCategory("Army", "{=TESTING}Army"),
             LocDescription("{=TESTING}Army command price"),
             PropertyOrder(2), UsedImplicitly]
            public int ArmyPrice { get; set; } = 50000;
            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.Value("!party/!party create/!party govern (fief)/!party stats");
                if (ArmyEnabled)
                    generator.Value("<strong>Army Config: </strong>" +
                                    "Price={price}{icon}".Translate(("price", ArmyEnabled.ToString()), ("icon", Naming.Gold)));
            }
        }
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
                onFailure("{=MPTOZqMS}You cannot manage your clan, as a mission is active!".Translate());
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
                    if (party?.Army != null && (party.AttachedTo != null || party.AttachedParties.Count > 0))
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
                        if (adoptedHero.PartyBelongedTo.MapEvent != null)
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
                        if (party != null)
                        {
                            onFailure("You are in a party");
                            return;
                        }
                        if (party == null)
                        {
                            if (govFief != null)
                            {
                                ChangeGovernorAction.RemoveGovernorOf(adoptedHero);
                            }
                            ChangeGovernorAction.Apply(desiredTown, adoptedHero);
                            onSuccess($"Governor of {desiredTown.Name}");
                        }
                        break;
                    }
                case "create":
                    {
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
                        if (adoptedHero.Clan.WarPartyComponents.Any(w => w.Leader == null))
                        {
                            onFailure("Cannot create party at this moment");
                            foreach (var p in adoptedHero.Clan.WarPartyComponents)
                            {
                                if (p.Leader == null)
                                {
                                    p.MobileParty.IsActive = true;
                                    DestroyPartyAction.ApplyForDisbanding(p.MobileParty, adoptedHero.Clan.HomeSettlement);
                                }
                            }
                            return;
                        }
                        if (adoptedHero.Clan.Leader.IsHumanPlayerCharacter)
                        {
                            onFailure("Cannot create party in player clan");
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
                        partyStats.Append($"Speed: {Math.Round(party.Speed, 1)} | ");
                        partyStats.Append($"Food: {(int)party.Food}({Math.Round(party.FoodChange, 1)}) | ");
                        partyStats.Append($"Morale: {(int)party.Morale} | ");
                        partyStats.Append($"Sight: {Math.Round(party.SeeingRange, 1)} | ");
                        partyStats.Append($"Wage: {party.TotalWage} ");
                        Settlement location = HeroHelper.GetClosestSettlement(adoptedHero);
                        if (location != null)
                            partyStats.Append($"| Near: {location.Name}");
                        onSuccess(partyStats.ToString());
                        break;
                    }
                case "army":
                    {
                        if (!settings.ArmyEnabled)
                        {
                            onFailure("Army disabled");
                            return;
                        }
                        if (adoptedHero.Clan.Kingdom == null)
                        {
                            onFailure("Not in a kingdom");
                            return;
                        }
                        if (adoptedHero.MapFaction.FactionsAtWarWith.Count == 0)
                        {
                            onFailure("No wars");
                            return;
                        }
                        if (adoptedHero.IsPrisoner)
                        {
                            onFailure("{=TESTING}You are prisoner!".Translate());
                            return;
                        }
                        if (!adoptedHero.IsPartyLeader)
                        {
                            onFailure("{=LVFh1Pd5}Your hero is not leading a party".Translate());
                            return;
                        }
                        if (adoptedHero.PartyBelongedTo.Army != null && adoptedHero.PartyBelongedTo.Army.LeaderParty != adoptedHero.PartyBelongedTo)
                        {
                            onFailure("Already in an army!");
                            return;
                        }
                        if (adoptedHero.PartyBelongedTo.MapEvent != null)
                        {
                            onFailure("Your party is busy.");
                            return;
                        }
                        Army.ArmyTypes armyType;
                        if (splitArgs.Length < 2)
                        {
                            onFailure("Specify an army type: defend/siege/raid/patrol");
                            return;
                        }
                        switch (desiredName)
                        {
                            case "siege":
                                armyType = Army.ArmyTypes.Besieger;
                                break;
                            //case "raid":
                            //    armyType = Army.ArmyTypes.Raider;
                            //    break;
                            case "defend":
                                armyType = Army.ArmyTypes.Defender;
                                break;
                            case "patrol":
                                armyType = Army.ArmyTypes.Patrolling;
                                break;
                            default:
                                onFailure($"Invalid army type: {desiredName}");
                                return;
                        }
                        if (adoptedHero.PartyBelongedTo.Army != null && adoptedHero.PartyBelongedTo.Army.LeaderParty == adoptedHero.PartyBelongedTo)
                        {
                            adoptedHero.PartyBelongedTo.Army.ArmyType = armyType;
                            onSuccess($"Changed army type to {armyType}");
                            return;
                        }
                        if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.ArmyPrice)
                        {
                            onFailure(Naming.NotEnoughGold(settings.ArmyPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                            return;
                        }
                        var pos = adoptedHero.LastKnownClosestSettlement ?? adoptedHero.HomeSettlement;
                        var sameClanParties = adoptedHero.Clan.Kingdom.AllParties
                            .Where(p => p?.ActualClan == adoptedHero.Clan && p != adoptedHero.PartyBelongedTo && p.Army == null && p.AttachedTo == null && p.LeaderHero != null)
                            .ToList();

                        adoptedHero.Clan.Kingdom.CreateArmy(adoptedHero, pos, armyType);
                        Army army = adoptedHero.PartyBelongedTo.Army;

                        //int addedPartiesCount = 0;
                        foreach (var party in sameClanParties)
                        {
                            party.Army = army;
                        }

                        BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.ArmyPrice, true);

                        onSuccess($"Gathering {armyType} army({army.Parties.Count}) at {pos}");
                        break;
                    }
            }
        }
    }
}
