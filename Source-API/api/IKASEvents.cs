// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv1 {

/// <summary>Defines global events that are triggered by KAS.</summary>
public interface IKasEvents {
  /// <summary>Triggers when a source has initiated linking mode.</summary>
  EventData<ILinkSource> OnStartLinking { get; }

  /// <summary>Triggers when source has stopped linking mode.</summary>
  EventData<ILinkSource> OnStopLinking { get; }

  /// <summary>Triggers when link between two parts has been successfully established.</summary>
  /// <remarks>
  /// Consider using <see cref="ILinkStateEventListener.OnKASLinkedState"/> when this state change
  /// is needed in scope of just one part.
  /// </remarks>
  EventData<IKasLinkEvent> OnLinkCreated { get; }

  /// <summary>Triggers when link between two parts has been broken.</summary>
  /// <remarks>
  /// Consider using <see cref="ILinkStateEventListener.OnKASLinkedState"/> when this state change
  /// is needed in scope of just one part.
  /// </remarks>
  EventData<IKasLinkEvent> OnLinkBroken { get; }
}

}  // namespace
