// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace KASAPIv1 {

/// <summary>Specifies how the linking mode is displayed in GUI.</summary>
public enum GUILinkMode {
  /// <summary>Uninitialized or no GUI mode pending.</summary>
  None = 0,
  /// <summary>
  /// The ending part of the link will be bound to the EVA kerbonaut until the link is completed or
  /// cancelled.
  /// </summary>
  Eva,
  /// <summary>
  /// The ending part of the link will be bound to the current mouse position until the link is
  /// completed or cancelled.
  /// </summary>
  Interactive,
  /// <summary>No GUI appearence is made for the linking.</summary>
  API,
}

}  // namespace
