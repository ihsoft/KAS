// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using UnityEngine;

namespace KASAPIv1 {

[KSPAddon(KSPAddon.Startup.Instantly, true /*once*/)]
class KASAPILauncher : MonoBehaviour {
  public static void LoadApi() {
    if (!KASAPI.isLoaded) {
      KASAPI.JointUtils = new KASImpl.JointUtilsImpl();
      KASAPI.AttachNodesUtils = new KASImpl.AttachNodesUtilsImpl();
      KASAPI.LinkUtils = new KASImpl.LinkUtilsImpl();
      KASAPI.isLoaded = true;
      Debug.LogWarning("KAS API v1 LOADED");
    }
  }
  
  void Awake() {
    LoadApi();
  }
}

}  // namespace
