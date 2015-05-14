using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using System.Text;

namespace KAS
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    internal class KASAddonCheckKIS : MonoBehaviour
    {
        public void Start()
        {
            Assembly KISAssembly = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "KIS")
                {
                    KISAssembly = assembly;
                    break;
                }             
            }
            if (KISAssembly != null)
            {
                KAS_Shared.DebugLog("Assembly : " + KISAssembly.GetName().Name + " | Version : " + KISAssembly.GetName().Version + " found !");
            }
            else
            {
                KAS_Shared.DebugError("KIS not found !");
                var sb = new StringBuilder();
                sb.AppendFormat("KIS is required for KAS."); sb.AppendLine();
                sb.AppendFormat("Please install the latest version of KIS before using KAS."); sb.AppendLine();
                sb.AppendFormat("If KIS is not installed, KSP will not run correctly (parts will be missing)."); sb.AppendLine();
                PopupDialog.SpawnPopupDialog("KIS not found !", sb.ToString(), "OK", false, HighLogic.Skin);
            }
        }
    }
}




