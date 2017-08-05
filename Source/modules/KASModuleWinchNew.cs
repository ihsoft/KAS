// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using KSPDev.PartUtils;
using KSPDev.ProcessingUtils;
using KSPDev.ResourceUtils;
using KSPDev.SoundsUtils;
using KSPDev.Types;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
// Next localization ID: #kasLOC_08021.
public class KASModuleWinchNew : KASModuleLinkSourceBase,
    // KAS interfaces.
    IHasContextMenu, IsPhysicalObject {

  #region Localizable GUI strings.

  #region WinchState enum values
  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message WinchStateMsg_HeadLocked = new Message(
      "#kasLOC_08001",
      defaultTemplate: "Head is locked",
      description: "A string in the context menu that tells that the winch head is rigidly attached"
      + " to the and is not movable.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message WinchStateMsg_HeadDeployed = new Message(
      "#kasLOC_08018",
      defaultTemplate: "Idle",
      description: "A string in the context menu that tells that the winch head is deployed and"
      + " attached to the winch via a cable.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message WinchStateMsg_CableExtending = new Message(
      "#kasLOC_08019",
      defaultTemplate: "Extending",
      description: "A string in the context menu that tells that the winch head is deployed and"
      + " the cable is being extended.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message WinchStateMsg_CableRetracting = new Message(
      "#kasLOC_08020",
      defaultTemplate: "Retracting",
      description: "A string in the context menu that tells that the winch head is deployed and"
      + " the cable size being retracted.");
  #endregion

  /// <summary>Translates <see cref="WinchState"/> enum into a localized message.</summary>
  protected static readonly MessageLookup<WinchState> WinchStatesMsgLookup =
      new MessageLookup<WinchState>(new Dictionary<WinchState, Message>() {
          {WinchState.HeadLocked, WinchStateMsg_HeadLocked},
          {WinchState.HeadDeployed, WinchStateMsg_HeadDeployed},
          {WinchState.CableExtending, WinchStateMsg_CableExtending},
          {WinchState.CableRetracting, WinchStateMsg_CableRetracting},
      });

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

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message CableLinkBrokenMsg = new Message(
      "#kasLOC_08011",
      defaultTemplate: "The head is detached due to the cable strength is exceeded",
      description: "A message to display when a too string force has broke the link between the"
      + "winch and it's target.");
  #endregion

  #region Part's config fields
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

  /// <summary>
  /// Name of the object that is used to align the cable head against the target part.
  /// </summary>
  /// <remarks>
  /// The value is a <see cref="Hierarchy.FindTransformByPath(Transform,string,Transform)"/> search
  /// path. The path is looked starting from the head model.
  /// </remarks>
  /// <seealso cref="headModel"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='M:KSPDev.Hierarchy.FindTransformByPath']/*"/>
  [KSPField]
  public string headPartAttachAt = "";

  /// <summary>Position and rotation of the target head-to-part attach point.</summary>
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

  /// <summary>Name of the object that is used to align the cable mesh to the cable head.</summary>
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

  /// <summary>Maximum distance at which an EVA kerbal can pickup a dropped connector.</summary>
  /// <seealso cref="InternalKASModulePhysicalConnector"/>
  /// <seealso cref="KASModuleKerbalLinkTarget"/>
  [KSPField]
  public float connectorInteractDistance = 0.3f;

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

  /// <summary>URL of the sound for the winch head grab event.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string sndPathGrabLock = "";

  /// <summary>URL of the sound for the cable emergency deatch event (link broken).</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string sndPathBroke = "";
  #endregion

  #region The context menu fields
  /// <summary>Status field to display the current winch status in the context menu.</summary>
  /// <see cref="winchState"/>
  /// <include file="SpecialDocTags.xml" path="Tags/UIConfigSetting/*"/>
  [KSPField(guiName = "Winch state", guiActive = true)]
  [LocalizableItem(
      tag = "#kasLOC_08012",
      defaultTemplate = "Winch state",
      description = "Status field to display the current winch status in the context menu.")]
  public string headDeployStateMenuInfo = "";

  /// <summary>A context menu item that presents the maximum allowed cable length.</summary>
  /// <seealso cref="KASModuleCableJointBase.maxAllowedCableLength"/>
  /// <include file="SpecialDocTags.xml" path="Tags/UIConfigSetting/*"/>
  [KSPField(guiName = "Deployed length", guiActive = true)]
  [LocalizableItem(
      tag = "#kasLOC_08013",
      defaultTemplate = "Deployed length",
      description = "A context menu item that presents the length of the currently deployed"
      + "cable.")]
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
    if (Mathf.Approximately(cableJointObj.maxAllowedCableLength, cableJointObj.cfgMaxCableLength)) {
      // Already at the maximum length.
      ScreenMessaging.ShowPriorityScreenMessage(
          MaxLengthReachedMsg.Format(cableJointObj.cfgMaxCableLength));
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
    if (winchState == WinchState.HeadDeployed
        && cableJointObj.maxAllowedCableLength < Mathf.Epsilon) {
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
  [KSPEvent(guiActive = true)]
  [LocalizableItem(
      tag = "#kasLOC_08014",
      defaultTemplate = "Release cable",
      description = "A context menu item that sets the cable length ot the maximum, and unlocks"
      + " the head if it was locked.")]
  public virtual void ReleaseCableEvent() {
    if (SetStateIfPossible(WinchState.HeadDeployed)) {
      SetCableLength(cableJointObj.cfgMaxCableLength);
      ScreenMessaging.ShowPriorityScreenMessage(
          MaxLengthReachedMsg.Format(cableJointObj.cfgMaxCableLength));
    }
  }

  /// <summary>
  /// A context menu event that sets the cable length to the current distance to the head.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true)]
  [LocalizableItem(
      tag = "#kasLOC_08015",
      defaultTemplate = "Instant stretch",
      description = "A context menu event that sets the cable length to the current distance to the"
      + " head.")]
  public virtual void InstantStretchEvent() {
    if (winchState != WinchState.HeadLocked && SetStateIfPossible(WinchState.HeadDeployed)) {
      SetCableLength(Mathf.Min(cableJointObj.realCableLength, cableJointObj.maxAllowedCableLength));
    }
  }

  /// <summary>Attaches the head to the EVA kerbal.</summary>
  /// <remarks>The active vessel must be a kerbal.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActiveUnfocused = true, externalToEVAOnly = false)]
  [LocalizableItem(
      tag = "#kasLOC_08016",
      defaultTemplate = "Grab head",
      description = "A context menu event that attaches the head to the EVA kerbal.")]
  public virtual void GrabHeadEvent() {
    if (FlightGlobals.ActiveVessel.isEVA && winchState == WinchState.HeadLocked) {
      var kerbalTarget = FlightGlobals.ActiveVessel.rootPart.FindModulesImplementing<ILinkTarget>()
          .FirstOrDefault(t => t.linkSource == null && t.cfgLinkType == cfgLinkType);
      if (kerbalTarget == null) {
        HostedDebugLog.Error(
            this, "{0} cannot grab the winch head", FlightGlobals.ActiveVessel.vesselName);
        return;
      }
      //FIXME: move it into parameter
      //FIXME: check if part's config allows TiePartsOnDifferentVessels
      linkMode = LinkMode.TiePartsOnDifferentVessels;
      if (StartLinking(GUILinkMode.API, LinkActorType.Player)) {
        if (!LinkToTarget(kerbalTarget)) {
          CancelLinking(LinkActorType.API);
          HostedDebugLog.Error(this, "Cannot link the winch head to kerbal {0}",
                               FlightGlobals.ActiveVessel.vesselName);
        }
      }
    }
  }

  /// <summary>Detaches the head from the kerbal and puts it back to the winch.</summary>
  /// <remarks>The active vessel must be a kerbal holding a headof this winch.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActiveUnfocused = true, externalToEVAOnly = false)]
  [LocalizableItem(
      tag = "#kasLOC_08017",
      defaultTemplate = "Lock head",
      description = "A context menu event that detaches the head from the kerbal and puts it back"
      + " to the winch.")]
  public virtual void LockHeadEvent() {
    if (FlightGlobals.ActiveVessel.isEVA && isCableHeadOnKerbal) {
      var kerbalTarget = FlightGlobals.ActiveVessel.rootPart.FindModulesImplementing<ILinkTarget>()
          .FirstOrDefault(t => ReferenceEquals(t.linkSource, this));
      if (kerbalTarget != null) {
        // Kerbal is a target for the winch, and we want the kerbal to keep the focus.
        BreakCurrentLink(LinkActorType.Player, moveFocusOnTarget: true);
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
  /// <value>The current winch state.</value>
  public virtual WinchState winchState {
    get { return stateMachine.currentState ?? WinchState.HeadLocked; }
    private set { stateMachine.currentState = value; }
  }

  /// <summary>Tells if the winch head is picked up by an EVA kerbal.</summary>
  /// <value><c>true</c> if the head is being carried by a kerbal.</value>
  public bool isCableHeadOnKerbal {
    get { return isLinked && linkTarget.part.vessel.isEVA; }
  }
  #endregion

  #region Inheritable fileds and properties
  /// <summary>Winch head model transformation object.</summary>
  /// <remarks>
  /// Depending on the current state this model can be a child to the part's model or a standalone
  /// object.
  /// </remarks>
  /// <seealso cref="WinchState"/>
  protected Transform headModelObj { get; private set; }
  #endregion

  #region Local properties and fields
  /// <summary>State machine that defines and controls the winch state.</summary>
  /// <remarks>
  /// The machine can be adjusted until it's started in the <see cref="OnStart"/> method.
  /// </remarks>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ProcessingUtils.SimpleStateMachine_1']/*"/>
  SimpleStateMachine<WinchState> stateMachine;

  //FIXME: add comments to each field.
  Transform headCableAnchor;
  Transform headPartAnchor;
  float motorCurrentSpeed;
  float motorTargetSpeed;

  AudioSource sndMotor;
  AudioSource sndMotorStart;
  AudioSource sndMotorStop;
  AudioSource sndHeadLock;

  ILinkCableJoint cableJointObj {
    get { return linkJoint as ILinkCableJoint; }
  }
  #endregion

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();

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
          this, "Head mass is greater than the part's mass: {0} > {1}", headMass, part.mass);
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
  }
  #endregion

  #region KASModuleLikSourceBase overrides
  /// <inheritdoc/>
  protected override void OnStateChange(LinkState? oldState) {
    base.OnStateChange(oldState);
    UpdateContextMenu();
  }

  /// <inheritdoc/>
  public override void OnKASLinkCreatedEvent(KASEvents.LinkEvent info) {
    base.OnKASLinkCreatedEvent(info);
    if (info.actor == LinkActorType.Player) {
      UISoundPlayer.instance.Play(sndPathGrabLock);
      HostedDebugLog.Info(this, "{0} has grabbed the winch head",
                          info.target.part.vessel.vesselName);
    } else if (info.actor == LinkActorType.API) {
      HostedDebugLog.Info(this, "Winch has linked to {0}", info.target.part.vessel.vesselName);
    }
  }

  /// <inheritdoc/>
  public override void OnKASLinkBrokenEvent(KASEvents.LinkEvent info) {
    base.OnKASLinkBrokenEvent(info);
    if (info.actor == LinkActorType.Physics) {
      UISoundPlayer.instance.Play(sndPathBroke);
      ScreenMessaging.ShowPriorityScreenMessage(CableLinkBrokenMsg);
    }
    HostedDebugLog.Info(this, "Winch has unlinked from {0}", info.target.part.vessel.vesselName);
  }

  /// <inheritdoc/>
  protected override void PhysicalLink(ILinkTarget target) {
    winchState = WinchState.HeadDeployed;  // Ensure the head is deployed and not moving.
    TurnHeadPhysics(false, newConnectorOwner: target.nodeTransform);
    AlignTransforms.SnapAlign(headModelObj, headPartAnchor, target.nodeTransform);
    base.PhysicalLink(target);
    SetCableLength(cableJointObj.cfgMaxCableLength);  // Link in the released state.
  }

  /// <inheritdoc/>
  protected override void LogicalUnlink(LinkActorType actorType) {
    base.LogicalUnlink(actorType);
    DeployCableHead();
    var headDistanceAtBreak = Vector3.Distance(physicalAnchorTransform.position,
                                               headCableAnchor.position);
    SetCableLength(Mathf.Min(headDistanceAtBreak, cableJointObj.cfgMaxCableLength));
  }
  #endregion

  #region IHasContextMenu implementation
  /// <inheritdoc/>
  public virtual void UpdateContextMenu() {
    headDeployStateMenuInfo = WinchStatesMsgLookup.Lookup(winchState);
    deployedCableLengthMenuInfo = DistanceType.Format(
        cableJointObj != null ? cableJointObj.maxAllowedCableLength : 0);
    
    PartModuleUtils.SetupEvent(this, ExtentCableEvent, e => {
      e.guiName = winchState == WinchState.CableExtending
          ? StopExtendingMenuTxt
          : ExtendCableMenuTxt;
    });
    PartModuleUtils.SetupEvent(this, RetractCableEvent, e => {
      e.guiName = winchState == WinchState.CableRetracting
          ? StopRetractingMenuTxt
          : RetractCableMenuTxt;
    });
    // These events are only available for an EVA kerbal.
    PartModuleUtils.SetupEvent(
        this, GrabHeadEvent, e => e.active = winchState == WinchState.HeadLocked);
    PartModuleUtils.SetupEvent(
        this, LockHeadEvent, e => e.active = isCableHeadOnKerbal);
  }
  #endregion

  #region Inheritable utility methods
  /// <summary>Shows a message in GUI if the reporting part belongs to the active vessel.</summary>
  /// <remarks>
  /// Use this method to present an update which is only important when the player is in control
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

  /// <summary>Sets the the maximum cable length and updates the winch state as needed.</summary>
  /// <param name="newLength">The new length in meters.</param>
  protected void SetCableLength(float newLength) {
    cableJointObj.maxAllowedCableLength = newLength;
    UpdateContextMenu();
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
      SetCableLength(
          cableJointObj.maxAllowedCableLength + motorCurrentSpeed * TimeWarp.fixedDeltaTime);
      if (motorCurrentSpeed > 0
          && cableJointObj.maxAllowedCableLength >= cableJointObj.cfgMaxCableLength) {
        SetCableLength(cableJointObj.cfgMaxCableLength);
        winchState = WinchState.HeadDeployed;
        ScreenMessaging.ShowPriorityScreenMessage(
            MaxLengthReachedMsg.Format(cableJointObj.cfgMaxCableLength));
      } else if (motorCurrentSpeed < 0 && cableJointObj.maxAllowedCableLength <= 0) {
        SetCableLength(0);
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
    if (cableJointObj.maxAllowedCableLength > Mathf.Epsilon  // Cable is not fully retracted.
        || cableJointObj.realCableLength > headLockMaxErrorDist  // The head is not close enough.
        || isCableHeadOnKerbal) {  // A live being is on the cable.
      if (logCheckResult) {
        HostedDebugLog.Info(this, "Head is not aligned: preconditions failed:"
                            + " maxLengh={0}, realLength={1}, isOnKerbal={2}",
                            cableJointObj.maxAllowedCableLength,
                            cableJointObj.realCableLength,
                            isCableHeadOnKerbal);
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
    TurnHeadPhysics(true);
    //FIXME: consider phisycal anchors
    linkRenderer.StartRenderer(physicalAnchorTransform, headCableAnchor);
    HostedDebugLog.Info(this, "Winch head is deployed");
  }

  /// <summary>Turns a physical head back into a physicsless mesh within the part's model.</summary>
  void LockCableHead() {
    TurnHeadPhysics(false);
    AlignTransforms.SnapAlign(headModelObj, headCableAnchor, physicalAnchorTransform);
    linkRenderer.StopRenderer();

    //FIXME: only play it if it's a player/eva action
    sndHeadLock.Play();
    HostedDebugLog.Info(this, "Winch head is locked");
  }

  /// <summary>
  /// Makes the winch connector an idependent physcal onbject or returns it into a part's model as
  /// a physicsless object.
  /// </summary>
  /// <remarks>
  /// Note, that physics obejcts on the connector don't die in this method call. They will be
  /// cleaned up at the frame end. The caller must consider it when dealing with the connector.
  /// </remarks>
  /// <param name="state">The physical state of the connector: <c>true</c> means "physical".</param>
  /// <param name="newConnectorOwner">
  /// The new parent of the physicsless connector model. If it's <c>null</c>, then the part's model
  /// will be the parent. This parameter has no meaning when <paramref name="state"/> is
  /// <c>true</c>.
  /// </param>
  void TurnHeadPhysics(bool state, Transform newConnectorOwner = null) {
    if (state && cableJointObj.headRb == null) {
      HostedDebugLog.Info(this, "Make the cable connector physical");
      var head = InternalKASModulePhysicalConnector.Promote(
          this, headModelObj.gameObject, headMass, connectorInteractDistance);
      cableJointObj.StartPhysicalHead(this, headCableAnchor);
      part.mass -= headMass;
      part.rb.mass -= headMass;
    } else if (!state && cableJointObj.headRb != null) {
      HostedDebugLog.Info(this, "Make the cable head non-physical");
      cableJointObj.StopPhysicalHead();
      InternalKASModulePhysicalConnector.Demote(
          headModelObj.gameObject,
          newOwner: newConnectorOwner ?? Hierarchy.GetPartModelTransform(part));
      part.mass += headMass;
      part.rb.mass += headMass;
    }
  }

  /// <summary>Intializes the head model object and its anchors.</summary>
  /// <remarks>
  /// <para>
  /// If the head model is not found then a stub object will be created. There will be no visual
  /// representation but the overall functionality of the winch should keep working.
  /// </para>
  /// <para>
  /// If the head doesn't have the anchors then the missed ones will be created basing on the
  /// provided position/rotation. If the config file doesn't provide anything then the anchors will
  /// have a zero position and a random rotation.
  /// </para>
  /// </remarks>
  void LoadOrCreateHeadModel() {
    headModelObj = Hierarchy.FindPartModelByPath(part, headModel);
    if (headModelObj != null) {
      headCableAnchor = Hierarchy.FindTransformByPath(headModelObj, headCableAttachAt);
      if (headCableAnchor == null) {
        HostedDebugLog.Info(this, "Creating head's cable transform");
        headCableAnchor = new GameObject(headCableAttachAt).transform;
        var posAndRot = PosAndRot.FromString(headCableAttachAtPosAndRot);
        Hierarchy.MoveToParent(headCableAnchor, headModelObj,
                               newPosition: posAndRot.pos, newRotation: posAndRot.rot);
      }
      headPartAnchor = Hierarchy.FindTransformByPath(headModelObj, headPartAttachAt);
      if (headPartAnchor == null) {
        HostedDebugLog.Info(this, "Creating head's part transform");
        headPartAnchor = new GameObject(headPartAttachAt).transform;
        var posAndRot = PosAndRot.FromString(headPartAttachAtPosAndRot);
        Hierarchy.MoveToParent(headPartAnchor, headModelObj,
                               newPosition: posAndRot.pos, newRotation: posAndRot.rot);
      }
    } else {
      HostedDebugLog.Error(this, "Cannot find a head model: {0}", headModel);
      // Fallback to not have the whole code to crash. 
      headModelObj = new GameObject().transform;
      headCableAnchor = headModelObj;
      headPartAnchor = headModelObj;
    }
    // Ensure the head is aligned as we expect it to be.
    AlignTransforms.SnapAlign(headModelObj, headCableAnchor, physicalAnchorTransform);
  }
  #endregion
}

}  // namespace
