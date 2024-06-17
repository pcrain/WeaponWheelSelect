using System;
using BepInEx;
using HarmonyLib;

namespace WeaponWheelSelect
{
    [BepInPlugin(Module.MOD_GUID, Module.MOD_NAME, Module.MOD_VERSION)]
    [BepInDependency(ETGModMainBehaviour.GUID)]
    public class Module : BaseUnityPlugin
    {
        public const string MOD_GUID = "pretzel.etg.weaponwheel";
        public const string MOD_NAME = "Weapon Wheel Select"; // referred to as WeaponWheelSelect internally
        public const string MOD_VERSION = "2.1.0";
        public void Start()
        {
            ETGModMainBehaviour.WaitForGameManagerStart(GMStart);
        }

        public void GMStart(GameManager manager)
        {
            try
            {
                OptionalGunfig.Init();
                new Harmony(MOD_GUID).PatchAll();
                WeaponWheelSelectController.Init();
                ETGModConsole.Log($"<color=#8888BB>{MOD_NAME} v{MOD_VERSION} started successfully!</color>");
            }
            catch (Exception e)
            {
                ETGModConsole.Log($"<color=#FF0000>{MOD_NAME} v{MOD_VERSION} failed to initialize!\n{e}</color>");
            }
        }
    }
}
