// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;

namespace KAS_API {

/// <summary>A generic source of a KAS link between two parts.</summary>
/// <remarks>Source is the initiator of the link to another part. It holds all the logic on making
/// and maintaining actual connection between two parts. The other end of the connection must be
/// <see cref="ILinkTarget"/> which implements own piece of logic.</remarks>
/// FIXME(ihsoft): Add state transtion diagram reference.
/// FIXME(ihsoft): Add code samples.
public interface ILinkSource {
  /// <summary>Part that owns the source.</summary>
  Part part { get; }

  /// <summary>A source link type identifier.</summary>
  /// <remarks>This type is used to match with compatible targets. Targets of different types will
  /// not be able to connect with the source. Type can be any string, including empty.</remarks>
  string linkType { get; }

  /// <summary>Linked target or <c>null</c> if nothing is linked.</summary>
  ILinkTarget linkTarget { get; }

  /// <summary>Current state of the source.</summary>
  /// <remarks>The state cannot be affected directly. Different methods change it to different
  /// values. Though, there is strict model of state tranistioning for the source.</remarks>
  /// FIXME(ihsoft): Add state transtion diagram.
  LinkState linkState { get; }

  /// <summary>Defines if source must not initiate link requests.</summary>
  /// <remarks>Setting of this property changes source state. Decendants can react on this action to
  /// do internal state adjustments (e.g. changing UI items visibility).</remarks>
  bool isLocked { get; set; }

  /// <summary>Starts linking mode of this source.</summary>
  /// <remarks>Only one source at time can be linking on the part. If part has more sources or
  /// targets they all will get <see cref="LinkState.Locked"/>.</remarks>
  /// <param name="mode">Defines how pending link should be displayed. See <see cref="LinkMode"/>
  /// for more details.</param>
  void StartLinking(GUILinkMode mode);

  /// <summary>Cancels linking mode without creating a link.</summary>
  /// <remarks>All sources and targets that were locked on mode start will be unlocked.</remarks>
  void CancelLinking();

  /// <summary>Establishes a link between two parts.</summary>
  /// <remarks>Depending on the implementation the link can be logical, physical, or both. Use the
  /// right implementation when calling this method to get the right output.
  /// <para>Conditions for the link are checked via <see cref="CheckCanLinkTo"/>, and all errors
  /// are reported to the GUI.</para>
  /// </remarks>
  /// <param name="target">Target to link with.</param>
  /// <returns><c>true</c> if parts were linked successfully.</returns>
  bool LinkToTarget(ILinkTarget target);

  /// <summary>Breaks a link between source and the current target.</summary>
  /// <remarks>Does nothing if there is no link but a warning will be logged in this case.</remarks>
  void BreakCurrentLink();

  /// <summary>Verifies if link between the parts can be successful.</summary>
  /// <param name="target">Target to connect with.</param>
  /// <param name="reportToGUI">If <c>true</c> then errors will be reported to the UI letting user
  /// know the link cannot be made. By default this mode is OFF.</param>
  /// <param name="reportToLog">If <c>true</c> then errors will be logged to the logs as warnings.
  /// Disabling of such logging makes sense when caller code just checks for the possibility of
  /// the link (e.g. when showing UI elements). If <paramref name="reportToGUI"/> set to <c>true</c>
  /// then errors will be logged regardless to the setting of this parameter. By default logging
  /// mode is ON.</param>
  /// <returns><c>true</c> if link can be made.</returns>
  bool CheckCanLinkTo(ILinkTarget target, bool reportToGUI = false, bool reportToLog = true);
}

}  // namespace
