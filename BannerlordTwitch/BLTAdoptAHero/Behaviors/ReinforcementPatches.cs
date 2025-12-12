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

                int bltCount = reinfBehavior.GetReinforcements(settlement);
                if (bltCount <= 0) return; // nothing to spawn

                // Build a unique string id for the party
                string partyId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<MobileParty>($"blt_reinforce_{settlement.StringId}");

                // Use MilitiaPartyComponent factory which associates the party to the settlement as militia
                MobileParty bltParty = null;
                try
                {
                    bltParty = MilitiaPartyComponent.CreateMilitiaParty(partyId, settlement);
                }
                catch
                {
                    // If CreateMilitiaParty is not available in this exact signature, fallback to a simpler party creation (less ideal).
                    // We'll attempt to create a generic mobile party tied to the settlement by using the LordPartyComponent.CreateLordParty or similar,
                    // but CreateMilitiaParty should exist in 1.3.x per apidoc.
                }

                if (bltParty == null) return;

                // Choose troop type(s) - use settlement culture's basic troop as a simple militia proxy.
                // Determine militia troop types from the culture (correct API)
                CharacterObject meleeType = settlement.Culture?.MeleeMilitiaTroop;
                CharacterObject rangedType = settlement.Culture?.RangedMilitiaTroop;

                // Fallbacks if culture somehow has a null pointer (rare but can happen in modded cultures)
                if (meleeType == null)
                    meleeType = settlement.Culture?.BasicTroop;
                if (rangedType == null)
                    rangedType = settlement.Culture?.BasicTroop;

                // As an absolute last resort, ensure at least something exists
                if (meleeType == null)
                    meleeType = CharacterObject.PlayerCharacter.Culture.MeleeMilitiaTroop;
                if (rangedType == null)
                    rangedType = CharacterObject.PlayerCharacter.Culture.RangedMilitiaTroop; ;

                // Split the count roughly 50/50 (odd -> melee gets +1)
                int meleeCount = bltCount / 2 + (bltCount % 2);
                int rangedCount = bltCount - meleeCount;

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
                            bltParty.MemberRoster.AddToCounts(meleeType, step);
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
                            bltParty.MemberRoster.AddToCounts(rangedType, step);
                            remaining -= step;
                        }
                    }
                }

                // Register this party so the behavior can reconcile survivors when the siege ends
                reinfBehavior.RegisterSiegeParty(settlement, bltParty.StringId);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[BLT Reinforcement] Siege spawn patch failed: {ex.Message}"));
            }
        }
    }
}
