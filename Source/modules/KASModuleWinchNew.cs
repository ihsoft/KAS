// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KASAPIv1.GUIUtils;
using KSPDev.ConfigUtils;
using KSPDev.Extensions;
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

/// <summary>Module for a simple winch with a deployable connector.</summary>
/// <remarks>
/// <para>
/// The connector is attached to the winch with a cable, and it can link with the compatible link
/// targets. The winch itself is a <see cref="ILinkSource">link source</see>. An EVA kerbal can
/// "grab" the connector and carry it as far as the cable maximum length allows.
/// </para>
/// <para>
/// Since the winch is a basic link source it can link with the target in the different modes.
/// However, it's highly recommended to use the mode <see cref="LinkMode.TieAnyParts"/>. As it
/// the most flexible, and the winch is capable of changing "docked" vs "non-docked" mode when the
/// link is already made.
/// </para>
/// <para>
/// This winch implementation requires the associated joint module to support coupling. The winch
/// cable targets are also required to support coupling. The winch module behavior is undetermined
/// if the coupling is rejected when a plugged connector is being locked (going into the "docked"
/// state).
/// </para>
/// </remarks>
/// <seealso cref="ILinkSource"/>
/// <seealso cref="ILinkTarget"/>
/// <seealso cref="ILinkSource.linkJoint"/>
/// <seealso cref="ILinkJoint.SetCoupleOnLinkMode"/>
// Next localization ID: #kasLOC_08028.
public class KASModuleWinchNew : KASModuleLinkSourceBase,
    // KAS interfaces.
    IHasContextMenu, IWinchControl,
    // KSPDev syntax sugar interfaces.
    IPartModule, IsPhysicalObject {
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

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ConnectorStateMsg_Docked = new Message(
      "#kasLOC_08024",
      defaultTemplate: "Docked",
      description: "A string in the context menu that tells that the winch connector is rigidly"
      + " attached in the winch socked, and the vessel on the connector is docked to the winch"
      + " owner vessel.");
  #endregion

  /// <summary>Translates <see cref="WinchConnectorState"/> enum into a localized message.</summary>
  static readonly MessageLookup<WinchConnectorState> ConnectorStatesMsgLookup =
      new MessageLookup<WinchConnectorState>(new Dictionary<WinchConnectorState, Message>() {
          {WinchConnectorState.Locked, ConnectorStateMsg_Locked},
          {WinchConnectorState.Deployed, ConnectorStateMsg_Deployed},
          {WinchConnectorState.Plugged, ConnectorStateMsg_Plugged},
          {WinchConnectorState.Docked, ConnectorStateMsg_Docked},
      });

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message NoEnergyMsg = new Message(
      "#kasLOC_08002",
      defaultTemplate: "No energy!",
      description: "Error message to present when the electricity charge has exhausted.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message LockConnectorNotAlignedMsg = new Message(
      "#kasLOC_08003",
      defaultTemplate: "Cannot lock the connector: not aligned",
      description: "Error message to present when an improperly aligned cable connector has"
      + " attempted to lock with the winch.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ConnectorLockedMsg = new Message(
      "#kasLOC_08004",
      defaultTemplate: "Connector locked!",
      description: "Info message to present when a cable connector has successfully locked to the"
      + " winch.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ConnectorDockedMsg = new Message(
      "#kasLOC_08025",
      defaultTemplate: "Connector docked to the winch",
      description: "Info message to present when a cable connector has successfully docked to the"
      + " winch.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.DistanceType']/*"/>
  static readonly Message<DistanceType> MaxLengthReachedMsg = new Message<DistanceType>(
      "#kasLOC_08005",
      defaultTemplate: "Maximum cable length reached: <<1>>",
      description: "An info message to present when the cable is extended at its maximum length."
      + "\nArgument <<1>> is the current cable length of type DistanceType.",
      example: "Maximum cable length reached: 1.23 m");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message StopExtendingMenuTxt = new Message(
      "#kasLOC_08007",
      defaultTemplate: "Stop extending",
      description: "Name of the context menu item that stops the cable extending.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ExtendCableMenuTxt = new Message(
      "#kasLOC_08008",
      defaultTemplate: "Extend cable",
      description: "Name of the context menu item that starts the cable extending.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message StopRetractingMenuTxt = new Message(
      "#kasLOC_08009",
      defaultTemplate: "Stop retracting",
      description: "Name of the context menu item that stops the cable retracting.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message RetractCableMenuTxt = new Message(
      "#kasLOC_08010",
      defaultTemplate: "Retract cable",
      description: "Name of the context menu item that starts the cable retracting.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CableLinkBrokenMsg = new Message(
      "#kasLOC_08011",
      defaultTemplate: "The connector is detached due to the cable strength is exceeded",
      description: "A message to display when a too string force has broke the link between the"
      + "winch and it's target.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message TargetIsNotDockableMsg = new Message(
      "#kasLOC_08026",
      defaultTemplate: "Target part cannot dock with the winch",
      description: "The message to present when the winch connector is being attempted to attach to"
      + " a target part which doesn't support coupling with the winch.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message<PartType> CannotLinkToPreattached = new Message<PartType>(
      "#kasLOC_08027",
      defaultTemplate: "Cannot link with: <<1>>",
      description: "The error message to present when a part is being attached externally to the"
      + " source's attach node, and it's not a valid link target for the source."
      + "\nArgument <<1>> is the name of the part being attached.");
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

  /// <summary>Maximum distance at which an EVA kerbal can pickup a dropped connector.</summary>
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

  /// <summary>URL of the sound for the event of docking the connector to the winch.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string sndPathDockConnector = "";

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
  public bool persistedIsConnectorLocked = true;

  /// <summary>Position and rotation of the deployed connector.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [PersistentField("connectorPosAndRot", group = StdPersistentGroups.PartPersistant)]
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

  /// <summary>A context menu item that presents the maximum allowed cable length.</summary>
  /// <seealso cref="KASModuleCableJointBase.maxAllowedCableLength"/>
  /// <include file="SpecialDocTags.xml" path="Tags/UIConfigSetting/*"/>
  [KSPField(guiActive = true)]
  [LocalizableItem(
      tag = "#kasLOC_08013",
      defaultTemplate = "Deployed cable length",
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
    SetMotor(motorTargetSpeed > 0 ? 0 : float.PositiveInfinity);
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
    SetMotor(motorTargetSpeed < 0  ? 0 : float.NegativeInfinity);
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
    ReleaseCable();
    ShowStatusMessage(MaxLengthReachedMsg.Format(cableJoint.cfgMaxCableLength));
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
    StretchCable();
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
    if (FlightGlobals.ActiveVessel.isEVA && connectorState == WinchConnectorState.Locked) {
      var kerbalTarget = FlightGlobals.ActiveVessel.rootPart.Modules.OfType<ILinkTarget>()
          .FirstOrDefault(t => t.cfgLinkType == cfgLinkType);
      if (kerbalTarget != null
          && CheckCanLinkTo(kerbalTarget, reportToGUI: true)
          && StartLinking(GUILinkMode.API, LinkActorType.Player)) {
        LinkToTarget(kerbalTarget);
        SetCableLength(float.PositiveInfinity);
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
      var kerbalTarget = FlightGlobals.ActiveVessel.rootPart.Modules.OfType<ILinkTarget>()
          .FirstOrDefault(t => ReferenceEquals(t.linkSource, this));
      // Kerbal is a target for the winch, and we want the kerbal to keep the focus.
      BreakCurrentLink(LinkActorType.Player, moveFocusOnTarget: true);
      connectorState = WinchConnectorState.Locked;
      HostedDebugLog.Info(
          this, "{0} has returned the winch connector", FlightGlobals.ActiveVessel.vesselName);
    }
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
  public float currentCableLength { get { return cableJoint.maxAllowedCableLength; } }

  /// <inheritdoc/>
  public float cfgMaxCableLength { get { return cableJoint.cfgMaxCableLength; } }

  /// <inheritdoc/>
  public bool isConnectorLocked {
    get {
      return connectorState == WinchConnectorState.Locked
          || connectorState == WinchConnectorState.Docked;
    }
  }
  #endregion

  #region Inheritable fields & properties
  /// <summary>State of the winch connector.</summary>
  /// <remarks>The main purpose of this enum is to simplify the winch state management.</remarks>
  protected enum WinchConnectorState {
    /// <summary>
    /// The connector is rigidly attached to the winch's body. The connector's model is a parent of
    /// the winch's model.
    /// </summary>
    Locked,

    /// <summary>The connector is docked to the winch with its attached part.</summary>
    Docked,

    /// <summary>
    /// The connector is a standalone physical object, attached to the winch via a cable.
    /// </summary>
    Deployed,

    /// <summary>
    /// The connector is plugged into a link target. It doesn't have physics, and its model is part
    /// of the target's model.
    /// </summary>
    /// <remarks>
    /// This state can only exist if the winch's link source is linked to a target.
    /// </remarks>
    /// <seealso cref="ILinkTarget"/>
    Plugged,
  }

  /// <summary>Sate of the winch connector head.</summary>
  /// <remarks>
  /// It's discouraged to deal with the connector state via the state machine. The winch has some
  /// logic over it.
  /// </remarks>
  /// <value>The connector state.</value>
  protected WinchConnectorState connectorState {
    get {
      return connectorStateMachine.currentState
          ?? (isLinked ? WinchConnectorState.Docked : WinchConnectorState.Locked);
    }
    set {
      if (connectorStateMachine.currentState != value) {
        KillMotor();
      }
      connectorStateMachine.currentState = value;
      persistedIsConnectorLocked = isConnectorLocked;
    }
  }

  /// <summary>Winch connector model transformation object.</summary>
  /// <remarks>
  /// Depending on the current state this model can be a child to the part's model or a standalone
  /// object.
  /// </remarks>
  /// <value>The root transformation of the connector object.</value>
  /// <seealso cref="WinchConnectorState"/>
  protected Transform connectorModelObj { get; private set; }

  /// <summary>Physical joint module that control the cable.</summary>
  /// <remarks>
  /// Note, that the winch will <i>not</i> notice any changes done to the joint. Always call
  /// <see cref="UpdateContextMenu"/> on the winch after the update to the joint settings.
  /// </remarks>
  /// <value>The module instance.</value>
  /// <seealso cref="SetCableLength"/>
  protected ILinkCableJoint cableJoint { get { return linkJoint as ILinkCableJoint; } }

  /// <summary>State machine that defines and controls the winch connector state.</summary>
  /// <remarks>
  /// It's not safe to change the connector state on a part with no physics! If the state needs to
  /// be changed on the part load, consider overriding <see cref="OnPartUnpack"/>.
  /// </remarks>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ProcessingUtils.SimpleStateMachine_1']/*"/>
  protected SimpleStateMachine<WinchConnectorState> connectorStateMachine { get; private set; }
  #endregion

  #region Local fields & properties
  /// <summary>Anchor transform at the connector to attach the cable.</summary>
  Transform connectorCableAnchor;

  /// <summary>Anchor transform at the connector to attach with the part.</summary>
  Transform connectorPartAnchor;

  /// <summary>Anchor transform at the winch to attach the cable.</summary>
  Transform winchCableAnchor;

  /// <summary>Sound to play when the motor is active.</summary>
  /// <seealso cref="motorCurrentSpeed"/>
  AudioSource sndMotor;

  /// <summary>Sounds to play when the motor starts.</summary>
  /// <seealso cref="motorCurrentSpeed"/>
  AudioSource sndMotorStart;

  /// <summary>Sounds to play when the motor stops.</summary>
  /// <seealso cref="motorCurrentSpeed"/>
  AudioSource sndMotorStop;

  /// <summary>Sounds to play when the connector get locked to the winch.</summary>
  /// <seealso cref="connectorState"/>
  AudioSource sndConnectorLock;

  /// <summary>Sounds to play when the connector get docked to the winch.</summary>
  /// <seealso cref="connectorState"/>
  AudioSource sndConnectorDock;
  #endregion

  #region KASModuleLikSourceBase overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    LocalizeModule();

    sndMotor = SpatialSounds.Create3dSound(part.gameObject, sndPathMotor, loop: true);
    sndMotorStart = SpatialSounds.Create3dSound(part.gameObject, sndPathMotorStart);
    sndMotorStop = SpatialSounds.Create3dSound(part.gameObject, sndPathMotorStop);
    sndConnectorLock = SpatialSounds.Create3dSound(part.gameObject, sndPathLockConnector);
    sndConnectorDock = SpatialSounds.Create3dSound(part.gameObject, sndPathDockConnector);
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    // This module can only operate with the docking peers.
    if (!allowCoupling) {
      HostedDebugLog.Error(this, "The winch must be allowed for coupling!");
      allowCoupling = true;  // A bad approach, but better than not having the attach node.
    }
    base.OnLoad(node);

    if (connectorMass > part.mass) {
      HostedDebugLog.Error(this,
          "Connector mass is greater than the part's mass: {0} > {1}", connectorMass, part.mass);
      connectorMass = 0.1f * part.mass;  // A fail safe value. 
    }
    LoadOrCreateConnectorModel();
    if (!persistedIsConnectorLocked) {
      ConfigAccessor.ReadFieldsFromNode(node, typeof(KASModuleWinchNew), this,
                                        group: StdPersistentGroups.PartPersistant);
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
  public override void OnSave(ConfigNode node) {
    base.OnSave(node);
    // Persist the connector data only if its position is not fixed to the winch model.
    // It must be the peristsent state since the state machine can be in a different state at this
    // moment (e.g. during the vessel backup).
    if (!persistedIsConnectorLocked) {
      persistedConnectorPosAndRot = gameObject.transform.InverseTransformPosAndRot(
          new PosAndRot(connectorModelObj.position, connectorModelObj.rotation.eulerAngles));
      ConfigAccessor.WriteFieldsIntoNode(node, typeof(KASModuleWinchNew), this,
                                         group: StdPersistentGroups.PartPersistant);
    }
  }

  /// <inheritdoc/>
  public override void OnPartUnpack() {
    base.OnPartUnpack();
    // The physics has started. It's safe to adjust the connector.
    if (isLinked) {
      connectorStateMachine.currentState = linkJoint.coupleOnLinkMode
          ? WinchConnectorState.Docked
          : WinchConnectorState.Plugged;
    } else if (!persistedIsConnectorLocked) {
      connectorStateMachine.currentState = WinchConnectorState.Deployed;
    } else {
      connectorStateMachine.currentState = WinchConnectorState.Locked;
    }
  }

  /// <inheritdoc/>
  protected override void SetupStateMachine() {
    base.SetupStateMachine();
    linkStateMachine.onAfterTransition += (start, end) => UpdateContextMenu();

    // The default state is "Locked". All the enter state handlers rely on it, and all the exit
    // state handlers reset the state back to the default.
    connectorStateMachine = new SimpleStateMachine<WinchConnectorState>(strict: true);
    connectorStateMachine.onAfterTransition += (start, end) => {
      UpdateContextMenu();
      HostedDebugLog.Info(this, "Connector state changed: {0} => {1}", start, end);
    };
    connectorStateMachine.SetTransitionConstraint(
        WinchConnectorState.Docked,
        new[] {
            WinchConnectorState.Plugged,
            WinchConnectorState.Locked,  // External detach.
        });
    connectorStateMachine.SetTransitionConstraint(
        WinchConnectorState.Locked,
        new[] {
            WinchConnectorState.Deployed,
            WinchConnectorState.Plugged,
            WinchConnectorState.Docked,  // External attach.
        });
    connectorStateMachine.SetTransitionConstraint(
        WinchConnectorState.Deployed,
        new[] {
            WinchConnectorState.Locked,
            WinchConnectorState.Plugged,
        });
    connectorStateMachine.SetTransitionConstraint(
        WinchConnectorState.Plugged,
        new[] {
            WinchConnectorState.Deployed,
            WinchConnectorState.Docked,
        });
    connectorStateMachine.AddStateHandlers(
        WinchConnectorState.Locked,
        enterHandler: oldState => {
          connectorModelObj.parent = nodeTransform;  // Ensure it for consistency.
          AlignTransforms.SnapAlign(connectorModelObj, connectorCableAnchor, winchCableAnchor);
          SetCableLength(0);
          if (oldState.HasValue) {  // Skip when restoring state.
            sndConnectorLock.Play();
          }
        });
    connectorStateMachine.AddStateHandlers(
        WinchConnectorState.Docked,
        enterHandler: oldState => {
          connectorModelObj.parent = nodeTransform;  // Ensure it for consistency.
          AlignTransforms.SnapAlign(connectorModelObj, connectorCableAnchor, winchCableAnchor);
          SetCableLength(0);

          // Align the docking part to the nodes if it's a separate vessel.
          if (oldState != null && linkTarget.part.vessel != vessel) {
            AlignTransforms.SnapAlign(
                linkTarget.part.transform, linkTarget.nodeTransform, nodeTransform);
            linkJoint.SetCoupleOnLinkMode(true);
            if (oldState.HasValue) {  // Skip when restoring state.
              sndConnectorDock.Play();
            }
          }
        },
        leaveHandler: newState => linkJoint.SetCoupleOnLinkMode(false));
    connectorStateMachine.AddStateHandlers(
        WinchConnectorState.Deployed,
        enterHandler: oldState => {
          part.attachNodes.Remove(attachNode);
          TurnConnectorPhysics(true);
          linkRenderer.StartRenderer(winchCableAnchor, connectorCableAnchor);
        },
        leaveHandler: newState => {
          if (part.attachNodes.IndexOf(attachNode) == -1) {
            part.attachNodes.Add(attachNode);
          }
          TurnConnectorPhysics(false);
          linkRenderer.StopRenderer();
        });
    connectorStateMachine.AddStateHandlers(
        WinchConnectorState.Plugged,
        enterHandler: oldState => {
          part.attachNodes.Remove(attachNode);
          connectorModelObj.parent = linkTarget.nodeTransform;
          PartModel.UpdateHighlighters(part);
          PartModel.UpdateHighlighters(linkTarget.part);
          AlignTransforms.SnapAlign(
              connectorModelObj, connectorPartAnchor, linkTarget.nodeTransform);
          linkRenderer.StartRenderer(winchCableAnchor, connectorCableAnchor);
        },
        leaveHandler: newState => {
          if (part.attachNodes.IndexOf(attachNode) == -1) {
            part.attachNodes.Add(attachNode);
          }
          var oldParent = connectorModelObj.parent;
          connectorModelObj.parent = nodeTransform;  // Back to the model.
          PartModel.UpdateHighlighters(part);
          PartModel.UpdateHighlighters(oldParent);
          linkRenderer.StopRenderer();
        });
  }

  /// <inheritdoc/>
  protected override void LogicalLink(ILinkTarget target) {
    base.LogicalLink(target);
    if (target.part == parsedAttachNode.attachedPart && part == target.attachNode.attachedPart) {
      // The target part is externally attached.
      connectorState = WinchConnectorState.Docked;
    } else {
      connectorState = WinchConnectorState.Plugged;
      if (linkActor == LinkActorType.Player) {
        UISoundPlayer.instance.Play(target.part.vessel.isEVA
            ? sndPathGrabConnector
            : sndPathPlugConnector);
      }
    }
    linkRenderer.StartRenderer(winchCableAnchor, connectorCableAnchor);
  }

  /// <inheritdoc/>
  protected override void LogicalUnlink(LinkActorType actorType) {
    base.LogicalUnlink(actorType);
    connectorState = isConnectorLocked ? WinchConnectorState.Locked : WinchConnectorState.Deployed;
    if (actorType == LinkActorType.Physics) {
      UISoundPlayer.instance.Play(sndPathBroke);
      ShowStatusMessage(CableLinkBrokenMsg, isError: true);
    } else if (actorType == LinkActorType.Player) {
      if (connectorState == WinchConnectorState.Deployed) {
        UISoundPlayer.instance.Play(sndPathUnplugConnector);
      }
    }
  }

  /// <inheritdoc/>
  protected override void PhysicaLink() {
    base.PhysicaLink();
    SetCableLength(cableJoint.realCableLength);
  }

  /// <inheritdoc/>
  protected override void PhysicaUnlink() {
    SetCableLength(cableJoint.realCableLength);
    base.PhysicaUnlink();
  }

  /// <inheritdoc/>
  protected override void RestoreOtherPeer() {
    base.RestoreOtherPeer();
    if (linkTarget != null) {
      // Only do it for the visual improvements, since the base class will attach the renderer to
      // the node, instead of the cable attach possition. The state machine will get it fixed, but
      // it'll only happen when the physics is started.
      linkRenderer.StartRenderer(winchCableAnchor, connectorCableAnchor);
    }
  }

  /// <inheritdoc/>
  protected override string[] CheckBasicLinkConditions(ILinkTarget target, bool checkStates) {
    // It's OK to link with the kerbal target even though it's not dockable. This case is explicitly
    // handled when doing the connector locking.
    var linkStatusErrors = new List<string>();
    if (!target.part.vessel.isEVA && target.attachNode == null) {
      linkStatusErrors.Add(TargetIsNotDockableMsg);
    }
    return linkStatusErrors
        .Concat(base.CheckBasicLinkConditions(target, checkStates))
        .ToArray();
  }

  /// <inheritdoc/>
  protected override void CheckAttachNode() {
    base.CheckAttachNode();
    if (linkState == LinkState.NodeIsBlocked && attachNode.attachedPart != null) {
      HostedDebugLog.Warning(this, "Decouple incompatible part from winch: {0}",
                             attachNode.FindOpposingNode().attachedPart);
      UISoundPlayer.instance.Play(CommonConfig.sndPathBipWrong);
      ShowStatusMessage(
          CannotLinkToPreattached.Format(attachNode.attachedPart), isError: true);
      KASAPI.LinkUtils.DecoupleParts(part, attachNode.attachedPart);
    }
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
  public virtual void UpdateContextMenu() {
    connectorStateMenuInfo = ConnectorStatesMsgLookup.Lookup(connectorState);
    deployedCableLengthMenuInfo = DistanceType.Format(
        cableJoint != null ? cableJoint.maxAllowedCableLength : 0);
    
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
    PartModuleUtils.SetupEvent(this, GrabConnectorEvent, e => {
      e.active = connectorState == WinchConnectorState.Locked;
    });
    PartModuleUtils.SetupEvent(this, ReturnConnectorEvent, e => {
      e.active = IsActiveEvaHoldingConnector();
    });
  }
  #endregion

  #region IWinControl implementation
  /// <inheritdoc/>
  public void SetMotor(float targetSpeed) {
    if (targetSpeed > 0 && cableJoint.maxAllowedCableLength >= cableJoint.cfgMaxCableLength) {
      ShowStatusMessage(MaxLengthReachedMsg.Format(cableJoint.cfgMaxCableLength));
      return;
    }
    if (targetSpeed < 0 && isConnectorLocked) {
      ShowStatusMessage(ConnectorLockedMsg);
      return;
    }
    if (targetSpeed > 0 && isConnectorLocked) {
      connectorState = isLinked
          ? WinchConnectorState.Plugged
          : WinchConnectorState.Deployed;
    }
    if (!isConnectorLocked) {
      if (Mathf.Abs(targetSpeed) < float.Epsilon) {
        KillMotor();
      } else {
        var newTargetSpeed = targetSpeed > float.Epsilon
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
    SetCableLength();
  }

  /// <inheritdoc/>
  public void ReleaseCable() {
    if (isConnectorLocked) {
      connectorState = isLinked
          ? WinchConnectorState.Plugged
          : WinchConnectorState.Deployed;
    }
    SetCableLength(float.PositiveInfinity);
  }
  #endregion

  #region Inheritable utility methods
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

  /// <summary>Sets the deployed cable length.</summary>
  /// <remarks>
  /// <para>
  /// If the new value is significantly less than the old one, then the physical effects may
  /// trigger.
  /// </para>
  /// <para>
  /// This method can be called at any winch or motor state. However, if the connector is locked,
  /// the call will not have effect. Once the connector get deployed from the locked state, it will
  /// set the cable length to <c>0</c>.
  /// </para>
  /// </remarks>
  /// <param name="length">
  /// The new length. Set it to <c>PositiveInfinity</c> to extend the cable at the maximum length.
  /// Omit the parameter to macth the length to the current distance to the connector (stretch the
  /// cable).
  /// </param>
  /// <seealso cref="connectorState"/>
  protected virtual void SetCableLength(float? length = null) {
    cableJoint.SetCableLength(length ?? float.NegativeInfinity);
    UpdateContextMenu();
  }

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
          cableJoint.maxAllowedCableLength + motorCurrentSpeed * TimeWarp.fixedDeltaTime);
      if (motorCurrentSpeed > 0
          && cableJoint.maxAllowedCableLength >= cableJoint.cfgMaxCableLength) {
        KillMotor();
        SetCableLength(float.PositiveInfinity);
        ShowStatusMessage(MaxLengthReachedMsg.Format(cableJoint.cfgMaxCableLength));
      } else if (motorCurrentSpeed < 0 && cableJoint.maxAllowedCableLength <= 0) {
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
      connectorState = WinchConnectorState.Docked;
      ShowStatusMessage(ConnectorDockedMsg);
    } else {
      connectorState = WinchConnectorState.Locked;
      ShowStatusMessage(ConnectorLockedMsg);
    }
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
    const string WinchCableAnchorName = "winchCableAnchor";
    winchCableAnchor = Hierarchy.FindPartModelByPath(part, WinchCableAnchorName);
    if (winchCableAnchor == null) {
      winchCableAnchor = new GameObject(WinchCableAnchorName).transform;
      // This anchor must match the one set in the Joint module!
      var physicalAnchorOffset =
          (connectorPartAnchor.position - connectorCableAnchor.position).magnitude;
      Hierarchy.MoveToParent(winchCableAnchor, nodeTransform,
                             newPosition: new Vector3(0, 0, -physicalAnchorOffset));
      HostedDebugLog.Info(this, "Winch cable anchor offset: {0}", physicalAnchorOffset);
    }
  }
  #endregion
}

}  // namespace
