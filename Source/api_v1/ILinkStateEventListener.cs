// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv1 {

/// <summary>Part module interface that defines the events for a link state changes.</summary>
/// <remarks>
/// Both the source and the target parts can recieve these events. To receive the events, a module
/// needs to implement this interface.
/// </remarks>
//TODO(ihsoft): Add code samples.
public interface ILinkStateEventListener {
  /// <summary>Triggers when a source on the part has created a link.</summary>
  /// <remarks>
  /// This event triggers <b>after</b> the physics changes on the part have been done. The listeners
  /// can expect the joint logic is setup, but the actual physics may not have kicked in yet.
  /// </remarks>
  /// <param name="info">Source and target information about the link.</param>
  void OnKASLinkCreatedEvent(KASEvents.LinkEvent info);

  /// <summary>Triggers when a source on the part has broke the link.</summary>
  /// <remarks>
  /// This event triggers <b>after</b> the physics changes on the part have been done. The listeners
  /// can expect the joint logic is setup, but some or all of the physical objects still can exist. 
  /// </remarks>
  /// <param name="info">Source and target information about the link.</param>
  void OnKASLinkBrokenEvent(KASEvents.LinkEvent info);

  /// <summary>
  /// Triggers when a peer locks itself due to its attach node is blocked by an incompatible part.
  /// </summary>
  /// <remarks>
  /// This event triggers <b>after</b> the link state has changed. The handlers must not change the
  /// state of the triggering module synchronously since there can be other modules which haven't
  /// handled the event yet. However, it can be done asynchronously when needed (schedule execution
  /// at the end of frame).
  /// </remarks>
  /// <param name="ownerPeer">The peer which goes into the (un)blocked state.</param>
  /// <param name="isBlocked">Tells if the peer got blocked or unblocked.</param>
  /// <seealso cref="LinkState.NodeIsBlocked"/>
  void OnKASNodeBlockedState(ILinkPeer ownerPeer, bool isBlocked);
}

}  // namespace
