using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using System.Text;

namespace KAS {

[KSPAddon(KSPAddon.Startup.MainMenu, true)]
internal class DependancyChecker : MonoBehaviour {
  const string currentModName = "KAS";
  const string assemblyName = "KIS";
  const int minimalVersionMajor = 1;
  const int minimalVersionMinor = 2;
  const int minimalVersionBuild = 1;

  public void Start() {
    string minimalVersion = minimalVersionMajor + "." + minimalVersionMinor + "." + minimalVersionBuild;
    Assembly dependancyAssembly = AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(x => x.GetName().Name == assemblyName);
    if (dependancyAssembly != null) {
      Debug.LogFormat("Assembly : {0} | Version : {1} found !",
                      dependancyAssembly.GetName().Name, dependancyAssembly.GetName().Version);
      Debug.LogFormat("Minimal version needed is : {0}", minimalVersion);
      int dependancyAssemblyVersion =
          (dependancyAssembly.GetName().Version.Major * 100)
          + (dependancyAssembly.GetName().Version.Minor * 10)
          + (dependancyAssembly.GetName().Version.Build);
      const int minimalAssemblyVersion =
          (minimalVersionMajor * 100) + (minimalVersionMinor * 10) + (minimalVersionBuild);
      Debug.Log("INT : " + dependancyAssemblyVersion + "/" + minimalAssemblyVersion);
      if (dependancyAssemblyVersion < minimalAssemblyVersion) {
        Debug.LogErrorFormat("{0} version {1}is not compatible with {2}!",
                             assemblyName, dependancyAssembly.GetName().Version, currentModName);
        var sb = new StringBuilder();
        sb.AppendFormat("{0} version must be {1} or greater for this version of {2}.",
                        assemblyName, minimalVersion, currentModName);
        sb.AppendLine();
        sb.AppendFormat("Please update {0} to the latest version.", assemblyName);
        sb.AppendLine();
        PopupDialog.SpawnPopupDialog(
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            currentModName + "/" + assemblyName + " Version mismatch", sb.ToString(),
            "OK", false, HighLogic.UISkin);
      }
    }
  }
}

}  // namespace
