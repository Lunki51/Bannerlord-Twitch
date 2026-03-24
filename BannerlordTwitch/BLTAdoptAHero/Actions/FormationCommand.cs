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
                generator.Value("Usage: !formation number");
                generator.Value("Format: [M-A-N]");
                generator.Value("M: C/A/R/H/F/M = Charge/Advance/Retreat/Hold/Follow/Move");
                generator.Value("A: LN/SH/LO/SQ/CI/CO/SC");
                generator.Value("N: distance to target");
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
            if (MissionHelpers.InTournament())
            {
                onFailure("Cannot change formation in tournament");
                return;
            }
            var splitArgs = context.Args.Split(' ');

            string num = context.Args.Length > 0 ? splitArgs[0].ToString() : "";

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
           
            var behavior = BLTHeroDetachmentBehavior.Current;
            var keywords = new[] { "detach", "attach", "charge", "hold", "mimic", "gate", "climb" };
            if (keywords.Contains(num))
            {
                
                if (behavior == null) { onFailure("Detachment system not active"); return; }

                string error = num switch
                {
                    "detach" => behavior.Detach(agent),
                    "attach" => behavior.Attach(agent),
                    "charge" => behavior.Charge(agent),
                    "hold" => behavior.Hold(agent),
                    "mimic" => behavior.Mimic(agent),
                    "gate" => behavior.TargetDoor(agent),
                    "walls" => behavior.Walls(agent),
                    _ => "Unknown command"
                };

                if (error != null) onFailure(error);
                else onSuccess($"{num} ok");
                return;
            }

            if (num == "front" || num == "back")
            {
                if (behavior.IsDetached(agent))
                {
                    onFailure("Reattach before moving");
                    return;
                }
                SetHeroFormationPosition(agent, num, onSuccess, onFailure);
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
                var allFormations = agent.Team.FormationsIncludingSpecialAndEmpty
                    .Where(f => f.PhysicalClass == formType && f.CountOfUnits > 0)
                    .OrderBy(f => f.Index);

                var indexes = allFormations.Select(f => f.Index).OrderBy(i => i).ToList();

                var sb = new StringBuilder();
                int number = 1;

                foreach (var f in allFormations)
                {
                    int troops = f.CountOfUnits;
                    string order = BuildCompact(f);
                    sb.Append($"{number}:{troops}[{order}], ");
                    number++;
                }

                int count = indexes.Count;
                int position = indexes.IndexOf(currentFormation.Index) + 1;

                if (string.IsNullOrEmpty(num) || !int.TryParse(num, out int numb))
                {
                    onSuccess($"{formType} {position}/{count} {currentFormation.CountOfUnits} | {sb}");
                    return;
                }
                if (behavior.IsDetached(agent))
                {
                    onFailure("Reattach before changing formations");
                    return;
                }
                if (numb > count || numb <= 0)
                {
                    onFailure("Invalid number");
                    return;
                }

                var newformation = allFormations.ElementAt(numb - 1);
                TransferHeroToFormation(agent, newformation);

                onSuccess($"Moved. {newformation.CountOfUnits} troops");
            }
            else
            {
                var allFormations = agent.Team.FormationsIncludingSpecialAndEmpty
                    .Where(f => f.CountOfUnits > 0)
                    .OrderBy(f => f.Index);

                var indexes = allFormations.Select(f => f.Index).OrderBy(i => i).ToList();

                var sb = new StringBuilder();
                int number = 1;

                foreach (var f in allFormations)
                {
                    var q = f.QuerySystem;
                    string type = q switch
                    {
                        _ when q.IsInfantryFormationReadOnly => "Infantry",
                        _ when q.IsRangedFormationReadOnly => "Ranged",
                        _ when q.IsCavalryFormationReadOnly => "Cavalry",
                        _ when q.IsRangedCavalryFormationReadOnly => "Horse archer",
                        _ => "unknown"
                    };

                    int troops = f.CountOfUnits;
                    string order = BuildCompact(f);

                    sb.Append($"{number}:{type}({troops})[{order}], ");
                    number++;
                }

                int count = indexes.Count;
                int position = indexes.IndexOf(currentFormation.Index) + 1;

                if (string.IsNullOrEmpty(num) || !int.TryParse(num, out int numb))
                {
                    onSuccess($"{formType} {position}/{count} {currentFormation.CountOfUnits} | {sb}");
                    return;
                }
                if (behavior.IsDetached(agent))
                {
                    onFailure("Reattach before changing formations");
                    return;
                }
                if (numb > count || numb <= 0)
                {
                    onFailure("Invalid number");
                    return;
                }

                var newformation = allFormations.ElementAt(numb - 1);
                TransferHeroToFormation(agent, newformation);

                onSuccess("Moved.");
            }
        }

        private void TransferHeroToFormation(Agent heroAgent, Formation target)
        {
            if (heroAgent == null || target == null) return;

            var oldFormation = heroAgent.Formation;
            heroAgent.Formation = target;

            oldFormation?.Team.TriggerOnFormationsChanged(oldFormation);
            target.Team.TriggerOnFormationsChanged(target);

            Log.Trace($"{heroAgent.Name} transferred to {target.FormationIndex.GetName()}");
        }


        string BuildCompact(Formation f)
        {
            var m = f.GetReadonlyMovementOrderReference().OrderEnum;
            var a = f.ArrangementOrder.OrderEnum;

            string dist = "";
            if (f.TargetFormation != null)
            {
                var q = f.TargetFormation.QuerySystem;
                var myPos = f.CachedAveragePosition;
                var targetPos = f.TargetFormation.CachedAveragePosition;
                float pos = (targetPos - myPos).Length;
                string type = q switch
                {
                    _ when q.IsInfantryFormationReadOnly => "Infantry",
                    _ when q.IsRangedFormationReadOnly => "Ranged",
                    _ when q.IsCavalryFormationReadOnly => "Cavalry",
                    _ when q.IsRangedCavalryFormationReadOnly => "Horse archer",
                    _ => "unknown"
                };

                dist += $"-Target:{type}-{pos:0}";
            }

            return $"{M(m)}-{A(a)}{dist}";
        }

        string M(MovementOrder.MovementOrderEnum o) => o switch
        {
            MovementOrder.MovementOrderEnum.Charge => "Charge",
            MovementOrder.MovementOrderEnum.ChargeToTarget => "Charge",
            MovementOrder.MovementOrderEnum.Advance => "Advance",
            MovementOrder.MovementOrderEnum.FallBack => "Retreat",
            MovementOrder.MovementOrderEnum.Retreat => "Retreat",
            MovementOrder.MovementOrderEnum.Invalid => "Hold",
            MovementOrder.MovementOrderEnum.Stop => "Hold",
            MovementOrder.MovementOrderEnum.Follow => "Follow",
            MovementOrder.MovementOrderEnum.FollowEntity => "Follow",
            MovementOrder.MovementOrderEnum.Move => "Move",
            _ => "?"
        };

        string A(ArrangementOrder.ArrangementOrderEnum o) => o switch
        {
            ArrangementOrder.ArrangementOrderEnum.Line => "Line",
            ArrangementOrder.ArrangementOrderEnum.ShieldWall => "Wall",
            ArrangementOrder.ArrangementOrderEnum.Loose => "Loose",
            ArrangementOrder.ArrangementOrderEnum.Square => "Square",
            ArrangementOrder.ArrangementOrderEnum.Circle => "Circle",
            ArrangementOrder.ArrangementOrderEnum.Column => "Column",
            ArrangementOrder.ArrangementOrderEnum.Scatter => "Scatter",
            _ => "--"
        };

        private void SetHeroFormationPosition(Agent heroAgent, string position, Action<string> onSuccess, Action<string> onFailure)
        {
            var formation = heroAgent.Formation;
            if (formation == null) { onFailure("No formation"); return; }

            var unit = heroAgent as IFormationUnit;
            if (unit == null) { onFailure("Not a formation unit"); return; }

            var arrangement = formation.Arrangement;

            try
            {
                switch (position.ToLowerInvariant())
                {
                    case "front":
                        {
                            var candidate = arrangement.GetAllUnits()
                                .Select(u => u as Agent)
                                .Where(a => a != null && a != heroAgent && a.GetHero() == null)
                                .OrderBy(a => ((IFormationUnit)a).FormationRankIndex)
                                .ThenBy(a => ((IFormationUnit)a).FormationFileIndex)
                                .FirstOrDefault();

                            if (candidate == null) { onFailure("No troop found"); break; }

                            arrangement.SwitchUnitLocationsWithUnpositionedUnit(candidate, unit);
                            onSuccess($"Moved to front");
                            break;
                        }

                    case "back":
                        {
                            var candidate = arrangement.GetAllUnits()
                                .Select(u => u as Agent)
                                .Where(a => a != null && a != heroAgent && a.GetHero() == null)
                                .OrderByDescending(a => ((IFormationUnit)a).FormationRankIndex)
                                .ThenBy(a => ((IFormationUnit)a).FormationFileIndex)
                                .FirstOrDefault();

                            if (candidate == null) { onFailure("No troop found"); break; }

                            arrangement.SwitchUnitLocationsWithUnpositionedUnit(candidate, unit);
                            onSuccess($"Moved to back");
                            break;
                        }
                }
            }
            catch (Exception e)
            {
                onFailure($"Formation type does not support this operation ({e.Message})");
            }
        }
    }
}
