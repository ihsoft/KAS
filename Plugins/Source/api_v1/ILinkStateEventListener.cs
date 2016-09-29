// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;

namespace KASAPIv1 {

/// <summary>Part module interface that defines events for link state changes.</summary>
/// <remarks>Both source and target parts can recieve these events. To receive events in the module
/// just implement this interface.</remarks>
//TODO(ihsoft): Add code samples.
public interface ILinkStateEventListener {
  /// <summary>Triggers when a source on the part has created a link.</summary>
  /// <param name="info">Source and target information about the link.</param>
  void OnKASLinkCreatedEvent(KASEvents.LinkEvent info);

  /// <summary>Triggers when a source on the part has broke the link.</summary>
  /// <param name="info">Source and target information about the link.</param>
  void OnKASLinkBrokenEvent(KASEvents.LinkEvent info);
}

// FIXME send it from source with a threshold
public interface ILinkNodesEventListener {
  /// <summary>Triggers when either source or target node has changed position.</summary>
  /// <remarks>
  /// Sent by the link source implementation when the nodes have significantly changed their
  /// positions. It's up to the sender's implementation to decide how to define what is a
  /// "significant change" but the rule of thumb is not sending this event due to normal physics
  /// calculation errors. Though, listeners must be prepared to get this event several times per a
  /// frame.
  /// </remarks>
  /// <param name="info">Source and target information about the link.</param>
  void OnKASLinkNodesMovedEvent(KASEvents.LinkEvent info);
}

}  // namespace
