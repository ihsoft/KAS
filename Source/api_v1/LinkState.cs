// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv1 {

/// <summary>Defines currect state of the link.</summary>
/// <remarks>Each implementation defines own state tranistion model. E.g.
/// <see cref="ILinkSource"/> or <see cref="ILinkTarget"/>. In every state the module can only
/// handle a very specific set of actions. Such approach helps keeping module logic more clear and
/// granular.
/// </remarks>
/// <seealso cref="KSPDev.ProcessingUtils.SimpleStateMachine&lt;T&gt;"/>
public enum LinkState {
  /// <summary>Initial and an invalid state. It must never be normally used.</summary>
  None = 0,
  /// <summary>Module is avalable for the links.</summary>
  Available,
  /// <summary>
  /// Module is unavailable for the link because of another module on the part has established one. 
  /// </summary>
  Locked,
  /// <summary>
  /// Module has initated an outgoing link request and expecting for it to be accepted.
  /// </summary>
  Linking,
  /// <summary>Module is linked to another module.</summary>
  Linked,
  /// <summary>Module is ready to accept a link and <i>may</i> accept the request.</summary>
  /// <remarks>It means all the reasonable conditions are met. Though, the link still can fail on
  /// the final attempt.</remarks>
  AcceptingLinks,
  /// <summary>Module doesn't accept link and will reject any request.</summary>
  RejectingLinks
}

}  // namespace
