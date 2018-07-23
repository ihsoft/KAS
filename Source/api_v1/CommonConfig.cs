// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv1 {

/// <summary>Container for the various global settings of the mod.</summary>
//FIXME: Make it API compatible (via an interface).
public static class CommonConfig {
  /// <summary>URL of the sound for the impossible action.</summary>
  public static string sndPathBipWrong = "";

  /// <summary>Keyboard key to trigger the drop connector event.</summary>
  public static string keyDropConnector = "";

  /// <summary>Keyboard key to trigger the pickup connector event.</summary>
  public static string keyPickupConnector = "";

  internal static void Load() {
    ConfigAccessor.ReadFieldsInType(typeof(CommonConfig), null);
  }
}
  
}  // namespace
