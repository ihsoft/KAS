// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ConfigUtils;
using KSPDev.FSUtils;
using KSPDev.LogUtils;
using UnityEngine;

namespace KASAPIv1 {

[KSPAddon(KSPAddon.Startup.Instantly, true /*once*/)]
sealed class KASAPILauncher : MonoBehaviour {
  const string CommonConfigFile = "KAS-1.0/settings.cfg";
  const string CommonConfigNode = "KASConfig";
  
  void LoadApi() {
    if (!KASAPI.isLoaded) {
      KASAPI.JointUtils = new KASImpl.JointUtilsImpl();
      KASAPI.AttachNodesUtils = new KASImpl.AttachNodesUtilsImpl();
      KASAPI.LinkUtils = new KASImpl.LinkUtilsImpl();
      KASAPI.PhysicsUtils = new KASImpl.PhysicsUtilsImpl();
      LoadCommonConfig();
      KASAPI.isLoaded = true;

      var assembly = GetType().Assembly;
      DebugEx.Info("Loading KAS API v1 from: {0} (v{1})",
                   KspPaths.MakeRelativePathToGameData(assembly.Location),
                   assembly.GetName().Version);
    }
  }
  
  void Awake() {
    LoadApi();
  }

  void LoadCommonConfig() {
    var node = ConfigNode.Load(KspPaths.MakeAbsPathForGameData(CommonConfigFile));
    if (node != null && CommonConfigNode.Length > 0) {
      node = node.GetNode(CommonConfigNode);
    }
    if (node != null) {
      CommonConfig.keyDropConnector =
          ConfigAccessor.GetValueByPath(node, "Winch/dropConnectorKey");
      CommonConfig.keyPickupConnector =
          ConfigAccessor.GetValueByPath(node, "Winch/pickupConnectorKey");
      CommonConfig.sndPathBipWrong =
          ConfigAccessor.GetValueByPath(node, "Sounds/bipWrong");
    } else {
      DebugEx.Error("Cannot load the KAS common config: node={0}, file={1}",
                    CommonConfigNode, CommonConfigFile);
    }
  }
}

}  // namespace
