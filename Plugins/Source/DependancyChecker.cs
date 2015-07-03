using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using System.Text;

namespace KAS
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    internal class DependancyChecker : MonoBehaviour
    {
        string currentModName = "KAS";
        string assemblyName = "KIS";
        int minimalVersionMajor = 1;
        int minimalVersionMinor = 1;
        int minimalVersionBuild = 6;
        bool checkPresence = true;

        public void Start()
        {
            string minimalVersion = minimalVersionMajor + "." + minimalVersionMinor + "." + minimalVersionBuild;
            Assembly dependancyAssembly = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == assemblyName)
                {
                    dependancyAssembly = assembly;
                    break;
                }
            }
            if (dependancyAssembly != null)
            {
                Debug.Log("Assembly : " + dependancyAssembly.GetName().Name + " | Version : " + dependancyAssembly.GetName().Version + " found !");
                Debug.Log("Minimal version needed is : " + minimalVersion);

                if (dependancyAssembly.GetName().Version.Major < minimalVersionMajor || dependancyAssembly.GetName().Version.Minor < minimalVersionMinor || dependancyAssembly.GetName().Version.Build < minimalVersionBuild)
                {
                    Debug.LogError(assemblyName + " version " + dependancyAssembly.GetName().Version + "is not compatible with " + currentModName + "!");
                    var sb = new StringBuilder();
                    sb.AppendFormat(assemblyName + " version must be " + minimalVersion + " or greater for this version of " + currentModName + "."); sb.AppendLine();
                    sb.AppendFormat("Please update " + assemblyName + " to the latest version."); sb.AppendLine();
                    PopupDialog.SpawnPopupDialog(currentModName + "/" + assemblyName + " Version mismatch", sb.ToString(), "OK", false, HighLogic.Skin);
                }
            }
            else if (checkPresence)
            {
                Debug.LogError(assemblyName + " not found !");
                var sb = new StringBuilder();
                sb.AppendFormat(assemblyName + " is required for " + currentModName + "."); sb.AppendLine();
                sb.AppendFormat("Please install " + assemblyName + " before using " + currentModName + "."); sb.AppendLine();
                PopupDialog.SpawnPopupDialog(assemblyName + " not found !", sb.ToString(), "OK", false, HighLogic.Skin);
            }
        }
    }
}
