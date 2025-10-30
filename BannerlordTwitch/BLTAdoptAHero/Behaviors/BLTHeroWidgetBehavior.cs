using System;
using System.Collections.Generic;
using System.Linq;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Util;
using BLTAdoptAHero.UI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.Screens;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.CampaignSystem.TournamentGames;
using SandBox.Tournaments.MissionLogics;
using SandBox.ViewModelCollection.Missions.NameMarker;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Mission.NameMarker;

namespace BLTAdoptAHero
{
    [DefaultView]
    public class HeroWidgetMissionView : MissionView
    {
        private GauntletLayer _layer;
        private HeroWidgetVM _vm;
        private IGauntletMovie _gauntletMovie;
        private Camera _camera;
        private readonly Dictionary<Hero, HeroIconVM> _heroToVM = new();
        private bool _isInitialized = false;
        private readonly float configWidth = GlobalCommonConfig.Get().NametagWidth;
        private readonly float configHeight = GlobalCommonConfig.Get().NametagHeight;
        private readonly float configFontsize = GlobalCommonConfig.Get().NametagFontsize;

        public override void OnMissionScreenTick(float dt)
        {
            if (!GlobalCommonConfig.Get().NametagEnabled)
                return;
            var heroBehavior = Mission.Current?.GetMissionBehavior<BLTAdoptAHeroCommonMissionBehavior>();
            var combatMission = Mission.Current.CombatType;
            if (heroBehavior == null || MissionScreen == null || combatMission == Mission.MissionCombatType.NoCombat)
                return;

            if (!_isInitialized)
            {
                if (heroBehavior.activeHeroes.Count > 0)
                {
                    InitializeUI();
                    _isInitialized = true;
                }
            }
            else
            {
                UpdateHeroIcons(heroBehavior);
            }
        }

        private void InitializeUI()
        {
            //Log.Trace("BLTAdoptAHero: Initializing UI.");
            this._vm = new HeroWidgetVM();
            this._layer = new GauntletLayer(111, "BLTHeroWidgetLayer", false);
            this._gauntletMovie = this._layer.LoadMovie("BLTHeroNametag", _vm);
            this.MissionScreen.AddLayer(_layer);
            //Log.Trace("BLTAdoptAHero: Layer added to MissionScreen.");
            //Log.Trace($"BLTAdoptAHero: Movie loaded. RootWidget is Null? {_gauntletMovie.RootWidget == null}");
            this._camera = MissionScreen.CombatCamera;
        }

        internal void UpdateHeroIcons(BLTAdoptAHeroCommonMissionBehavior heroBehavior)
        {
            bool inTournament = MissionHelpers.InTournament();
            if (!_isInitialized || _camera == null) return;

            var heroVMs = new List<(Hero hero, HeroIconVM vm, float dist)>();

            var heroTeamCache = new Dictionary<Hero, string>();
            foreach (var hero in heroBehavior.activeHeroes)
            {
                if (!_heroToVM.TryGetValue(hero, out var vm))
                {
                    vm = new HeroIconVM { HeroName = hero.FirstName?.Raw() ?? "" };
                    _vm.Heroes.Add(vm);
                    _heroToVM[hero] = vm;
                }

                var agent = hero.GetAgent();
                if (agent != null && agent.IsActive())
                {
                    Vec3 globalPos = agent.Position;
                    globalPos.z += agent.GetEyeGlobalHeight() + 0.15f;

                    float x = 0f, y = 0f, z = 0f;
                    MBWindowManager.WorldToScreen(_camera, globalPos, ref x, ref y, ref z);

                    bool onScreen = z > 0f && x > 0f && y > 0f &&
                                    x < Screen.RealScreenResolutionWidth &&
                                    y < Screen.RealScreenResolutionHeight;

                    if (onScreen)
                    {
                        float dist = agent.Position.Distance(_camera.Position);
                        if (dist < 350f)
                        {
                            float scale = MBMath.Lerp(1f, 0.5f, (dist - 30f) / 170f, 0.0f); //Min:30 Max:200
                            scale = MBMath.ClampFloat(scale, 0.5f, 1f);

                            vm.IsVisible = true;
                            vm.Width = configWidth * scale;
                            vm.Height = configHeight * scale;
                            vm.FontSize = Math.Max(15, (int)(configFontsize * scale));
                            vm.PositionX = x - vm.Width * 0.5f;
                            vm.PositionY = y - vm.Height * 0.5f - 5f;


                            heroVMs.Add((hero, vm, dist));

                            if (!heroTeamCache.ContainsKey(hero))
                                heroTeamCache[hero] = inTournament
                                    ? GetTournamentTeamColor(hero)
                                    : BLTAdoptAHeroCommonMissionBehavior.IsHeroOnPlayerSide(hero)
                                        ? "#4EE04CF0"
                                        : "#ED1C24F0";
                        }
                        else
                        {
                            vm.IsVisible = false;
                        }
                    }
                    else
                    {
                        vm.IsVisible = false;
                    }
                }
                else
                {
                    _vm.Heroes.Remove(vm);
                    _heroToVM.Remove(hero);
                }
            }

            var sorted = heroVMs
                .Where(h => h.vm.IsVisible)
                .OrderBy(h => h.dist)
                .ToList();

            float minOverlapY = 4f;
            float paddingY = 2f;
            float slideFactor = 0.5f;

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                var anchor = sorted[i].vm;

                for (int j = i + 1; j < sorted.Count; j++)
                {
                    var farther = sorted[j].vm;
                    if (!farther.IsVisible) continue;

                    if (farther.PositionY - (anchor.PositionY + anchor.Height) > 50f)
                        break;

                    bool overlapX = farther.PositionX < anchor.PositionX + anchor.Width &&
                                    farther.PositionX + farther.Width > anchor.PositionX;
                    bool overlapY = farther.PositionY < anchor.PositionY + anchor.Height &&
                                    farther.PositionY + farther.Height > anchor.PositionY;

                    if (overlapX && overlapY)
                    {
                        float overlapAmountY = (anchor.PositionY + anchor.Height) - farther.PositionY;
                        if (overlapAmountY > minOverlapY)
                        {
                            farther.PositionY += overlapAmountY * slideFactor + paddingY;
                        }
                    }
                }
            }

