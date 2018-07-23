// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv1 {

/// <summary>Container for the various global settings of the mod.</summary>
public interface ICommonConfig {
  /// <summary>URL of the sound for the impossible action.</summary>
  string sndPathBipWrong { get; }

  /// <summary>Keyboard key to trigger the drop connector event.</summary>
  string keyDropConnector { get; }

  /// <summary>Keyboard key to trigger the pickup connector event.</summary>
  string keyPickupConnector { get; }
}
  
}  // namespace
