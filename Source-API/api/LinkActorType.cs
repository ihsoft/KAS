// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv1 {

/// <summary>Defines an actor that changes KAS link.</summary>
/// <remarks>
/// The implementations of <see cref="ILinkSource"/> and <see cref="ILinkTarget"/> may check the
/// type to determine how the action needs to be presented to the player. The type
/// <see cref="LinkActorType.API"/> must never be presented to the player, it's used by the internal
/// logic to manage the sate of the links. For all the other types it's up to the implementation how
/// to present it.
/// </remarks>
/// <seealso cref="IKasLinkEvent"/>
/// <example><code source="Examples/ILinkSource-Examples.cs" region="DisconnectParts"/></example>
public enum LinkActorType {
  /// <summary>Actor is unspecified.</summary>
  /// <remarks>
  /// It really depends on the situation how to treat this actor. In a normal case ther is always a
  /// specific actor set, but if an event originator cannot determine the actor then this type can
  /// be used. However, the event originator must ensure that the components that receive this event
  /// will know how to deal with it.
  /// </remarks>
  None = 0,
  /// <summary>Thrid-party code has affected the link during its normal workflow.</summary>
  /// <remarks>The implementations must <i>not</i> execute any user facing effects when the action
  /// is executed from the API.
  /// </remarks>
  API,
  /// <summary>Link has changed as a result of physics effect.</summary>
  Physics,
  /// <summary>Player input has affected the link state.</summary>
  Player
}

}  // namespace
