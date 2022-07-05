using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RadialGunSelect
{
    public static class ConsoleCommandsManager
    {
        const string prefix = "weaponwheelselect";

        public static void Init()
        {
            ETGModConsole.Commands.AddGroup(prefix, (args) =>
            {
                MorphUtils.LogRainbow($"{Module.MOD_NAME} is being made by Morphious86#6617 on Discord!");
            });
        }

        static ConsoleCommandGroup AddCommand(string name, Action<string[]> action)
        {
            return ETGModConsole.Commands.GetGroup(prefix).AddUnit(name, action);
        }

        static void TryParseFloat(string arg, Action<float> action)
        {
            if (float.TryParse(arg, out var f))
                action(f);
            else
                MorphUtils.LogError($"\"{arg}\" is not a proper value!");
        }
    }
}
