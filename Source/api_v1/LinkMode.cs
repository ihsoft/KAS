// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

namespace KASAPIv1 {

/// <summary>Defines how source connects to the target in terms of part hierarchy.</summary>
public enum LinkMode {
  /// <summary>
  /// Merge two different vessels into one. This mode is not allowed on the parts that belong to the
  /// same vessel.
  /// </summary>
  DockVessels,
  /// <summary>
  /// Tie two different vessel with a joint but keep them separate. This mode is not allowed on the
  /// parts that belong to the same vessel.
  /// </summary>
  TieVessels,
  /// <summary>
  /// Tie two parts within same vessel by creating a joint. This mode requires for the two parts to
  /// belong to the same vessel.
  /// </summary>
  Strut
}

}  // namespace
