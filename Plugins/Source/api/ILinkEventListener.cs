// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;

namespace KAS_API {

/// <summary>An interface that defines part scope events.</summary>
/// <remarks>All these events are called for every module on the part when the relevant event has
/// triggered. Events are sent via Unity messaging mechanism, so it's not required to implement the
/// interface in the component to start listenening. Though, explicit interface declaration will
/// help maintaining the module.
/// <para>It's required for non-sealed classes to implement the methods as <c>virtual</c>.
/// Otherwise, only the sub-class will be notified, and all the parents won't have ability to react.
/// </para>
/// </remarks>
//TODO(ihsoft): Add code samples.
public interface ILinkEventListener {
  /// <summary>Triggers when a source on the part has created a link.</summary>
  /// <remarks>Sent by either source or target link implementation when a new link has been
  /// esatblished.</remarks>
  /// <param name="info">Source and target information about the link.</param>
  void OnKASLinkCreatedEvent(KASEvents.LinkInfo info);

  /// <summary>Triggers when a source on the part has broke the link.</summary>
  /// <remarks>Sent by either source or target link implementation when a link has been
  /// broken.</remarks>
  /// <param name="info">Source and target information about the link.</param>
  void OnKASLinkBrokenEvent(KASEvents.LinkInfo info);
}

}  // namespace
