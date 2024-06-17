using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace WeaponWheelSelect
{
  /// <summary>Loads Gunfig configuration through reflection if the library is available.</summary>
  internal static class OptionalGunfig
  {
    private const string GUNFIG_USE_COLOR = "Use Colors";
    private static readonly Color GunmetalBlue = new Color(.533f, .533f, .733f, 1f);

    private static bool ColorEnabledNoGunfig() => false;
    private static bool ColorEnabledGunfig() => GunfigEnabled(GUNFIG_USE_COLOR);
    private static Func<string, bool> GunfigEnabled = null;

    internal static Func<bool> ColorEnabled = ColorEnabledNoGunfig;

    internal static void Init()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.FullName.Contains("Gunfig"))
                continue;
            Type gunfigType = assembly.GetType("Gunfiguration.Gunfig");
            string coloredModName = (string)assembly.GetType("Gunfiguration.GunfigHelpers").GetMethod("WithColor").Invoke(null, new object[]{Module.MOD_NAME, GunmetalBlue});
            object Gunfig = gunfigType.GetMethod("Get").Invoke(null, new object[]{coloredModName});
            gunfigType.GetMethod("AddToggle").Invoke(Gunfig, new object[]{
                /*key*/        GUNFIG_USE_COLOR,
                /*enabled*/    false,
                /*label*/      null,
                /*callback*/   null,
                /*updateType*/ 1 /*OnConfirm*/
            });
            GunfigEnabled = (Func<string, bool>)Delegate.CreateDelegate(typeof(Func<string, bool>), Gunfig, gunfigType.GetMethod("Enabled"));
            ColorEnabled = ColorEnabledGunfig;
            break;
        }
    }
  }
}
