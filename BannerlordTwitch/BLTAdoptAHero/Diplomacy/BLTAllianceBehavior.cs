using BannerlordTwitch.Util;
using BLTAdoptAHero;
using System;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

public class BLTAllianceBehavior : CampaignBehaviorBase
{
    public static BLTAllianceBehavior Current { get; private set; }

    public BLTAllianceBehavior() { Current = this; }

    public override void RegisterEvents()
    {
        CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnWarDeclared(IFaction faction1, IFaction faction2,
        DeclareWarAction.DeclareWarDetail detail)
    {
        try
        {
            // ── Safety guard: NEVER process during a mission ──────────────────
            // War can be implicitly declared when the player attacks a party mid-map.
            // The event fires before the battle loads, but map-level operations
            // (army creation, faction changes) will crash in a mission context.
            if (Mission.Current != null)
            {
#if DEBUG
                Log.Trace("[BLT Alliance] Skipping OnWarDeclared — mission is active");
#endif
                return;
            }

            // Don't re-enter from our own war declarations
            if (AdoptedHeroFlags._allowDiplomacyAction) return;

            // Don't process rebellions
            if (detail == DeclareWarAction.DeclareWarDetail.CausedByRebellion) return;

            // faction1 = aggressor, faction2 = defender (ApplyByDefault convention)
            // We only auto-join for the DEFENDER side.
            // Attacker-side allies must be called in manually via !diplomacy clan ctw.

            // ── Kingdom-level defensive alliances (BLTTreatyManager) ──────────
            var k1 = faction1 as Kingdom;
            var k2 = faction2 as Kingdom;
            if (k1 != null && k2 != null)
                HandleKingdomDefensiveAlliance(k1, k2);

            // ── Clan-level defensive alliances (BLTClanDiplomacyBehavior) ─────
            // The defender (faction2) may be a bare Clan with no Kingdom.
            // The attacker (faction1) may also be a bare Clan.
            HandleClanDefensiveAlliance(faction1, faction2);
        }
        catch (Exception ex)
        {
            Log.Error($"[BLT Alliance] OnWarDeclared error: {ex}");
        }
    }

    // ── Kingdom-level (original logic, extracted) ─────────────────────────────

    private void HandleKingdomDefensiveAlliance(Kingdom attacker, Kingdom defender)
    {
        if (BLTTreatyManager.Current == null) return;

        var defenderAlliances = BLTTreatyManager.Current.GetAlliancesFor(defender);
        if (defenderAlliances.Count == 0) return;

        var war = BLTTreatyManager.Current.GetWar(attacker, defender)
               ?? BLTTreatyManager.Current.CreateWar(attacker, defender);

        AdoptedHeroFlags._allowDiplomacyAction = true;
        try
        {
            foreach (var alliance in defenderAlliances)
            {
                var ally = alliance.GetOtherKingdom(defender);
                if (ally == null || ally.IsEliminated) continue;
                if (ally == attacker) continue;
                if (ally.IsAtWarWith(attacker)) continue;

                // Cannot auto-join if allied with both sides
                if (BLTTreatyManager.Current.GetAlliance(ally, attacker) != null)
                {
                    Log.ShowInformation(
                        $"{ally.Name} cannot join {defender.Name}'s defense — allied with both sides!",
                        ally.Leader?.CharacterObject);
                    continue;
                }

                // Defensive alliance overrides NAP and truce
                BLTTreatyManager.Current.RemoveNAP(ally, attacker);
                var truce = BLTTreatyManager.Current.GetTruce(ally, attacker);
                if (truce != null && !truce.IsExpired())
                    BLTTreatyManager.Current.RemoveTruce(ally, attacker);
                BLTTreatyManager.Current.RemoveTribute(ally, attacker);

                war.AddDefenderAlly(ally);
                DeclareWarAction.ApplyByDefault(ally, attacker);
                FactionManager.DeclareWar(ally, attacker);

                Log.ShowInformation(
                    $"{ally.Name} joined {defender.Name}'s defense against {attacker.Name}!",
                    ally.Leader?.CharacterObject, Log.Sound.Horns2);

                if (ally.Leader?.IsAdopted() == true)
                {
                    string n = ally.Leader.FirstName.ToString()
                        .Replace(BLTAdoptAHeroModule.Tag, "")
                        .Replace(BLTAdoptAHeroModule.DevTag, "").Trim();
                    Log.LogFeedResponse(
                        $"@{n} Defensive alliance activated — {defender.Name} was attacked by {attacker.Name}!");
                }
            }
        }
        finally
        {
            AdoptedHeroFlags._allowDiplomacyAction = false;
        }
    }

    // ── Clan-level (new) ──────────────────────────────────────────────────────

    private void HandleClanDefensiveAlliance(IFaction aggressor, IFaction defender)
    {
        if (BLTClanDiplomacyBehavior.Current == null) return;

        // Extract the Clan object for the defender side.
        // It may be a bare Clan or the RulingClan of a Kingdom.
        // We only care if the defender is an INDEPENDENT clan (no kingdom),
        // because kingdom-level alliances are handled by HandleKingdomDefensiveAlliance.
        Clan defenderClan = defender as Clan;
        if (defenderClan == null) return;          // kingdom — already handled above
        if (defenderClan.Kingdom != null) return;  // vassal — skip

        var alliedClans = BLTClanDiplomacyBehavior.Current.GetAlliedClans(defenderClan);
        if (alliedClans.Count == 0) return;

#if DEBUG
        Log.Trace($"[BLT Alliance] {defenderClan.Name} (clan) attacked by {aggressor.Name} — " +
                  $"checking {alliedClans.Count} clan allies");
#endif

        AdoptedHeroFlags._allowDiplomacyAction = true;
        try
        {
            foreach (var ally in alliedClans)
            {
                if (ally == null || ally.IsEliminated) continue;
                if (ally.Kingdom != null) continue; // kingdom clan — different system
                if ((IFaction)ally == aggressor) continue;
                if (ally.IsAtWarWith(aggressor)) continue;

                // Cannot auto-join if also allied with the aggressor
                Clan aggressorClan = aggressor as Clan;
                if (aggressorClan != null &&
                    BLTClanDiplomacyBehavior.Current.HasAlliance(ally, aggressorClan))
                {
                    Log.ShowInformation(
                        $"{ally.Name} cannot defend {defenderClan.Name} — allied with both sides!",
                        ally.Leader?.CharacterObject);
                    continue;
                }

                DeclareWarAction.ApplyByDefault(ally, aggressor);
                FactionManager.DeclareWar(ally, aggressor);

                Log.ShowInformation(
                    $"{ally.Name} joined {defenderClan.Name}'s defense against {aggressor.Name}!",
                    ally.Leader?.CharacterObject, Log.Sound.Horns2);

                if (ally.Leader?.IsAdopted() == true)
                {
                    string n = ally.Leader.FirstName.ToString()
                        .Replace(BLTAdoptAHeroModule.Tag, "")
                        .Replace(BLTAdoptAHeroModule.DevTag, "").Trim();
                    Log.LogFeedResponse(
                        $"@{n} Defensive alliance activated — {defenderClan.Name} was attacked by {aggressor.Name}!");
                }
            }
        }
        finally
        {
            AdoptedHeroFlags._allowDiplomacyAction = false;
        }
    }
}