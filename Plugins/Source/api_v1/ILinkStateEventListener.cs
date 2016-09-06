// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;

namespace KASAPIv1 {

/// <summary>Interface that defines part scope events for links state changes.</summary>
/// <remarks>All these events are called for every module on the part when the relevant event has
/// triggered. Events are sent via Unity messaging mechanism, so it's not required to implement the
/// interface in the component to start listenening. Though, explicit interface declaration will
/// help maintaining the module.
/// <para>It's required for non-sealed classes to implement the methods as <c>virtual</c>.
/// Otherwise, only the sub-class will be notified, and all the parents won't have ability to react.
/// </para>
/// </remarks>
//TODO(ihsoft): Add code samples.
//FIXME: change doc to not mentioning unity message 
public interface ILinkStateEventListener {
  /// <summary>Triggers when a source on the part has created a link.</summary>
  /// <remarks>Sent by the link source implementation when a new link has been established.
  /// </remarks>
  /// <param name="info">Source and target information about the link.</param>
  void OnKASLinkCreatedEvent(KASEvents.LinkEvent info);

  /// <summary>Triggers when a source on the part has broke the link.</summary>
  /// <remarks>Sent by the link source implementation when an existing link has been broken.
  /// </remarks>
  /// <param name="info">Source and target information about the link.</param>
  void OnKASLinkBrokenEvent(KASEvents.LinkEvent info);
}

public interface ILinkNodesEventListener {
  /// <summary>Triggers when either source or target node has changed position.</summary>
  /// <remarks>Sent by the link source implementation when the nodes have significantly changed
  /// their positions. It's up to the sender's implementation to decide how to define what is a
  /// "significant change" but the rule of thumb is not sending this event due to normal physics
  /// calculation errors. Though, listeners must be prepared to get this event several times per a
  /// frame.</remarks>
  /// <param name="info">Source and target information about the link.</param>
  void OnKASLinkNodesMovedEvent(KASEvents.LinkEvent info);
}

}  // namespace
