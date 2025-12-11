using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using BannerlordTwitch.Util;
using BLTAdoptAHero;
using BannerlordTwitch.Rewards;

namespace BLTAdoptAHero
{
    /// <summary>
    /// Template campaign behavior with common patterns for BLT behaviors
    /// </summary>
    public class TemplateBehavior : CampaignBehaviorBase
    {
        // Sub-behaviors can be exposed as properties
        public TemplateSubBehavior SubBehavior { get; } = new TemplateSubBehavior();

        // Private states for tracking data
        private Dictionary<Hero, float> _heroData = new();
        private CampaignTime _lastProcessTime;

        public override void RegisterEvents()
        {
            // Register sub-behaviors
            SubBehavior.RegisterEvents();

            // Daily tick - runs once per in-game day
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);

            // Weekly tick - runs once per in-game week
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);

            // Hourly tick - runs once per in-game hour (use sparingly, performance impact)
            // CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);

            // Hero-specific events
            CampaignEvents.OnHeroChangedClanEvent.AddNonSerializedListener(this, OnHeroChangedClan);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);

            // Other event examples:
            // CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            // CampaignEvents.BeforeHeroesMarried.AddNonSerializedListener(this, OnHeroesMarried);
            // CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Save/load persistent data here
            // dataStore.SyncData("_heroData", ref _heroData);
        }

        private void OnDailyTick()
        {
            // Process all BLT heroes daily
            var bltHeroes = Hero.AllAliveHeroes
                .Where(h => h != null && h.IsAdopted())
                .ToList();

            foreach (var hero in bltHeroes)
            {
                ProcessBLTHero(hero);
            }

            // Update last process time
            _lastProcessTime = CampaignTime.Now;
        }

        private void OnWeeklyTick()
        {
            // Perform less frequent but more intensive operations
            CleanupDeadHeroData();
            ProcessClanBonuses();
        }

        private void OnHeroChangedClan(Hero hero, Clan oldClan)
        {
            if (hero == null || !hero.IsAdopted())
                return;

            // React to BLT hero changing clans
            Log.LogFeedEvent($"{hero.Name} changed from {oldClan?.Name} to {hero.Clan?.Name}");
        }

        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            if (victim != null && victim.IsAdopted())
            {
                // Cleanup data for dead BLT hero
                _heroData.Remove(victim);
            }
        }

        private void ProcessBLTHero(Hero hero)
        {
            if (hero == null || hero.IsDead)
                return;

            // Initialize data if needed
            if (!_heroData.ContainsKey(hero))
                _heroData[hero] = 0f;

            // Example: Give daily bonus
            _heroData[hero] += 1f;

            // Example: Apply effects based on conditions
            if (hero.Clan != null && hero.IsAlive)
            {
                // Do something with the hero
            }
        }

        private void ProcessClanBonuses()
        {
            foreach (var clan in Clan.All)
            {
                if (clan == null || !clan.IsInitialized || clan.IsEliminated)
                    continue;

                var leader = clan.Leader;
                if (leader == null || !leader.IsAlive || !leader.IsAdopted())
                    continue;

                // Apply clan-wide bonuses
                clan.Renown += 1f;
            }
        }

        private void CleanupDeadHeroData()
        {
            var deadHeroes = _heroData.Keys
                .Where(h => h == null || h.IsDead)
                .ToList();

            foreach (var hero in deadHeroes)
            {
                _heroData.Remove(hero);
            }
        }

        /// <summary>
        /// Example sub-behavior that can be separated for organization
        /// </summary>
        public class TemplateSubBehavior
        {
            private readonly Dictionary<Hero, CampaignTime> _lastActionTime = new();

            public void RegisterEvents()
            {
                CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            }

            private void OnDailyTick()
            {
                // Sub-behavior specific logic
            }

            public bool CanPerformAction(Hero hero, int cooldownHours)
            {
                if (!_lastActionTime.TryGetValue(hero, out var lastTime))
                    return true;

                return (CampaignTime.Now - lastTime).ToHours >= cooldownHours;
            }

            public void RecordAction(Hero hero)
            {
                _lastActionTime[hero] = CampaignTime.Now;
            }
        }
    }
}