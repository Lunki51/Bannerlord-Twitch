using System;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero
{

	[LocDisplayName("{=YDcnEEbS}Rejuvenate")]
	[LocDescription("{=22nn0uG5}Rejuvenate your hero")]
	[UsedImplicitly]
	public class Rejuvenate : ActionHandlerBase
	{

		protected override Type ConfigType
		{
			get
			{
				return typeof(Rejuvenate.Settings);
			}
		}

		protected override void ExecuteInternal(ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
		{
			Rejuvenate.Settings settings = (Rejuvenate.Settings)config;
			Hero adoptedHero = BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(context.UserName);
			if (adoptedHero == null)
			{
				onFailure(AdoptAHero.NoHeroMessage);
				return;
			}
			if (Mission.Current != null)
			{
				onFailure("{=wkhZ6q7b}You cannot rejuvenate, as a mission is active!".Translate());
				return;
			}
			int heroGold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero);
			if (heroGold < settings.Price)
			{
				onFailure("{=Z4vYZzSq}Not enough gold !".Translate());
				return;
			}
			double num = Math.Truncate((double)(adoptedHero.Age - (float)settings.Age));
			if (num < (double)Campaign.Current.Models.AgeModel.BecomeChildAge)
			{
				onFailure("{=yWo2v3yu}You cannot rejuvenate bellow child age".Translate());
				return;
			}
			if (adoptedHero.Age < 18)
            {
                onFailure("{=yWo2v3yu}You cannot rejuvenate bellow child age".Translate());
                return;
            }
			BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.Price, true);
			adoptedHero.SetBirthDay(adoptedHero.BirthDay + CampaignTime.Years((float)settings.Age));
			onSuccess("{=XidEZXAO}Your rejuvenated of {Age} years you are now {newAge}".Translate(new ValueTuple<string, object>[]
			{
				new ValueTuple<string, object>("Age", settings.Age),
				new ValueTuple<string, object>("newAge", num)
			}));
		}

		public Rejuvenate()
		{
		}

		public class Settings : IDocumentable
		{

			[LocDisplayName("{=7WIjNgF2}Price")]
			[LocDescription("{=QaK58Z3j}The price of the rejuvenation")]
			[PropertyOrder(1)]
			[ExpandableObject]
			[Expand]
			[UsedImplicitly]
			public int Price { get; set; } = 10000;

			[LocDisplayName("{=eyrNUsxM}Age")]
			[LocDescription("{=oyzYoByT}The age that will be substracted from the hero.")]
			[PropertyOrder(2)]
			[UsedImplicitly]
			public int Age { get; set; } = 1;

			public void GenerateDocumentation(IDocumentationGenerator generator)
			{
				generator.PropertyValuePair("Age".Translate(), string.Format("{0}", this.Age));
				generator.PropertyValuePair("Price".Translate(), string.Format("{0}", this.Price));
			}

			public Settings()
			{
			}
		}
	}
}
