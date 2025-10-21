using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using BannerlordTwitch.Helpers;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.Core;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=TESTING}Diplomacy"),
     LocDescription("{=TESTING}Manage your kingdom diplomacy"),
     UsedImplicitly]
    class Diplomacy : HeroCommandHandlerBase
    {
        [CategoryOrder("War", 0),
         CategoryOrder("Peace", 1)]
         //CategoryOrder("Policy", 2)
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=TESTING}War"),
             LocCategory("War", "{=TESTING}War"),
             LocDescription("{=TESTING}Enable joining kingdoms command"),
             PropertyOrder(1), UsedImplicitly]
            public bool WarEnabled { get; set; } = true;

            [LocDisplayName("{=TESTING}Price"),
             LocCategory("War", "{=TESTING}War"),
             LocDescription("{=TESTING}Enable joining kingdoms command"),
             PropertyOrder(2), UsedImplicitly]
            public int WarPrice { get; set; } = 250000;

            [LocDisplayName("{=TESTING}Peace"),
             LocCategory("Peace", "{=TESTING}Peace"),
             LocDescription("{=TESTING}Enable joining kingdoms command"),
             PropertyOrder(3), UsedImplicitly]
            public bool PeaceEnabled { get; set; } = true;

            [LocDisplayName("{=TESTING}Price"),
             LocCategory("Peace", "{=TESTING}Peace"),
             LocDescription("{=TESTING}Enable joining kingdoms command"),
             PropertyOrder(4), UsedImplicitly]
            public int PeacePrice { get; set; } = 100000;

            //[LocDisplayName("{=TESTING}Policy"),
            // LocCategory("Policy", "{=TESTING}Policy"),
            // LocDescription("{=TESTING}Enable joining kingdoms command"),
            // PropertyOrder(4), UsedImplicitly]
            //public bool PolicyEnabled { get; set; } = true;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                var EnabledCommands = new StringBuilder();

                if (WarEnabled)
                    EnabledCommands.Append("War, ");
                if (PeaceEnabled)
                    EnabledCommands.Append("Peace, ");
                //if (PolicyEnabled)
                //    EnabledCommands.Append("Leave, ");
                if (EnabledCommands.Length > 0)
                    generator.Value("<strong>Enabled Commands:</strong> {commands}".Translate(("commands", EnabledCommands.ToString(0, EnabledCommands.Length - 2))));

            }
        }
        
        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            var settings = config as Settings ?? new Settings();
            if (string.IsNullOrWhiteSpace(context.Args))
            {
                ActionManager.SendReply(context,
                    context.ArgsErrorMessage("{=TESTING}invalid mode (use kingdomlist, culturelist, warlist, kingdom (kingdom), war (kingdom)".Translate()));
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=B86KnTcu}You are not in a clan".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom == null)
            {
                onFailure("{=EJ4Pd2Lg}Your clan is not in a Kingdom".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=HS14GdUa}You cannot manage your kingdom, as you are not your clans leader!".Translate());
                return;
            }
            
            var splitArgs = context.Args.Split(' ');
            var mode = splitArgs[0];
            var desiredName = string.Join(" ", splitArgs.Skip(1)).Trim();
            var kingdom = adoptedHero.Clan.Kingdom;

            void ForceWar(Kingdom kingdomA, Kingdom kingdomB)
            {
                DeclareWarAction.ApplyByDefault(kingdomA, kingdomB);
            }
            //void VoteWar()
            //{
            //    DeclareWarDecision decision = new DeclareWarDecision(desiredKingdom, targetKingdom, desiredKingdom.Leader);
            //    decision.Initiate();
            //}
            void ForcePeace(Kingdom kingdomA, Kingdom kingdomB, int dailyTributeFrom1To2)
            {                
                MakePeaceAction.Apply(kingdomA, kingdomB, dailyTributeFrom1To2);
            }
            int CalculateTribute(Kingdom kingdomA, Kingdom kingdomB, StanceLink stance)
            {
                int cA = stance.GetCasualties(kingdomA); 
                int cB = stance.GetCasualties(kingdomB);  
                int rA = stance.GetSuccessfulRaids(kingdomA);
                int rB = stance.GetSuccessfulRaids(kingdomB);
                int sA = stance.GetSuccessfulSieges(kingdomA);
                int sB = stance.GetSuccessfulSieges(kingdomB);

                float scoreA = 2f * cA + 250f * rA + 1000f * sA;
                float scoreB = 2f * cB + 250f * rB + 1000f * sB;

                float scoreDiff = scoreB - scoreA;
                if (scoreDiff == 0)
                    return 0;
                bool A_lost = scoreDiff > 0;

                Kingdom loser = A_lost ? kingdomA : kingdomB;
                Kingdom winner = A_lost ? kingdomB : kingdomA;

                float fiefMultiplier = (float)loser.Settlements.Count / Math.Max(winner.Settlements.Count, 1);
                float strengthLoser = loser.TotalStrength;
                float strengthWinner = winner.TotalStrength;
                float strengthMultiplier = strengthLoser / Math.Max(strengthWinner, 1f);

                float relativeMultiplier = 0.85f + 0.1f * fiefMultiplier + 0.05f * strengthMultiplier;
                relativeMultiplier = Math.Max(0.5f, Math.Min(relativeMultiplier, 2f));

                float baseTribute;
                if (Math.Abs(scoreDiff) < 1000)
                {
                    baseTribute = 50f + Math.Abs(scoreDiff) * 0.35f;
                }
                else
                {
                    baseTribute = 250f + Math.Abs(scoreDiff) * 0.2f;
                }
                float tribute = baseTribute * relativeMultiplier;

                tribute = Math.Max(1f, Math.Min(tribute, 10000f));
                tribute *= Math.Sign(scoreDiff);
                return (int)tribute;
            }
            var desiredKingdom = CampaignHelpers.AllHeroes.Select(h => h?.Clan?.Kingdom).Distinct().FirstOrDefault(c => c?.Name.ToString().Equals(desiredName, StringComparison.OrdinalIgnoreCase) == true);

            switch (mode)
            {
                case "war":
                    {
                        if (!settings.WarEnabled)
                        {
                            onFailure("War disabled".Translate());
                            return;
                        }
                        if (adoptedHero.Clan.Influence < 200f || adoptedHero.Clan.Kingdom.RulingClan != adoptedHero.Clan)
                        {
                            onFailure("Not enough influence.");
                            break;
                        }
                        if (kingdom.IsAtWarWith(desiredKingdom))
                        {
                            onFailure($"Already at war with {desiredKingdom}");
                            break;
                        }
                        if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.WarPrice)
                        {
                            onFailure(Naming.NotEnoughGold(settings.WarPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                            return;
                        }
                        BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.WarPrice, true);
                        ForceWar(kingdom, desiredKingdom);
                        adoptedHero.Clan.Influence -= 200f;
                        onSuccess($"Declared war on {desiredKingdom}");
                        break;
                    }
                case "peace":
                    {
                        if (!settings.PeaceEnabled)
                        {
                            onFailure("Peace disabled".Translate());
                            return;
                        }
                        if (adoptedHero.Clan.Influence < 200f || adoptedHero.Gold < 50000 || adoptedHero.Clan.Kingdom.RulingClan != adoptedHero.Clan)
                        {
                            onFailure("Not enough influence or gold");
                            break;
                        }

                        var stance = kingdom.GetStanceWith(desiredKingdom);
                        if (!kingdom.IsAtWarWith(desiredKingdom))
                        {
                            onFailure($"Already at peace with {desiredKingdom}");
                            break;
                        }
                        if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.PeacePrice)
                        {
                            onFailure(Naming.NotEnoughGold(settings.PeacePrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                            return;
                        }
                        BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.PeacePrice, true);
                        int tribute = CalculateTribute(kingdom, desiredKingdom, stance);
                        ForcePeace(kingdom, desiredKingdom, tribute);
                        adoptedHero.Gold -= 50000;
                        adoptedHero.Clan.Influence -= 200f;
                        tribute *= -1;
                        onSuccess($"Made peace with {desiredKingdom} for {tribute}");
                        break;
                    }
                //case "policy":
                //    {
                        
                //        break;
                //    }
                //case "army":
                //    {
                //        //if (kingdom.IsAtWarWith)
                //        break;
                //    }
                default:
                    {
                        break;
                    }
            }
        }

    }
}
