// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv1 {

/// <summary>State of the winch's motor.</summary>
public enum WinchMotorState {
  /// <summary>The motor is not spinning.</summary>
  /// <remarks>In this mode the electric charge is <i>not</i> being consumed.</remarks>
  Idle,

  /// <summary>The motor is spinning, giving an extra length of the available cable.</summary>
  /// <remarks>In this mode the electric charge is being consumed.</remarks>
  Extending,

  /// <summary>The motor is spinning, reducing the length of the available cable.</summary>
  /// <remarks>In this mode the electric charge is being consumed.</remarks>
  Retracting,
}

}  // namespace
