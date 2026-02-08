using System;
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
            try
            {
                // Only handle BLT treaty system wars
                if (BLTTreatyManager.Current == null)
                    return;

                // Skip if this is a BLT-initiated action (to avoid double processing)
                if (AdoptedHeroFlags._allowDiplomacyAction)
                {
#if DEBUG
                    Log.Trace($"[BLT Alliance] Skipping - BLT-initiated war declaration");
#endif
                    return;
                }

                Kingdom attacker = faction1 as Kingdom;
                Kingdom defender = faction2 as Kingdom;

                if (attacker == null || defender == null)
                    return;

                // Don't process rebellions
                if (declareWarDetail == DeclareWarAction.DeclareWarDetail.CausedByRebellion)
                {
#if DEBUG
                    Log.Trace($"[BLT Alliance] Skipping rebellion war: {attacker.Name} vs {defender.Name}");
#endif
                    return;
                }

#if DEBUG
                Log.Trace($"[BLT Alliance] War declared: {attacker.Name} (attacker) vs {defender.Name} (defender)");
#endif

                // DEFENSIVE ONLY: Check if defender has allies that should auto-join
                var defenderAlliances = BLTTreatyManager.Current.GetAlliancesFor(defender);

                if (defenderAlliances.Count == 0)
                {
#if DEBUG
                    Log.Trace($"[BLT Alliance] {defender.Name} has no allies to call");
#endif
                    return;
                }

#if DEBUG
                Log.Trace($"[BLT Alliance] {defender.Name} has {defenderAlliances.Count} alliance(s), checking for auto-join...");
#endif

                // Find or create the BLT war
                var war = BLTTreatyManager.Current.GetWar(attacker, defender);

                if (war == null)
                {
                    // War was declared outside BLT system, create tracking
                    war = BLTTreatyManager.Current.CreateWar(attacker, defender);
#if DEBUG
                    Log.Trace($"[BLT Alliance] Created war tracking for non-BLT war: {attacker.Name} vs {defender.Name}");
#endif
                }

                AdoptedHeroFlags._allowDiplomacyAction = true;
                try
                {
                    foreach (var alliance in defenderAlliances)
                    {
                        var ally = alliance.GetOtherKingdom(defender);

                        if (ally == null || ally.IsEliminated)
                        {
#if DEBUG
                            Log.Trace($"[BLT Alliance] Skipping null/eliminated ally");
#endif
                            continue;
                        }

#if DEBUG
                        Log.Trace($"[BLT Alliance] Checking ally {ally.Name}...");
#endif

                        // Skip if ally is the attacker (shouldn't happen but safety check)
                        if (ally == attacker)
                        {
#if DEBUG
                            Log.Trace($"[BLT Alliance] {ally.Name} is the attacker - skipping");
#endif
                            continue;
                        }

                        // Skip if already at war with attacker
                        if (ally.IsAtWarWith(attacker))
                        {
#if DEBUG
                            Log.Trace($"[BLT Alliance] {ally.Name} already at war with {attacker.Name}");
#endif
                            continue;
                        }

                        // Check for alliance with attacker (shouldn't happen but just in case)
                        var allianceWithAttacker = BLTTreatyManager.Current.GetAlliance(ally, attacker);
                        if (allianceWithAttacker != null)
                        {
                            // Cannot auto-join if allied with both sides
                            Log.Trace($"[BLT Alliance] {ally.Name} cannot auto-join {defender.Name} - allied with both sides!");
                            Log.ShowInformation(
                                $"{ally.Name} cannot join {defender.Name}'s defense - they are allied with both sides!",
                                ally.Leader?.CharacterObject
                            );
                            continue;
                        }

                        // DEFENSIVE ALLIANCE: Overrides NAPs and truces
                        var napWithAttacker = BLTTreatyManager.Current.GetNAP(ally, attacker);
                        if (napWithAttacker != null)
                        {
                            BLTTreatyManager.Current.RemoveNAP(ally, attacker);
#if DEBUG
                            Log.Trace($"[BLT Alliance] Removed NAP between {ally.Name} and {attacker.Name} for defensive alliance");
#endif
                        }

                        var truceWithAttacker = BLTTreatyManager.Current.GetTruce(ally, attacker);
                        if (truceWithAttacker != null && !truceWithAttacker.IsExpired())
                        {
                            BLTTreatyManager.Current.RemoveTruce(ally, attacker);
#if DEBUG
                            Log.Trace($"[BLT Alliance] Removed truce between {ally.Name} and {attacker.Name} for defensive alliance");
#endif
                        }

                        // Remove any existing tribute between ally and attacker
                        BLTTreatyManager.Current.RemoveTribute(ally, attacker);

                        // Add ally to the war as defender
                        war.AddDefenderAlly(ally);

                        // Declare actual game war
                        DeclareWarAction.ApplyByDefault(ally, attacker);
                        FactionManager.DeclareWar(ally, attacker);

#if DEBUG
                        Log.Trace($"[BLT Alliance] {ally.Name} auto-joined DEFENSIVE war for {defender.Name} against {attacker.Name}");
#endif

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
                                $"@{allyLeaderName} Your defensive alliance with {defender.Name} has automatically brought you into war against {attacker.Name}!"
                            );
                        }
                    }
                }
                finally
                {
                    AdoptedHeroFlags._allowDiplomacyAction = false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT Alliance] OnWarDeclared error: {ex}");
            }
        }
    }
}