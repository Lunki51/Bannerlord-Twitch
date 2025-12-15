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
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
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
        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
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
            var party = adoptedHero.PartyBelongedTo;
            var army = party?.Army;
            string behaviorText = adoptedHero.PartyBelongedTo?.GetBehaviorText().ToString();
            string armyBehavior = "";
            if (army != null)
            {
                armyBehavior = army.LeaderParty.GetBehaviorText().ToString();
            }
            var partyStats = new StringBuilder();

            if (string.IsNullOrEmpty(mode))
            {
                
                if (adoptedHero.HeroState == Hero.CharacterStates.Released)
                    partyStats.Append("{=r1nJTiSA}Your hero has just been released".Translate());
                else if (adoptedHero.HeroState == Hero.CharacterStates.Traveling)
                    partyStats.Append("{=TESTING}Your hero is travelling".Translate());
                else if (adoptedHero.HeroState == Hero.CharacterStates.Fugitive)
                    partyStats.Append("{=TESTING}Your hero is fugitive".Translate());
                else if (adoptedHero.IsPrisoner && adoptedHero.PartyBelongedToAsPrisoner.IsMobile)
                {
                    partyStats.Append("{=zVDODxiN}Prisoner: {prisoner}".Translate(("prisoner", adoptedHero.PartyBelongedToAsPrisoner.Name.ToString())));
                    partyStats.Append(" | ");
                    var place = adoptedHero.PartyBelongedToAsPrisoner?.LeaderHero?.LastKnownClosestSettlement?.Name?.ToString() ?? "Unknown";
                    partyStats.Append("{=B2xDasDx}Last seen near {Place}".Translate(("Place", place)));
                }
                else if (adoptedHero.IsPrisoner && adoptedHero.PartyBelongedToAsPrisoner.IsSettlement)
                    partyStats.Append("{=zVDODxiN}Prisoner: {prisoner}".Translate(("prisoner", adoptedHero.PartyBelongedToAsPrisoner.Settlement.Name.ToString())));
                else if (adoptedHero.GovernorOf != null && adoptedHero.Clan.Settlements.Count > 0)
                {
                    var govFief = adoptedHero.Clan.Settlements.Find(s => s.Town != null && s.Town.Governor == adoptedHero);
                    partyStats.Append("{=ocrxKWUF}Governor: {governor}".Translate(("governor", govFief.Name.ToString())));
                }
                else if (adoptedHero.IsPartyLeader)
                {
                    partyStats.Append("{=sN2NzoA7}Party(Strength: {party_strength} - ".Translate(("party_strength", Math.Round(adoptedHero.PartyBelongedTo.Party.EstimatedStrength).ToString())));
                    string partySizeStr = $"{adoptedHero.PartyBelongedTo.MemberRoster.TotalHealthyCount}({party.MemberRoster.TotalWounded})/{adoptedHero.PartyBelongedTo.Party.PartySizeLimit}";
                    if (adoptedHero.PartyBelongedTo.PrisonRoster.Count > 0)
                    {
                        partyStats.Append("{=4HDBsO9U}Size: {size} - ".Translate(("size", partySizeStr)));
                        partyStats.Append("{=jrBszDI8}Prisoners: {prisoners}) | ".Translate(("prisoners", adoptedHero.PartyBelongedTo.PrisonRoster.Count)));
                    }
                    else partyStats.Append("{=Sunm7EKS}Size: {size}) | ".Translate(("size", partySizeStr)));
                    if (party.IsCurrentlyAtSea)
                    {
                        partyStats.Append("Sailing | ");
                    }
                    if (behaviorText != null && behaviorText != armyBehavior)
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
                            partyStats.Append("{=9aFoBcPY}Target: {target} - ".Translate(("target", target.Name)));
                            partyStats.Append("{=D3dcUxuj}Size: {size} | ".Translate(("size", target.MemberRoster.TotalManCount)));
                        }
                    }
                    if (party.Army != null)
                    {
                        partyStats.Append("{=CVzSgXhT}Army: {army}".Translate(("army", army.Name.ToString())));
                        partyStats.Append("{=d76wc5iS}[Strength: {strength} | ".Translate(("strength", Math.Round(army.EstimatedStrength).ToString())));
                        partyStats.Append("{=D3dcUxuj}Size: {size} | ".Translate(("size", army.TotalHealthyMembers.ToString())));
                        partyStats.Append("{=7p5j5Mlx}Party nº: {count}] ".Translate(("count", army.LeaderPartyAndAttachedPartiesCount.ToString())));
                        
                        if (armyBehavior != null)
                        {
                            partyStats.Append($"Your army is: {armyBehavior} | ");
                        }
                        if (army.LeaderParty.TargetParty != null || army.LeaderParty.ShortTermTargetParty != null)
                        {
                            var target = army.LeaderParty.ShortTermTargetParty ?? army.LeaderParty.TargetParty;
                            partyStats.Append("{=9aFoBcPY}Target: {target} - ".Translate(("target", target.Name.ToString())));
                            partyStats.Append("{=D3dcUxuj}Size: {size} | ".Translate(("size", target.MemberRoster.TotalManCount)));
                        }
                    }
                    if (adoptedHero.PartyBelongedTo.MapEvent != null)
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
                    else if (!adoptedHero.IsPartyLeader) partyStats.Append("You have no party");
                }
                onSuccess(partyStats.ToString());
            }
            switch (mode)
            {
                case "govern":
                    {
                        var desiredTown = adoptedHero.Clan.Fiefs.FirstOrDefault(c => c.Name.ToString().IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0);
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
                        if (party != null && party.IsDisbanding == false && party.IsActive)
                        {
                            //party.IsDisbanding = true;
                            DisbandPartyAction.StartDisband(party);
                            onFailure("Started disband of your party");
                            return;
                        }
                        if (party != null && party.IsDisbanding == true)
                        {
                            onFailure("Party is disbanding");
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
                        if (party != null && adoptedHero.IsPartyLeader && !party.IsDisbanding)
                        {
                            onFailure("You already have a party");
                            return;
                        }
                        if (party != null && party.IsDisbanding)
                        {
                            onFailure("Your party is disbanding");
                            
                            DisbandPartyAction.CancelDisband(party);
                            return;
                        }
                        if (adoptedHero.IsPrisoner)
                        {
                            onFailure("You are prisoner");
                            return;
                        }
                        if (adoptedHero.Clan.Leader.IsHumanPlayerCharacter)
                        {
                            onFailure("Cannot create party in player clan");
                            return;
                        }
                        int parties = adoptedHero.Clan.WarPartyComponents.Count(pc => pc.MobileParty?.IsLordParty == true);
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
                        Settlement spawnSettlement = adoptedHero.CurrentSettlement ?? adoptedHero.HomeSettlement;
                        var newParty = Helpers.MobilePartyHelper.SpawnLordParty(adoptedHero, spawnSettlement);
                        var retinue = BLTAdoptAHeroCampaignBehavior.Current.GetRetinue(adoptedHero).ToList();
                        var retinue2 = BLTAdoptAHeroCampaignBehavior.Current.GetRetinue2(adoptedHero).ToList();
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

                        PartyTemplateObject template = adoptedHero.Culture?.DefaultPartyTemplate;
                        
                        if (template != null)
                        {
                            foreach (var stack in template.Stacks)
                            {
                                int amount = MBRandom.RandomInt(stack.MinValue, stack.MaxValue + 1);

                                if (amount > 0)
                                    newParty.MemberRoster.AddToCounts(stack.Character, amount);
                            }
                        }
                        newParty.IsActive = true;
                        newParty.IsDisbanding = false;
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
                        partyStats.Append($"Speed: {Math.Round(party.Speed, 1)} | ");
                        partyStats.Append($"Food: {(int)party.Food}({Math.Round(party.FoodChange, 1)}) | ");
                        partyStats.Append($"Morale: {(int)party.Morale} | ");
                        partyStats.Append($"Speed: {Math.Round(party.SeeingRange, 1)} | ");
                        partyStats.Append($"Wage: {party.TotalWage} |");
                        onSuccess(partyStats.ToString());
                        break;
                    }
            }
        }
    }
}
