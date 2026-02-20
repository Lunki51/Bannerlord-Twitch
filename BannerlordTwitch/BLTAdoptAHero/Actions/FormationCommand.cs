using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using BLTAdoptAHero;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=TESTING}FormationCommand"),
     LocDescription("{=TESTING}Show and change hero formation"),
     UsedImplicitly]
    public class FormationCommand : HeroCommandHandlerBase
    {
        public class Settings : IDocumentable
        {
            [LocDisplayName("{=TESTING}Respect class"),
             LocCategory("General", "{=TESTING}General"),
             LocDescription("{=TESTING}Turn off to allow any formation otherwise infantry can only change to other infantry formations"),
             PropertyOrder(1), UsedImplicitly]
            public bool Filter { get; set; } = true;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.Value($"Usage: !formation number");
            }
        }

        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) return;
            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }

            if (Mission.Current == null)
            {
                onFailure("{=TESTING}No mission!".Translate());
                return;
            }
            if (Mission.Current.IsNavalBattle)
            {
                onFailure("Cannot change formation in naval battle");
                return;
            }
            string num = "";
            if (context.Args.Length > 0)
                num = context.Args[0].ToString();

            var agent = adoptedHero.GetAgent();
            if (agent == null)
            {
                onFailure("No hero");
                return;
            }
            Formation currentFormation = agent.Formation;

            if (currentFormation == null)
            {
                onFailure("No formation");
                return;
            }

            var query = currentFormation.QuerySystem;
            FormationClass formType = query switch
            {
                _ when query.IsInfantryFormationReadOnly => FormationClass.Infantry,
                _ when query.IsRangedFormationReadOnly => FormationClass.Ranged,
                _ when query.IsCavalryFormationReadOnly => FormationClass.Cavalry,
                _ when query.IsRangedCavalryFormationReadOnly => FormationClass.HorseArcher,
                _ => FormationClass.Infantry
            };
            if (settings.Filter)
            {
                IEnumerable<Formation> allFormations = agent.Team.FormationsIncludingSpecialAndEmpty.Where(f => f.PhysicalClass == formType && f.CountOfUnits > 0).OrderBy(f => f.Index);
                List<int> indexes = new();
                var sb = new StringBuilder();
                int number = 1;
                foreach (var f in allFormations)
                {
                    int troops = f.CountOfUnits;
                    var order = f.GetMovementState();
                    sb.Append($"{number}: {troops}({order}), ");
                    number++;
                }

                foreach (var a in allFormations)
                {
                    indexes.Add(a.Index);
                }
                indexes = indexes.OrderBy(i => i).ToList();

                int count = indexes.Count;
                int position = indexes.IndexOf(currentFormation.Index) + 1;

                if (string.IsNullOrEmpty(num) || !int.TryParse(num, out int numb))
                {
                    string result = $"{formType} formation {position} out of {count}. {currentFormation.CountOfUnits} troops | {sb}";
                    onSuccess(result);
                    return;
                }

                if (numb > count || numb <= 0)
                {
                    onFailure("Invalid number");
                    return;
                }

                var newIndex = indexes[numb - 1];

                var newformation = allFormations.FirstOrDefault(f => f.Index == newIndex);

                TransferHeroToFormation(agent, newformation);

                onSuccess($"Moved hero to new formation. It has {newformation.CountOfUnits} troops");
                return;
            }
            else
            {
                IEnumerable<Formation> allFormations = agent.Team.FormationsIncludingSpecialAndEmpty.Where(f => f.CountOfUnits > 0).OrderBy(f => f.Index);
                //IEnumerable<Formation> infantries = agent.Team.FormationsIncludingSpecialAndEmpty.Where(f => f.CountOfUnits > 0 && f.PhysicalClass == FormationClass.Infantry);
                //IEnumerable<Formation> ranged = agent.Team.FormationsIncludingSpecialAndEmpty.Where(f => f.CountOfUnits > 0 && f.PhysicalClass == FormationClass.Ranged);
                //IEnumerable<Formation> cavalries = agent.Team.FormationsIncludingSpecialAndEmpty.Where(f => f.CountOfUnits > 0 && f.PhysicalClass == FormationClass.Cavalry);
                //IEnumerable<Formation> horsearchers = agent.Team.FormationsIncludingSpecialAndEmpty.Where(f => f.CountOfUnits > 0 && f.PhysicalClass == FormationClass.HorseArcher);

                var sb = new StringBuilder();
                int number = 1;
                foreach (var f in allFormations)
                {
                    var q = f.QuerySystem;
                    string type = q switch
                    {
                        _ when q.IsInfantryFormationReadOnly && q.IsRangedFormationReadOnly => "Mixed",
                        _ when q.IsInfantryFormationReadOnly => "Infantry",
                        _ when q.IsRangedFormationReadOnly => "Ranged",
                        _ when q.IsCavalryFormationReadOnly && q.IsRangedCavalryFormationReadOnly => "Horse mixed",
                        _ when q.IsCavalryFormationReadOnly => "Cavalry",
                        _ when q.IsRangedCavalryFormationReadOnly => "Horse archer",
                        _ => "unknown"
                    };
                    var order = f.GetMovementState();
                    int troops = f.CountOfUnits;
                    sb.Append($"{number}: {type}({troops},{order}), ");
                    number++;
                }
                List<int> indexes = new();
                foreach (var a in allFormations)
                {
                    indexes.Add(a.Index);
                }
                indexes = indexes.OrderBy(i => i).ToList();

                int count = indexes.Count;
                int position = indexes.IndexOf(currentFormation.Index) + 1;

                if (string.IsNullOrEmpty(num) || !int.TryParse(num, out int numb))
                {
                    string result = $"{formType} formation {position} | {currentFormation.CountOfUnits} troops | {sb}";
                    onSuccess(result);
                    return;
                }

                if (numb > count || numb <= 0)
                {
                    onFailure("Invalid number");
                    return;
                }

                var newIndex = indexes[numb - 1];

                var newformation = allFormations.FirstOrDefault(f => f.Index == newIndex);

                var newquery = newformation.QuerySystem;
                string newformType = newquery switch
                {
                    _ when newquery.IsInfantryFormationReadOnly && newquery.IsRangedFormationReadOnly => "Mixed",
                    _ when newquery.IsInfantryFormationReadOnly => "Infantry",
                    _ when newquery.IsRangedFormationReadOnly => "Ranged",
                    _ when newquery.IsCavalryFormationReadOnly && newquery.IsRangedCavalryFormationReadOnly => "Horse mixed",
                    _ when newquery.IsCavalryFormationReadOnly => "Cavalry",
                    _ when newquery.IsRangedCavalryFormationReadOnly => "Horse archer",
                    _ => "unknown"
                };

                TransferHeroToFormation(agent, newformation);

                onSuccess($"Moved hero to new formation({newformType})");
                return;
            }         
        }

        private void TransferHeroToFormation(Agent heroAgent, Formation target)
        {
            if (heroAgent == null || target == null) return;

            Formation oldFormation = heroAgent.Formation;
            heroAgent.Formation = target;

            if (oldFormation != null)
            {
                oldFormation.Team.TriggerOnFormationsChanged(oldFormation);
            }
            target.Team.TriggerOnFormationsChanged(target);

            Log.Trace($"{heroAgent.Name} transferred to {target.FormationIndex.GetName()}");

        }
    }
}