// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.ConfigUtils;
using KSPDev.GUIUtils;
using KSPDev.Extensions;
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

/// <summary>Module for a simple winch with a deployable connector.</summary>
/// <remarks>
/// <para>
/// The connector is attached to the winch with a cable, and it can link with the compatible link
/// targets. The winch itself is a <see cref="ILinkSource">link source</see>. An EVA kerbal can
/// "grab" the connector and carry it as far as the cable maximum length allows.
/// </para>
/// <para>
/// Since the winch is a basic link source it can link with the target in the differnt modes.
/// However, it's highly recommended to use the mode <see cref="LinkMode.TieAnyParts"/>. As it
/// the most flexible, and the winch is capable of changing "docked" vs "non-docked" mode when the
/// link is already made.
/// <br/>TODO: Implement
/// </para>
/// </remarks>
/// <seealso cref="ILinkSource"/>
/// <seealso cref="ILinkTarget"/>
// Next localization ID: #kasLOC_08024.
public class KASModuleWinchNew : KASModuleLinkSourceBase,
    // KAS interfaces.
    IHasContextMenu, IsPhysicalObject, IKasPropertyChangeListener {
  #region Localizable GUI strings.

  #region ConnectorState enum values
  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ConnectorStateMsg_Locked = new Message(
      "#kasLOC_08001",
      defaultTemplate: "Locked",
      description: "A string in the context menu that tells that the winch connector is rigidly"
      + " attached to the and is not movable.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ConnectorStateMsg_Deployed = new Message(
      "#kasLOC_08018",
      defaultTemplate: "Deployed",
      description: "A string in the context menu that tells that the winch connector is deployed"
      + " and attached to the winch via a cable.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ConnectorStateMsg_Plugged = new Message(
      "#kasLOC_08023",
      defaultTemplate: "Plugged in",
      description: "A string in the context menu that tells that the winch connector is plugged in"
      + " a socked or is being carried by a kerbal, and attached to the winch via a cable.");
  #endregion

  /// <summary>Translates <see cref="ConnectorState"/> enum into a localized message.</summary>
  protected static readonly MessageLookup<ConnectorState> ConnectorStatesMsgLookup =
      new MessageLookup<ConnectorState>(new Dictionary<ConnectorState, Message>() {
          {ConnectorState.Locked, ConnectorStateMsg_Locked},
          {ConnectorState.Deployed, ConnectorStateMsg_Deployed},
          {ConnectorState.Plugged, ConnectorStateMsg_Plugged},
      });

  #region MotorState enum values
  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message MotorStateMsg_Idle = new Message(
      "#kasLOC_08022",
      defaultTemplate: "Idle",
      description: "A string in the context menu that tells that the winch the motor is inactive.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message MotorStateMsg_Extending = new Message(
      "#kasLOC_08019",
      defaultTemplate: "Extending",
      description: "A string in the context menu that tells that the winch connector is deployed"
      + " and the cable is being extended.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message MotorStateMsg_Retracting = new Message(
      "#kasLOC_08020",
      defaultTemplate: "Retracting",
      description: "A string in the context menu that tells that the winch connector is deployed"
      + " and the cable size being retracted.");
  #endregion

  /// <summary>Translates <see cref="MotorState"/> enum into a localized message.</summary>
  protected static readonly MessageLookup<MotorState> MotorStatesMsgLookup =
      new MessageLookup<MotorState>(new Dictionary<MotorState, Message>() {
          {MotorState.Idle, MotorStateMsg_Idle},
          {MotorState.Extending, MotorStateMsg_Extending},
          {MotorState.Retracting, MotorStateMsg_Retracting},
      });

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message NoEnergyMsg = new Message(
      "#kasLOC_08002",
      defaultTemplate: "No energy!",
      description: "Error message to present when the electricity charge has exhausted.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message LockConnectorNotAlignedMsg = new Message(
      "#kasLOC_08003",
      defaultTemplate: "Cannot lock the connector: not aligned",
      description: "Error message to present when an improperly aligned cable connector has"
      + " attempted to lock with the winch.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message ConnectorLockedMsg = new Message(
      "#kasLOC_08004",
      defaultTemplate: "Connector locked!",
      description: "Info message to present when a cable connector has successfully locked to the"
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
      defaultTemplate: "The connector is detached due to the cable strength is exceeded",
      description: "A message to display when a too string force has broke the link between the"
      + "winch and it's target.");
  #endregion

  #region Part's config fields
  /// <summary>Object that represents the connector's model.</summary>
  /// <remarks>
  /// The value is a <see cref="Hierarchy.FindTransformByPath(Transform,string,Transform)"/> search
  /// path. The path is looked globally, starting from the part's model root.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='M:KSPDev.Hierarchy.FindTransformByPath']/*"/>
  [KSPField]
  public string connectorModel = "";

  /// <summary>Mass of the connector of the winch.</summary>
  /// <remarks>
  /// It's substracted from the part's mass on deploy, and added back on the lock. For this reason
  /// it must not be greater then the total part's mass. Also, try to avoid making the connector
  /// heavier than the part iteself - the Unity physics may start behaving awkward. 
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float connectorMass = 0.01f;

  /// <summary>
  /// Name of the object that is used to align the cable connector against the target part.
  /// </summary>
  /// <remarks>
  /// The value is a <see cref="Hierarchy.FindTransformByPath(Transform,string,Transform)"/> search
  /// path. The path is looked starting from the connector's model.
  /// </remarks>
  /// <seealso cref="connectorModel"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='M:KSPDev.Hierarchy.FindTransformByPath']/*"/>
  [KSPField]
  public string connectorPartAttachAt = "";

  /// <summary>Position and rotation of the connector-to-part attach point.</summary>
  /// <remarks>
  /// <para>
  /// The values must be given in the coordinates local to the connector. This value will only be
  /// used if there is no object named <see cref="connectorPartAttachAt"/> in the connector's object
  /// hierarchy.
  /// </para>
  /// <para>The value is a serialized format of <see cref="PosAndRot"/>.</para>
  /// </remarks>
  /// <seealso cref="connectorPartAttachAt"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.Types.PosAndRot']/*"/>
  [KSPField]
  public string connectorPartAttachAtPosAndRot = "";

  /// <summary>
  /// Name of the object that is used to align the cable mesh to the cable connector.
  /// </summary>
  /// <remarks>
  /// The value is a <see cref="Hierarchy.FindTransformByPath(Transform,string,Transform)"/> search
  /// path. The path is looked starting from the connector model.
  /// </remarks>
  /// <seealso cref="connectorModel"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='M:KSPDev.Hierarchy.FindTransformByPath']/*"/>
  [KSPField]
  public string connectorCableAttachAt = "";

  /// <summary>Position and rotation of the cable-to-connector attach point.</summary>
  /// <remarks>
  /// The values must be given in the coordinates local to the connector. This value will only be
  /// used if there is no object named <see cref="connectorCableAttachAt"/> in the connector's
  /// object hierarchy.
  /// </remarks>
  /// <seealso cref="connectorCableAttachAt"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="Unity3D_HelpIndex.xml" path="//item[@name='T:UnityEngine.Vector3']/*"/>
  [KSPField]
  public string connectorCableAttachAtPosAndRot;

  /// <summary>Maximum cable length at which the cable connector can lock to the winch.</summary>
  /// <remarks>
  /// A spring joint in PhysX will never pull the objects together to the zero distance regardless
  /// to the spring strength. For this reason the there should be always be a reasonable error
  /// allowed. Setting the error to a too big value will result in unpleasant locking behavior and
  /// increase the force at which the connector hits the winch on locking. A too small value of the
  /// allowed error will make the locking harder, up to not being able to lock at all.
  /// </remarks>
  /// <seealso cref="CheckIsConnectorAligned"/>
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
  /// <seealso cref="CheckIsConnectorAligned"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="Unity3D_HelpIndex.xml" path="//item[@name='T:UnityEngine.Vector3']/*"/>
  [KSPField]
  public float connectorLockMaxErrorDir = 1;

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

  /// <summary>URL of the sound for the event of returning the connector to the winch.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string sndPathLockConnector = "";

  /// <summary>URL of the sound for the event of acquiring the connector by an EVA kerbal.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string sndPathGrabConnector = "";

  /// <summary>URL of the sound for the event of plugging the connector into a socket.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string sndPathPlugConnector = "";
  
  /// <summary>URL of the sound for the event of unplugging the connector from a socket.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string sndPathUnplugConnector = "";

  /// <summary>URL of the sound for the event of cable emergency detachment (link broken).</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string sndPathBroke = "";
  #endregion

  #region Persistent fields
  /// <summary>Connector state in the last save action.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  public ConnectorState persistedConnectorState = ConnectorState.Locked;

  /// <summary>Position and rotation of the deployed connector.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [PersistentField("connectorPosAndRot", group = PersistentGroup)]
  PosAndRot persistedConnectorPosAndRot;
  #endregion

  #region The context menu fields
  /// <summary>Status field to display the current connector status in the context menu.</summary>
  /// <see cref="connectorState"/>
  /// <include file="SpecialDocTags.xml" path="Tags/UIConfigSetting/*"/>
  [KSPField(guiActive = true)]
  [LocalizableItem(
      tag = "#kasLOC_08012",
      defaultTemplate = "Connector state",
      description = "Status field to display the current winch connector status in the context"
      + " menu.")]
  public string connectorStateMenuInfo = "";

  /// <summary>Status field to display the current motor status in the context menu.</summary>
  /// <see cref="motorState"/>
  /// <include file="SpecialDocTags.xml" path="Tags/UIConfigSetting/*"/>
  [KSPField(guiActive = true)]
  [LocalizableItem(
      tag = "#kasLOC_08021",
      defaultTemplate = "Motor state",
      description = "Status field to display the current motor status in the context menu.")]
  public string motorStateMenuInfo = "";

  /// <summary>A context menu item that presents the maximum allowed cable length.</summary>
  /// <seealso cref="KASModuleCableJointBase.maxAllowedCableLength"/>
  /// <include file="SpecialDocTags.xml" path="Tags/UIConfigSetting/*"/>
  [KSPField(guiActive = true)]
  [LocalizableItem(
      tag = "#kasLOC_08013",
      defaultTemplate = "Deployed length",
      description = "A context menu item that presents the length of the currently deployed"
      + " cable.")]
  public string deployedCableLengthMenuInfo = "";
  #endregion

  #region Context menu events/actions
  /// <summary>A context menu item that starts/stops extending the cable.</summary>
  /// <remarks>
  /// If the connector was locked it will be deployed. This method does nothing is the cable cannot
  /// be extended for any reason.
  /// </remarks>
  /// <seealso cref="UpdateContextMenu"/>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true)]
  [LocalizableItem(tag = null)]
  public virtual void ToggleExtendCableEvent() {
    if (Mathf.Approximately(cableJoint.maxAllowedCableLength, cableJoint.cfgMaxCableLength)) {
      // Already at the maximum length.
      ScreenMessaging.ShowPriorityScreenMessage(
          MaxLengthReachedMsg.Format(cableJoint.cfgMaxCableLength));
      return;
    }
    if (connectorState == ConnectorState.Locked) {
      connectorState = ConnectorState.Deployed;
    }
    if (IsCableDeployed()) {
      motorState = motorState == MotorState.Extending ? MotorState.Idle : MotorState.Extending;
    };
  }

  /// <summary>A context menu item that starts/stops retracting the cable.</summary>
  /// <remarks>
  /// If the cable length is zero but the connector is not locked, then this method will try to lock
  /// the connector. It does nothing is the cable cannot be retracted for any reason.
  /// </remarks>
  /// <seealso cref="UpdateContextMenu"/>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true)]
  [LocalizableItem(tag = null)]
  public virtual void ToggleRetractCableEvent() {
    if (connectorState == ConnectorState.Locked) {
      ShowMessageForActiveVessel(ConnectorLockedMsg);
      return;  // Nothing to do.
    }
    // If the whole cable has been retracted, then just try to lock.
    if (IsCableDeployed()) {
      if (cableJoint.maxAllowedCableLength < Mathf.Epsilon) {
        TryLockingConnector();
        return;
      }
      motorState = motorState == MotorState.Retracting ? MotorState.Idle : MotorState.Retracting;
    }
  }

  /// <summary>
  /// A context menu item that sets the cable length ot the maximum, and unlocks the connector if it
  /// was locked.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true)]
  [LocalizableItem(
      tag = "#kasLOC_08014",
      defaultTemplate = "Release cable",
      description = "A context menu item that sets the cable length ot the maximum, and unlocks"
      + " the connector if it was locked.")]
  public virtual void ReleaseCableEvent() {
    if (connectorState == ConnectorState.Locked) {
      connectorState = ConnectorState.Deployed;
    }
    if (IsCableDeployed()) {
      cableJoint.maxAllowedCableLength = cableJoint.cfgMaxCableLength;
      ScreenMessaging.ShowPriorityScreenMessage(
          MaxLengthReachedMsg.Format(cableJoint.cfgMaxCableLength));
    }
  }

  /// <summary>
  /// A context menu event that sets the cable length to the current distance to the connector.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true)]
  [LocalizableItem(
      tag = "#kasLOC_08015",
      defaultTemplate = "Instant stretch",
      description = "A context menu event that sets the cable length to the current distance to the"
      + " connector.")]
  public virtual void InstantStretchEvent() {
    if (IsCableDeployed()) {
      cableJoint.maxAllowedCableLength =
          Mathf.Min(cableJoint.realCableLength, cableJoint.maxAllowedCableLength);
    }
  }

  /// <summary>Attaches the connector to the EVA kerbal.</summary>
  /// <remarks>The active vessel must be a kerbal.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActiveUnfocused = true, externalToEVAOnly = false)]
  [LocalizableItem(
      tag = "#kasLOC_08016",
      defaultTemplate = "Grab connector",
      description = "A context menu event that attaches the connector to the EVA kerbal.")]
  public virtual void GrabConnectorEvent() {
    if (FlightGlobals.ActiveVessel.isEVA && connectorState == ConnectorState.Locked) {
      var kerbalTarget = FlightGlobals.ActiveVessel.rootPart.FindModulesImplementing<ILinkTarget>()
          .FirstOrDefault(t => t.cfgLinkType == cfgLinkType);
      if (kerbalTarget != null
          && CheckCanLinkTo(kerbalTarget, reportToGUI: true)
          && StartLinking(GUILinkMode.API, LinkActorType.Player)) {
        LinkToTarget(kerbalTarget);
        cableJoint.maxAllowedCableLength = cableJoint.cfgMaxCableLength;
      } else {
        UISoundPlayer.instance.Play(CommonConfig.sndPathBipWrong);
      }
    }
  }

  /// <summary>Detaches the connector from the kerbal and puts it back to the winch.</summary>
  /// <remarks>The active vessel must be a kerbal holding a connector of this winch.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActiveUnfocused = true, externalToEVAOnly = false)]
  [LocalizableItem(
      tag = "#kasLOC_08017",
      defaultTemplate = "Return connector",
      description = "A context menu event that detaches the connector from the kerbal and puts it"
      + " back to the winch.")]
  public virtual void ReturnConnectorEvent() {
    if (FlightGlobals.ActiveVessel.isEVA
        && linkTarget != null && linkTarget.part.vessel == FlightGlobals.ActiveVessel) {
      var kerbalTarget = FlightGlobals.ActiveVessel.rootPart.FindModulesImplementing<ILinkTarget>()
          .FirstOrDefault(t => ReferenceEquals(t.linkSource, this));
      // Kerbal is a target for the winch, and we want the kerbal to keep the focus.
      BreakCurrentLink(LinkActorType.Player, moveFocusOnTarget: true);
      connectorState = ConnectorState.Locked;
      HostedDebugLog.Info(
          this, "{0} has returned the winch connector", FlightGlobals.ActiveVessel.vesselName);
    }
  }
  #endregion

  #region Externally visible state of the winch
  /// <summary>State of the winch connector.</summary>
  public enum ConnectorState {
    /// <summary>
    /// The connector is rigidly attached to the winch's body. The model is a parent of the winch
    /// model.
    /// </summary>
    Locked,

    /// <summary>
    /// The connector is a standalone physical object, attached to the winch via the cable.
    /// </summary>
    Deployed,
    
    /// <summary>
    /// The connector is plugged to a link target. It doesn't have physics, and its model is part of
    /// the target's model.
    /// </summary>
    /// <remarks>
    /// This state can only exist if the winch's link source is linked to a target.
    /// </remarks>
    /// <seealso cref="KASModuleLinkSourceBase"/>
    Plugged,
  }

  /// <summary>Controls the state of the winch.</summary>
  /// <value>The current winch state.</value>
  public ConnectorState connectorState {
    get { return connectorStateMachine.currentState ?? persistedConnectorState; }
    private set {
      if (connectorStateMachine.currentState != value) {
        motorState = MotorState.Idle;
      }
      connectorStateMachine.currentState = value;
      persistedConnectorState = value;
    }
  }

  #region IWinchControl candidates
  /// <summary>State of the motor.</summary>
  public enum MotorState {
    /// <summary>The motor is not spinning.</summary>
    /// <remarks>In this mode the electric charge is <i>not</i> consumed.</remarks>
    Idle,
    /// <summary>The motor is spinning, giving an extra length of the available cable.</summary>
    /// <remarks>In this mode the electric charge is consumed.</remarks>
    Extending,
    /// <summary>The motor is spinning, reducing the length of the available cable.</summary>
    /// <remarks>In this mode the electric charge is consumed.</remarks>
    Retracting,
  }

  /// <summary>Current state of the winch motor.</summary>
  /// <remarks>
  /// If state is changed to <see cref="MotorState.Idle"/>, then the motor stops immediately. It
  /// may result in the physical effects. When the mass on the cable is big and the speed is fast,
  /// it's better to set the motor speed to <c>0</c> and wait until the state is reset.
  /// </remarks>
  /// <value>The state.</value>
  /// <seealso cref="cfgMotorMaxSpeed"/>
  public MotorState motorState {
    get { return motorStateMachine.currentState ?? MotorState.Idle; }
    set {
      if (value != MotorState.Idle && !IsCableDeployed()) {
        HostedDebugLog.Warning(this, "Cannot start motor is state: {0}", connectorState);
        return;
      }
      motorStateMachine.currentState = value;
    }
  }

  /// <summary>Cable extend/retract speed.</summary>
  /// <remarks>
  /// When the motor starts from an idle state, it may need some time to get to the speed. If the
  /// speed is changed on the active motor, then the actual speed will change with respect to the
  /// <see cref="cfgMotorAcceleration"/>. When the motor need to be shut-off immediately, set the
  /// motor state to <see cref="MotorState.Idle"/>.
  /// </remarks>
  /// <value>Speed in meters per second.</value>
  /// <seealso cref="cfgMotorAcceleration"/>
  /// <seealso cref="motorState"/>
  public float cfgMotorMaxSpeed {
    get { return motorMaxSpeed; }
    set { motorMaxSpeed = value; }
  }

  /// <summary>Acceleration to apply when changin the actual winch motor speed.</summary>
  /// <value>Acceleration in meters per second squared.</value>
  public float cfgMotorAcceleration {
    get { return motorAcceleration; }
    set { motorAcceleration = value; }
  }

  /// <summary>Physical joint module that control the cable.</summary>
  /// <remarks>
  /// Note, that the winch will <i>not</i> notice any changes done to the joint. Always call
  /// <see cref="UpdateContextMenu"/> on the winch after the update.
  /// </remarks>
  /// <value>The module instance.</value>
  public ILinkCableJoint cableJoint {
    get { return linkJoint as ILinkCableJoint; }
  }
  #endregion

  #endregion

  #region Inheritable fileds and properties
  /// <summary>
  /// Name of persistent fields group that needs saving/load during the normal part's phases.
  /// </summary>
  /// <seealso cref="OnLoad"/>
  /// <seealso cref="OnSave"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ConfigUtils.ConfigAccessor']/*"/>
  protected const string PersistentGroup = "persistent";

  /// <summary>Winch connector model transformation object.</summary>
  /// <remarks>
  /// Depending on the current state this model can be a child to the part's model or a standalone
  /// object.
  /// </remarks>
  /// <seealso cref="ConnectorState"/>
  protected Transform connectorModelObj { get; private set; }
  #endregion

  #region Local properties and fields
  /// <summary>State machine that defines and controls the winch state.</summary>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ProcessingUtils.SimpleStateMachine_1']/*"/>
  SimpleStateMachine<ConnectorState> connectorStateMachine;

  /// <summary>State machine that controls the motor states.</summary>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ProcessingUtils.SimpleStateMachine_1']/*"/>
  SimpleStateMachine<MotorState> motorStateMachine;

  //FIXME: add comments to each field.
  Transform connectorCableAnchor;
  Transform connectorPartAnchor;
  float motorCurrentSpeed;

  AudioSource sndMotor;
  AudioSource sndMotorStart;
  AudioSource sndMotorStop;
  AudioSource sndConnectorLock;
  #endregion

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    linkStateMachine.onAfterTransition += (start, end) => UpdateContextMenu();

    sndMotor = SpatialSounds.Create3dSound(part.gameObject, sndPathMotor, loop: true);
    sndMotorStart = SpatialSounds.Create3dSound(part.gameObject, sndPathMotorStart);
    sndMotorStop = SpatialSounds.Create3dSound(part.gameObject, sndPathMotorStop);
    sndConnectorLock = SpatialSounds.Create3dSound(part.gameObject, sndPathLockConnector);

    #region Connector state machine
    // The default state is "Locked". All the enter state handlers rely on it, and all the exit
    // state handlers reset the state back to the default.
    connectorStateMachine = new SimpleStateMachine<ConnectorState>(strict: true);
    connectorStateMachine.onAfterTransition += (start, end) => {
      UpdateContextMenu();
      HostedDebugLog.Info(this, "Connector state changed: {0} => {1}", start, end);
    };
    connectorStateMachine.SetTransitionConstraint(
        ConnectorState.Locked, new[] { ConnectorState.Deployed, ConnectorState.Plugged });
    connectorStateMachine.SetTransitionConstraint(
        ConnectorState.Deployed, new[] { ConnectorState.Locked, ConnectorState.Plugged });
    connectorStateMachine.SetTransitionConstraint(
        ConnectorState.Plugged, new[] { ConnectorState.Deployed });
    connectorStateMachine.AddStateHandlers(
        ConnectorState.Locked,
        enterHandler: oldState => {
          connectorModelObj.parent = nodeTransform;  // Ensure it for consistency.
          AlignTransforms.SnapAlign(
              connectorModelObj, connectorCableAnchor, physicalAnchorTransform);
          cableJoint.maxAllowedCableLength = 0;
          if (oldState.HasValue) {  // Skip when restoring state.
            sndConnectorLock.Play();
          }
        });
    connectorStateMachine.AddStateHandlers(
        ConnectorState.Deployed,
        enterHandler: oldState => {
          TurnConnectorPhysics(true);
          linkRenderer.StartRenderer(physicalAnchorTransform, connectorCableAnchor);
        },
        leaveHandler: newState => {
          TurnConnectorPhysics(false);
          linkRenderer.StopRenderer();
        });
    connectorStateMachine.AddStateHandlers(
        ConnectorState.Plugged,
        enterHandler: oldState => {
          connectorModelObj.parent = linkTarget.nodeTransform;
          PartModel.UpdateHighlighters(part);
          PartModel.UpdateHighlighters(linkTarget.part);
          AlignTransforms.SnapAlign(
              connectorModelObj, connectorPartAnchor, linkTarget.nodeTransform);
          linkRenderer.StartRenderer(physicalAnchorTransform, linkTarget.physicalAnchorTransform);
        },
        leaveHandler: newState => {
          var oldParent = connectorModelObj.parent;
          connectorModelObj.parent = nodeTransform;  // Back to the model.
          PartModel.UpdateHighlighters(part);
          PartModel.UpdateHighlighters(oldParent);
          linkRenderer.StopRenderer();
        });
    #endregion

    #region Motor state machine
    motorStateMachine = new SimpleStateMachine<MotorState>(strict: false);
    motorStateMachine.onAfterTransition += (start, end) => {
      UpdateContextMenu();
      HostedDebugLog.Info(this, "Motor state changed: {0} => {1}", start, end);
    };
    motorStateMachine.AddStateHandlers(
        MotorState.Idle,
        enterHandler: oldState => {
          motorCurrentSpeed = 0;
        });
    motorStateMachine.AddStateHandlers(
        MotorState.Extending,
        enterHandler: oldState => {
          sndMotorStart.Play();
          sndMotor.Play();
        },
        leaveHandler: newState => {
          sndMotorStop.Play();
          sndMotor.Stop();
          motorCurrentSpeed = 0;
        });
    motorStateMachine.AddStateHandlers(
        MotorState.Retracting,
        enterHandler: oldState => {
          sndMotorStart.Play();
          sndMotor.Play();
        },
        leaveHandler: newState => {
          sndMotorStop.Play();
          sndMotor.Stop();
          motorCurrentSpeed = 0;
        });
    #endregion
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);
    if (connectorMass > part.mass) {
      HostedDebugLog.Error(this,
          "Connector mass is greater than the part's mass: {0} > {1}", connectorMass, part.mass);
      connectorMass = 0.1f * part.mass;  // A fail safe value. 
    }
    LoadOrCreateConnectorModel();
    if (persistedConnectorState == ConnectorState.Deployed) {
      ConfigAccessor.ReadFieldsFromNode(node, typeof(KASModuleWinchNew), this,
                                        group: PersistentGroup);
      // In case of the connector is not locked to either the winch or the target part, adjust its
      // model position and rotation. The rest of the state will be erstored in the state machine. 
      if (persistedConnectorPosAndRot != null) {
        var world = gameObject.transform.TransformPosAndRot(persistedConnectorPosAndRot);
        connectorModelObj.position = world.pos;
        connectorModelObj.rotation = world.rot;
      }
    }
  }

  /// <inheritdoc/>
  public override void OnStartFinished(PartModule.StartState state) {
    base.OnStartFinished(state);

    // The renderer will be started anyways in the state machine, which starts when the physics
    // kicks in. We start it here only to improve the visual representation during the loading.
    if (persistedConnectorState == ConnectorState.Deployed) {
      linkRenderer.StartRenderer(physicalAnchorTransform, connectorCableAnchor);
    }
  }

  /// <inheritdoc/>
  public override void OnSave(ConfigNode node) {
    base.OnSave(node);
    // Persist the connector data only if its position is not fixed to the winch model.
    // It must be the peristsent state since the state machine can be in a different state at this
    // moment (e.g. during the vessel backup).
    if (persistedConnectorState == ConnectorState.Deployed) {
      persistedConnectorPosAndRot = gameObject.transform.InverseTransformPosAndRot(
          new PosAndRot(connectorModelObj.position, connectorModelObj.rotation.eulerAngles));
      ConfigAccessor.WriteFieldsIntoNode(node, typeof(KASModuleWinchNew), this,
                                         group: PersistentGroup);
    }
  }

  /// <inheritdoc/>
  public override void OnPartUnpack() {
    base.OnPartUnpack();
    connectorState = persistedConnectorState;
  }
  #endregion

  #region IsPhysicalObject implementation
  /// <inheritdoc/>
  public virtual void FixedUpdate() {
    //TODO: Do it in the OnFixedUpdate().
    if (HighLogic.LoadedSceneIsEditor) {
      return;
    }
    if (motorState != MotorState.Idle) {
      UpdateMotor();
    }
  }
  #endregion

  #region IKASPropertyChangeListener implementation
  /// <inheritdoc/>
  public void OnKASPropertyChanged(object owner, string name) {
    if (ReferenceEquals(owner, cableJoint)) {
      UpdateContextMenu();
    }
  }
  #endregion

  #region KASModuleLikSourceBase overrides
  /// <inheritdoc/>
  public override void OnKASLinkCreatedEvent(KASEvents.LinkEvent info) {
    base.OnKASLinkCreatedEvent(info);
    if (info.actor == LinkActorType.Player) {
      UISoundPlayer.instance.Play(info.target.part.vessel.isEVA
          ? sndPathGrabConnector
          : sndPathPlugConnector);
    }
  }

  /// <inheritdoc/>
  public override void OnKASLinkBrokenEvent(KASEvents.LinkEvent info) {
    base.OnKASLinkBrokenEvent(info);
    if (info.actor == LinkActorType.Physics) {
      UISoundPlayer.instance.Play(sndPathBroke);
      ScreenMessaging.ShowPriorityScreenMessage(CableLinkBrokenMsg);
    } else if (info.actor == LinkActorType.Player && !info.target.part.vessel.isEVA) {
      UISoundPlayer.instance.Play(sndPathUnplugConnector);
    }
  }

  /// <inheritdoc/>
  protected override void LogicalLink(ILinkTarget target) {
    base.LogicalLink(target);
    connectorState = ConnectorState.Plugged;
  }

  /// <inheritdoc/>
  protected override void PhysicalUnlink(ILinkTarget target) {
    cableJoint.maxAllowedCableLength = cableJoint.realCableLength;
    base.PhysicalUnlink(target);
  }

  /// <inheritdoc/>
  protected override void LogicalUnlink(LinkActorType actorType) {
    base.LogicalUnlink(actorType);
    connectorState = ConnectorState.Deployed;
  }
  #endregion

  #region IHasContextMenu implementation
  /// <inheritdoc/>
  public virtual void UpdateContextMenu() {
    connectorStateMenuInfo = ConnectorStatesMsgLookup.Lookup(connectorState);
    motorStateMenuInfo = MotorStatesMsgLookup.Lookup(motorState);
    deployedCableLengthMenuInfo = DistanceType.Format(
        cableJoint != null ? cableJoint.maxAllowedCableLength : 0);
    
    PartModuleUtils.SetupEvent(this, ToggleExtendCableEvent, e => {
      e.guiName = motorState == MotorState.Extending
          ? StopExtendingMenuTxt
          : ExtendCableMenuTxt;
    });
    PartModuleUtils.SetupEvent(this, ToggleRetractCableEvent, e => {
      e.guiName = motorState == MotorState.Retracting
          ? StopRetractingMenuTxt
          : RetractCableMenuTxt;
    });
    PartModuleUtils.SetupEvent(this, GrabConnectorEvent, e => {
      e.active = connectorState == ConnectorState.Locked;
    });
    PartModuleUtils.SetupEvent(this, ReturnConnectorEvent, e => {
      e.active = IsActiveEvaHoldingConnector();
    });
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

  /// <summary>
  /// Tells if the winch is not rigidly attached to the winch, and there is an active cable link to
  /// the connector.
  /// </summary>
  /// <returns><c>true</c> if there is a deployed cable.</returns>
  protected bool IsCableDeployed() {
    return connectorState == ConnectorState.Deployed || connectorState == ConnectorState.Plugged;
  }

  /// <summary>
  /// Tells if the currently active vessel is an EVA kerbal who carries this winch connector.
  /// </summary>
  /// <returns><c>true</c> if the connector on the kerbal.</returns>
  protected bool IsActiveEvaHoldingConnector() {
    return FlightGlobals.ActiveVessel != null  // It's null in the non-flight scenes.
        && FlightGlobals.ActiveVessel.isEVA
        && linkTarget != null && linkTarget.part != null
        && linkTarget.part.vessel == FlightGlobals.ActiveVessel;
  }
  #endregion

  #region Local utility methods
  /// <summary>Updates the winch connector cable according to the current motor movements.</summary>
  /// <remarks>This method is only called when the motor is consuming electricity.</remarks>
  void UpdateMotor() {
    // Adjust the motor speed to the target.
    if (motorState == MotorState.Extending) {
      motorCurrentSpeed += motorAcceleration * Time.fixedDeltaTime;
      if (motorCurrentSpeed > cfgMotorMaxSpeed) {
        motorCurrentSpeed = cfgMotorMaxSpeed;
      }
    } else if (motorState == MotorState.Retracting) {
      motorCurrentSpeed -= motorAcceleration * Time.fixedDeltaTime;
      if (motorCurrentSpeed < -cfgMotorMaxSpeed) {
        motorCurrentSpeed = -cfgMotorMaxSpeed;
      }
    }

    // Adjust the cable length.
    var powerDemand = motorPowerDrain * TimeWarp.fixedDeltaTime;
    var gotEnergy = part.RequestResource(StockResourceNames.ElectricCharge, powerDemand);
    if (Mathf.Approximately(gotEnergy, powerDemand)) {
      cableJoint.maxAllowedCableLength =
          cableJoint.maxAllowedCableLength + motorCurrentSpeed * TimeWarp.fixedDeltaTime;
      if (motorCurrentSpeed > 0
          && cableJoint.maxAllowedCableLength >= cableJoint.cfgMaxCableLength) {
        cableJoint.maxAllowedCableLength = cableJoint.cfgMaxCableLength;
        motorState = MotorState.Idle;
        ScreenMessaging.ShowPriorityScreenMessage(
            MaxLengthReachedMsg.Format(cableJoint.cfgMaxCableLength));
      } else if (motorCurrentSpeed < 0 && cableJoint.maxAllowedCableLength <= 0) {
        cableJoint.maxAllowedCableLength = 0;
        motorState = MotorState.Idle;
        TryLockingConnector();
      }
    } else {
      motorState = MotorState.Idle;
      ScreenMessaging.ShowErrorScreenMessage(NoEnergyMsg);
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
    if (cableJoint.maxAllowedCableLength > Mathf.Epsilon  // Cable is not fully retracted.
        || cableJoint.realCableLength > connectorLockMaxErrorDist) {  // Not close enough.
      if (logCheckResult) {
        HostedDebugLog.Info(this, "Connector cannot lock, the preconditions failed:"
                            + " maxLengh={0}, realLength={1}, isLinked={2}",
                            cableJoint.maxAllowedCableLength,
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
    //TODO(ihsoft): Implement docking.
    if (isLinked) {
      if (linkTarget.part.vessel.isEVA) {
        return false;  // Silently don't not allow docking with a kerbal.
      }
      if (reportIfCannot) {
        ShowMessageForActiveVessel("Docking to the winch is not yet implemented");
      }
      return false;
    }
    if (!CheckIsConnectorAligned(reportIfCannot)) {
      if (reportIfCannot) {
        ShowMessageForActiveVessel(LockConnectorNotAlignedMsg);
      }
      return false;
    }
    connectorState = ConnectorState.Locked;
    ShowMessageForActiveVessel(ConnectorLockedMsg);
    return true;
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
  void TurnConnectorPhysics(bool state) {
    if (state && cableJoint.headRb == null) {
      HostedDebugLog.Info(this, "Make the cable connector physical");
      var connector = InternalKASModulePhysicalConnector.Promote(
          this, connectorModelObj.gameObject, connectorInteractDistance);
      cableJoint.StartPhysicalHead(this, connectorCableAnchor);
      connector.connectorRb.mass = connectorMass;
      part.mass -= connectorMass;
      part.rb.mass -= connectorMass;
    } else if (!state && cableJoint.headRb != null) {
      HostedDebugLog.Info(this, "Make the cable connector non-physical");
      cableJoint.StopPhysicalHead();
      InternalKASModulePhysicalConnector.Demote(connectorModelObj.gameObject);
      part.mass += connectorMass;
      part.rb.mass += connectorMass;
    }
  }

  /// <summary>Intializes the connector model object and its anchors.</summary>
  /// <remarks>
  /// <para>
  /// If the connector model is not found then a stub object will be created. There will be no visual
  /// representation but the overall functionality of the winch should keep working.
  /// </para>
  /// <para>
  /// If the connector doesn't have the anchors then the missed ones will be created basing on the
  /// provided position/rotation. If the config file doesn't provide anything then the anchors will
  /// have a zero position and a random rotation.
  /// </para>
  /// </remarks>
  void LoadOrCreateConnectorModel() {
    connectorModelObj = Hierarchy.FindPartModelByPath(part, connectorModel);
    if (connectorModelObj != null) {
      connectorCableAnchor = Hierarchy.FindTransformByPath(connectorModelObj, connectorCableAttachAt);
      if (connectorCableAnchor == null) {
        HostedDebugLog.Info(this, "Creating connector's cable transform");
        connectorCableAnchor = new GameObject(connectorCableAttachAt).transform;
        var posAndRot = PosAndRot.FromString(connectorCableAttachAtPosAndRot);
        Hierarchy.MoveToParent(connectorCableAnchor, connectorModelObj,
                               newPosition: posAndRot.pos, newRotation: posAndRot.rot);
      }
      connectorPartAnchor = Hierarchy.FindTransformByPath(connectorModelObj, connectorPartAttachAt);
      if (connectorPartAnchor == null) {
        HostedDebugLog.Info(this, "Creating connector's part transform");
        connectorPartAnchor = new GameObject(connectorPartAttachAt).transform;
        var posAndRot = PosAndRot.FromString(connectorPartAttachAtPosAndRot);
        Hierarchy.MoveToParent(connectorPartAnchor, connectorModelObj,
                               newPosition: posAndRot.pos, newRotation: posAndRot.rot);
      }
    } else {
      HostedDebugLog.Error(this, "Cannot find a connector model: {0}", connectorModel);
      // Fallback to not have the whole code to crash. 
      connectorModelObj = new GameObject().transform;
      connectorCableAnchor = connectorModelObj;
      connectorPartAnchor = connectorModelObj;
    }
  }
  #endregion
}

}  // namespace
