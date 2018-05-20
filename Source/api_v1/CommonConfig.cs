// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ConfigUtils;

namespace KASAPIv1 {

/// <summary>Container for the various global settings of the mod.</summary>
//FIXME: Make it API compatible (via an interface).
[PersistentFieldsFile("KAS-1.0/settings.cfg", "KASConfig")]
public static class CommonConfig {
  /// <summary>URL of the sound for the impossible action.</summary>
  [PersistentField("Sounds/bipWrong")]
  public static string sndPathBipWrong = "KAS-1.0/Sounds/bipwrong2";

  /// <summary>Keyboard key to trigger the drop connector event.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [PersistentField("Winch/dropConnectorKey")]
  public static string keyDropConnector = "Y";

  /// <summary>Keyboard key to trigger the pickup connector event.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [PersistentField("Winch/pickupConnectorKey")]
  public static string keyPickupConnector = "Y";

  internal static void Load() {
    ConfigAccessor.ReadFieldsInType(typeof(CommonConfig), null);
  }
}
  
}  // namespace