            // --- Step 4: Apply cached colors ---
            foreach (var (hero, vm, dist) in heroVMs)
            {
                if (heroTeamCache.TryGetValue(hero, out var color))
                    vm.Color = color;
            }

            // --- Step 5: Remove inactive heroes ---
            var toRemove = _heroToVM.Keys.Except(heroBehavior.activeHeroes).ToList();
            foreach (var hero in toRemove)
            {
                _vm.Heroes.Remove(_heroToVM[hero]);
                _heroToVM.Remove(hero);
            }
        }

        private string GetTournamentTeamColor(Hero hero)
        {
            if (!MissionHelpers.InTournament() || hero == null)
                return "#FFFFFFF0"; // fallback if not in a tournament

            var agents = Mission.Current?.Agents;
            if (agents == null) return "#FFFFFFF0";

            foreach (var agent in agents)
            {
                var agentHero = agent.GetAdoptedHero();
                if (agentHero == null) continue;

                if (agentHero == hero)
                {
                    int teamIndex = agent.Team?.TeamIndex ?? -1;

                    // Map team index to a color
                    string[] teamColors = { "#0000FFF0", "#FF0000F0", "#00FF00F0", "#FFFF00F0" };
                    if (teamIndex < 0 || teamIndex >= teamColors.Length)
                        return "#FFFFFFF0"; // fallback for unexpected team index

                    return teamColors[teamIndex];
                }
            }

            // Hero not found in tournament
            return "#FFFFFFF0";
        }

        public override void OnRemoveBehavior()
        {
            _heroToVM.Clear();
            _vm?.Heroes.Clear();

            if (_layer != null && MissionScreen != null)
                MissionScreen.RemoveLayer(_layer);

            _layer = null;
            _vm = null;
            _camera = null;
            base.OnRemoveBehavior();
        }
    }

    public class HeroWidgetVM : ViewModel
    {
        [DataSourceProperty]
        public MBBindingList<HeroIconVM> Heroes { get; } = new();
    }

    public class HeroIconVM : ViewModel
    {
        private string _heroName;
        private bool _isVisible;
        private float _positionX;
        private float _positionY;
        private string _color;
        private float _width;
        private float _height;
        private int _fontSize;

        [DataSourceProperty]
        public string HeroName
        {
            get => _heroName;
            set { if (_heroName != value) { _heroName = value; OnPropertyChanged(nameof(HeroName)); } }
        }

        [DataSourceProperty]
        public bool IsVisible
        {
            get => _isVisible;
            set { if (_isVisible != value) { _isVisible = value; OnPropertyChanged(nameof(IsVisible)); } }
        }

        [DataSourceProperty]
        public float PositionX
        {
            get => _positionX;
            set { if (_positionX != value) { _positionX = value; OnPropertyChanged(nameof(PositionX)); } }
        }

        [DataSourceProperty]
        public float PositionY
        {
            get => _positionY;
            set { if (_positionY != value) { _positionY = value; OnPropertyChanged(nameof(PositionY)); } }
        }

        [DataSourceProperty]
        public string Color
        {
            get => _color;
            set { if (_color != value) { _color = value; OnPropertyChanged(nameof(Color)); } }
        }

        [DataSourceProperty]
        public float Width
        {
            get => _width;
            set { if (_width != value) { _width = value; OnPropertyChanged(nameof(Width)); } }
        }

        [DataSourceProperty]
        public float Height
        {
            get => _height;
            set { if (_height != value) { _height = value; OnPropertyChanged(nameof(Height)); } }
        }

        [DataSourceProperty]
        public int FontSize
        {
            get => _fontSize;
            set { if (_fontSize != value) { _fontSize = value; OnPropertyChanged(nameof(FontSize)); } }
        }
    }
}