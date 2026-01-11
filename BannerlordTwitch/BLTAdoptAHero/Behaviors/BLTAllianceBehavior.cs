using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using BannerlordTwitch.Util;

namespace BLTAdoptAHero
{
    /// <summary>
    /// Handles defensive alliance auto-joins when allies are attacked
    /// </summary>
    public class BLTAllianceBehavior : CampaignBehaviorBase
    {
        public static BLTAllianceBehavior Current { get; private set; }

        public BLTAllianceBehavior()
        {
            Current = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No persistence needed
        }

        private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
        {
            // Only handle BLT treaty system wars
            if (BLTTreatyManager.Current == null)
                return;

            Kingdom attacker = faction1 as Kingdom;
            Kingdom defender = faction2 as Kingdom;

            if (attacker == null || defender == null)
                return;

            // Check if defender has allies that should auto-join
            var defenderAlliances = BLTTreatyManager.Current.GetAlliancesFor(defender);

            if (defenderAlliances.Count == 0)
                return;

            // Find the BLT war
            var war = BLTTreatyManager.Current.GetWar(attacker, defender);

            if (war == null)
                return;

            AdoptedHeroFlags._allowDiplomacyAction = true;
            try
            {
                foreach (var alliance in defenderAlliances)
                {
                    var ally = alliance.GetOtherKingdom(defender);

                    if (ally == null || ally.IsEliminated)
                        continue;

                    // Skip if already at war with attacker
                    if (ally.IsAtWarWith(attacker))
                        continue;

                    // Check if ally can join (truce, NAP with attacker, etc.)
                    // Defensive alliance calls OVERRIDE NAPs and truces
                    var napWithAttacker = BLTTreatyManager.Current.GetNAP(ally, attacker);
                    var truceWithAttacker = BLTTreatyManager.Current.GetTruce(ally, attacker);

                    // Remove NAP if exists (defensive alliance overrides NAP)
                    if (napWithAttacker != null)
                    {
                        BLTTreatyManager.Current.RemoveNAP(ally, attacker);
                    }

                    // Remove truce if exists (defensive alliance overrides truce)
                    if (truceWithAttacker != null)
                    {
                        BLTTreatyManager.Current.RemoveTruce(ally, attacker);
                    }

                    // Check for alliance with attacker (shouldn't happen but just in case)
                    var allianceWithAttacker = BLTTreatyManager.Current.GetAlliance(ally, attacker);
                    if (allianceWithAttacker != null)
                    {
                        // Cannot auto-join if allied with both sides
                        Log.Trace($"[BLT] {ally.Name} cannot auto-join {defender.Name} - allied with both sides");
                        continue;
                    }

                    // Remove any existing tribute between ally and attacker
                    BLTTreatyManager.Current.RemoveTribute(ally, attacker);

                    // Add ally to the war
                    war.AddDefenderAlly(ally);

                    // Declare actual game war
                    DeclareWarAction.ApplyByDefault(ally, attacker);
                    FactionManager.DeclareWar(ally, attacker);

                    // Log the auto-join
                    Log.ShowInformation(
                        $"{ally.Name} has joined {defender.Name}'s defense against {attacker.Name}!",
                        ally.Leader?.CharacterObject,
                        Log.Sound.Horns2
                    );

                    // Notify ally kingdom leader if BLT
                    if (ally.Leader != null && ally.Leader.IsAdopted())
                    {
                        string allyLeaderName = ally.Leader.FirstName.ToString()
                            .Replace(BLTAdoptAHeroModule.Tag, "")
                            .Replace(BLTAdoptAHeroModule.DevTag, "")
                            .Trim();

                        Log.LogFeedResponse(
                            $"@{allyLeaderName} Your alliance with {defender.Name} has automatically brought you into war against {attacker.Name}!"
                        );
                    }
                }
            }
            finally
            {
                AdoptedHeroFlags._allowDiplomacyAction = false;
            }
        }
    }
}