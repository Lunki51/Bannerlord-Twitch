using System;
using System.Linq;
using System.Collections.Generic;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using BLTAdoptAHero;

namespace BLTAdoptAHero.Actions
{
    public class FormationCommand : HeroCommandHandlerBase
    {
        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
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
            string num = context.Args[0].ToString();
            var agent = adoptedHero.GetAgent();
            var currentFormation = agent.Formation;

            if (currentFormation == null)
            {
                onFailure("No formation");
                return;
            }

            var query = currentFormation.QuerySystem;
            FormationClass formType = query switch
            {
                _ when query.IsMeleeFormationReadOnly => FormationClass.Infantry,
                _ when query.IsRangedFormationReadOnly => FormationClass.Ranged,
                _ when query.IsCavalryFormationReadOnly => FormationClass.Cavalry,
                _ when query.IsRangedCavalryFormationReadOnly => FormationClass.HorseArcher,
                _ => FormationClass.Infantry
            };
            //FormationClass formType = currentFormation.FormationIndex;

            var allFormations = agent.Team.FormationsIncludingSpecialAndEmpty.Where(f => f.FormationIndex == formType && f.CountOfUnits > 0);

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
                string result = $"{formType} formation {position} out of {count}. It has {currentFormation.CountOfUnits} troops";
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

            currentFormation.RemoveUnit(agent);
            newformation.AddUnit(agent);

            onSuccess("Moved hero to new formation");
            return;
        }
    }
}