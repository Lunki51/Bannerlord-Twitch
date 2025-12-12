using System;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace BLTAdoptAHero.Patches
{
    [HarmonyPatch]
    public static class ReinforcementPatches
    {
        // Target: SiegeEventManager.StartSiegeEvent(Settlement, MobileParty) -> returns SiegeEvent
        static System.Reflection.MethodBase TargetMethod()
        {
            var t = typeof(SiegeEventManager);
            var mi = AccessTools.Method(t, "StartSiegeEvent", new Type[] { typeof(Settlement), typeof(MobileParty) });
            return mi;
        }

        // Postfix: after a siege event is created, create BLT militia parties for that settlement if any reinforcements exist
        static void Postfix(SiegeEvent __result)
        {
            try
            {
                if (__result == null) return;

                var settlement = __result?.BesiegedSettlement;
                if (settlement == null) return;

                var reinfBehavior = ReinforcementBehavior.Current;
                if (reinfBehavior == null) return;

                // Spawn normal militia party if any
                int bltCount = reinfBehavior.GetReinforcements(settlement);
                if (bltCount > 0)
                {
                    TryCreateAndRegisterPartyForTier(settlement, bltCount, isElite: false);
                }

                // Spawn elite militia party if any
                int eliteCount = reinfBehavior.GetEliteReinforcements(settlement);
                if (eliteCount > 0)
                {
                    TryCreateAndRegisterPartyForTier(settlement, eliteCount, isElite: true);
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[BLT Reinforcement] Siege spawn patch failed: {ex.Message}"));
            }
        }

        private static void TryCreateAndRegisterPartyForTier(Settlement settlement, int count, bool isElite)
        {
            if (settlement == null || count <= 0) return;

            // Build a unique string id for the party
            string suffix = isElite ? "blt_reinforce_elite" : "blt_reinforce";
            string partyId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<MobileParty>($"{suffix}_{settlement.StringId}");

            MobileParty bltParty = null;
            try
            {
                bltParty = MilitiaPartyComponent.CreateMilitiaParty(partyId, settlement);
            }
            catch
            {
                // If CreateMilitiaParty fails for any reason, bail - better to not spawn than spawn broken parties
            }

            if (bltParty == null) return;

            // Choose troop types from settlement culture for each tier
            CharacterObject meleeType = null;
            CharacterObject rangedType = null;

            if (!isElite)
            {
                meleeType = settlement.Culture?.MeleeMilitiaTroop;
                rangedType = settlement.Culture?.RangedMilitiaTroop;
            }
            else
            {
                meleeType = settlement.Culture?.MeleeEliteMilitiaTroop;
                rangedType = settlement.Culture?.RangedEliteMilitiaTroop;
            }

            // Fallback to basic troop if culture entries missing
            if (meleeType == null) meleeType = settlement.Culture?.BasicTroop;
            if (rangedType == null) rangedType = settlement.Culture?.BasicTroop;
            if (meleeType == null && rangedType != null) meleeType = rangedType;
            if (rangedType == null && meleeType != null) rangedType = meleeType;
            if (meleeType == null || rangedType == null)
            {
                // If we still don't have a viable troop type, bail out.
                return;
            }

            // Split the count roughly 50/50 (odd -> melee gets +1)
            int meleeCount = count / 2 + (count % 2);
            int rangedCount = count - meleeCount;

            // Add melee troops
            if (meleeCount > 0)
            {
                try
                {
                    bltParty.MemberRoster.AddToCounts(meleeType, meleeCount);
                }
                catch
                {
                    int batch = Math.Min(50, meleeCount);
                    int remaining = meleeCount;
                    while (remaining > 0)
                    {
                        int step = Math.Min(batch, remaining);
                        try { bltParty.MemberRoster.AddToCounts(meleeType, step); } catch { }
                        remaining -= step;
                    }
                }
            }

            // Add ranged troops
            if (rangedCount > 0)
            {
                try
                {
                    bltParty.MemberRoster.AddToCounts(rangedType, rangedCount);
                }
                catch
                {
                    int batch = Math.Min(50, rangedCount);
                    int remaining = rangedCount;
                    while (remaining > 0)
                    {
                        int step = Math.Min(batch, remaining);
                        try { bltParty.MemberRoster.AddToCounts(rangedType, step); } catch { }
                        remaining -= step;
                    }
                }
            }

            // Register the created party so the behavior will reconcile survivors later
            ReinforcementBehavior.Current.RegisterSiegeParty(settlement, bltParty.StringId, isElite);
        }
    }
}
