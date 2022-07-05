using System.IO;
using System;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace RadialGunSelect
{
    public static class AssetsManager
    {
        public static AssetBundle LoadAssetBundleFromResource(string filePath)
        {
            filePath = filePath.Replace("/", ".");
            filePath = filePath.Replace("\\", ".");
            using (Stream manifestResourceStream = Assembly.GetCallingAssembly().GetManifestResourceStream(filePath))
            {
                if (manifestResourceStream != null)
                {
                    byte[] array = new byte[manifestResourceStream.Length];
                    manifestResourceStream.Read(array, 0, array.Length);
                    return AssetBundle.LoadFromMemory(array);
                }
            }
            ETGModConsole.Log("No bytes found in " + filePath, false);
            return null;
        }
    }
}
