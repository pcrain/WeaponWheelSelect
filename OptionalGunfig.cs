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
    private const string GUNFIG_MOD_ENABLED = "Enable Weapon Wheel";
    private const string GUNFIG_USE_COLOR = "Use Colors";
    private const string GUNFIG_HIGHLIGHT = "Use Different Background Color for Current Gun";
    private const string GUNFIG_SHOW_AMMO = "Display Gun Ammo";
    private const string GUNFIG_SHOW_NAME = "Display Gun Name";
    private static readonly Color GunmetalBlue = new Color(.533f, .533f, .733f, 1f);

    private static bool DefaultFalse() => false;
    private static bool DefaultTrue() => true;

    internal static Func<bool> WheelEnabled = DefaultTrue;
    internal static Func<bool> ColorEnabled = DefaultFalse;
    internal static Func<bool> HighlightEnabled = DefaultFalse;
    internal static Func<bool> AmmoEnabled = DefaultTrue;
    internal static Func<bool> NameEnabled = DefaultFalse;

    private static Func<string, bool> GunfigEnabled = null;
    private static bool WheelEnabledGunfig() => GunfigEnabled(GUNFIG_MOD_ENABLED);
    private static bool ColorEnabledGunfig() => GunfigEnabled(GUNFIG_USE_COLOR);
    private static bool HighlightEnabledGunfig() => GunfigEnabled(GUNFIG_HIGHLIGHT);
    private static bool AmmoEnabledGunfig() => GunfigEnabled(GUNFIG_SHOW_AMMO);
    private static bool NameEnabledGunfig() => GunfigEnabled(GUNFIG_SHOW_NAME);

    internal static void Init()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.FullName.Contains("Gunfig"))
                continue;
            Type gunfigType = assembly.GetType("Gunfiguration.Gunfig");
            string coloredModName = (string)assembly.GetType("Gunfiguration.GunfigHelpers").GetMethod("WithColor").Invoke(null, new object[]{Module.MOD_NAME, GunmetalBlue});
            object Gunfig = gunfigType.GetMethod("Get").Invoke(null, new object[]{coloredModName});
            MethodInfo addToggle = gunfigType.GetMethod("AddToggle");
            // Gunfig.AddToggle(key, enabled, label, callback, updateType)
            addToggle.Invoke(Gunfig, new object[]{ GUNFIG_MOD_ENABLED, true,  null, null, 1 /*OnConfirm*/ });
            addToggle.Invoke(Gunfig, new object[]{ GUNFIG_USE_COLOR,   false, null, null, 1 /*OnConfirm*/ });
            addToggle.Invoke(Gunfig, new object[]{ GUNFIG_HIGHLIGHT,   false, null, null, 1 /*OnConfirm*/ });
            addToggle.Invoke(Gunfig, new object[]{ GUNFIG_SHOW_AMMO,   true,  null, null, 1 /*OnConfirm*/ });
            addToggle.Invoke(Gunfig, new object[]{ GUNFIG_SHOW_NAME,   false, null, null, 1 /*OnConfirm*/ });
            GunfigEnabled = (Func<string, bool>)Delegate.CreateDelegate(typeof(Func<string, bool>), Gunfig, gunfigType.GetMethod("Enabled"));
            WheelEnabled = WheelEnabledGunfig;
            ColorEnabled = ColorEnabledGunfig;
            HighlightEnabled = HighlightEnabledGunfig;
            AmmoEnabled = AmmoEnabledGunfig;
            NameEnabled = NameEnabledGunfig;
            break;
        }
    }
  }
}
