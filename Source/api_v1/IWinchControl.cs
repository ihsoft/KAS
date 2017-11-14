// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace KASAPIv1 {

/// <summary>Interface that allows operating the winch parts.</summary>
public interface IWinchControl {
  /// <summary>Sets the deployed cable length.</summary>
  /// <remarks>
  /// If the new value is significantly less than the old one, then the physical effects may
  /// trigger.
  /// </remarks>
  /// <param name="length">
  /// The new length. Set it to <c>PositiveInfinity</c> to extend the cable at the maximum length.
  /// Omit the parameter to macth the length to the current distance to the connector (stretch the
  /// cable).
  /// </param>
  void SetCableLength(float? length = null);
}

}  // namespace
