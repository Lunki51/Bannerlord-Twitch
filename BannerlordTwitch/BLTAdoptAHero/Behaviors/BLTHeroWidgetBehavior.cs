//using System;
//using System.Collections.Generic;
//using System.Linq;
//using BannerlordTwitch.Helpers;
//using BannerlordTwitch.Util;
//using TaleWorlds.CampaignSystem;
//using TaleWorlds.Core;
//using TaleWorlds.Engine;
//using TaleWorlds.Engine.Screens;
//using TaleWorlds.Engine.GauntletUI;
//using TaleWorlds.Library;
//using TaleWorlds.MountAndBlade;
//using TaleWorlds.MountAndBlade.View.MissionViews;

//namespace BLTAdoptAHero
//{
//    // ==============================================================
//    // Mission Behavior: tracks adopted heroes
//    // ==============================================================
//    public class BLTHeroWidgetBehavior : AutoMissionBehavior<BLTHeroWidgetBehavior>
//    {
//        private readonly Dictionary<Agent, HeroData> _trackedHeroes = new();
//        public IReadOnlyDictionary<Agent, HeroData> TrackedHeroes => _trackedHeroes;

//        public class HeroData
//        {
//            public Hero Hero;
//            public Agent Agent;
//        }

//        // Events for the View
//        public event Action<Agent, Hero> HeroTracked;
//        public event Action<Agent> HeroUntracked;

//        public override void OnAgentBuild(Agent agent, Banner banner)
//        {
//            SafeCall(() =>
//            {
//                var hero = agent.GetHero();
//                if (hero == null || !hero.IsAdopted()) return;

//                Log.Trace($"[Behavior] Tracking adopted hero {hero.Name}");

//                _trackedHeroes[agent] = new HeroData
//                {
//                    Hero = hero,
//                    Agent = agent
//                };

//                HeroTracked?.Invoke(agent, hero);
//            });
//        }

//        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent,
//            AgentState state, KillingBlow blow)
//        {
//            SafeCall(() => ForceRemove(affectedAgent));
//        }

//        internal void ForceRemove(Agent agent)
//        {
//            SafeCall(() =>
//            {
//                if (_trackedHeroes.TryGetValue(agent, out var data))
//                {
//                    Log.Trace($"[Behavior] Removed adopted hero {data.Hero.Name}");
//                    _trackedHeroes.Remove(agent);
//                    HeroUntracked?.Invoke(agent);
//                }
//            });
//        }

//        public override void OnMissionStateFinalized()
//        {
//            SafeCall(() =>
//            {
//                foreach (var kvp in _trackedHeroes)
//                {
//                    HeroUntracked?.Invoke(kvp.Value.Agent);
//                }

//                _trackedHeroes.Clear();
//                Log.Trace("[Behavior] Cleaned up on mission end");
//            });
//        }
//    }

//    // ==============================================================
//    // Mission View: renders adopted hero tags
//    // ==============================================================
//    public class BLTHeroWidgetView : MissionView
//    {
//        private GauntletLayer _layer;
//        private HeroTagsVM _tagsVM;
//        private readonly Dictionary<Agent, HeroTagVM> _agentToVM = new();
//        private static bool _initialized;
//        private BLTHeroWidgetBehavior _behavior;

//        private const float MinScale = 0.7f;
//        private const float MaxScale = 1.2f;
//        private const float ScaleDistance = 25f;

//        // -------------------------
//        // ViewModels
//        // -------------------------
//        public class HeroTagVM : ViewModel
//        {
//            private string _name;
//            private Vec2 _screenPosition;
//            private bool _isEnabled;
//            private float _scale;
//            private Color _color;

//            [DataSourceProperty] public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } } }
//            [DataSourceProperty] public Vec2 ScreenPosition { get => _screenPosition; set { if (_screenPosition != value) { _screenPosition = value; OnPropertyChanged(nameof(ScreenPosition)); } } }
//            [DataSourceProperty] public bool IsEnabled { get => _isEnabled; set { if (_isEnabled != value) { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); } } }
//            [DataSourceProperty] public float Scale { get => _scale; set { if (Math.Abs(_scale - value) > 0.001f) { _scale = value; OnPropertyChanged(nameof(Scale)); } } }
//            [DataSourceProperty] public Color Color { get => _color; set { if (_color != value) { _color = value; OnPropertyChanged(nameof(Color)); } } }
//        }

