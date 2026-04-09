using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Annotations;
using BLTAdoptAHero.Actions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using System.ComponentModel.DataAnnotations;
using BLTAdoptAHero.Behaviors;
namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("Vassal Management"),
     LocDescription("Allow viewer to manage their vassals"),
     UsedImplicitly]
    public class VassalManagement : HeroCommandHandlerBase
    {
        [CategoryOrder("Vassal", 1)]
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Vassal", "{=TESTING}Vassal"),
             LocDescription("{=TESTING}Enable viewer create vassal"),
             PropertyOrder(1), UsedImplicitly]
            public bool VassalEnabled { get; set; } = true;

            [LocDisplayName("{=BLT_MaxVassals}Max vassal"),
             LocCategory("Vassal", "{=TESTING}Vassal"),
             LocDescription("{=BLT_MaxVassalsDesc}Max vassal clans"),
             PropertyOrder(2), UsedImplicitly]
            public int VassalAmount { get; set; } = 3;

            [LocDisplayName("{=6PUxQuLg}Gold Cost"),
             LocCategory("Vassal", "{=TESTING}Vassal"),
             LocDescription("{=TESTING}Cost of creating a vassal clan"),
             PropertyOrder(3), UsedImplicitly]
            public int VassalPrice { get; set; } = 250000;

            [LocDisplayName("{=TESTING}Vassal Merc Income Share %"),
             LocCategory("Vassal", "{=TESTING}Vassal"),
             LocDescription("{=TESTING}Percentage of vassal mercenary income shared with master (0.0 - 2.0, 0.25 = 25%)"),
             PropertyOrder(4), UsedImplicitly,
             Range(0f, 2f)]
            public float VassalMercIncomeShare { get; set; } = 0.25f; // 25% default

            [LocDisplayName("{=TESTING}Vassal Fief Income Share %"),
             LocCategory("Vassal", "{=TESTING}Vassal"),
             LocDescription("{=TESTING}Percentage of vassal fief income shared with master (0.0 - 2.0, 0.25 = 25%)"),
             PropertyOrder(5), UsedImplicitly,
             Range(0f, 2f)]
            public float VassalFiefIncomeShare { get; set; } = 0.25f; // 25% default

            [LocDisplayName("{=TESTING}King Vassals Only"),
             LocCategory("Vassal", "{=TESTING}Vassal"),
             LocDescription("{=TESTING}Prevents anyone except kings to create vassal clans"),
             PropertyOrder(6), UsedImplicitly]
            public bool KingVassalsOnly { get; set; } = false;

            [LocDisplayName("{=TESTING}Vassals tied to kingdom"),
             LocCategory("Vassal", "{=TESTING}Vassal"),
             LocDescription("{=TESTING}Should vassal be tied to kingdom(On kingdom leave/destruction, vassals get destroyed and members go back to main clan)"),
             PropertyOrder(7), UsedImplicitly]
            public bool KingdomVassals { get; set; } = false;

            public void GenerateDocumentation(IDocumentationGenerator generator) { }
        }

        public override Type HandlerConfigType => typeof(Settings);


        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) return;

            if (VassalBehavior.Current == null)
            {
                onFailure("Vassal behavior not initialized");
                return;
            }
            
            VassalBehavior.MercenaryIncomeSharePercent = settings.VassalMercIncomeShare;
            VassalBehavior.FiefIncomeSharePercent = settings.VassalFiefIncomeShare;
            var behavior = VassalBehavior.Current;
            
            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }
            if (Mission.Current != null)
            {
                onFailure("{=CRCwDnag}You cannot manage your vassals, as a mission is active!".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=DYgac2Ut}You cannot manage your kingdom, as you are not in a clan".Translate());
                return;
            }

            if (context.Args.IsEmpty())
            {
                if (adoptedHero.Clan.Kingdom == null)
                {
                    onFailure("{=EJ4Pd2Lg}Your clan is not in a Kingdom".Translate());
                    return;
                }
                string vassals = string.Join(",", behavior.GetVassalClans(adoptedHero.Clan).Select(c => c.Name));
                onSuccess($"Vassals:{vassals}");
                return;
            }

            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=jQZ93EID}You are not the leader of your clan".Translate());
                return;
            }
            

            var splitArgs = context.Args.Split(' ');
            var command = splitArgs[0];       
            var desiredName = string.Join(" ", splitArgs.Skip(1)).Trim();

            switch (command.ToLower())
            {
                case "create":
                    CreateVassalCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "rename":
                    RenameVassalCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "banner":
                    HandleBannerCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                default:
                    break;
            }

        }

        private void CreateVassalCommand(Settings settings, Hero adoptedHero, string args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.VassalEnabled)
            {
                onFailure("Vassal creation is disabled");
                return;
            }

            var splitargs = args.Split(' ');
            var childName = splitargs[0];
            var setname = string.Join(" ", splitargs.Skip(1)).Trim();

            if (settings.KingVassalsOnly && adoptedHero.Clan.Kingdom.Leader != adoptedHero)
            {
                onFailure("{=GEGrsLPm}You must be a king to create vassals".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom == null)
            {
                onFailure("{=RvkJO6J9}Your clan is not in a kingdom".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=HS14GdUa}You cannot manage your kingdom, as you are not your clans leader!".Translate());
                return;
            }
            if (string.IsNullOrWhiteSpace(childName) || string.IsNullOrWhiteSpace(setname))
            {
                onFailure("{=ETfJQatX}Usage: (vassal) (hero name) (clan name)".Translate());
                return;
            }
            var existingClan = Clan.All.FirstOrDefault(c => c.Name.ToString().ToLower() == setname.ToLower() || c.Name.ToString().ToLower() == $"[vassal] {setname.ToLower()}" || c.Name.ToString().ToLower() == $"[blt clan] {setname.ToLower()}");
            if (existingClan != null)
            {
                onFailure("{=TESTING}A clan with the name {name} already exists".Translate(("name", setname)));
                return;
            }
            if (VassalBehavior.Current.GetVassalClans(adoptedHero.Clan).Count >= (settings.VassalAmount + UpgradeBehavior.Current.GetTotalMaxVassalsBonus(adoptedHero.Clan)))
            {
                onFailure($"Max vassals: {settings.VassalAmount + UpgradeBehavior.Current.GetTotalMaxVassalsBonus(adoptedHero.Clan)}");
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.VassalPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.VassalPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }


            Hero vassal = adoptedHero.Clan.Heroes.Find(h => h.FirstName.ToString().ToLower() == childName.ToLower());

            if (vassal == null)
            {
                onFailure($"No hero named {childName}");
                return;
            }
            if (vassal.Age < 18)
            {
                onFailure($"{childName} is too young");
                return;
            }
            if (vassal.Spouse != null && vassal.Spouse.IsAdopted())
            {
                onFailure("Cannot vassal a blt spouse");
                return;
            }
            if (vassal.IsAdopted())
            {
                onFailure("Cannot vassal a blt");
                return;
            }
            var heir = Campaign.Current.GetCampaignBehavior<BLTHeirBehavior>();
            if (heir !=null && heir._heirs.Contains(vassal))
            {
                onFailure("Cannot vassal a heir");
                return;
            }
            if (vassal.IsPrisoner)
            {
                onFailure($"{childName} is prisoner");
                return;
            }
            if (vassal.HeroState == Hero.CharacterStates.Fugitive || vassal.HeroState == Hero.CharacterStates.Released || vassal.HeroState == Hero.CharacterStates.Traveling)
            {
                onFailure($"{childName} is busy");
                return;
            }
            if (vassal.Spouse == null)
            {
                HeroFeatures.SpawnSpouse(vassal, vassal.Culture);
            }
            if (vassal.GovernorOf != null)
            {
                ChangeGovernorAction.RemoveGovernorOf(vassal);
            }
            if (vassal.PartyBelongedTo != null)
            {
                var oldParty = vassal.PartyBelongedTo;
                bool wasLeader = oldParty.LeaderHero == vassal;
                oldParty.MemberRoster.RemoveTroop(vassal.CharacterObject, 1, default(UniqueTroopDescriptor), 0);
                MakeHeroFugitiveAction.Apply(vassal, false);
                if (wasLeader && oldParty.IsLordParty)
                    DisbandPartyAction.StartDisband(oldParty);
            }
            var fullClanName = $"[Vassal] {setname}";
            var newClan = Clan.CreateClan(fullClanName);
            newClan.ChangeClanName(new TextObject(fullClanName), new TextObject(fullClanName));
            newClan.Culture = vassal.Culture;
            newClan.Banner = Banner.CreateOneColoredBannerWithOneIcon(adoptedHero.Clan.Banner.GetPrimaryColor(), adoptedHero.Clan.Banner.GetFirstIconColor(), -1);
            newClan.SetInitialHomeSettlement(Settlement.All.SelectRandom());
            vassal.Clan = newClan;
            if (vassal.Spouse != null)
            {
                if (vassal.Spouse.GovernorOf != null)
                {
                    ChangeGovernorAction.RemoveGovernorOf(vassal.Spouse);
                }
                if (vassal.Spouse.PartyBelongedTo != null)
                {
                    var oldParty = vassal.Spouse.PartyBelongedTo;
                    bool wasLeader = oldParty.LeaderHero == vassal.Spouse;
                    oldParty.MemberRoster.RemoveTroop(vassal.Spouse.CharacterObject, 1, default(UniqueTroopDescriptor), 0);
                    MakeHeroFugitiveAction.Apply(vassal.Spouse, false);
                    if (wasLeader && oldParty.IsLordParty)
                        DisbandPartyAction.StartDisband(oldParty);
                }
                vassal.Spouse.Clan = newClan;
            }
            if (vassal.Children.Count > 0)
            {
                foreach (Hero child in vassal.Children)
                {
                    if (child.GovernorOf != null)
                    {
                        ChangeGovernorAction.RemoveGovernorOf(child);
                    }
                    if (child.PartyBelongedTo != null)
                    {
                        var oldParty = child.PartyBelongedTo;
                        bool wasLeader = oldParty.LeaderHero == child;
                        oldParty.MemberRoster.RemoveTroop(child.CharacterObject, 1, default(UniqueTroopDescriptor), 0);
                        MakeHeroFugitiveAction.Apply(child, false);
                        if (wasLeader && oldParty.IsLordParty)
                            DisbandPartyAction.StartDisband(oldParty);
                    }
                    child.Clan = newClan;
                }
            }
            var tierModel = Campaign.Current.Models.ClanTierModel;
            newClan.AddRenown(tierModel.GetRequiredRenownForTier(tierModel.CompanionToLordClanStartingTier));
            newClan.SetLeader(vassal);
            newClan.IsNoble = true;
            vassal.Gold += 50000;
            if (adoptedHero.Clan.Kingdom != null)
            {
                AdoptedHeroFlags._allowKingdomMove = true;
                if (adoptedHero.Clan.IsUnderMercenaryService)
                    ChangeKingdomAction.ApplyByJoinFactionAsMercenary(newClan, adoptedHero.Clan.Kingdom);
                else
                    ChangeKingdomAction.ApplyByJoinToKingdom(newClan, adoptedHero.Clan.Kingdom);
                AdoptedHeroFlags._allowKingdomMove = false;
            }
            CampaignEventDispatcher.Instance.OnClanCreated(newClan, false);
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(adoptedHero, vassal, 100, false);

            // Register the vassal with the VassalBehavior
            VassalBehavior.Current?.RegisterVassal(newClan, adoptedHero.Clan);

            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.VassalPrice, true);
            string response = $"Vassal created by {adoptedHero.FirstName}: {newClan.Name}";
            Log.LogFeedResponse(response);
        }

        private void RenameVassalCommand(Settings settings, Hero adoptedHero, string args, Action<string> onSuccess, Action<string> onFailure)
        {
            var vassalList = VassalBehavior.Current.GetVassalClans(adoptedHero.Clan);
            if (vassalList == null || vassalList.Count == 0)
            {
                onFailure("Your clan has no vassals");
                return;
            }

            var splitArgs = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (splitArgs.Length < 2)
            {
                onFailure("Usage: rename (vassal number) (new name)");
                return;
            }

            if (!int.TryParse(splitArgs[0], out int vas))
            {
                onFailure("Not a number");
                return;
            }
            if (vas <= 0 || vas > vassalList.Count)
            {
                onFailure("Invalid number");
                return;
            }
            Clan vassal = vassalList[vas - 1];
            string oldName = vassal.Name.ToString();
            string newName = string.Join(" ", splitArgs.Skip(1));
            string fullClanName = $"[Vassal] {newName}";
            
            vassal.ChangeClanName(new TextObject(fullClanName), new TextObject(fullClanName));

            onSuccess($"Renamed vassal {oldName} to {fullClanName}");
        }

        private Dictionary<Clan, string> _bannerBuffer = new();
        private void HandleBannerCommand(Settings settings, Hero adoptedHero, string args, Action<string> onSuccess, Action<string> onFailure)
        {
            var vassalList = VassalBehavior.Current.GetVassalClans(adoptedHero.Clan);
            if (vassalList == null || vassalList.Count == 0)
            {
                onFailure("Your clan has no vassals");
                return;
            }

            var splitArgs = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (splitArgs.Length < 2)
            {
                onFailure("Usage: banner (vassal name) (bannerCode)");
                return;
            }

            // last element manually
            string bannerCode = splitArgs[splitArgs.Length - 1];

            // everything except last
            string name = string.Join(" ", splitArgs, 0, splitArgs.Length - 1);
            Clan vassal = vassalList.FirstOrDefault(v => v.Name.ToString().IndexOf(name, StringComparison.InvariantCultureIgnoreCase) >= 0);
            if (vassal == null)
            {
                onFailure($"No vassal named {name}");
                return;
            }

            if (string.IsNullOrWhiteSpace(bannerCode))
            {
                onFailure("{=PSDbhv3a}Make your banner at https://bannerlord.party/banner and paste it directly".Translate());
                return;
            }
            if (bannerCode == "start")
            {
                if (_bannerBuffer.ContainsKey(vassal))
                {
                    onFailure("Banner input already started");
                    return;
                }

                _bannerBuffer[vassal] = "";
                onSuccess("Banner input started. Send lines. Use 'end' to finish.");
                return;
            }
            if (bannerCode == "end")
            {
                if (!_bannerBuffer.TryGetValue(vassal, out string stored))
                {
                    onFailure("Banner input was not started");
                    return;
                }

                bannerCode = stored;
                _bannerBuffer.Remove(vassal);
            }
            else if (_bannerBuffer.TryGetValue(vassal, out string current))
            {
                _bannerBuffer[vassal] = current + bannerCode;
                onSuccess("Line added");
                return;
            }
            try
            {
                if (Banner.TryGetBannerDataFromCode(bannerCode, out var bannerDataList))
                {
                    var newBanner = new Banner(bannerCode);  // creates a Banner object directly from the code                  
                    var color1 = newBanner.GetPrimaryColor();
                    var color2 = newBanner.GetFirstIconColor();


                    vassal.Banner = newBanner;
                    vassal.Banner.ChangeBackgroundColor(color1, color2);
                    vassal.Color = color1;
                    vassal.Color2 = color2;

                }
                onSuccess("{=BiiO7KQx}Banner updated successfully!".Translate());
            }
            catch (Exception ex)
            {
                onFailure($"Failed to update banner: {ex.Message}");
            }
        }
    }
}
