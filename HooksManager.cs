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
        static Dictionary<Type, Dictionary<string, MemberInfo>> memberInfoDict;

        public static void Init()
        {
            // add hooks
            AddHook(typeof(GameUIRoot), "HandleMetalGearGunSelect");

            // make fields public
            PublicizeField(typeof(GameUIRoot), "m_metalGearGunSelectActive");
            PublicizeField(typeof(GameUIRoot), "additionalGunBoxesSecondary");
            PublicizeField(typeof(GameUIRoot), "additionalGunBoxes");
            PublicizeField(typeof(GameUIRoot), "m_manager");

            // make methods public
            PublicizeMethod(typeof(GameUIRoot), "HandlePauseInventoryFolding");
            PublicizeMethod(typeof(GameUIRoot), "AssignClippedSpriteFadeFractions");
            PublicizeMethod(typeof(GameUIRoot), "SetFadeFractions");
            PublicizeMethod(typeof(GameUIRoot), "SetFadeMaterials");
            PublicizeMethod(typeof(GameUIRoot), "GetDFAtlasMaterialForMetalGear");
        }

        // PUBLICIZATION
        static void PublicizeMember(Type type, string memberName)
        {
            if (memberInfoDict == null)
                memberInfoDict = new Dictionary<Type, Dictionary<string, MemberInfo>>();
            if (!memberInfoDict.ContainsKey(type))
                memberInfoDict[type] = new Dictionary<string, MemberInfo>();
        }

        public static void PublicizeMethod(Type type, string methodName)
        {
            PublicizeMember(type, methodName);
            memberInfoDict[type][methodName] = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public static void PublicizeField(Type type, string fieldName)
        {
            PublicizeMember(type, fieldName);
            memberInfoDict[type][fieldName] = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        }

        // GETTERS AND SETTERS
        static object GetValue(Type type, object self, string fieldName)
        {
            return (memberInfoDict[type][fieldName] as FieldInfo).GetValue(self);
        }

        public static object GetObject<T>(this T self, string fieldName)
        {
            return GetValue(typeof(T), self, fieldName);
        }

        public static float GetFloat<T>(this T self, string fieldName)
        {
            return (float)GetValue(typeof(T), self, fieldName);
        }

        public static bool GetBool<T>(this T self, string fieldName)
        {
            return (bool)GetValue(typeof(T), self, fieldName);
        }

        public static void SetValue<T>(this T self, string fieldName, object value)
        {
            (memberInfoDict[typeof(T)][fieldName] as FieldInfo).SetValue(self, value);
        }

        static object RunMethod(Type type, object self, string methodName, object[] arguments)
        {
            return (memberInfoDict[type][methodName] as MethodInfo).Invoke(self, arguments);
        }

        // GameUIRoot private methods to publicize
        public static IEnumerator HandlePauseInventoryFolding(this GameUIRoot self, PlayerController targetPlayer, bool doGuns = true, bool doItems = true, float overrideTransitionTime = -1f, int numToL = 0, bool forceUseExistingList = false)
        {
            yield return RunMethod(typeof(GameUIRoot), self, "HandlePauseInventoryFolding", new object[] 
                { targetPlayer, doGuns, doItems, overrideTransitionTime, numToL, forceUseExistingList });
        }

        public static void SetFadeMaterials(this GameUIRoot self, dfSprite targetSprite, bool leftAligned)
        {
            RunMethod(typeof(GameUIRoot), self, "SetFadeMaterials", new object[] 
                { targetSprite, leftAligned });
        }

        public static void AssignClippedSpriteFadeFractions(this GameUIRoot self, tk2dClippedSprite gunSpr, float fadeScreenSpaceY, float fadeScreenSpaceXStart, float fadeScreenSpaceXEnd, bool leftAligned)
        {
            RunMethod(typeof(GameUIRoot), self, "AssignClippedSpriteFadeFractions", new object[] 
                { gunSpr, fadeScreenSpaceY, fadeScreenSpaceXStart, fadeScreenSpaceXEnd, leftAligned });
        }

        public static void SetFadeFractions(this GameUIRoot self, dfSprite targetSprite, float fadeScreenSpaceXStart, float fadeScreenSpaceXEnd, float fadeScreenSpaceY, bool isLeftAligned)
        {
            RunMethod(typeof(GameUIRoot), self, "SetFadeFractions", new object[]
                {  targetSprite, fadeScreenSpaceXStart, fadeScreenSpaceXEnd, fadeScreenSpaceY, isLeftAligned });
        }

        public static Material GetDFAtlasMaterialForMetalGear(this GameUIRoot self, Material source, bool leftAligned)
        {
            return (Material)RunMethod(typeof(GameUIRoot), self, "GetDFAtlasMaterialForMetalGear", new object[]
                {  self, source, leftAligned });
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