//        public class HeroTagsVM : ViewModel
//        {
//            [DataSourceProperty] public MBBindingList<HeroTagVM> Tags { get; set; } = new();

//            public override void OnFinalize()
//            {
//                base.OnFinalize();
//                Tags.Clear();
//            }
//        }

//        // -------------------------
//        // Lifecycle
//        // -------------------------
//        public override void OnMissionScreenActivate()
//        {
//            base.OnMissionScreenActivate();

//            if (_initialized || MissionScreen == null)
//                return;

//            Log.Trace("[View] Initializing UI layer (OnMissionScreenActivate)");
//            _tagsVM = new HeroTagsVM();
//            _layer = new GauntletLayer(1, "BLTHeroWidgetLayer");

//            try
//            {
//                _layer.LoadMovie("BLTHeroNametag", _tagsVM);
//            }
//            catch (Exception ex)
//            {
//                Log.Error($"[View] Failed to load BLTHeroNametag prefab - {ex.Message}");
//            }

//            MissionScreen.AddLayer(_layer);

//            _behavior = Mission.Current.GetMissionBehavior<BLTHeroWidgetBehavior>();
//            if (_behavior != null)
//            {
//                _behavior.HeroTracked += AddHeroTag;
//                _behavior.HeroUntracked += RemoveHeroTag;
//            }

//            _initialized = true;
//        }

//        private void AddHeroTag(Agent agent, Hero hero)
//        {
//            if (_agentToVM.ContainsKey(agent))
//                return;

//            var vm = new HeroTagVM
//            {
//                Name = hero.Name.ToString(),
//                IsEnabled = true,
//                Scale = 1f,
//                Color = Color.White
//            };
//            _tagsVM.Tags.Add(vm);
//            _agentToVM[agent] = vm;

//            Log.Trace($"[View] Added HeroTagVM for {hero.Name}");
//        }

//        private void RemoveHeroTag(Agent agent)
//        {
//            if (_agentToVM.TryGetValue(agent, out var vm))
//            {
//                _tagsVM.Tags.Remove(vm);
//                _agentToVM.Remove(agent);
//                Log.Trace($"[View] Removed tag for {agent.Name}");
//            }
//        }

//        public override void OnMissionTick(float dt)
//        {
//            base.OnMissionTick(dt);
//            if (!_initialized || _behavior == null) return;

//            foreach (var (agent, vm) in _agentToVM.ToList())
//            {
//                if (!agent.IsActive())
//                {
//                    vm.IsEnabled = false;
//                    continue;
//                }

//                // Name
//                vm.Name = agent.Name;

//                // Position (project 3D world pos to screen)
//                var worldPos = agent.Position + new Vec3(0, 0, agent.GetEyeGlobalHeight());
//                var cam = MissionScreen.CombatCamera;
//                Vec3 viewportPos = cam.WorldPointToViewPortPoint(ref worldPos);

//                // Convert normalized viewport coords to actual screen pixels
//                float screenX = viewportPos.x * Screen.RealScreenResolutionWidth;
//                float screenY = (1 - viewportPos.y) * Screen.RealScreenResolutionHeight;

//                // Assign
//                vm.ScreenPosition = new Vec2(screenX, screenY);
//                vm.IsEnabled = viewportPos.z > 0;

//                // Scale with distance
//                var dist = (worldPos - MissionScreen.CombatCamera.Position).Length;
//                vm.Scale = MaxScale - Math.Min(dist / ScaleDistance, 1f) * (MaxScale - MinScale);

//                // Coloring based on relation
//                if (agent.Team == Mission.PlayerTeam)
//                    vm.Color = Color.FromUint(0x0000FFFF); // Blue
//                else if (Mission.PlayerTeam != null && agent.Team.IsEnemyOf(Mission.PlayerTeam))
//                    vm.Color = Color.FromUint(0xFF0000FF); // Red
//                else
//                    vm.Color = Color.White;
//            }
//        }

//        public override void OnMissionScreenFinalize()
//        {
//            base.OnMissionScreenFinalize();

//            if (_layer != null)
//            {
//                MissionScreen.RemoveLayer(_layer);
//                _layer = null;
//            }

//            if (_behavior != null)
//            {
//                _behavior.HeroTracked -= AddHeroTag;
//                _behavior.HeroUntracked -= RemoveHeroTag;
//            }

//            _tagsVM?.OnFinalize();
//            _tagsVM = null;
//            _agentToVM.Clear();
//            _initialized = false;
//        }
//    }
//}
