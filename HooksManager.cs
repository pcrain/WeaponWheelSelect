using System;
using System.Reflection;
using UnityEngine;
using MonoMod.RuntimeDetour;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Collections.Specialized;

namespace RadialGunSelect
{
    public static class HooksManager
    {
        public static void Init()
        {
            // add hooks
            AddHook(typeof(GameUIRoot), "HandleMetalGearGunSelect");
        }

        // (re-use hook)
        public static IEnumerator HandleMetalGearGunSelect (this GameUIRoot self, PlayerController targetPlayer, int numToL)
        {
            return self.HandleMetalGearGunSelect(targetPlayer, numToL);
        }

        // HOOKS
        static IEnumerator HandleMetalGearGunSelect(Func<GameUIRoot, PlayerController, int, IEnumerator> orig, GameUIRoot self, PlayerController targetPlayer, int numToL)
        {
            yield return RadialGunSelectController.HandleRadialGunSelect(targetPlayer, numToL);
            //yield return orig(self, targetPlayer, numToL);
        }

        // etc
        public static Hook AddHook(Type type, string sourceMethodName, string hookMethodName = null)
        {
            if (hookMethodName == null) hookMethodName = sourceMethodName;
            return new Hook(
                type.GetMethod(sourceMethodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
                typeof(HooksManager).GetMethod(hookMethodName, BindingFlags.NonPublic | BindingFlags.Static)
            );
        }
    }
}
