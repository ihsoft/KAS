// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev;
using KSPDev.GUIUtils;
using KSPDev.ModelUtils;
using KSPDev.KSPInterfaces;
using KSPDev.ProcessingUtils;
using KSPDev.Types;
using System;
using System.Linq;
using UnityEngine;
using KSPDev.LogUtils;
using KSPDev.ResourceUtils;

namespace KAS {

/// <summary>Module for a simple winch with a deployable head.</summary>
/// <remarks>
/// <para>
/// The head is attached to the winch with a cable. The head can link with the compatible link
/// targets. The winch itself is a <see cref="ILinkSource">link source</see>. An EVA kerbal can
/// "grab" the head and carry it as far as the cable maximum length allows.
/// </para>
/// <para>
/// Since the winch is a basic link source it can link with the target in the differnt modes.
/// However, it's highly recommended to use the mode <see cref="LinkMode.TieAnyParts"/>. As it
/// the most flexible, and the winch is capable of changing "docked" vs "non-docked" mode when the
/// link is already made.
/// <br/>FIXME: Implement
/// </para>
/// <para>A compatible target must take into account that the winch's head has some height, and its
/// part attach anchor is shifted to compensate this height. The target needs to shift the attach
/// node transform accordingly to make the link looking and behaving naturally.
/// </para>
/// </remarks>
/// <seealso cref="ILinkSource"/>
/// <seealso cref="ILinkTarget"/>
public class KASModuleWinchNew : KASModuleLinkSourceBase,
    // KAS interfaces.
    IHasContextMenu, IsPhysicalObject {

  #region Localizable UI strings  
  /// <summary>Info string in the part's context menu for the winch state.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/MessageEnumValue/*"/>
  protected static readonly MessageEnumValue<WinchState> WinchStatesMsg =
      new MessageEnumValue<WinchState>() {
          {WinchState.HeadLocked, "Head is locked"},
          {WinchState.HeadDeployed, "Idle"},
          {WinchState.CableExtending, "Extending"},
          {WinchState.CableRetracting, "Retracting"},
      };

  /// <summary>Error message to present when the electricity charge has exhausted.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/Message/*"/>
  protected static readonly Message NoEnergyMsg = "No energy!";

  /// <summary>
  /// Error message to present when an improperly aligned cable head has attempted to lock with the
  /// winch.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/Message/*"/>
  protected static readonly Message CannotLockHeadMsg = "Head is not aligned. Cannot lock";
  #endregion

  #region Part's config fields
  /// <summary>
  /// Force per one meter of the stretched cable to apply to keep the objects close to each other.
  /// </summary>
  /// <remarks>A too high value may result in the joints destruction.</remarks>
  /// <seealso cref="cableDamper"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float cableSpring = 1000f;
  
  /// <summary>Dampering force to apply to the cable to calm down the oscillations.</summary>
  /// <remarks>A too high value may reduce the cable spring strength.</remarks>
  /// <seealso cref="cableSpring"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float cableDamper = 0.1f;
  
  /// <summary>Maximum allowed length of the winch cable.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float cableMaxLenght = 10;

  /// <summary>Object that represents the head model.</summary>
  /// <remarks>
  /// The value is a <see cref="Hierarchy.FindTransformByPath(Transform, string)"/> search path. The
  /// path is looked globally, starting from the part's model root.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='M:KSPDev.Hierarchy.FindTransformByPath']/*"/>
  [KSPField]
  public string headModel = "";

  /// <summary>Object that is used to align the cable head against the target part.</summary>
  /// <remarks>
  /// The value is a <see cref="Hierarchy.FindTransformByPath(Transform, string)"/> search path. The
  /// path is looked starting from the head model.
  /// </remarks>
  /// <seealso cref="headModel"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='M:KSPDev.Hierarchy.FindTransformByPath']/*"/>
  [KSPField]
  public string headPartAttachAt = "";

  /// <summary>Position and rotation of the target part-to-head attach point.</summary>
  /// <remarks>
  /// <para>
  /// The values must be given in the coordinates local to the head. This value will only be used
  /// if there is no object named <see cref="headPartAttachAt"/> in the head's object hierarchy.
  /// </para>
  /// <para>The value is a serialized format of <see cref="PosAndRot"/>.</para>
  /// </remarks>
  /// <seealso cref="headPartAttachAt"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.Types.PosAndRot']/*"/>
  [KSPField]
  public string headPartAttachAtPosAndRot = "";

  /// <summary>Object that is used to align the cable mesh to the cable head.</summary>
  /// <remarks>
  /// The value is a <see cref="Hierarchy.FindTransformByPath(Transform, string)"/> search path. The
  /// path is looked starting from the head model.
  /// </remarks>
  /// <seealso cref="headModel"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='M:KSPDev.Hierarchy.FindTransformByPath']/*"/>
  [KSPField]
  public string headCableAttachAt = "";

  /// <summary>Position of the cable-to-head attach point.</summary>
  /// <remarks>
  /// The position must be given in the coordinates local to the head. This value will only be used
  /// if there is no object named <see cref="headCableAttachAt"/> in the head's object hierarchy.
  /// </remarks>
  /// <seealso cref="headCableAttachAt"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="KSPAPI_HelpIndex.xml" path="//item[@name='T:UnityEngine.Vector3']/*"/>
  [KSPField]
  public Vector3 headCableAttachAtPos;

  /// <summary>Maximum cable length at which the cable head can lock to the winch.</summary>
  /// <seealso cref="CheckIsHeadAligned"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float headLockMaxErrorDist = 0.01f;

  /// <summary>Maximum direction error to allow for the cable head to lock to the winch.</summary>
  /// <remarks>
  /// This value is always positive, and it determines how significantly the <c>forward</c> and
  /// <c>up</c> vectors of the head can differ from the winch's attach node ones. The value is
  /// compared with an absolute value of a difference between <c>1.0f</c> and a
  /// <see cref="Vector3.Dot"/> between the vectors.
  /// </remarks>
  /// <seealso cref="CheckIsHeadAligned"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="KSPAPI_HelpIndex.xml" path="//item[@name='T:UnityEngine.Vector3']/*"/>
  [KSPField]
  public float headLockMaxErrorDir = 0.01f;

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
  #endregion

  #region The context menu fields
  /// <summary>Status field to display the current wimch status.</summary>
  /// <see cref="winchState"/>
  /// <include file="SpecialDocTags.xml" path="Tags/UIConfigSetting/*"/>
  [KSPField(guiName = "Winch state", guiActive = true)]
  public string headDeployStateMenuInfo = "";

  /// <summary>Presents the current cable length.</summary>
  /// <seealso cref="currentCableLength"/>
  /// <include file="SpecialDocTags.xml" path="Tags/UIConfigSetting/*"/>
  [KSPField(guiName = "Cable lenght", guiActive = true, guiFormat = "F2", guiUnits = "m")]
  public float cableLengthMenuInfo = 0.0f;
  #endregion

  #region Context menu events/actions
  /// <summary>Starts extending the cable.</summary>
  /// <remarks>
  /// If the head was locked it will be deployed. This method does nothing is the cable cannot be
  /// extended for any reason.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiName = "Extend cable", guiActive = true)]
  public virtual void ExtentCableEvent() {
    if (Mathf.Approximately(currentCableLength, cableMaxLenght)) {
      return;
    }
    if (winchState == WinchState.CableRetracting || winchState == WinchState.HeadLocked) {
      SetStateIfPossible(WinchState.HeadDeployed);
    }
    SetStateIfPossible(WinchState.CableExtending);
  }

  /// <summary>Starts retracting the cable.</summary>
  /// <remarks>
  /// If the cable length is zero but the head is not locked, then this method will try to lock the
  /// head. It does nothing is the cable cannot be retracted for any reason.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiName = "Retract cable", guiActive = true)]
  public virtual void RetractCableEvent() {
    if (winchState == WinchState.HeadDeployed && currentCableLength <= headLockMaxErrorDist) {
      if (CheckIsHeadAligned()) {
        winchState = WinchState.HeadLocked;
      } else {
        ScreenMessaging.ShowInfoScreenMessage(CannotLockHeadMsg);
      }
      return;
    }
    if (winchState == WinchState.CableExtending) {
      SetStateIfPossible(WinchState.HeadDeployed);
    }
    SetStateIfPossible(WinchState.CableRetracting);
  }

  /// <summary>Stops any current motor operation.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiName = "Stop motor", guiActive = true)]
  public virtual void StopMotorEvent() {
    if (CheckIfDrainsElectricity()) {
      SetStateIfPossible(WinchState.HeadDeployed);
    }
  }

  /// <summary>Allows the cable be retracted at the maximum length.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiName = "Release cable", guiActive = true)]
  public virtual void ReleaseCableEvent() {
    if (SetStateIfPossible(WinchState.HeadDeployed)) {
      currentCableLength = cableMaxLenght;
    }
  }

  /// <summary>Sets the cable length to the current distance of the head.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiName = "Instant stretch", guiActive = true)]
  public virtual void InstantStretchEvent() {
    if (SetStateIfPossible(WinchState.HeadDeployed)) {
      currentCableLength = currentHeadDistance;
    }
  }

  /// <summary>Attaches the head to the EVA kerbal.</summary>
  /// <remarks>The active vessel must be a kerbal.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiName = "Grab head", guiActiveUnfocused = true, externalToEVAOnly = false)]
  public virtual void GrabHeadEvent() {
    if (FlightGlobals.ActiveVessel.isEVA && winchState == WinchState.HeadLocked) {
      var kerbalTarget = FlightGlobals.ActiveVessel.rootPart.FindModulesImplementing<ILinkTarget>()
          .FirstOrDefault(t => t.linkSource == null && t.cfgLinkType == cfgLinkType);
      linkMode = LinkMode.TiePartsOnDifferentVessels;
      if (kerbalTarget != null && StartLinking(GUILinkMode.API)) {
        LinkToTarget(kerbalTarget);
        //FIXME: LOG level
        Debug.LogWarning(DbgFormatter2.HostedLog(
            part, "{0} has grabbed the winch head", FlightGlobals.ActiveVessel.vesselName));
      } else {
        Debug.LogError(DbgFormatter2.HostedLog(
            part, "{0} cannot grab the winch head", FlightGlobals.ActiveVessel.vesselName));
      }
    }
  }

  /// <summary>Detaches the head from the kerbal and pust it back into the winch.</summary>
  /// <remarks>The active vessel must be a kerbal holding the head.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiName = "Lock head", guiActiveUnfocused = true, externalToEVAOnly = false)]
  public virtual void LockHeadEvent() {
    if (FlightGlobals.ActiveVessel.isEVA && isCableHeadOnKerbal) {
      var kerbalTarget = FlightGlobals.ActiveVessel.rootPart.FindModulesImplementing<ILinkTarget>()
          .FirstOrDefault(t => ReferenceEquals(t.linkSource, this));
      if (kerbalTarget != null) {
        BreakCurrentLink(LinkActorType.Player);
        winchState = WinchState.HeadLocked;
        //FIXME: LOG level
        Debug.LogWarning(DbgFormatter2.HostedLog(
            part, "{0} has returned the winch head", FlightGlobals.ActiveVessel.vesselName));
      } else {
        Debug.LogError(DbgFormatter2.HostedLog(part, "Wrong target for the lock head action"));
      }
    }
  }
  #endregion

  // Don't access the part's GUI fields via the `Fields` and `Events` properties. Instead, refer
  // the specific control via a property defined in this section. It will allow your code to have
  // a compile time checking for the right names. If some field is not exposed here, then it wasn't
  // supposed to be exposed at all.
  #region UI control cache
  /// <summary>Field instance for <see cref="ExtentCableEvent"/>.</summary>
  /// <seealso cref="LoadUIControlsCache"/>
  protected BaseEvent uiExtendCableEvent { get; private set; }

  /// <summary>Field instance for <see cref="RetractCableEvent"/>.</summary>
  /// <seealso cref="LoadUIControlsCache"/>
  protected BaseEvent uiRetractCableEvent { get; private set; }

  /// <summary>Field instance for <see cref="StopMotorEvent"/>.</summary>
  /// <seealso cref="LoadUIControlsCache"/>
  protected BaseEvent uiStopMotorEvent { get; private set; }

  /// <summary>Field instance for <see cref="GrabHeadEvent"/>.</summary>
  /// <seealso cref="LoadUIControlsCache"/>
  protected BaseEvent uiGrabHeadEvent { get; private set; }

  /// <summary>Field instance for <see cref="LockHeadEvent"/>.</summary>
  /// <seealso cref="LoadUIControlsCache"/>
  protected BaseEvent uiLockHeadEvent { get; private set; }

  /// <summary>Field instance for <see cref="ReleaseCableEvent"/>.</summary>
  /// <seealso cref="LoadUIControlsCache"/>
  protected BaseEvent uiReleaseCableEvent { get; private set; }

  /// <summary>Field instance for <see cref="InstantStretchEvent"/>.</summary>
  /// <seealso cref="LoadUIControlsCache"/>
  protected BaseEvent uiInstantStretchEvent { get; private set; }
  #endregion

  #region Externally visible state of the winch 
  /// <summary>State of the motor.</summary>
  /// <seealso cref="FixedUpdate"/>
  public enum WinchState {
    /// <summary>The head is rigidly attached to the winch's body.</summary>
    HeadLocked,
    /// <summary>The motor is not moving, and the head is hanging free of the cable.</summary>
    HeadDeployed,
    /// <summary>The motor is spinning, giving an extra length of the available cable.</summary>
    /// <remarks>In this mode the electric charge resource is consumed.</remarks>
    CableExtending,
    /// <summary>The motor is spinning, reducing the length of the available cable.</summary>
    /// <remarks>In this mode the electric charge resource is consumed.</remarks>
    CableRetracting,
  }

  /// <summary>Controls the state of the winch.</summary>
  /// <remarks>
  /// Not any state transition is allowed. See <see cref="OnAwake"/> for the transitions definition.
  /// </remarks>
  /// <value>The current winch state.</value>
  // FIXME: DOCS: put state transitions here
  public virtual WinchState winchState {
    get { return stateMachine.currentState; }
    set {
      stateMachine.currentState = value;
      headDeployStateMenuInfo = WinchStatesMsg.Format(value);
      UpdateContextMenu();
    }
  }

  /// <summary>Tells the current length of the cable between the winch and the head.</summary>
  /// <remarks>
  /// <para>
  /// It's not a real distance between the winch and the head. It's an allowance for the maximum
  /// distance.
  /// </para>
  /// <para>
  /// When the head is deployed, changing of this property will affect the actual physical link with
  /// the head. A too rapid change in the cable length may result in the distructive physical
  /// effects.
  /// </para>
  /// </remarks>
  /// <value>The current length in meters.</value>
  /// <seealso cref="winchState"/>
  /// <seealso cref="currentHeadDistance"/>
  // FIXME: read/write it from the physics joint.
  public float currentCableLength {
    get { return _cableLength; }
    protected set {
      _cableLength = value;
      cableLengthMenuInfo = value;
      UpdateCableLength();
    }
  }
  float _cableLength;

  /// <summary>Actual distance of the head from the winch.</summary>
  /// <remarks>
  /// It may be different from the <see cref="currentCableLength"/> if the head is moving or it
  /// hasn't reached the cable limit yet.
  /// </remarks>
  /// <value>The current distance in meters./</value>
  public float currentHeadDistance {
    get {
      return winchState != WinchState.HeadLocked
          ? (headCableAnchor.position - nodeTransform.position).magnitude
          : 0;
    }
  }

  /// <summary>Tells if the winch head is picked up by an EVA kerbal.</summary>
  /// <value><c>true</c> if the head is being carried by a kerbal.</value>
  public bool isCableHeadOnKerbal {
    get {
      return isLinked && linkTarget.part.vessel.isEVA;
    }
  }

  /// <summary>State machine that defines and controls the winch state.</summary>
  /// <remarks>
  /// The machine can be adjusted until it's started in the <see cref="OnStart"/> method.
  /// </remarks>
  /// <value>The state machine.</value>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ProcessingUtils.SimpleStateMachine_1']/*"/>
  protected SimpleStateMachine<WinchState> stateMachine { get; private set; }
  #endregion

  Transform headModelObj;
  Transform headCableAnchor;
  Transform headPartAnchor;

  float motorCurrentSpeed;
  float motorTargetSpeed;

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    LoadUIControlsCache();

    stateMachine = new SimpleStateMachine<WinchState>(strict: true);
    stateMachine.SetTransitionConstraint(
        WinchState.HeadLocked, new[] { WinchState.HeadDeployed });
    stateMachine.SetTransitionConstraint(
        WinchState.HeadDeployed,
        new[] { WinchState.HeadLocked, WinchState.CableExtending, WinchState.CableRetracting });
    stateMachine.SetTransitionConstraint(
        WinchState.CableExtending, new[] { WinchState.HeadDeployed });
    stateMachine.SetTransitionConstraint(
        WinchState.CableRetracting, new[] { WinchState.HeadDeployed });

    stateMachine.AddStateHandlers(
        WinchState.HeadDeployed,
        enterHandler: oldState => {
          motorTargetSpeed = 0;
          motorCurrentSpeed = 0;
        });
    stateMachine.AddStateHandlers(
        WinchState.HeadLocked,
        enterHandler: oldState => LockCableHead(),
        leaveHandler: oldState => DeployCableHead());
    stateMachine.AddStateHandlers(
        WinchState.CableExtending,
        enterHandler: oldState => {
          motorTargetSpeed = motorMaxSpeed;
        });
    stateMachine.AddStateHandlers(
        WinchState.CableRetracting,
        enterHandler: oldState => {
          motorTargetSpeed = -motorMaxSpeed;
        });
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);
    LoadOrCreateHeadModel();
    UpdateContextMenu();
  }
  
  /// <inheritdoc/>
  public override void OnStart(StartState state) {
    base.OnStart(state);
    stateMachine.Start(WinchState.HeadLocked);
    UpdateContextMenu();
  }
  #endregion

  #region IsPhysicalObject implementation
  /// <inheritdoc/>
  public virtual void FixedUpdate() {
    if (CheckIfDrainsElectricity()) {
      UpdateMotor();
    }
  }
  #endregion

  #region KASModuleLikSourceBase overrides
  /// <inheritdoc/>
  protected override void OnStateChange(LinkState oldState) {
    base.OnStateChange(oldState);
    UpdateContextMenu();
  }

  /// <inheritdoc/>
  protected override void PhysicalLink(ILinkTarget target) {
    winchState = WinchState.HeadDeployed;  // Ensure the head is deployed and not moving.
    base.PhysicalLink(target);
  }
  #endregion

  #region IHasContextMenu implementation
  /// <inheritdoc/>
  public virtual void UpdateContextMenu() {
    uiExtendCableEvent.active =
        winchState == WinchState.CableExtending || !CheckIfDrainsElectricity();
    uiRetractCableEvent.active =
        winchState == WinchState.CableRetracting || !CheckIfDrainsElectricity();
    uiStopMotorEvent.active = CheckIfDrainsElectricity();
    uiGrabHeadEvent.active = winchState == WinchState.HeadLocked;
    uiLockHeadEvent.active = isCableHeadOnKerbal;
    //TODO: Consider checking the cable length. Updates from currentCableLength will be needed.
    uiReleaseCableEvent.active = winchState == WinchState.HeadDeployed;
    uiInstantStretchEvent.active = winchState == WinchState.HeadDeployed;
  }
  #endregion

  #region Overridable methods
  /// <summary>Tells if the winch is a state that requires the electricity charge.</summary>
  /// <returns><c>true</c> if winch is consuming electricity.</returns>
  protected virtual bool CheckIfDrainsElectricity() {
    return winchState == WinchState.CableExtending || winchState == WinchState.CableRetracting;
  }

  /// <summary>Updates the winch state according to the current motor movements.</summary>
  /// <remarks>This method is only called when the winch is consuming electricity.</remarks>
  /// <seealso cref="CheckIfDrainsElectricity"/>
  protected virtual void UpdateMotor() {
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

    // Adjust the cable length.
    var powerDemand = motorPowerDrain * TimeWarp.fixedDeltaTime;
    var gotEnergy = part.RequestResource(StockResourceNames.ElectricCharge, powerDemand);
    if (Mathf.Approximately(gotEnergy, powerDemand)) {
      currentCableLength += motorCurrentSpeed * TimeWarp.fixedDeltaTime;
      if (currentCableLength >= cableMaxLenght) {
        currentCableLength = cableMaxLenght;
        winchState = WinchState.HeadDeployed;
      } else if (currentCableLength <= 0) {
        currentCableLength = 0;
        winchState = WinchState.HeadDeployed;  // Stop the motor.
        if (CheckIsHeadAligned()) {
          winchState = WinchState.HeadLocked;
        } else {
          ScreenMessaging.ShowInfoScreenMessage(CannotLockHeadMsg);
        }
      }
      if (winchState == WinchState.CableRetracting && currentCableLength <= headLockMaxErrorDist
          && CheckIsHeadAligned()) {
        winchState = WinchState.HeadDeployed;  // Abort the motor state.
        winchState = WinchState.HeadLocked;
      }
    } else {
      winchState = WinchState.HeadDeployed;
      ScreenMessaging.ShowErrorScreenMessage(NoEnergyMsg);
    }
  }

  /// <summary>Updates the physical joint state according to the current cable length.</summary>
  //FIXME: Implement
  protected virtual void UpdateCableLength() {
  }

  /// <summary>Caches the UI elements into the local state.</summary>
  protected virtual void LoadUIControlsCache() {
    uiExtendCableEvent = Events["ExtentCableEvent"];
    uiRetractCableEvent = Events["RetractCableEvent"];
    uiStopMotorEvent = Events["StopMotorEvent"];
    uiGrabHeadEvent = Events["GrabHeadEvent"];
    uiLockHeadEvent = Events["LockHeadEvent"];
    uiReleaseCableEvent = Events["ReleaseCableEvent"];
    uiInstantStretchEvent = Events["InstantStretchEvent"];
  }
  #endregion

  #region Inheritable utility methods
  /// <summary>
  /// Checks if the cable head is located very close to the winch's attach node and is looking in
  /// the approaximnatle same direction.
  /// </summary>
  /// <returns>
  /// <c>true</c> if projection of the position and direction of the head, and whatever is attached
  /// to it, won't deal a physics damage.
  /// </returns>
  protected bool CheckIsHeadAligned() {
    // Check if the distance is small enough.
    if (currentHeadDistance > headLockMaxErrorDist) {
      return false;
    }
    // The forward vectors of the attach node and the head cable anchor must point in the
    // completely opposite directions.
    var fwdDot = 1.0f + Vector3.Dot(headCableAnchor.forward, nodeTransform.forward);
    if (Mathf.Abs(fwdDot) > headLockMaxErrorDir) {
      return false;
    }
    var rollDot = 1.0f + Vector3.Dot(headCableAnchor.forward, nodeTransform.forward);
    if (Mathf.Abs(rollDot) > headLockMaxErrorDir) {
      return false;
    }
    return true;
  }
  #endregion

  #region Local utility methods
  /// <summary>Changes the winch state if the transition is allowed.</summary>
  /// <remarks>Transition to the same state is always allowed.</remarks>
  /// <param name="newState">The new state of the winch to set.</param>
  /// <returns><c>true</c> if state change was successful.</returns>
  bool SetStateIfPossible(WinchState newState) {
    if (newState == winchState) {
      return true;
    }
    if (stateMachine.CheckCanSwitchTo(newState)) {
      winchState = newState;
      return true;
    }
    return false;
  }

  // FIXME: Implement
  void DeployCableHead() {
    //FIXME: LOG level
    Debug.LogWarning(DbgFormatter2.HostedLog(part, "Winch head is deployed"));
  }

  // FIXME: Implement
  void LockCableHead() {
    currentCableLength = 0;
    //FIXME: LOG level
    Debug.LogWarning(DbgFormatter2.HostedLog(part, "Winch head is locked"));
    //TODO: fix physical joint.
  }

  /// <summary>Intializes the head model object and its anchors.</summary>
  /// <remarks>
  /// <para>
  /// If the head model is not found than a stub object will be created. There will be no visual
  /// representation but the overall functionality of the winch should keep working.
  /// </para>
  /// <para>
  /// If the head doesn't have the anchors then the missed onces will be created basing on the
  /// provided position/rotation. If the config file doesn't provide anything then the anchors will
  /// have a zero position and a random rotation.
  /// </para>
  /// </remarks>
  void LoadOrCreateHeadModel() {
    headModelObj = Hierarchy2.FindPartModelByPath(part, headModel);
    if (headModelObj != null) {
      headCableAnchor = Hierarchy2.FindTransformByPath(headModelObj, headCableAttachAt);
      if (headCableAnchor == null) {
        //FIXME: LOG level
        Debug.LogWarning(DbgFormatter2.HostedLog(part, "Creating cable transform"));
        headCableAnchor = new GameObject(headCableAttachAt).transform;
        Hierarchy.MoveToParent(headCableAnchor, headModelObj,
                               newPosition: headCableAttachAtPos, newRotation: Quaternion.identity);
      }
      headPartAnchor = Hierarchy2.FindTransformByPath(headModelObj, headPartAttachAt);
      if (headPartAnchor == null) {
        //FIXME: LOG level
        Debug.LogWarning(DbgFormatter2.HostedLog(part, "Creating part transform"));
        headPartAnchor = new GameObject(headPartAttachAt).transform;
        var posAndRot = PosAndRot2.FromString(headPartAttachAtPosAndRot);
        Hierarchy.MoveToParent(headPartAnchor, headModelObj,
                               newPosition: posAndRot.pos, newRotation: posAndRot.rot);
      }
    } else {
      Debug.LogError(DbgFormatter2.HostedLog(part, "Cannot find a head model: {1}", headModel));
      // Fallback to not have the whole code to crush. 
      headModelObj = new GameObject().transform;
      headCableAnchor = headModelObj;
      headPartAnchor = headModelObj;
    }
  }
  #endregion
}

}  // namespace
