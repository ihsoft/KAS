// Kerbal Attachment System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv1 {

/// <summary>Part module interface that defines the events for a link state changes.</summary>
/// <remarks>
/// Both the source and the target parts can recieve these events. To receive the events, a module
/// needs to implement this interface.
/// </remarks>
public interface ILinkStateEventListener {
  /// <summary>Triggers when any module on the part has created a link.</summary>
  /// <remarks>
  /// This is a notification event. When it triggers, the modules, involved in the link, has already
  /// completed their settings change.
  /// </remarks>
  /// <param name="info">The source and target information about the link.</param>
  /// <param name="isLinked">The new link state.</param>
  void OnKASLinkedState(IKasLinkEvent info, bool isLinked);

  /// <summary>
  /// Triggers when a peer locks itself due to its attach node is blocked by an incompatible part.
  /// </summary>
  /// <remarks>
  /// The event is sent to all the modules on the part except the module which triggred the event.
  /// It allows coordinating the work of a group of link modules on the same part. The event
  /// handlers must not synchronously affect the state of module which triggered the event.
  /// </remarks>
  /// <param name="ownerPeer">The peer which goes into the (un)blocked state.</param>
  /// <param name="isBlocked">Tells if the peer got blocked or unblocked.</param>
  /// <seealso cref="LinkState.NodeIsBlocked"/>
  void OnKASNodeBlockedState(ILinkPeer ownerPeer, bool isBlocked);
}

}  // namespace
