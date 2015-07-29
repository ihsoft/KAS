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
        int minimalVersionMinor = 2;
        int minimalVersionBuild = 1;
        bool checkPresence = false;

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
                int dependancyAssemblyVersion = (dependancyAssembly.GetName().Version.Major * 100) + (dependancyAssembly.GetName().Version.Minor * 10) + (dependancyAssembly.GetName().Version.Build);
                int minimalAssemblyVersion = (minimalVersionMajor * 100) + (minimalVersionMinor * 10) + (minimalVersionBuild);
                Debug.Log("INT : " + dependancyAssemblyVersion + "/" + minimalAssemblyVersion);
                if (dependancyAssemblyVersion < minimalAssemblyVersion)
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
