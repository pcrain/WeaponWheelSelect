using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using SGUI;

namespace RadialGunSelect
{
    public class Module : ETGModule
    {
        public static readonly string MOD_NAME = "Weapon Wheel Select"; // referred to as RadialGunSelect internally
        public static readonly string VERSION = "1.0.0";

        public override void Init()
        {
            
        }

        public override void Start()
        {
            try
            {
                HooksManager.Init();
                ConsoleCommandsManager.Init();
                RadialGunSelectController.Init();

                MorphUtils.LogRainbow($"{MOD_NAME} v{VERSION} started successfully.");
            }
            catch (Exception e)
            {
                MorphUtils.LogError($"{MOD_NAME} v{VERSION} failed to initialize!", e);
            }
        }

        public override void Exit()
        {

        }
    }
}
