// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using KSPDev.ResourceUtils;
using KSPDev.SoundsUtils;
using System.Linq;
using UnityEngine;

namespace KAS {

/// <summary>Module for a simple winch with a deployable connector and a motor.</summary>
/// <remarks>
/// <para>
/// The connector is attached to the winch with a cable, and it can link with the compatible link
/// targets. The winch itself is a <see cref="ILinkSource">link source</see>. An EVA kerbal can
/// "grab" the connector and carry it as far as the cable maximum length allows.
/// </para>
/// <para>
/// This winch implementation requires the associated joint module to support coupling. The winch
/// cable targets are also required to support coupling. The winch module behavior is undetermined
/// if the coupling is rejected when a plugged connector is being locked (going into the "docked"
/// state).
/// </para>
/// <para>
/// The descendants of this module can use the custom persistent fields of groups:
/// </para>
/// <list type="bullet">
/// <item><c>StdPersistentGroups.PartConfigLoadGroup</c></item>
/// <item><c>StdPersistentGroups.PartPersistant</c></item>
/// </list>
/// </remarks>
/// <seealso cref="ILinkJoint.SetCoupleOnLinkMode"/>
// Next localization ID: #kasLOC_08013.
public class KASLinkWinch : KASLinkSourcePhysical,
    // KAS interfaces.
    IWinchControl,
    // KSPDev syntax sugar interfaces.
    IPartModule, IsPhysicalObject {
  #region Localizable GUI strings.
  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message NoEnergyMsg = new Message(
      "#kasLOC_08000",
      defaultTemplate: "No energy!",
      description: "Error message to present when the electricity charge has exhausted.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message LockConnectorNotAlignedMsg = new Message(
      "#kasLOC_08001",
      defaultTemplate: "Cannot lock the connector: not aligned",
      description: "Error message to present when an improperly aligned cable connector has"
      + " attempted to lock with the winch.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ConnectorLockedMsg = new Message(
      "#kasLOC_08002",
      defaultTemplate: "Connector locked!",
      description: "Info message to present when a cable connector has successfully locked to the"
      + " winch.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ConnectorDockedMsg = new Message(
      "#kasLOC_08003",
      defaultTemplate: "Connector docked to the winch",
      description: "Info message to present when a cable connector has successfully docked to the"
      + " winch.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.DistanceType']/*"/>
  static readonly Message<DistanceType> MaxLengthReachedMsg = new Message<DistanceType>(
      "#kasLOC_08004",
      defaultTemplate: "Maximum cable length reached: <<1>>",
      description: "An info message to present when the cable is extended at its maximum length."
      + "\nArgument <<1>> is the current cable length of type DistanceType.",
      example: "Maximum cable length reached: 1.23 m");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message StopExtendingMenuTxt = new Message(
      "#kasLOC_08005",
      defaultTemplate: "Stop extending",
      description: "Name of the context menu item that stops the cable extending.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ExtendCableMenuTxt = new Message(
      "#kasLOC_08006",
      defaultTemplate: "Extend cable",
      description: "Name of the context menu item that starts the cable extending.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message StopRetractingMenuTxt = new Message(
      "#kasLOC_08007",
      defaultTemplate: "Stop retracting",
      description: "Name of the context menu item that stops the cable retracting.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message RetractCableMenuTxt = new Message(
      "#kasLOC_08008",
      defaultTemplate: "Retract cable",
      description: "Name of the context menu item that starts the cable retracting.");
  #endregion

  #region Part's config fields
  /// <summary>Maximum cable length at which the cable connector can lock to the winch.</summary>
  /// <remarks>
  /// A spring joint in PhysX will never pull the objects together to the zero distance regardless
  /// to the spring strength. For this reason the there should be always be a reasonable error
  /// allowed. Setting the error to a too big value will result in unpleasant locking behavior and
  /// increase the force at which the connector hits the winch on locking. A too small value of the
  /// allowed error will make the locking harder, up to not being able to lock at all.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float connectorLockMaxErrorDist = 0.05f;

  /// <summary>
  /// Maximum direction error to allow for the cable connector to lock to the winch. It's in
  /// degrees.
  /// </summary>
  /// <remarks>
  /// This value is always positive, and it determines how significantly the deriction of
  /// <c>forward</c> and <c>up</c> vectors of the connector can differ from the winch's attach node
  /// direction.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="Unity3D_HelpIndex.xml" path="//item[@name='T:UnityEngine.Vector3']/*"/>
  [KSPField]
  public float connectorLockMaxErrorDir = 1;

  /// <summary>Maximum target speed of the motor. Meters per second.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float motorMaxSpeed = 2;

  /// <summary>
  /// Acceleration to apply to reach the target motor speed. Meters per second squared.
  /// </summary>
  /// <remarks>It must not be <c>0</c>, since in this case the motor will never start.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float motorAcceleration = 0.4f;

  /// <summary>Amount of the electricity to consume each second of the motor activity.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float motorPowerDrain = 0.5f;

  /// <summary>URL of the sound for the working winch motor.</summary>
  /// <remarks>This sound will be looped while the motor is active.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string sndPathMotor = "";

  /// <summary>URL of the sound for the starting winch motor.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string sndPathMotorStart = "";

  /// <summary>URL of the sound for the stopping winch motor.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string sndPathMotorStop = "";
  #endregion

  #region The context menu fields
  /// <summary>A context menu item that presents the deployed cable length.</summary>
  /// <seealso cref="KASJointCableBase.deployedCableLength"/>
  /// <include file="SpecialDocTags.xml" path="Tags/UIConfigSetting/*"/>
  [KSPField(guiActive = true)]
  [LocalizableItem(
      tag = "#kasLOC_08009",
      defaultTemplate = "Deployed cable length",
      description = "A context menu item that presents the length of the currently deployed"
      + " cable.")]
  public string deployedCableLengthMenuInfo = "";
  #endregion

  #region Context menu events/actions
  // Keep the events that may change their visibility states at the bottom. When an item goes out
  // of the menu, its height is reduced, but the lower left corner of the dialog is retained. 

  /// <summary>A context menu item that opens the winches GUI.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true, guiActiveUnfocused = true, guiActiveUncommand = true)]
  [LocalizableItem(
      tag = "#kasLOC_08010",
      defaultTemplate = "Open winches GUI",
      description = "A context menu item that opens the remote control GUI to operate the winches"
      + " in the scene.")]
  public virtual void OpenGUIEvent() {
    ControllerWinchRemote.ToggleGUI(true);
  }

  /// <summary>A context menu item that starts/stops extending the cable.</summary>
  /// <remarks>
  /// If the connector was locked it will be deployed. This method does nothing is the cable cannot
  /// be extended for any reason.
  /// </remarks>
  /// <seealso cref="UpdateContextMenu"/>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true, guiActiveUnfocused = true)]
  [LocalizableItem(tag = null)]
  public virtual void ToggleExtendCableEvent() {
    SetMotor(motorTargetSpeed > 0 ? 0 : float.PositiveInfinity);
  }

  /// <summary>A context menu item that starts/stops retracting the cable.</summary>
  /// <remarks>
  /// If the cable length is zero but the connector is not locked, then this method will try to lock
  /// the connector. It does nothing is the cable cannot be retracted for any reason.
  /// </remarks>
  /// <seealso cref="UpdateContextMenu"/>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true, guiActiveUnfocused = true)]
  [LocalizableItem(tag = null)]
  public virtual void ToggleRetractCableEvent() {
    SetMotor(motorTargetSpeed < 0  ? 0 : float.NegativeInfinity);
  }

  /// <summary>
  /// A context menu item that sets the cable length ot the maximum, and unlocks the connector if it
  /// was locked.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true, guiActiveUnfocused = true)]
  [LocalizableItem(
      tag = "#kasLOC_08011",
      defaultTemplate = "Release cable",
      description = "A context menu item that sets the cable length ot the maximum, and unlocks"
      + " the connector if it was locked.")]
  public virtual void ReleaseCableEvent() {
    ReleaseCable();
    ShowStatusMessage(MaxLengthReachedMsg.Format(cableJoint.cfgMaxCableLength));
  }

  /// <summary>
  /// A context menu event that sets the cable length to the current distance to the connector.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true, guiActiveUnfocused = true)]
  [LocalizableItem(
      tag = "#kasLOC_08012",
      defaultTemplate = "Instant stretch",
      description = "A context menu event that sets the cable length to the current distance to the"
      + " connector.")]
  public virtual void InstantStretchEvent() {
    StretchCable();
  }
  #endregion

  #region IWinchControl properties
  /// <inheritdoc/>
  public float cfgMotorMaxSpeed { get { return motorMaxSpeed; } }

  /// <inheritdoc/>
  public float motorTargetSpeed { get; private set; }

  /// <inheritdoc/>
  public float motorCurrentSpeed {
    get { return _motorCurrentSpeed; }
    private set {
      if (Mathf.Abs(value) < float.Epsilon && Mathf.Abs(_motorCurrentSpeed) > float.Epsilon) {
        sndMotorStop.Play();
        sndMotor.Stop();
      }
      if (Mathf.Abs(value) > float.Epsilon && Mathf.Abs(_motorCurrentSpeed) < float.Epsilon) {
        sndMotorStart.Play();
        sndMotor.Play();
      }
      _motorCurrentSpeed = value;
    }
  }
  float _motorCurrentSpeed;

  /// <inheritdoc/>
  public new bool isConnectorLocked { get { return base.isConnectorLocked; } }

  /// <inheritdoc/>
  public new float currentCableLength { get { return base.currentCableLength; } }

  /// <inheritdoc/>
  public new float cfgMaxCableLength { get { return base.cfgMaxCableLength; } }
  #endregion

  #region Local fields & properties
  /// <summary>Sound to play when the motor is active.</summary>
  /// <seealso cref="motorCurrentSpeed"/>
  AudioSource sndMotor;

  /// <summary>Sounds to play when the motor starts.</summary>
  /// <seealso cref="motorCurrentSpeed"/>
  AudioSource sndMotorStart;

  /// <summary>Sounds to play when the motor stops.</summary>
  /// <seealso cref="motorCurrentSpeed"/>
  AudioSource sndMotorStop;
  #endregion

  #region KASLikSourcePhysical overrides
  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    // This module can only operate with the docking peers.
    if (!allowCoupling) {
      HostedDebugLog.Error(this, "The winch must be allowed for coupling!");
      allowCoupling = true;  // A bad approach, but better than not having the attach node.
    }
    base.OnLoad(node);

    sndMotor = SpatialSounds.Create3dSound(part.gameObject, sndPathMotor, loop: true);
    sndMotorStart = SpatialSounds.Create3dSound(part.gameObject, sndPathMotorStart);
    sndMotorStop = SpatialSounds.Create3dSound(part.gameObject, sndPathMotorStop);
  }
  #endregion

  #region IsPhysicalObject implementation
  /// <inheritdoc/>
  public virtual void FixedUpdate() {
    //TODO: Do it in the OnFixedUpdate().
    if (HighLogic.LoadedSceneIsEditor) {
      return;
    }
    if (Mathf.Abs(motorTargetSpeed) > float.Epsilon
        || Mathf.Abs(motorCurrentSpeed) > float.Epsilon) {
      UpdateMotor();
    }
  }
  #endregion

  #region IHasContextMenu implementation
  /// <inheritdoc/>
  public override void UpdateContextMenu() {
    base.UpdateContextMenu();
    deployedCableLengthMenuInfo = DistanceType.Format(
        cableJoint != null ? cableJoint.deployedCableLength : 0);

    PartModuleUtils.SetupEvent(this, ToggleExtendCableEvent, e => {
      e.guiName = motorTargetSpeed > float.Epsilon
          ? StopExtendingMenuTxt
          : ExtendCableMenuTxt;
    });
    PartModuleUtils.SetupEvent(this, ToggleRetractCableEvent, e => {
      e.guiName = motorTargetSpeed < -float.Epsilon
          ? StopRetractingMenuTxt
          : RetractCableMenuTxt;
    });
  }
  #endregion

  #region IWinControl implementation
  /// <inheritdoc/>
  public void SetMotor(float targetSpeed) {
    if (targetSpeed > 0 && cableJoint.deployedCableLength >= cableJoint.cfgMaxCableLength) {
      ShowStatusMessage(MaxLengthReachedMsg.Format(cableJoint.cfgMaxCableLength));
      return;
    }
    if (targetSpeed < 0 && isConnectorLocked) {
      ShowStatusMessage(ConnectorLockedMsg);
      return;
    }
    if (targetSpeed > 0 && isConnectorLocked) {
      connectorState = isLinked
          ? ConnectorState.Plugged
          : ConnectorState.Deployed;
    }
    if (!isConnectorLocked) {
      if (Mathf.Abs(targetSpeed) < float.Epsilon) {
        KillMotor();
      } else {
        var newTargetSpeed = targetSpeed > 0
            ? Mathf.Min(targetSpeed, cfgMotorMaxSpeed)
            : Mathf.Max(targetSpeed, -cfgMotorMaxSpeed);
        if (newTargetSpeed * motorCurrentSpeed < 0) {
          // Shutdown the motor immediately when the rotation direction is changed. 
          motorCurrentSpeed = 0;
        }
        motorTargetSpeed = newTargetSpeed;
      }
    }
  }

  /// <inheritdoc/>
  public void StretchCable() {
    SetCableLength(float.NegativeInfinity);
  }

  /// <inheritdoc/>
  public void ReleaseCable() {
    if (isConnectorLocked) {
      connectorState = isLinked
          ? ConnectorState.Plugged
          : ConnectorState.Deployed;
    }
    SetCableLength(float.PositiveInfinity);
  }
  #endregion

  #region Inheritable utility methods
  /// <summary>Immediately shuts down the motor.</summary>
  /// <remarks>
  /// The motor speed instantly becomes zero, it may trigger the physical effects.
  /// </remarks>
  protected void KillMotor() {
    motorTargetSpeed = 0;
    motorCurrentSpeed = 0;
    UpdateContextMenu();
  }
  #endregion

  #region Local utility methods
  /// <summary>Updates the winch connector cable according to the current motor movements.</summary>
  /// <remarks>This method is only called when the motor is consuming electricity.</remarks>
  void UpdateMotor() {
    // Adjust the motor speed to the target.
    if (motorCurrentSpeed < motorTargetSpeed) {
      motorCurrentSpeed += motorAcceleration * Time.fixedDeltaTime;
      if (motorCurrentSpeed > motorTargetSpeed) {
        motorCurrentSpeed = motorTargetSpeed;
      }
    } else if (motorCurrentSpeed > motorTargetSpeed) {
      motorCurrentSpeed -= motorAcceleration * Time.fixedDeltaTime;
      if (motorCurrentSpeed < motorTargetSpeed) {
        motorCurrentSpeed = motorTargetSpeed;
      }
    }

    // Consume energy and adjust the cable length.
    var powerDemand = motorPowerDrain * TimeWarp.fixedDeltaTime;
    var gotEnergy = part.RequestResource(StockResourceNames.ElectricCharge, powerDemand);
    if (Mathf.Approximately(gotEnergy, powerDemand)) {
      SetCableLength(
          cableJoint.deployedCableLength + motorCurrentSpeed * TimeWarp.fixedDeltaTime);
      if (motorCurrentSpeed > 0
          && cableJoint.deployedCableLength >= cableJoint.cfgMaxCableLength) {
        KillMotor();
        SetCableLength(float.PositiveInfinity);
        ShowStatusMessage(MaxLengthReachedMsg.Format(cableJoint.cfgMaxCableLength));
      } else if (motorCurrentSpeed < 0 && cableJoint.deployedCableLength <= 0) {
        KillMotor();
        SetCableLength(0);
        TryLockingConnector();
      }
    } else {
      KillMotor();
      ShowStatusMessage(NoEnergyMsg, isError: true);
    }
  }

  /// <summary>
  /// Checks if the cable connector can be locked without triggering significant physical froces. 
  /// </summary>
  /// <param name="logCheckResult">
  /// If <c>true</c> then the result of the check will be logged.
  /// </param>
  /// <returns>
  /// <c>true</c> if projection of the position and direction of the connector, and whatever is
  /// attached to it, won't deal a significant disturbance to the system.
  /// </returns>
  bool CheckIsConnectorAligned(bool logCheckResult) {
    // Check the pre-conditions. 
    if (cableJoint.deployedCableLength > Mathf.Epsilon  // Cable is not fully retracted.
        || cableJoint.realCableLength > connectorLockMaxErrorDist) {  // Not close enough.
      if (logCheckResult) {
        HostedDebugLog.Info(this, "Connector cannot lock, the preconditions failed:"
                            + " maxLengh={0}, realLength={1}, isLinked={2}",
                            cableJoint.deployedCableLength,
                            cableJoint.realCableLength,
                            isLinked);
      }
      return false;
    }
    // The alignment doesn't matter if the connector is not attached to anything.
    if (!isLinked) {
      if (logCheckResult) {
        HostedDebugLog.Info(this, "Unplugged connector is allowed to lock");
      }
      return true;
    }
    // Check if the alignment error is small enough to not awake Krakken on dock.
    var fwdAngleErr = 180 - Vector3.Angle(connectorCableAnchor.forward, nodeTransform.forward);
    if (fwdAngleErr > connectorLockMaxErrorDir) {
      if (logCheckResult) {
        HostedDebugLog.Info(
            this, "Plugged connector align error: yaw/pitch={0}",
            fwdAngleErr);
      }
      return false;
    }

    if (logCheckResult) {
      HostedDebugLog.Info(this, "Plugged connector is allowed to lock");
    }
    return true;
  }

  /// <summary>Checks if the cable connector can be locked, and attempts to lock it.</summary>
  /// <remarks>The successful attempt will be logged to GUI.</remarks>
  /// <param name="reportIfCannot">
  /// If <c>true</c> then the failed attempt will be logged to GUI.
  /// </param>
  /// <returns><c>true</c> if the connector was successfully locked.</returns>
  bool TryLockingConnector(bool reportIfCannot = true) {
    if (isLinked && linkTarget.part.vessel.isEVA) {
      return false;  // Silently don't allow docking with a kerbal.
    }
    if (!CheckIsConnectorAligned(reportIfCannot)) {
      if (reportIfCannot) {
        ShowStatusMessage(LockConnectorNotAlignedMsg, isError: true);
      }
      return false;
    }
    if (isLinked) {
      //FIXME: support decoupling by external actors and reset to Locked state
      connectorState = ConnectorState.Docked;
      ShowStatusMessage(ConnectorDockedMsg);
    } else {
      connectorState = ConnectorState.Locked;
      ShowStatusMessage(ConnectorLockedMsg);
    }
    return true;
  }
  #endregion
}

}  // namespace
