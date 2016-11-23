// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace KASAPIv1 {

/// <summary>Part module interface that defines events for link state changes.</summary>
/// <remarks>Both source and target parts can recieve these events. To receive events in the module
/// just implement this interface.</remarks>
//TODO(ihsoft): Add code samples.
public interface ILinkStateEventListener {
  /// <summary>Triggers when a source on the part has created a link.</summary>
  /// <remarks>
  /// This event triggers <b>after</b> the physics changes on the part. Modules can expect the
  /// joints logic is setup but actual physics may not have kicked in yet.
  /// </remarks>
  /// <param name="info">Source and target information about the link.</param>
  void OnKASLinkCreatedEvent(KASEvents.LinkEvent info);

  /// <summary>Triggers when a source on the part has broke the link.</summary>
  /// <remarks>
  /// This event comes at different link states depending on what has initiated the link break:
  /// <list>
  /// <item>
  /// If link has been broken via KAS source (<see cref="ILinkSource.BreakCurrentLink"/>) then this
  /// event is fired <b>before</b> the physics changes.
  /// </item>
  /// <item>
  /// If link has been broken externally, e.g. via physics or by invoking
  /// <see cref="Part.decouple"/> method, then this event is fired <b>after</b> the physics changes.
  /// </item>
  /// </list>
  /// </remarks>
  /// <param name="info">Source and target information about the link.</param>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_part.html#a397e83d33e3053648580246c5bfc6408">
  /// KSP: Part.decouple</seealso>
  void OnKASLinkBrokenEvent(KASEvents.LinkEvent info);
}

}  // namespace
