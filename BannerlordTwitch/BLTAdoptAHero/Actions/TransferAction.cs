using System;
using System.Linq;
using System.Text;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero;
using BLTAdoptAHero.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=TransferCmd}Transfer"),
     LocDescription("{=TransferDesc}Transfer a settlement to another clan (specify clan or clan leader hero)."),
     UsedImplicitly]
    public class TransferAction : HeroCommandHandlerBase
    {
        [CategoryOrder("General", 0),
         CategoryOrder("Restrictions", 1)]
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=TransferEnabled}Enabled"),
             LocCategory("General", "{=GeneralCat}General"),
             LocDescription("{=TransferEnabledDesc}Enable the transfer command"),
             PropertyOrder(1), UsedImplicitly]
            public bool Enabled { get; set; } = true;

            [LocDisplayName("{=AllowKingsToForceTransfers}Allow Kings To Force Transfers"),
             LocCategory("Restrictions", "{=RestrictionsCat}Restrictions"),
             LocDescription("{=AllowKingsToForceTransfersDesc}Allow kingdom rulers to forcibly transfer settlements between kingdoms using 'force'"),
             PropertyOrder(3), UsedImplicitly]
            public bool AllowKingsToForceTransfers { get; set; } = true;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                if (!Enabled)
                {
                    generator.Value("<strong>Enabled:</strong> No");
                    return;
                }

                generator.Value("<strong>Enabled:</strong> Yes");
                generator.Value("<strong>Allow kings to force cross-kingdom transfers:</strong> {v}".Translate(("v", AllowKingsToForceTransfers ? "Yes" : "No")));
            }
        }

        public override Type HandlerConfigType => typeof(Settings);

        // Entry point
        protected override void ExecuteInternal(
            Hero adoptedHero,
            ReplyContext context,
            object config,
            Action<string> onSuccess,
            Action<string> onFailure)
        {
            if (config is not Settings settings) return;

            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }

            if (!settings.Enabled)
            {
                onFailure("Transfer command is disabled.");
                return;
            }

            if (Mission.Current != null)
            {
                onFailure("Cannot transfer while a mission is active.");
                return;
            }

            if (context.Args.IsEmpty())
            {
                onFailure("Usage: [force] <settlement> <clan|hero [BLT]>");
                return;
            }

            var parts = context.Args.Split(' ', (char)StringSplitOptions.RemoveEmptyEntries);
            int idx = 0;
            bool isForce = false;

            // optional "force" token
            if (parts.Length > 0 && parts[0].Equals("force", StringComparison.OrdinalIgnoreCase))
            {
                isForce = true;
                idx = 1;
            }

            // validate remaining args
            if (parts.Length - idx < 2)
            {
                onFailure("Usage: [force] <settlement> <clan|hero [BLT]>");
                return;
            }

            // target settlement name may be multiple words; hero/clan is last token
            bool endtag = false;
            string targetSpecifier = parts[parts.Length - 1]; // last token
            string settlementName = null;
            if (targetSpecifier == "[BLT]")
            {
                targetSpecifier = parts[parts.Length - 2] + " [BLT]";
                endtag = true;
            }
            else if (targetSpecifier == "[DEV]")
            {
                targetSpecifier = parts[parts.Length - 2] + " [DEV]";
                endtag = true;
            }
            else
            {
                targetSpecifier = parts[parts.Length - 1];
                endtag = false;
            }

            if (!endtag)
            {
                settlementName = string.Join(" ", parts.Skip(idx).Take(parts.Length - idx - 1)).Trim();
            }
            else
            {
                settlementName = string.Join(" ", parts.Skip(idx).Take(parts.Length - idx - 2)).Trim();
            }

            if (string.IsNullOrWhiteSpace(settlementName))
            {
                onFailure("Invalid settlement name.");
                return;
            }

            // find settlement
            Settlement targetSettlement = FindSettlement(settlementName);
            if (targetSettlement == null)
            {
                onFailure($"Settlement '{settlementName}' not found.");
                return;
            }

            // Resolve clan/hero
            // Try hero first
            Hero targetHero = FindHero(targetSpecifier);
            Hero targetBLTHero = FindHero(targetSpecifier.Add(" [BLT]", false));
            Hero targetDEVHero = FindHero(targetSpecifier.Add(" [DEV]", false));
            Clan targetClan = null;
            if (targetHero != null)
            {
                if (targetHero.Clan == null)
                {
                    onFailure($"Hero '{targetHero.Name}' is not in a clan.");
                    return;
                }

                // The hero must be the leader of their clan per requirement
                if (targetHero != targetHero.Clan.Leader)
                {
                    onFailure($"Hero '{targetHero.Name}' is not the leader of clan '{targetBLTHero.Clan.Name}'. Transfers must specify a clan leader.");
                    return;
                }

                targetClan = targetHero.Clan;
            }
            else if (targetBLTHero != null)
            {
                if (targetBLTHero.Clan == null)
                {
                    onFailure($"Hero '{targetBLTHero.Name}' is not in a clan.");
                    return;
                }

                // The hero must be the leader of their clan per requirement
                if (targetBLTHero != targetBLTHero.Clan.Leader)
                {
                    onFailure($"Hero '{targetBLTHero.Name}' is not the leader of clan '{targetBLTHero.Clan.Name}'. Transfers must specify a clan leader.");
                    return;
                }

                targetClan = targetBLTHero.Clan;
            }
            else if (targetDEVHero != null)
            {
                if (targetDEVHero.Clan == null)
                {
                    onFailure($"Hero '{targetDEVHero.Name}' is not in a clan.");
                    return;
                }

                // The hero must be the leader of their clan per requirement
                if (targetDEVHero != targetDEVHero.Clan.Leader)
                {
                    onFailure($"Hero '{targetDEVHero.Name}' is not the leader of clan '{targetDEVHero.Clan.Name}'. Transfers must specify a clan leader.");
                    return;
                }

                targetClan = targetDEVHero.Clan;
            }
            else
            {
                // Try clan by name
                targetClan = FindClan(targetSpecifier);
                var targetBLTClan = FindClan("[BLT Clan]".Add(targetSpecifier, false));
                var safetargetBLTClan = FindClan("[BLT Clan] ".Add(targetSpecifier, false));

                if (targetClan == null)
                {
                    if (targetBLTClan != null)
                    {
                        targetClan = targetBLTClan;
                    }
                    else if (safetargetBLTClan != null)
                    {
                        targetClan = safetargetBLTClan;
                    }
                }

                if (targetClan == null)
                {
                    onFailure($"Could not find a hero or clan named '{targetSpecifier}'.");
                    return;
                }
            }

            // Permission checks for the issuer
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("Only clan leaders may initiate transfers.");
                return;
            }

            // Check ownership: only the owning clan (by default) or kingdom ruler (with force & config) may transfer
            var owningClan = targetSettlement.Owner.Clan;
            if (owningClan == null)
            {
                onFailure("This settlement has no owning clan and cannot be transferred.");
                return;
            }

            // If issuer's clan is the owner, allow them to transfer (they can give away their own fief)
            bool issuerOwnsSettlement = adoptedHero.Clan != null && adoptedHero.Clan == owningClan;

            // Kingdom-level allowance: if enabled, owner kingdom's ruler may transfer any owned settlement in their kingdom
            bool issuerIsOwnerKing = false;
            if (adoptedHero.Clan != null && adoptedHero.Clan.Kingdom != null)
            {
                var ownerKingdom = owningClan.Kingdom;
                if (ownerKingdom != null && ownerKingdom.Leader == adoptedHero)
                {
                    issuerIsOwnerKing = true;
                }
            }

            // If issuer neither owns the settlement nor is the owner-king allowed, block.
            if (!issuerOwnsSettlement && !issuerIsOwnerKing)
            {
                onFailure("You do not own this settlement, and you are not the ruler of the owning kingdom.");
                return;
            }

            // Kingdom crossing checks
            var owningKingdom = owningClan?.Kingdom;
            var targetKingdom = targetClan?.Kingdom;
            if (targetKingdom == null || owningKingdom == null)
            {
                onFailure("Either you or the target clan does not have a kingdom.");
                return;
            }

            bool kingdomsDiffer = false;
            if (owningKingdom != targetKingdom)
            {
                kingdomsDiffer = true;
            }

            if (kingdomsDiffer)
            {
                // If 'force' is used, ensure only owner kingdom rulers may force a transfer between kingdoms when config forbids non-forced cross-kingdom transfers.
                // !settings.AllowKingsToForceTransfers && !issuerIsOwnerKing
                if (settings.AllowKingsToForceTransfers)
                {
                    if (!issuerIsOwnerKing)
                    {
                        onFailure("Only the kingdom ruler can do cross-kingdom transfers.");
                        return;
                    }
                    if (!isForce)
                    {
                        onFailure("Cross-kingdom transfers require force transfers, use !transfer force.");
                        return;
                    }
                }
                else
                {
                    onFailure("Cross-kingdom transfers disabled.");
                    return;
                }
            }
            else
            {
                // same kingdom: 'force' may be used by owner king to override normal restrictions
                if (!issuerOwnsSettlement)
                {
                    if (issuerIsOwnerKing && !isForce)
                    {
                        onFailure("Kingdom ruler must use 'force' to move settlements that are not their clan's.");
                        return;
                    }
                }
            }

            // Final safety checks: cannot transfer to the same clan that already owns it
            if (owningClan == targetClan)
            {
                onFailure($"{targetSettlement.Name} is already owned by {targetClan.Name}.");
                return;
            }

            // All checks passed — perform transfer using native action
            try
            {
                // Choose the most appropriate ChangeOwner action:
                // If forced by king, use ApplyByKingDecision(Hero, Settlement)
                // Otherwise use ApplyByDefault(Hero, Settlement) — this keeps it simple and stable across versions.
                var newOwnerHero = targetClan.Leader;

                if (isForce)
                {
                    try
                    {
                        ChangeOwnerOfSettlementAction.ApplyByKingDecision(newOwnerHero, targetSettlement);
                    }
                    catch
                    {
                        // fallback to default apply if KingDecision isn't available for some reason
                        ChangeOwnerOfSettlementAction.ApplyByDefault(newOwnerHero, targetSettlement);
                    }
                }
                else
                {
                    try
                    {
                        // normal transfer — use ApplyByDefault
                        ChangeOwnerOfSettlementAction.ApplyByDefault(newOwnerHero, targetSettlement);
                    }
                    catch
                    {
                        // fallback: try ApplyByKingDecision if default fails for some reason
                        try
                        {
                            ChangeOwnerOfSettlementAction.ApplyByKingDecision(newOwnerHero, targetSettlement);
                        }
                        catch (Exception ex)
                        {
                            onFailure($"Transfer failed (apply error): {ex.Message}");
                            return;
                        }
                    }
                }

                // Success message
                string mode = isForce ? "forcibly " : "";
                onSuccess($"{adoptedHero.Name} {mode}transferred {targetSettlement.Name} to clan {targetClan.Name} (leader: {newOwnerHero.Name}).");
                Log.ShowInformation($"{adoptedHero.Name} {mode}transferred {targetSettlement.Name} to clan {targetClan.Name}.", adoptedHero.CharacterObject, Log.Sound.Notification1);
            }
            catch (Exception ex)
            {
                onFailure($"Transfer failed: {ex.Message}");
            }
        }

        // Helper: find settlement by exact case-insensitive name
        private Settlement FindSettlement(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return Settlement.All.FirstOrDefault(s => s?.Name?.ToString().Equals(name, StringComparison.OrdinalIgnoreCase) == true);
        }

        // Helper: best-effort find hero by name (search all known heroes)
        private Hero FindHero(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            try
            {
                // Use Hero.All if available (works on most versions). Fallback to AllAliveHeroes if not.
                var list = typeof(Hero).GetProperty("All") != null
                    ? (Hero[])typeof(Hero).GetProperty("All").GetValue(null)
                    : Hero.AllAliveHeroes?.ToArray();

                if (list != null)
                {
                    return list.FirstOrDefault(h => h != null && h.Name?.ToString().Equals(name, StringComparison.OrdinalIgnoreCase) == true);
                }
            }
            catch
            {
                // Last resort: try AllAliveHeroes
                try
                {
                    return Hero.AllAliveHeroes.FirstOrDefault(h => h != null && h.Name?.ToString().Equals(name, StringComparison.OrdinalIgnoreCase) == true);
                }
                catch { }
            }

            return null;
        }

        // Helper: find clan by name
        private Clan FindClan(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            try
            {
                return Clan.All.FirstOrDefault(c => c != null && c.Name?.ToString().Equals(name, StringComparison.OrdinalIgnoreCase) == true);
            }
            catch
            {
                // defensive fallback
                return Campaign.Current?.Clans?.FirstOrDefault(c => c != null && c.Name?.ToString().Equals(name, StringComparison.OrdinalIgnoreCase) == true);
            }
        }
    }
}
