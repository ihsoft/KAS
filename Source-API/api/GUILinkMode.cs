// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv1 {

/// <summary>Specifies how the linking mode is displayed in GUI.</summary>
public enum GUILinkMode {
  /// <summary>Uninitialized value. Must never be used in the real calls.</summary>
  None = 0,
  /// <summary>
  /// The ending part of the link will expect the player's input to complete or cancel the link.
  /// </summary>
  Interactive,
  /// <summary>No GUI interaction is expected to complete the link.</summary>
  API,
}

}  // namespace
