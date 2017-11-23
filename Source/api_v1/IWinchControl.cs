// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace KASAPIv1 {

/// <summary>Interface that allows operating the winch parts.</summary>
public interface IWinchControl {
  /// <summary>Maximum speed of retracting or release the cable.</summary>
  /// <value>Speed in meters per second.</value>
  /// <seealso cref="motorState"/>
  /// <seealso cref="SetMotor"/>
  float cfgMotorMaxSpeed { get; }

  /// <summary>Maximum reserve of the cable in the winch.</summary>
  /// <remarks>
  /// This is the maximum possible distance between the winch and its connector head. 
  /// </remarks>
  /// <value>The length of the cable in meters.</value>
  float cfgMaxCableLength { get; }

  /// <summary>Sate of the winch connector head.</summary>
  /// <value>The connector state.</value>
  WinchConnectorState connectorState { get; }
  
  /// <summary>State of the winch motor.</summary>
  /// <value>The motor state.</value>
  WinchMotorState motorState { get; }

  /// <summary>Amount of the cable that was extended till the moment.</summary>
  /// <remarks>
  /// This value is dynamic and can be affected by the motor.
  /// </remarks>
  /// <value>The length of the cable in meters.</value>
  /// <seealso cref="SetMotor"/>
  float currentCableLength { get; }

  /// <summary>Current speed of the motor spindel.</summary>
  /// <remarks>
  /// This is the speed at which the cable is being extended or retracted at the current moment.
  /// The actual speed of the motor can differ from what was set via the control methods (e.g.
  /// <see cref="SetMotor"/>) due to there is some inetria momentum. Negative speed means the cable
  /// is being retracted, and the positive speed means the cable is being extened.
  /// </remarks>
  /// <value>
  /// The speed in meters per second. A negative value means the cable is being retracting.
  /// </value>
  /// <seealso cref="SetMotor"/>
  float motorCurrentSpeed { get; }

  /// <summary>Desired speed of the motor spindel.</summary>
  /// <remarks>
  /// Ideally, the motor is always working at this speed. However, in the physics world of KSP the
  /// motor may operate at the lower or the higher speeds. It depends of the various conditions.
  /// </remarks>
  /// <seealso cref="motorCurrentSpeed"/>
  /// <seealso cref="SetMotor"/>
  float motorTargetSpeed { get; }

  /// <summary>Sets the winch motor to the desired speed.</summary>
  /// <remarks>
  /// <para>
  /// The motor is responsible for the deployed cable length changing. It can extend the cable,
  /// retract the cable, or do nothing (idle). The winch and its head cannot get separated at a
  /// greater distance than the current deployed cable length. That said, the motor is controlling
  /// the distance.
  /// </para>
  /// <para>
  /// The motor speed is not required to change immediately. The motor may need some time to get to
  /// the target speed. It depends on the part implementation and configuration. The rule of thumb
  /// is to not expect the <see cref="motorCurrentSpeed"/> to match the
  /// <paramref name="targetSpeed"/> right after the method call. There may be some time needed
  /// before the values will match.
  /// </para>
  /// <para>
  /// Setting the motor speed may affect its state, as well as the connector state. E.g. if the
  /// motor is <i>idle</i>, and the target speed is set to a positive value, then the motor state
  /// will switch to <see cref="WinchMotorState.Extending"/>. Likewise, if the connector was locked
  /// and the motor speed is set to a positive value (extending), then the connector is get
  /// deployed.
  /// </para>
  /// <para>
  /// The motor will automatically stop when the cable length reaches zero or the maximum allowed
  /// value. In case of the zero length, the connector will be attempted to lock into the winch.
  /// This attempt may fail due to the bad align of the connector. To retry the attempt, just call
  /// this method again with a negative value.
  /// </para>
  /// </remarks>
  /// <param name="targetSpeed">
  /// The new speed of the motor. The <i>positive</i> value instructs to extend the cable, and the
  /// <i>negative</i> value commands to retract the cable. Zero value turns the motor off. The
  /// infinite values can be used to set the target speed to the maximum allowed speed on the part.
  /// </param>
  /// <seealso cref="motorState"/>
  /// <seealso cref="connectorState"/>
  /// <seealso cref="cfgMaxCableLength"/>
  /// <seealso cref="currentCableLength"/>
  void SetMotor(float targetSpeed);
  
  /// <summary>
  /// Sets the deployed cable length to the actual distance between the winch and the connector.
  /// </summary>
  /// <remarks>This will "stretch" the cable by reducing the unused cable.</remarks>
  /// <seealso cref="currentCableLength"/>
  void StretchCable();

  /// <summary>Sets the deployed cable length to the maximum value allowed by the part.</summary>
  /// <seealso cref="cfgMaxCableLength"/>
  void ReleaseCable();
}

}  // namespace
