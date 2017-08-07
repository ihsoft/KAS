// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ConfigUtils;

namespace KASAPIv1 {

/// <summary>Container for the various global settings of the mod.</summary>
//FIXME: Make it API compatible (via an interface).
[PersistentFieldsFile("KAS-1.0/PluginData/settings.cfg", "UI")]
public static class CommonConfig {
  /// <summary>URL of the sound for the impossible action.</summary>
  [PersistentField("Sounds/bipWrong")]
  public static string sndPathBipWrong = "KAS-1.0/Sounds/bipwrong";

  internal static void Load() {
    ConfigAccessor.ReadFieldsInType(typeof(CommonConfig), null);
  }
}
  
}  // namespace
