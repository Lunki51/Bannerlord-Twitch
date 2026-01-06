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
            [LocDescription("{=oyzYoByT}The age that will be substracted from the hero")]
            [PropertyOrder(2)]
            [UsedImplicitly]
            public int Age { get; set; } = 1;

            [LocDisplayName("{=TESTING}Spouse")]
            [LocDescription("{=TESTING}Should spouse de-age with the hero")]
            [PropertyOrder(3)]
            [UsedImplicitly]
            public bool Spouse { get; set; } = false;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.PropertyValuePair("Age".Translate(), string.Format("{0}", this.Age));
                generator.PropertyValuePair("Price".Translate(), string.Format("{0}", this.Price));
            }
        }

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
			if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.Price)
			{
				onFailure("{=Z4vYZzSq}Not enough gold !".Translate());
				return;
			}
			if ((adoptedHero.Age - (float)settings.Age) < 18)
			{
				onFailure("{=yWo2v3yu}You cannot rejuvenate bellow child age".Translate());
				return;
			}
			int num = BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.Price, true);
			ActionManager.SendReply(context, new string[]
			{
				string.Format("{0}{1}{2}{3}{4}{5}", new object[]
				{
					Naming.Dec,
					settings.Price,
					Naming.Gold,
					Naming.To,
					num,
					Naming.Gold
				})
			});
			adoptedHero.SetBirthDay(adoptedHero.BirthDay + CampaignTime.Years((float)settings.Age));
			int newAge = (int)adoptedHero.Age;
			if (settings.Spouse && adoptedHero.Spouse != null && ((adoptedHero.Spouse.Age - (float)settings.Age) >= 18f))
			{
				adoptedHero.Spouse.SetBirthDay(adoptedHero.Spouse.BirthDay + CampaignTime.Years((float)settings.Age));
			}
			onSuccess("{=XidEZXAO}Your rejuvenated of {Age} years you are now {newAge}".Translate(("Age", settings.Age), ("newAge", newAge)));
		}         
		
	}
}
