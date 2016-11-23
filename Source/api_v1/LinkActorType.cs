// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace KASAPIv1 {

/// <summary>Defines an actor that changes KAS link.</summary>
/// <seealso cref="KASEvents.LinkEvent"/>
public enum LinkActorType {
  /// <summary>Actor is unspecified.</summary>
  None = 0,
  /// <summary>Thrid-party code has affected the link during its normal workflow.</summary>
  API,
  /// <summary>Link has changed as a result of physics effect.</summary>
  Physics,
  /// <summary>Player input has affected the link state.</summary>
  Player
}

}  // namespace
