// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.FSUtils;
using UnityEngine;

namespace KASAPIv1 {

[KSPAddon(KSPAddon.Startup.Instantly, true /*once*/)]
sealed class KASAPILauncher : MonoBehaviour {
  void LoadApi() {
    if (!KASAPI.isLoaded) {
      KASAPI.JointUtils = new KASImpl.JointUtilsImpl();
      KASAPI.AttachNodesUtils = new KASImpl.AttachNodesUtilsImpl();
      KASAPI.LinkUtils = new KASImpl.LinkUtilsImpl();
      KASAPI.PhysicsUtils = new KASImpl.PhysicsUtilsImpl();
      CommonConfig.Load();
      KASAPI.isLoaded = true;

      var assembly = GetType().Assembly;
      Debug.LogWarningFormat("Loading KAS API v1 from: {0} (v{1})",
                             KspPaths.MakeRelativePathToGameData(assembly.Location),
                             assembly.GetName().Version);
    }
  }
  
  void Awake() {
    LoadApi();
  }
}

}  // namespace
