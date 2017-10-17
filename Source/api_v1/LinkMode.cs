// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv1 {

/// <summary>Defines how source connects to the target in terms of part hierarchy.</summary>
public enum LinkMode {
  /// <summary>
  /// Tie two parts given they belong to the different vessels. This mode is not allowed on the
  /// parts that belong to the same vessel.
  /// </summary>
  TiePartsOnDifferentVessels,
  /// <summary>
  /// Tie two parts within same vessel. This mode requires for the two parts to belong to the same
  /// vessel.
  /// </summary>
  TiePartsOnSameVessel,
  /// <summary>
  /// Tie two parts regardless what vessel own them.
  /// </summary>
  TieAnyParts,
}

}  // namespace
