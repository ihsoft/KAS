// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv1 {
  
/// <summary>State of the winch connector.</summary>
public enum WinchConnectorState {
  /// <summary>
  /// The connector is rigidly attached to the winch's body. The connector's model is a parent of
  /// the winch's model.
  /// </summary>
  Locked,

  /// <summary>
  /// The connector is a standalone physical object, attached to the winch via a cable.
  /// </summary>
  Deployed,

  /// <summary>
  /// The connector is plugged into a link target. It doesn't have physics, and its model is part of
  /// the target's model.
  /// </summary>
  /// <remarks>
  /// This state can only exist if the winch's link source is linked to a target.
  /// </remarks>
  /// <seealso cref="ILinkTarget"/>
  Plugged,
}

}  // namespace
