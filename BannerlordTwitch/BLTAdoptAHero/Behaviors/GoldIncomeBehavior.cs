using BLTAdoptAHero.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace BLTAdoptAHero.Behaviors
{
    public class BLTGoldIncomeBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, OnDailyTickClan);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No persistence required
        }

        private void OnDailyTickClan(Clan clan)
        {
            if (clan == null)
                return;

            if (!BLTAdoptAHeroModule.CommonConfig.GoldIncomeEnabled)
                return;

            Hero leader = clan.Leader;
            if (leader == null || !leader.IsAdopted())
                return;

            int total = 0;

            // Calculate fief income
            if (BLTAdoptAHeroModule.CommonConfig.FiefIncomeEnabled && clan.Settlements != null)
            {
                foreach (var settlement in clan.Settlements)
                {
                    total += GoldIncomeAction.CalculateSettlementIncome(settlement);
                }
            }

            // Calculate bonus from vassal fief income
            if (BLTAdoptAHeroModule.CommonConfig.FiefIncomeEnabled)
            {
                total += VassalBehavior.Current.CalculateVassalFiefIncome(clan);
            }

            // Calculate mercenary income for this BLT clan
            if (BLTAdoptAHeroModule.CommonConfig.MercenaryIncomeEnabled && clan.IsUnderMercenaryService)
            {
                total += GoldIncomeAction.CalculateMercenaryIncome(clan);
            }

            // Calculate bonus from vassal mercenary contracts
            if (BLTAdoptAHeroModule.CommonConfig.MercenaryIncomeEnabled && VassalBehavior.Current != null)
            {
                int vassalBonus = VassalBehavior.Current.CalculateVassalMercenaryBonus(clan);
                total += vassalBonus;
            }

            // Apply gold change if there's any income
            if (total != 0)
            {
                BLTAdoptAHeroCampaignBehavior.Current?.ChangeHeroGold(leader, total, false);
            }
        }
    }
}
