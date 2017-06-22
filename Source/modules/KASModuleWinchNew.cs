// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.ModelUtils;
using KSPDev.KSPInterfaces;
using KSPDev.SoundsUtils;
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
/// <br/>TODO: Implement
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
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  protected static readonly Message<EnumType<WinchState>> WinchStatesMsg =
      new Message<EnumType<WinchState>>(
          "#kasLOC_08001",
          defaultTemplate: "<<1[Head is locked/Idle/Extending/Retracting]>>",
          description: "Info string in the part's context menu for the winch state."
          + "\nArgument <<1>> is an int value of one of the FOUR states.",
          example: "Head is locked");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message NoEnergyMsg = new Message(
      "#kasLOC_08002",
      defaultTemplate: "No energy!",
      description: "Error message to present when the electricity charge has exhausted.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message LockHeadNotAlignedMsg = new Message(
      "#kasLOC_08003",
      defaultTemplate: "Cannot lock the head: not aligned",
      description: "Error message to present when an improperly aligned cable head has attempted"
      + " to lock with the winch.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message HeadLockedMsg = new Message(
      "#kasLOC_08004",
      defaultTemplate: "Head locked!",
      description: "Info message to present when a cable head has successfully locked to the"
      + " winch.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.DistanceType']/*"/>
  protected static readonly Message<DistanceType> MaxLengthReachedMsg = new Message<DistanceType>(
      "#kasLOC_08005",
      defaultTemplate: "Maximum cable length reached: <<1>>",
      description: "An info message to present when the cable is extended at its maximum length."
      + "\nArgument <<1>> is the current cable length of type DistanceType.",
      example: "Maximum cable length reached: 1.23 m");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  //FIXME: drop in favor of HeadLockedMsg
  protected static readonly Message HeadIsAlreadyLockedMsg = new Message(
      "#kasLOC_08006",
      defaultTemplate: "The head is already locked",
      description: "An info message to present when the cable retract action is attempted on a"
      + " locked head.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message StopExtendingMenuTxt = new Message(
      "#kasLOC_08007",
      defaultTemplate: "Stop extending",
      description: "Name of the context menu item that stops the cable extending.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message ExtendCableMenuTxt = new Message(
      "#kasLOC_08008",
      defaultTemplate: "Extend cable",
      description: "Name of the context menu item that starts the cable extending.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message StopRetractingMenuTxt = new Message(
      "#kasLOC_08009",
      defaultTemplate: "Stop retracting",
      description: "Name of the context menu item that stops the cable retracting.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message RetractCableMenuTxt = new Message(
      "#kasLOC_08010",
      defaultTemplate: "Retract cable",
      description: "Name of the context menu item that starts the cable retracting.");
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
  /// The value is a <see cref="Hierarchy.FindTransformByPath(Transform,string,Transform)"/> search
  /// path. The path is looked globally, starting from the part's model root.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='M:KSPDev.Hierarchy.FindTransformByPath']/*"/>
  [KSPField]
  public string headModel = "";

  /// <summary>Mass of the head of the winch.</summary>
  /// <remarks>
  /// It's substracted from the part's mass on deploy, and added back on the lock. For this reason
  /// it must not be greater then the total part's mass. Also, try to avoid making the head heavier
  /// than the part iteself - the Unity physics may start behaving awkward. 
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float headMass = 0.01f;

  /// <summary>Object that is used to align the cable head against the target part.</summary>
  /// <remarks>
  /// The value is a <see cref="Hierarchy.FindTransformByPath(Transform,string,Transform)"/> search
  /// path. The path is looked starting from the head model.
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
  /// The value is a <see cref="Hierarchy.FindTransformByPath(Transform,string,Transform)"/> search
  /// path. The path is looked starting from the head model.
  /// </remarks>
  /// <seealso cref="headModel"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='M:KSPDev.Hierarchy.FindTransformByPath']/*"/>
  [KSPField]
  public string headCableAttachAt = "";

  /// <summary>Position and rotation of the cable-to-head attach point.</summary>
  /// <remarks>
  /// The values must be given in the coordinates local to the head. This value will only be used
  /// if there is no object named <see cref="headCableAttachAt"/> in the head's object hierarchy.
  /// </remarks>
  /// <seealso cref="headCableAttachAt"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="Unity3D_HelpIndex.xml" path="//item[@name='T:UnityEngine.Vector3']/*"/>
  [KSPField]
  public string headCableAttachAtPosAndRot;

  /// <summary>Maximum cable length at which the cable head can lock to the winch.</summary>
  /// <remarks>
  /// A spring joint in PhysX will never pull the objects together to the zero distance regardless
  /// to the spring strength. For this reason the there should be always be a reasonable error
  /// allowed. Setting the error to a too big value will result in unpleasant locking behavior and
  /// increase the force at which the head hits the winch on locking. A too small value of the
  /// allowed error will make the locking harder, up to not being able to lock at all.
  /// </remarks>
  /// <seealso cref="CheckIsHeadAligned"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float headLockMaxErrorDist = 0.05f;

  /// <summary>
  /// Maximum direction error to allow for the cable head to lock to the winch. It's in degrees.
  /// </summary>
  /// <remarks>
  /// This value is always positive, and it determines how significantly the deriction of
  /// <c>forward</c> and <c>up</c> vectors of the head can differ from the winch's attach node
  /// direction.
  /// </remarks>
  /// <seealso cref="CheckIsHeadAligned"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="Unity3D_HelpIndex.xml" path="//item[@name='T:UnityEngine.Vector3']/*"/>
  [KSPField]
  public float headLockMaxErrorDir = 1;

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

  /// <summary>URL of the sound for the winch head lock event.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string sndPathHeadLock = "";
  #endregion

  #region The context menu fields
  /// <summary>Status field to display the current winch status in the context menu.</summary>
  /// <see cref="winchState"/>
  /// <include file="SpecialDocTags.xml" path="Tags/UIConfigSetting/*"/>
  [KSPField(guiName = "Winch state", guiActive = true)]
  [LocalizableItem(
      tag = "#kasLOC_08011",
      defaultTemplate = "Winch state",
      description = "Status field to display the current winch status in the context menu.")]
  public string headDeployStateMenuInfo = "";

  /// <summary>
  /// Presents the real cable length that connects the winch and the head in the context menu.
  /// </summary>
  /// <seealso cref="realHeadDistance"/>
  /// <include file="SpecialDocTags.xml" path="Tags/UIConfigSetting/*"/>
  [KSPField(guiName = "Actual lenght", guiActive = true, guiFormat = "F2", guiUnits = "m")]
  public float cableLengthMenuInfo = 0.0f;

  /// <summary>A context menu item that presents the maximum allowed cable length.</summary>
  /// <seealso cref="maxAllowedCableLength"/>
  /// <include file="SpecialDocTags.xml" path="Tags/UIConfigSetting/*"/>
  [KSPField(guiName = "Deployed length", guiActive = true)]
  [LocalizableItem(
      tag = "#kasLOC_08012",
      defaultTemplate = "Deployed length",
      description = "A context menu item that presents the maximum allowed cable length.")]
  public string deployedCableLengthMenuInfo = "";
  #endregion

  #region Context menu events/actions
  /// <summary>A context menu item that starts extending the cable.</summary>
  /// <remarks>
  /// If the head was locked it will be deployed. This method does nothing is the cable cannot be
  /// extended for any reason.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true)]
  [LocalizableItem(tag = null)]
  public virtual void ExtentCableEvent() {
    if (Mathf.Approximately(maxAllowedCableLength, cableMaxLenght)) {
      // Already at the maximum length.
      ScreenMessaging.ShowPriorityScreenMessage(MaxLengthReachedMsg.Format(cableMaxLenght));
      return;
    }
    // Bring the winch into the inital state for the extending cable action.
    var oldState = winchState;
    if (!SetStateIfPossible(WinchState.HeadDeployed, reportNegative: true)) {
      return;  // Unexpected.
    }
    if (oldState != WinchState.CableExtending) {
      winchState = WinchState.CableExtending;  // Override any other state except the self.
    }
  }

  /// <summary>Starts retracting the cable.</summary>
  /// <remarks>
  /// If the cable length is zero but the head is not locked, then this method will try to lock the
  /// head. It does nothing is the cable cannot be retracted for any reason.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true)]
  [LocalizableItem(tag = null)]
  public virtual void RetractCableEvent() {
    if (winchState == WinchState.HeadLocked) {
      ShowMessageForActiveVessel(HeadIsAlreadyLockedMsg);
      return;  // Nothing to do.
    }
    // If the whole cable has been retracted, then just try to lock.
    if (winchState == WinchState.HeadDeployed && maxAllowedCableLength < Mathf.Epsilon) {
      TryLockingHead();
      return;
    }
    // Bring the winch into the inital state for the retract cable action.
    var oldState = winchState;
    if (!SetStateIfPossible(WinchState.HeadDeployed, reportNegative: true)) {
      return;  // Unexpected.
    }
    if (oldState != WinchState.CableRetracting) {
      winchState = WinchState.CableRetracting;  // Override any other state except the self.
    }
  }

  /// <summary>
  /// A context menu item that sets the cable length ot the maximum, and unlocks the head if it was
  /// locked.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiName = "Release cable", guiActive = true)]
  [LocalizableItem(
      tag = "#kasLOC_08013",
      defaultTemplate = "Release cable",
      description = "A context menu item that sets the cable length ot the maximum, and unlocks"
      + " the head if it was locked.")]
  public virtual void ReleaseCableEvent() {
    if (SetStateIfPossible(WinchState.HeadDeployed)) {
      maxAllowedCableLength = cableMaxLenght;
      ScreenMessaging.ShowPriorityScreenMessage(MaxLengthReachedMsg.Format(cableMaxLenght));
    }
  }

  /// <summary>
  /// A context menu event that sets the cable length to the current distance to the head.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiName = "Instant stretch", guiActive = true)]
  [LocalizableItem(
      tag = "#kasLOC_08014",
      defaultTemplate = "Instant stretch",
      description = "A context menu event that sets the cable length to the current distance to the"
      + " head.")]
  public virtual void InstantStretchEvent() {
    if (SetStateIfPossible(WinchState.HeadDeployed)) {
      maxAllowedCableLength = Mathf.Min(realHeadDistance, maxAllowedCableLength);
    }
  }

  /// <summary>Attaches the head to the EVA kerbal.</summary>
  /// <remarks>The active vessel must be a kerbal.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiName = "Grab head", guiActiveUnfocused = true, externalToEVAOnly = false)]
  [LocalizableItem(
      tag = "#kasLOC_08015",
      defaultTemplate = "Grab head",
      description = "A context menu event that attaches the head to the EVA kerbal.")]
  public virtual void GrabHeadEvent() {
    if (FlightGlobals.ActiveVessel.isEVA && winchState == WinchState.HeadLocked) {
      var kerbalTarget = FlightGlobals.ActiveVessel.rootPart.FindModulesImplementing<ILinkTarget>()
          .FirstOrDefault(t => t.linkSource == null && t.cfgLinkType == cfgLinkType);
      linkMode = LinkMode.TiePartsOnDifferentVessels;
      if (kerbalTarget != null && StartLinking(GUILinkMode.Eva, LinkActorType.Player)) {
        LinkToTarget(kerbalTarget);
        HostedDebugLog.Info(
            this, "{0} has grabbed the winch head", FlightGlobals.ActiveVessel.vesselName);
      } else {
        HostedDebugLog.Error(
            this, "{0} cannot grab the winch head", FlightGlobals.ActiveVessel.vesselName);
      }
    }
  }

  /// <summary>Detaches the head from the kerbal and puts it back to the winch.</summary>
  /// <remarks>The active vessel must be a kerbal holding a headof this winch.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiName = "Lock head", guiActiveUnfocused = true, externalToEVAOnly = false)]
  [LocalizableItem(
      tag = "#kasLOC_08016",
      defaultTemplate = "Lock head",
      description = "A context menu event that detaches the head from the kerbal and puts it back"
      + " to the winch.")]
  public virtual void LockHeadEvent() {
    if (FlightGlobals.ActiveVessel.isEVA && isCableHeadOnKerbal) {
      var kerbalTarget = FlightGlobals.ActiveVessel.rootPart.FindModulesImplementing<ILinkTarget>()
          .FirstOrDefault(t => ReferenceEquals(t.linkSource, this));
      if (kerbalTarget != null) {
        BreakCurrentLink(LinkActorType.Player);
        winchState = WinchState.HeadLocked;
        HostedDebugLog.Info(
            this, "{0} has returned the winch head", FlightGlobals.ActiveVessel.vesselName);
      } else {
        HostedDebugLog.Error(this, "Wrong target for the lock head action");
      }
    }
  }
  #endregion

  #region Externally visible state of the winch 
  /// <summary>State of the motor.</summary>
  /// <seealso cref="FixedUpdate"/>
  public enum WinchState {
    /// <summary>The head is rigidly attached to the winch's body.</summary>
    HeadLocked,
    /// <summary>The motor is not moving, and the head is hanging free on the cable.</summary>
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
  // TODO(ihsoft): DOCS: put state transitions here
  public virtual WinchState winchState {
    get { return stateMachine.currentState ?? WinchState.HeadLocked; }
    set { stateMachine.currentState = value; }
  }

  /// <summary>
  /// Tells how much of the cable is available. It defines the maximum possible distance of the head
  /// from the winch.
  /// </summary>
  /// <remarks>
  /// When the head is deployed, changing of this property may affect the actual physical link with
  /// the head. A too rapid decrease in the limit may result in the distructive physical effects.
  /// </remarks>
  /// <value>The length in meters.</value>
  /// <seealso cref="winchState"/>
  /// <seealso cref="realHeadDistance"/>
  public float maxAllowedCableLength {
    get {
      return cableJoint != null ? cableJoint.maxDistance : 0;
    }
    set {
      if (cableJoint != null) {
        cableJoint.maxDistance = value;
        UpdateContextMenu();
      } else {
        HostedDebugLog.Error(
            this, "Setting of the cable length length to {0} on a non-existing joint object");
      }
    }
  }

  /// <summary>Actual distance of the head from the winch.</summary>
  /// <remarks>
  /// It can be slightly larger than the maximum allowed cable size due to the spring joint
  /// flexibility. Due to the PhysX engine specifics, the joint's stretching cannot be avoided
  /// even by setting an infinite <see cref="cableSpring"/> setting.
  /// </remarks>
  /// <value>The distance in meters.</value>
  /// <seealso cref="maxAllowedCableLength"/>
  public float realHeadDistance {
    get {
      return cableJoint != null
         ? Vector3.Distance(headCableAnchor.position, nodeTransform.position)
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

  #region Local properties and fields
  /// <summary>State machine that defines and controls the winch state.</summary>
  /// <remarks>
  /// The machine can be adjusted until it's started in the <see cref="OnStart"/> method.
  /// </remarks>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ProcessingUtils.SimpleStateMachine_1']/*"/>
  SimpleStateMachine<WinchState> stateMachine;

  //FIXME: add comments to each field.
  SpringJoint cableJoint;
  Transform headModelObj;
  Transform headCableAnchor;
  Transform headPartAnchor;
  float motorCurrentSpeed;
  float motorTargetSpeed;

  AudioSource sndMotor;
  AudioSource sndMotorStart;
  AudioSource sndMotorStop;
  AudioSource sndHeadLock;
  #endregion

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    LoadUIControlsCache();

    sndMotor = SpatialSounds.Create3dSound(part.gameObject, sndPathMotor, loop: true);
    sndMotorStart = SpatialSounds.Create3dSound(part.gameObject, sndPathMotorStart);
    sndMotorStop = SpatialSounds.Create3dSound(part.gameObject, sndPathMotorStop);
    sndHeadLock = SpatialSounds.Create3dSound(part.gameObject, sndPathHeadLock);

    stateMachine = new SimpleStateMachine<WinchState>(strict: true);
    stateMachine.onAfterTransition += (start, end) => UpdateContextMenu();
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
        enterHandler: oldState => {
          // The module's default state is "locked". Skip the state's machine start.
          if (oldState.HasValue) {
            LockCableHead();
          }
        },
        leaveHandler: newState => {
          // Don't deploy if it's just the state machine stopping.
          if (newState.HasValue) {
            DeployCableHead();
          }
        });
    stateMachine.AddStateHandlers(
        WinchState.CableExtending,
        enterHandler: oldState => {
          motorTargetSpeed = motorMaxSpeed;
          sndMotorStart.Play();
          sndMotor.Play();
        },
        leaveHandler: newState => {
          sndMotorStop.Play();
          sndMotor.Stop();
        });
    stateMachine.AddStateHandlers(
        WinchState.CableRetracting,
        enterHandler: oldState => {
          motorTargetSpeed = -motorMaxSpeed;
          sndMotorStart.Play();
          sndMotor.Play();
        },
        leaveHandler: newState => {
          sndMotorStop.Play();
          sndMotor.Stop();
        });
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    stateMachine.currentState = WinchState.HeadLocked;
    base.OnLoad(node);
    if (headMass > part.mass) {
      HostedDebugLog.Error(
          this, "Head mass is greater that the part's mass: {0} > {1}", headMass, part.mass);
      headMass = 0.1f * part.mass;  // A fail safe value. 
    }
    LoadOrCreateHeadModel();
  }
  
  /// <inheritdoc/>
  public override void OnStart(StartState state) {
    base.OnStart(state);
    UpdateContextMenu();
  }
  #endregion

  #region IsPhysicalObject implementation
  /// <inheritdoc/>
  public virtual void FixedUpdate() {
    //TODO: Do it in the OnFixedUpdate().
    if (HighLogic.LoadedSceneIsEditor) {
      return;
    }
    if (CheckIfDrainsElectricity()) {
      UpdateMotor();
    }
    if (winchState != WinchState.HeadLocked) {
      // Apply to the head the same forces as are affecting the part.
      headModelObj.GetComponent<Rigidbody>()
          .AddForce(vessel.precalc.integrationAccel, ForceMode.Acceleration);
      UpdateContextMenu();  // The real cable length may have changed.
    }
  }
  #endregion

  #region KASModuleLikSourceBase overrides
  /// <inheritdoc/>
  protected override void OnStateChange(LinkState? oldState) {
    base.OnStateChange(oldState);
    UpdateContextMenu();
  }

  /// <inheritdoc/>
  protected override void PhysicalLink(ILinkTarget target) {
    //FIXME: handle persisted release state.
    //FIXME: release the cable.
    winchState = WinchState.HeadDeployed;  // Ensure the head is deployed and not moving.
    base.PhysicalLink(target);
    //FIXME: fix the cable length.
  }
  
  //FIXME: handle break link event
  #endregion

  #region IHasContextMenu implementation
  /// <inheritdoc/>
  public virtual void UpdateContextMenu() {
    //TODO: Move to the state preview handler.
    headDeployStateMenuInfo = WinchStatesMsg.Format(winchState);
    cableLengthMenuInfo = realHeadDistance;
    deployedCableLengthMenuInfo = maxAllowedCableLength;
    // Keep the visibility states so that the context menu is not "jumping" when the state is
    // changed. In general, if a menu item disappears then another one should show up. 
    uiExtendCableEvent.active = true;
    uiExtendCableEvent.guiName = winchState == WinchState.CableExtending
        ? StopExtendingMenuTxt
        : ExtendCableMenuTxt;
    uiRetractCableEvent.active = true;
    uiRetractCableEvent.guiName = winchState == WinchState.CableRetracting
        ? StopRetractingMenuTxt
        : RetractCableMenuTxt;
    uiReleaseCableEvent.active = true;
    uiInstantStretchEvent.active = true;
    // These events are only available for an EVA kerbal.
    uiGrabHeadEvent.active = winchState == WinchState.HeadLocked;
    uiLockHeadEvent.active = isCableHeadOnKerbal;
  }
  #endregion

  #region Overridable methods
  /// <summary>Caches the UI elements into the local state.</summary>
  protected virtual void LoadUIControlsCache() {
    uiExtendCableEvent = Events["ExtentCableEvent"];
    uiRetractCableEvent = Events["RetractCableEvent"];
    uiGrabHeadEvent = Events["GrabHeadEvent"];
    uiLockHeadEvent = Events["LockHeadEvent"];
    uiReleaseCableEvent = Events["ReleaseCableEvent"];
    uiInstantStretchEvent = Events["InstantStretchEvent"];
  }
  #endregion

  #region Inheritable utility methods
  /// <summary>Shows a message in GUI if the reporting part belongs to the active vessel.</summary>
  /// <remarks>
  /// Use this method to a present an update which is only important when the player is in control
  /// of the owner vessel. In general, when an update happens on an inactive vessel, the GUI message
  /// will look confusing since the player may not have the context.
  /// </remarks>
  /// <remarks>The message is also reported to the log.</remarks>
  /// <param name="message">The message to present.</param>
  protected void ShowMessageForActiveVessel(string message) {
    HostedDebugLog.Info(this, message);
    if (vessel.isActiveVessel) {
      ScreenMessaging.ShowPriorityScreenMessage(message);
    }
  }

  /// <summary>Tells if the winch is a state that requires the electricity charge.</summary>
  /// <returns><c>true</c> if winch is consuming electricity.</returns>
  protected bool CheckIfDrainsElectricity() {
    return winchState == WinchState.CableExtending || winchState == WinchState.CableRetracting;
  }
  #endregion

  #region Local utility methods
  /// <summary>Updates the winch state according to the current motor movements.</summary>
  /// <remarks>This method is only called when the winch is consuming electricity.</remarks>
  /// <seealso cref="CheckIfDrainsElectricity"/>
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

    // Adjust the cable length.
    var powerDemand = motorPowerDrain * TimeWarp.fixedDeltaTime;
    var gotEnergy = part.RequestResource(StockResourceNames.ElectricCharge, powerDemand);
    if (Mathf.Approximately(gotEnergy, powerDemand)) {
      maxAllowedCableLength += motorCurrentSpeed * TimeWarp.fixedDeltaTime;
      if (motorCurrentSpeed > 0 && maxAllowedCableLength >= cableMaxLenght) {
        maxAllowedCableLength = cableMaxLenght;
        winchState = WinchState.HeadDeployed;
        ScreenMessaging.ShowPriorityScreenMessage(MaxLengthReachedMsg.Format(cableMaxLenght));
      } else if (motorCurrentSpeed < 0 && maxAllowedCableLength <= 0) {
        maxAllowedCableLength = 0;
        winchState = WinchState.HeadDeployed;  // Stop the motor.
        TryLockingHead();
      }
    } else {
      winchState = WinchState.HeadDeployed;
      ScreenMessaging.ShowErrorScreenMessage(NoEnergyMsg);
    }
  }

  /// <summary>
  /// Checks if the cable head can be locked without triggering significant physical froces. 
  /// </summary>
  /// <param name="logCheckResult">
  /// If <c>true</c> then the result of the check will be logged.
  /// </param>
  /// <returns>
  /// <c>true</c> if projection of the position and direction of the head, and whatever is attached
  /// to it, won't deal a significant disturbance to the system.
  /// </returns>
  bool CheckIsHeadAligned(bool logCheckResult) {
    // Check the pre-conditions. 
    if (maxAllowedCableLength > Mathf.Epsilon  // Cable is not fully retracted.
        || realHeadDistance > headLockMaxErrorDist  // The head is not close enough.
        || isCableHeadOnKerbal) {  // A live being is on the cable.
      if (logCheckResult) {
        HostedDebugLog.Info(this, "Head is not aligned: preconditions failed");
      }
      return false;
    }
    // The alignment doesn't matter if the head is not attached to anything.
    if (!isLinked) {
      if (logCheckResult) {
        HostedDebugLog.Info(this, "Unplugged head is allowed to lock");
      }
      return true;
    }
    // Check if the alignment error is small enough to not awake Krakken on dock.
    var fwdAngleErr = 180 - Vector3.Angle(headCableAnchor.forward, nodeTransform.forward);
    if (fwdAngleErr > headLockMaxErrorDir) {
      if (logCheckResult) {
        HostedDebugLog.Info(
            this, "Plugged head align error is beyond the allowed limits: yaw/pitch={0}",
            fwdAngleErr);
      }
      return false;
    }

    if (logCheckResult) {
      HostedDebugLog.Info(this, "Plugged head is allowed to lock");
    }
    return true;
  }

  /// <summary>Checks if the cable head can be locked, and attempts to lock it.</summary>
  /// <remarks>The successful attempt will be logged to GUI.</remarks>
  /// <param name="reportIfCannot">
  /// If <c>true</c> then the failed attempt will be logged to GUI.
  /// </param>
  /// <returns><c>true</c> if the head was successfully locked.</returns>
  bool TryLockingHead(bool reportIfCannot = true) {
    if (!CheckIsHeadAligned(reportIfCannot)) {
      if (reportIfCannot) {
        ShowMessageForActiveVessel(LockHeadNotAlignedMsg);
      }
      return false;
    }
    winchState = WinchState.HeadLocked;
    ShowMessageForActiveVessel(HeadLockedMsg);
    return true;
  }

  /// <summary>Changes the winch state if the transition is allowed.</summary>
  /// <remarks>Transition to the same state is always allowed.</remarks>
  /// <param name="newState">The new state of the winch to set.</param>
  /// <param name="reportNegative">
  /// If <c>true</c> then the negative responses will be logged as a warning in the logs. Set this
  /// parameter if the call is done for a sanity check.
  /// </param>
  /// <returns><c>true</c> if state change was successful.</returns>
  bool SetStateIfPossible(WinchState newState, bool reportNegative = false) {
    if (newState == winchState) {
      return true;
    }
    if (stateMachine.CheckCanSwitchTo(newState)) {
      winchState = newState;
      return true;
    }
    if (reportNegative) {
      HostedDebugLog.Warning(
          this, "Ignore impossible state transition: {0} => {1}", winchState, newState);
    }
    return false;
  }
  
  /// <summary>
  /// Turns the head into a physical object that's linked to the winch via a cable.
  /// </summary>
  void DeployCableHead() {
    headModelObj.parent = headModelObj;
    //TODO(ihsoft): Make a KAS shared method.
    var headRb = headModelObj.gameObject.AddComponent<Rigidbody>();
    headRb.useGravity = false;
    headRb.velocity = part.rb.velocity;
    headRb.angularVelocity = part.rb.angularVelocity;
    headRb.mass = headMass;
    part.mass -= headRb.mass;
    part.rb.mass -= headRb.mass;

    // The cable is indestructable. In case of too string froces the head or the winch joint should
    // break.
    //TODO(ihsoft): use KAS shared library.
    cableJoint = headModelObj.gameObject.AddComponent<SpringJoint>();
    cableJoint.connectedBody = part.rb;
    cableJoint.maxDistance = 0;
    cableJoint.minDistance = 0;
    cableJoint.spring = cableSpring;
    cableJoint.damper = cableDamper;
    cableJoint.breakForce = Mathf.Infinity;
    cableJoint.breakTorque = Mathf.Infinity;
    cableJoint.autoConfigureConnectedAnchor = false;
    cableJoint.anchor = headModelObj.InverseTransformPoint(headCableAnchor.position);
    cableJoint.connectedAnchor = part.rb.transform.InverseTransformPoint(nodeTransform.position);
    
    linkRenderer.StartRenderer(nodeTransform, headCableAnchor);
    HostedDebugLog.Info(this, "Winch head is deployed");
  }

  /// <summary>Turns a physical head back into a physicsless mesh within the part's model.</summary>
  void LockCableHead() {
    // Turn the head into a physicsless object.
    var headRb = headModelObj.gameObject.GetComponent<Rigidbody>();
    part.mass += headRb.mass;
    part.rb.mass += headRb.mass;
    // We want the immediate destruction to not get affected by the physics left-offs.
    UnityEngine.Object.DestroyImmediate(cableJoint);
    UnityEngine.Object.DestroyImmediate(headRb);
    cableJoint = null;

    // Bring the head back into the part's model.
    headModelObj.parent = Hierarchy.GetPartModelTransform(part);
    AlignTransforms.SnapAlign(headModelObj, headCableAnchor, nodeTransform);
    UpdateContextMenu();  // The real cable length may have changed.

    linkRenderer.StopRenderer();

    sndHeadLock.Play();
    HostedDebugLog.Info(this, "Winch head is locked");
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
    headModelObj = Hierarchy.FindPartModelByPath(part, headModel);
    if (headModelObj != null) {
      headCableAnchor = Hierarchy.FindTransformByPath(headModelObj, headCableAttachAt);
      if (headCableAnchor == null) {
        HostedDebugLog.Info(this, "Creating cable transform");
        headCableAnchor = new GameObject(headCableAttachAt).transform;
        var posAndRot = PosAndRot.FromString(headCableAttachAtPosAndRot);
        Hierarchy.MoveToParent(headCableAnchor, headModelObj,
                               newPosition: posAndRot.pos, newRotation: posAndRot.rot);
      }
      headPartAnchor = Hierarchy.FindTransformByPath(headModelObj, headPartAttachAt);
      if (headPartAnchor == null) {
        HostedDebugLog.Info(this, "Creating part transform");
        headPartAnchor = new GameObject(headPartAttachAt).transform;
        var posAndRot = PosAndRot.FromString(headPartAttachAtPosAndRot);
        Hierarchy.MoveToParent(headPartAnchor, headModelObj,
                               newPosition: posAndRot.pos, newRotation: posAndRot.rot);
      }
    } else {
      HostedDebugLog.Error(this, "Cannot find a head model: {1}", headModel);
      // Fallback to not have the whole code to crush. 
      headModelObj = new GameObject().transform;
      headCableAnchor = headModelObj;
      headPartAnchor = headModelObj;
    }

    // Ensure the head is aligned as we expect it to be.    
    AlignTransforms.SnapAlign(headModelObj, headCableAnchor, nodeTransform);
  }
  #endregion
}

}  // namespace
