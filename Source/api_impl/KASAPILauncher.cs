// Kerbal Attachment System API
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.FSUtils;
using KSPDev.LogUtils;
using UnityEngine;

namespace KASAPIv2 {

[KSPAddon(KSPAddon.Startup.Instantly, true /*once*/)]
sealed class KASAPILauncher : MonoBehaviour {
  void Awake() {
    if (!KASAPI.isLoaded) {
      KASAPI.JointUtils = new KASImpl.JointUtilsImpl();
      KASAPI.AttachNodesUtils = new KASImpl.AttachNodesUtilsImpl();
      KASAPI.LinkUtils = new KASImpl.LinkUtilsImpl();
      KASAPI.PhysicsUtils = new KASImpl.PhysicsUtilsImpl();
      KASAPI.CommonConfig = new KASImpl.CommonConfigImpl();
      KASAPI.KasEvents = new KASImpl.KasEventsImpl();
      KASAPI.isLoaded = true;

      var assembly = GetType().Assembly;
      DebugEx.Info("Loading KAS API v2 from: {0} (v{1})",
                   KspPaths.MakeRelativePathToGameData(assembly.Location),
                   assembly.GetName().Version);
    }
  }
}

}  // namespace
