using BLTAdoptAHero.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace BLTAdoptAHero
{
    /// <summary>
    /// Manages vassal clan relationships with their master BLT clans.
    /// Ensures vassals follow their masters through kingdom changes, wars, and rebellions.
    /// Persisted: _vassalToMaster (vassal clan stringId -> master clan stringId)
    /// </summary>
    public class VassalBehavior : CampaignBehaviorBase
    {
        public static VassalBehavior Current { get; private set; }

        public static float MercenaryIncomeSharePercent { get; set; } = 0.25f; // 25% default

        // Persisted: maps vassal clan StringId to master clan StringId
        private Dictionary<string, string> _vassalToMaster = new();

        public VassalBehavior()
        {
            Current = this;
            Initialize();
        }

        private void Initialize()
        {
            _vassalToMaster ??= new Dictionary<string, string>();
        }

        public override void RegisterEvents()
        {
            Initialize();

            // Listen for clan kingdom changes
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(
                this, OnClanChangedKingdom);

            // Listen for war declarations
            CampaignEvents.WarDeclared.AddNonSerializedListener(
                this, WarDeclared);

            // Listen for peace made
            CampaignEvents.MakePeace.AddNonSerializedListener(
                this, MakePeace);

            // Listen for clan destroyed (cleanup)
            CampaignEvents.OnClanDestroyedEvent.AddNonSerializedListener(
                this, OnClanDestroyed);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("BLT_VassalToMaster", ref _vassalToMaster);
            Initialize();
        }

        // ----------------------
        // Public API
        // ----------------------

        /// <summary>
        /// Register a vassal clan as belonging to a master clan
        /// </summary>
        public void RegisterVassal(Clan vassalClan, Clan masterClan)
        {
            if (vassalClan == null || masterClan == null) return;

            string vassalKey = vassalClan.StringId;
            string masterKey = masterClan.StringId;

            _vassalToMaster[vassalKey] = masterKey;
        }

        /// <summary>
        /// Get the master clan for a vassal clan
        /// </summary>
        public Clan GetMasterClan(Clan vassalClan)
        {
            if (vassalClan == null) return null;

            if (_vassalToMaster.TryGetValue(vassalClan.StringId, out string masterKey))
            {
                return Clan.All.FirstOrDefault(c => c.StringId == masterKey);
            }

            return null;
        }

        /// <summary>
        /// Get all vassal clans for a master clan
        /// </summary>
        public List<Clan> GetVassalClans(Clan masterClan)
        {
            if (masterClan == null) return new List<Clan>();

            var vassals = new List<Clan>();
            string masterKey = masterClan.StringId;

            foreach (var kvp in _vassalToMaster)
            {
                if (kvp.Value == masterKey)
                {
                    var vassal = Clan.All.FirstOrDefault(c => c.StringId == kvp.Key);
                    if (vassal != null)
                    {
                        vassals.Add(vassal);
                    }
                }
            }

            return vassals;
        }

        /// <summary>
        /// Check if a clan is a vassal
        /// </summary>
        public bool IsVassal(Clan clan)
        {
            if (clan == null) return false;
            return _vassalToMaster.ContainsKey(clan.StringId);
        }

        /// <summary>
        /// Remove vassal relationship
        /// </summary>
        public void RemoveVassal(Clan vassalClan)
        {
            if (vassalClan == null) return;
            _vassalToMaster.Remove(vassalClan.StringId);
        }

        // ----------------------
        // Event Handlers
        // ----------------------

        private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
        {
            try
            {
                if (clan == null) return;

                InformationManager.DisplayMessage(new InformationMessage($"[DEBUG] OnClanChangedKingdom fired for {clan.Name}"));

                // Check if this clan has vassals that need to follow
                var vassals = GetVassalClans(clan);

                InformationManager.DisplayMessage(new InformationMessage($"[DEBUG] {clan.Name} has {vassals.Count} vassals"));

                if (vassals.Count > 0)
                {
                    foreach (var vassal in vassals)
                    {
                        try
                        {
                            InformationManager.DisplayMessage(new InformationMessage($"[DEBUG] Processing vassal {vassal.Name}, current kingdom: {vassal.Kingdom?.Name?.ToString() ?? "None"}, target kingdom: {newKingdom?.Name?.ToString() ?? "None"}"));

                            // Skip if vassal is already in the correct kingdom
                            if (vassal.Kingdom == newKingdom)
                            {
                                InformationManager.DisplayMessage(new InformationMessage($"[DEBUG] Vassal {vassal.Name} already in correct kingdom, skipping"));
                                continue;
                            }

                            InformationManager.DisplayMessage(new InformationMessage($"[DEBUG] Moving vassal {vassal.Name}, detail: {detail}"));

                            AdoptedHeroFlags._allowKingdomMove = true;

                            if (newKingdom == null)
                            {
                                InformationManager.DisplayMessage(new InformationMessage($"[DEBUG] Making vassal {vassal.Name} leave kingdom"));

                                if (vassal.IsUnderMercenaryService)
                                {
                                    vassal.EndMercenaryService(true);
                                }
                                vassal.ClanLeaveKingdom(true);
                            }
                            else if (detail == ChangeKingdomAction.ChangeKingdomActionDetail.JoinAsMercenary)
                            {
                                InformationManager.DisplayMessage(new InformationMessage($"[DEBUG] Making vassal {vassal.Name} join as mercenary"));

                                if (vassal.IsUnderMercenaryService)
                                {
                                    vassal.EndMercenaryService(true);
                                }

                                if (vassal.Kingdom != null && vassal.Kingdom != newKingdom)
                                {
                                    vassal.ClanLeaveKingdom(true);
                                }

                                ChangeKingdomAction.ApplyByJoinFactionAsMercenary(vassal, newKingdom, default, vassal.MercenaryAwardMultiplier);
                            }
                            else if (oldKingdom != null && newKingdom != null)
                            {
                                InformationManager.DisplayMessage(new InformationMessage($"[DEBUG] Making vassal {vassal.Name} switch kingdoms"));

                                if (vassal.IsUnderMercenaryService)
                                {
                                    vassal.EndMercenaryService(true);
                                }

                                if (vassal.Kingdom != null && vassal.Kingdom != newKingdom)
                                {
                                    vassal.ClanLeaveKingdom(true);
                                }

                                ChangeKingdomAction.ApplyByJoinToKingdom(vassal, newKingdom, default, false);

                                if (oldKingdom != null && newKingdom.IsAtWarWith(oldKingdom))
                                {
                                    if (!vassal.IsAtWarWith(oldKingdom))
                                    {
                                        DeclareWarAction.ApplyByDefault(vassal, oldKingdom);
                                    }
                                }
                            }
                            else if (oldKingdom == null && newKingdom != null)
                            {
                                InformationManager.DisplayMessage(new InformationMessage($"[DEBUG] Making vassal {vassal.Name} join kingdom"));

                                ChangeKingdomAction.ApplyByJoinToKingdom(vassal, newKingdom, default, false);
                            }

                            InformationManager.DisplayMessage(new InformationMessage($"[DEBUG] Vassal {vassal.Name} now in kingdom: {vassal.Kingdom?.Name?.ToString() ?? "None"}"));

                            AdoptedHeroFlags._allowKingdomMove = false;
                        }
                        catch (Exception ex)
                        {
                            InformationManager.DisplayMessage(
                                new InformationMessage($"[BLT Vassal] Error moving vassal {vassal.Name}: {ex.Message}")
                            );
                        }
                    }
                }

                // Check if this clan is a vassal that needs to follow master
                var masterClan = GetMasterClan(clan);
                if (masterClan != null)
                {
                    InformationManager.DisplayMessage(new InformationMessage($"[DEBUG] {clan.Name} is a vassal of {masterClan.Name}"));

                    // Vassal changed kingdom but should follow master
                    if (clan.Kingdom != masterClan.Kingdom)
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"[DEBUG] Vassal {clan.Name} in wrong kingdom, correcting..."));

                        try
                        {
                            AdoptedHeroFlags._allowKingdomMove = true;

                            if (masterClan.Kingdom == null)
                            {
                                if (clan.IsUnderMercenaryService)
                                {
                                    clan.EndMercenaryService(true);
                                }
                                clan.ClanLeaveKingdom(true);
                            }
                            else
                            {
                                if (clan.IsUnderMercenaryService)
                                {
                                    clan.EndMercenaryService(true);
                                }

                                if (clan.Kingdom != null)
                                {
                                    clan.ClanLeaveKingdom(true);
                                }

                                // Match master's mercenary status
                                if (masterClan.IsUnderMercenaryService)
                                {
                                    ChangeKingdomAction.ApplyByJoinFactionAsMercenary(clan, masterClan.Kingdom, default, clan.MercenaryAwardMultiplier);
                                }
                                else
                                {
                                    ChangeKingdomAction.ApplyByJoinToKingdom(clan, masterClan.Kingdom, default, false);
                                }
                            }

                            AdoptedHeroFlags._allowKingdomMove = false;
                        }
                        catch (Exception ex)
                        {
                            InformationManager.DisplayMessage(
                                new InformationMessage($"[BLT Vassal] Error correcting vassal {clan.Name}: {ex.Message}")
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(
                    new InformationMessage($"[BLT Vassal] OnClanChangedKingdom error: {ex.Message}")
                );
            }
        }

        /// <summary>
        /// Calculate bonus mercenary income for master from vassal's mercenary service
        /// This ADDS gold to the master, doesn't take from vassal (vassal is AI)
        /// </summary>
        public int CalculateVassalMercenaryBonus(Clan masterClan)
        {
            try
            {
                if (masterClan == null) return 0;

                var vassals = GetVassalClans(masterClan);
                if (vassals.Count == 0) return 0;

                int totalBonus = 0;

                foreach (var vassal in vassals)
                {
                    // Only calculate bonus if vassal is under mercenary service
                    if (vassal.IsUnderMercenaryService)
                    {
                        // Calculate what the vassal would earn (same as BLT calculation)
                        int vassalMercIncome = GoldIncomeAction.CalculateMercenaryIncome(vassal);

                        // Master gets a percentage as bonus
                        int bonus = (int)(vassalMercIncome * MercenaryIncomeSharePercent);
                        totalBonus += bonus;
                    }
                }

                return totalBonus;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(
                    new InformationMessage($"[BLT Vassal] CalculateVassalMercenaryBonus error: {ex.Message}")
                );
                return 0;
            }
        }

        private void WarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
        {
            try
            {
                // Check if faction1 is a master clan with vassals
                if (faction1 is Clan clan1)
                {
                    var vassals = GetVassalClans(clan1);
                    foreach (var vassal in vassals)
                    {
                        try
                        {
                            if (!vassal.IsAtWarWith(faction2))
                            {
                                DeclareWarAction.ApplyByDefault(vassal, faction2);
                            }
                        }
                        catch (Exception ex)
                        {
                            InformationManager.DisplayMessage(
                                new InformationMessage($"[BLT Vassal] Error declaring war for vassal {vassal.Name}: {ex.Message}")
                            );
                        }
                    }
                }

                // Check if faction2 is a master clan with vassals
                if (faction2 is Clan clan2)
                {
                    var vassals = GetVassalClans(clan2);
                    foreach (var vassal in vassals)
                    {
                        try
                        {
                            if (!vassal.IsAtWarWith(faction1))
                            {
                                DeclareWarAction.ApplyByDefault(vassal, faction1);
                            }
                        }
                        catch (Exception ex)
                        {
                            InformationManager.DisplayMessage(
                                new InformationMessage($"[BLT Vassal] Error declaring war for vassal {vassal.Name}: {ex.Message}")
                            );
                        }
                    }
                }

                // Check if master clan's kingdom declares war
                if (faction1 is Kingdom kingdom1)
                {
                    foreach (var clan in kingdom1.Clans)
                    {
                        var vassals = GetVassalClans(clan);
                        foreach (var vassal in vassals)
                        {
                            try
                            {
                                if (!vassal.IsAtWarWith(faction2))
                                {
                                    DeclareWarAction.ApplyByDefault(vassal, faction2);
                                }
                            }
                            catch { }
                        }
                    }
                }

                if (faction2 is Kingdom kingdom2)
                {
                    foreach (var clan in kingdom2.Clans)
                    {
                        var vassals = GetVassalClans(clan);
                        foreach (var vassal in vassals)
                        {
                            try
                            {
                                if (!vassal.IsAtWarWith(faction1))
                                {
                                    DeclareWarAction.ApplyByDefault(vassal, faction1);
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(
                    new InformationMessage($"[BLT Vassal] WarDeclared error: {ex.Message}")
                );
            }
        }

        private void MakePeace(IFaction faction1, IFaction faction2, MakePeaceAction.MakePeaceDetail detail)
        {
            try
            {
                // Check if faction1 is a master clan with vassals
                if (faction1 is Clan clan1)
                {
                    var vassals = GetVassalClans(clan1);
                    foreach (var vassal in vassals)
                    {
                        try
                        {
                            if (vassal.IsAtWarWith(faction2))
                            {
                                MakePeaceAction.Apply(vassal, faction2);
                            }
                        }
                        catch (Exception ex)
                        {
                            InformationManager.DisplayMessage(
                                new InformationMessage($"[BLT Vassal] Error making peace for vassal {vassal.Name}: {ex.Message}")
                            );
                        }
                    }
                }

                // Check if faction2 is a master clan with vassals
                if (faction2 is Clan clan2)
                {
                    var vassals = GetVassalClans(clan2);
                    foreach (var vassal in vassals)
                    {
                        try
                        {
                            if (vassal.IsAtWarWith(faction1))
                            {
                                MakePeaceAction.Apply(vassal, faction1);
                            }
                        }
                        catch (Exception ex)
                        {
                            InformationManager.DisplayMessage(
                                new InformationMessage($"[BLT Vassal] Error making peace for vassal {vassal.Name}: {ex.Message}")
                            );
                        }
                    }
                }

                // Check if master clan's kingdom makes peace
                if (faction1 is Kingdom kingdom1)
                {
                    foreach (var clan in kingdom1.Clans)
                    {
                        var vassals = GetVassalClans(clan);
                        foreach (var vassal in vassals)
                        {
                            try
                            {
                                if (vassal.IsAtWarWith(faction2))
                                {
                                    MakePeaceAction.Apply(vassal, faction2);
                                }
                            }
                            catch { }
                        }
                    }
                }

                if (faction2 is Kingdom kingdom2)
                {
                    foreach (var clan in kingdom2.Clans)
                    {
                        var vassals = GetVassalClans(clan);
                        foreach (var vassal in vassals)
                        {
                            try
                            {
                                if (vassal.IsAtWarWith(faction1))
                                {
                                    MakePeaceAction.Apply(vassal, faction1);
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(
                    new InformationMessage($"[BLT Vassal] MakePeace error: {ex.Message}")
                );
            }
        }

        private void OnClanDestroyed(Clan destroyedClan)
        {
            try
            {
                if (destroyedClan == null) return;

                // If destroyed clan was a vassal, remove it
                if (IsVassal(destroyedClan))
                {
                    RemoveVassal(destroyedClan);
                }

                // If destroyed clan was a master, remove all its vassals
                var vassals = GetVassalClans(destroyedClan);
                foreach (var vassal in vassals)
                {
                    RemoveVassal(vassal);
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(
                    new InformationMessage($"[BLT Vassal] OnClanDestroyed error: {ex.Message}")
                );
            }
        }
    }
}