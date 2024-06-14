using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using SGUI;
using BepInEx;
using HarmonyLib;

namespace RadialGunSelect
{
    [BepInPlugin(Module.MOD_GUID, Module.MOD_NAME, Module.VERSION)]
    [BepInDependency(ETGModMainBehaviour.GUID)]
    public class Module : BaseUnityPlugin
    {
        public const string MOD_GUID = "pretzel.etg.weaponwheel";
        public const string MOD_NAME = "Weapon Wheel Select"; // referred to as RadialGunSelect internally
        public const string VERSION = "2.0.0";

        public void Start()
        {
            ETGModMainBehaviour.WaitForGameManagerStart(GMStart);
        }

        public void GMStart(GameManager manager)
        {
            try
            {
                new Harmony(MOD_GUID).PatchAll();
                RadialGunSelectController.Init();
                ETGModConsole.Log($"<color=#8888BB>{MOD_NAME} v{VERSION} started successfully!</color>");
            }
            catch (Exception e)
            {
                ETGModConsole.Log($"<color=#FF0000>{MOD_NAME} v{VERSION} failed to initialize!\n{e}</color>");
            }
        }
    }
}
