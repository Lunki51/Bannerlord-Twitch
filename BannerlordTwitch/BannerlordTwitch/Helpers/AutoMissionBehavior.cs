﻿using System;
using System.Runtime.CompilerServices;
using BannerlordTwitch.Util;
using TaleWorlds.MountAndBlade;

namespace BannerlordTwitch.Helpers
{
    public abstract class AutoMissionBehavior<T> : MissionBehavior where T : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public static T Current => MissionState.Current?.CurrentMission?.GetMissionBehavior<T>();

        protected void SafeCall(Action a, [CallerMemberName] string fnName = "")
        {
#if !DEBUG
            try
            {
#endif
                a();
#if !DEBUG
            }
            catch (Exception e)
            {
                Log.Exception($"{this.GetType().Name}.{fnName}", e);
            }
#endif
        }

        protected static void SafeCallStatic(Action a, [CallerMemberName] string fnName = "")
        {
#if !DEBUG
            try
            {
#endif
                a();
#if !DEBUG
            }
            catch (Exception e)
            {
                Log.Exception($"{typeof(T).Name}.{fnName}", e);
            }
#endif
        }

        protected static U SafeCallStatic<U>(Func<U> a, U def, [CallerMemberName] string fnName = "")
        {
#if !DEBUG
            try
            {
#endif
                return a();
#if !DEBUG
            }
            catch (Exception e)
            {
                Log.Exception($"{typeof(T).Name}.{fnName}", e);
                return def;
            }
#endif
        }
        // public static T CurrentState
        // {
        //     get
        //     {
        //         var current = MissionState.Current.CurrentMission.GetMissionBehavior<T>();
        //         if (current == null)
        //         {
        //             current = new T();
        //             MissionState.Current.CurrentMission.AddMissionBehavior(current);
        //         }
        //
        //         return current;
        //     }
        // }
    }
}