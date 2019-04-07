// Kerbal Attachment System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv2;
using KSPDev.ConfigUtils;
using KSPDev.Extensions;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using KSPDev.PartUtils;
using KSPDev.ProcessingUtils;
using KSPDev.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KAS {

/// <summary>Module that allows connecting the parts via an EVA kerbal.</summary>
/// <remarks>
/// <para>
/// In this concept there is a "connector", a physical object that is connected to the source part
/// via a cable. An EVA kerbal can pick it up and carry to the target part. Then, the connector
/// can be plugged into the compatible socked, and this will make the link. The EVA kerbal can also
/// unplug the plugged connectors, breaking the link. The connector unplug function is also availabe
/// to the unmanned vessels, but they must be fully controllable.
/// </para>
/// <para>
/// For the proper work, the renderer must provide mesh
/// <see cref="KASRendererPipe.TargetNodeMesh"/>.
/// </para>
/// <para>
/// The descendants of this module can use the custom persistent fields of groups:
/// </para>
/// <list type="bullet">
/// <item><c>StdPersistentGroups.PartConfigLoadGroup</c></item>
/// <item><c>StdPersistentGroups.PartPersistant</c></item>
/// </list>
/// </remarks>
/// <seealso cref="ILinkSource"/>
/// <seealso cref="ILinkTarget"/>
/// <seealso cref="ILinkCableJoint"/>
/// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ConfigUtils.StdPersistentGroups']/*"/>
// Next localization ID: #kasLOC_13011.
public class KASLinkSourcePhysical : KASLinkSourceBase {

  #region Localizable GUI strings.
  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ConnectorStateMsg_Locked = new Message(
      "#kasLOC_13000",
      defaultTemplate: "Locked",
      description: "A string in the context menu that tells that the connector is rigidly attached"
      + " to the part and is not movable.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ConnectorStateMsg_Deployed = new Message(
      "#kasLOC_13001",
      defaultTemplate: "Deployed",
      description: "A string in the context menu that tells that the connector is deployed and"
      + " attached to the part via a cable.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ConnectorStateMsg_Plugged = new Message(
      "#kasLOC_13002",
      defaultTemplate: "Plugged in",
      description: "A string in the context menu that tells that the connector is plugged in"
      + " a socked or is being carried by a kerbal, and attached to the part via a cable.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ConnectorStateMsg_Docked = new Message(
      "#kasLOC_13003",
      defaultTemplate: "Docked",
      description: "A string in the context menu that tells that the connector is rigidly"
      + " attached in the winch socked, and the vessel on the connector is docked to the part"
      + " owner vessel.");

  /// <summary>Translates <see cref="ConnectorState"/> enum into a localized message.</summary>
  static readonly MessageLookup<ConnectorState> ConnectorStatesMsgLookup =
      new MessageLookup<ConnectorState>(new Dictionary<ConnectorState, Message>() {
          {ConnectorState.Locked, ConnectorStateMsg_Locked},
          {ConnectorState.Deployed, ConnectorStateMsg_Deployed},
          {ConnectorState.Plugged, ConnectorStateMsg_Plugged},
          {ConnectorState.Docked, ConnectorStateMsg_Docked},
      });

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CableLinkBrokenMsg = new Message(
      "#kasLOC_13004",
      defaultTemplate: "The link between the connector and the part has broke",
      description: "A message to display when a link between the part and the connector has broke"
      + " due to the unexpected external forces or actions.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<PartType> CannotLinkToPreattached = new Message<PartType>(
      "#kasLOC_13005",
      defaultTemplate: "Cannot link with: <<1>>",
      description: "The error message to present when a part is being attached externally to the"
      + " source's attach node, and it's not a valid link target for the source."
      + "\nArgument <<1>> is the name of the part being attached.");
  #endregion

  #region Part's config fields
  /// <summary>Mass of the connector of the winch.</summary>
  /// <remarks>
  /// It's substracted from the part's mass on deploy, and added back on the lock. For this reason
  /// it must not be greater then the total part's mass. Also, try to avoid making the connector
  /// heavier than the part iteself - the Unity physics may start behaving awkward. 
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Connector mass")]
  public float connectorMass = 0.01f;

  /// <summary>Maximum distance at which an EVA kerbal can pickup a dropped connector.</summary>
  /// <seealso cref="KASLinkTargetKerbal"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Connector interact distance")]
  public float connectorInteractDistance = 0.3f;

  /// <summary>URL of the sound for the event of returning the connector to the winch.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Sound - lock connector")]
  public string sndPathLockConnector = "";

  /// <summary>URL of the sound for the event of docking the connector to the winch.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Sound - dock connector")]
  public string sndPathDockConnector = "";

  /// <summary>URL of the sound for the event of acquiring the connector by an EVA kerbal.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Sound - grab connector")]
  public string sndPathGrabConnector = "";

  /// <summary>URL of the sound for the event of plugging the connector into a socket.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Sound - plug connector")]
  public string sndPathPlugConnector = "";
  
  /// <summary>URL of the sound for the event of unplugging the connector from a socket.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Sound - unplug connector")]
  public string sndPathUnplugConnector = "";

  /// <summary>URL of the sound for the event of cable emergency detachment (link broken).</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Sound - link broke")]
  public string sndPathBroke = "";
  #endregion

  #region Persistent fields
  /// <summary>Connector state in the last save action.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  public bool persistedIsConnectorLocked = true;

  /// <summary>Position and rotation of the deployed connector.</summary>
  /// <remarks>It's relative to the source part.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [PersistentField("connectorPosAndRot", group = StdPersistentGroups.PartPersistant)]
  public PosAndRot persistedConnectorPosAndRot;
  #endregion

  #region The context menu fields
  /// <summary>Status field to display the current connector status in the context menu.</summary>
  /// <see cref="connectorState"/>
  /// <include file="SpecialDocTags.xml" path="Tags/UIConfigSetting/*"/>
  [KSPField(guiActive = true)]
  [LocalizableItem(
      tag = "#kasLOC_13006",
      defaultTemplate = "Connector state",
      description = "Status field to display the current winch connector status in the context"
      + " menu.")]
  public string connectorStateMenuInfo = "";
  #endregion

  #region Context menu events/actions
  /// <summary>Context menu to instantly lock the deployed connector.</summary>
  /// <remarks>It's a hack, but sometimes it's the only way to recover the connector.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true, guiActiveUnfocused = true, advancedTweakable = true)]
  [LocalizableItem(
      tag = "#kasLOC_13010",
      defaultTemplate = "Lock connector",
      description = "Advanced tweakable. Context menu item to instantly lock the deployed connector"
                    + " into the base.")]
  public virtual void InstantLockConnectorEvent() {
    if (connectorState == ConnectorState.Deployed) {
      SetConnectorState(ConnectorState.Locked);
    }
  }

  // Keep the events that may change their visibility states at the bottom. When an item goes out
  // of the menu, its height is reduced, but the lower left corner of the dialog is retained. 
  /// <summary>Attaches the connector to the EVA kerbal.</summary>
  /// <remarks>The active vessel must be a kerbal.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActiveUnfocused = true)]
  [LocalizableItem(
      tag = "#kasLOC_13007",
      defaultTemplate = "Grab connector",
      description = "A context menu event that attaches the connector to the EVA kerbal.")]
  public virtual void GrabConnectorEvent() {
    if (FlightGlobals.ActiveVessel.isEVA && connectorState == ConnectorState.Locked) {
      var kerbalTarget = FlightGlobals.ActiveVessel.rootPart.Modules.OfType<ILinkTarget>()
          .FirstOrDefault(t => t.cfgLinkType == cfgLinkType && t.linkState == LinkState.Available);
      if (kerbalTarget != null && LinkToTarget(LinkActorType.Player, kerbalTarget)) {
        SetCableLength(float.PositiveInfinity);
      } else {
        UISoundPlayer.instance.Play(KASAPI.CommonConfig.sndPathBipWrong);
      }
    }
  }

  /// <summary>Detaches the connector from the kerbal and puts it back to the winch.</summary>
  /// <remarks>The active vessel must be a kerbal holding a connector of this winch.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActiveUnfocused = true)]
  [LocalizableItem(
      tag = "#kasLOC_13008",
      defaultTemplate = "Return connector",
      description = "A context menu event that detaches the connector from the kerbal and puts it"
      + " back to the winch.")]
  public virtual void ReturnConnectorEvent() {
    if (FlightGlobals.ActiveVessel.isEVA
        && linkTarget != null && linkTarget.part.vessel == FlightGlobals.ActiveVessel) {
      var kerbalTarget = FlightGlobals.ActiveVessel.rootPart.Modules.OfType<ILinkTarget>()
          .FirstOrDefault(t => ReferenceEquals(t.linkSource, this));
      BreakCurrentLink(LinkActorType.Player);
      SetConnectorState(ConnectorState.Locked);
      HostedDebugLog.Info(
          this, "{0} has returned the winch connector", FlightGlobals.ActiveVessel.vesselName);
    }
  }

  /// <summary>Context menu item to break the currently established link.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true, guiActiveUnfocused = true)]
  [LocalizableItem(
      tag = "#kasLOC_13009",
      defaultTemplate = "Detach connector",
      description = "Context menu item to break the currently established link.")]
  public virtual void DetachConnectorEvent() {
    if (isLinked) {
      BreakCurrentLink(LinkActorType.Player);
    }
  }
  #endregion

  #region Inheritable types & properties
  /// <summary>State of the connector.</summary>
  protected enum ConnectorState {
    /// <summary>
    /// The connector is non-physical and is merged to the owner part's model. Nothing is attached
    /// to it.
    /// </summary>
    Locked,

    /// <summary>
    /// The connector is non-physical and is merged to the owner part's model. There is a part
    /// attached to it, which is meregd (docked) to the parent vessel.
    /// </summary>
    /// <remarks>This state can only exist if the link source is linked to a target.</remarks>
    Docked,

    /// <summary>
    /// The connector is a standalone physical object, attached to the owner part via a distant
    /// joint.
    /// </summary>
    Deployed,

    /// <summary>The connector is non-physical and is merged to the target part's model.</summary>
    /// <remarks>This state can only exist if the link source is linked to a target.</remarks>
    /// <seealso cref="ILinkTarget"/>
    Plugged,
  }

  /// <summary>Maximum possible distance between the owner part and the connector.</summary>
  /// <remarks>
  /// This is a desired distance. The engine will try to keep it equal or less to this value, but
  /// depending on the forces that affect the objects, this distance may be never reached. Various
  /// implementations can adjust this value, but not greater than <see cref="cfgMaxCableLength"/>.
  /// </remarks>
  /// <value>
  /// The length in meters. Always positive, if the PhysX joint is created. Zero, otherwise.
  /// </value>
  /// <seealso cref="SetCableLength"/>
  /// <seealso cref="cableJoint"/>
  protected float currentCableLength { get { return cableJoint.deployedCableLength; } }

  /// <summary>
  /// Maximum allowed distance between the owner part and the connector to establish a link.
  /// </summary>
  /// <value>Distance in meters. It's constant and doesn't depend on the joint state.</value>
  /// <seealso cref="currentCableLength"/>
  protected float cfgMaxCableLength { get { return cableJoint.cfgMaxCableLength; } }

  /// <summary>
  /// Tells if the connector is physicsless, and its model is a child of the owning part.
  /// </summary>
  /// <value>The status of the connector model.</value>
  protected bool isConnectorLocked {
    get {
      return connectorState == ConnectorState.Locked
          || connectorState == ConnectorState.Docked;
    }
  }

  /// <summary>State of the connector head.</summary>
  /// <value>The connector state.</value>
  /// <seealso cref="SetConnectorState"/>
  protected ConnectorState connectorState {
    get {
      if (connectorStateMachine.currentState.HasValue) {
        return connectorStateMachine.currentState.Value;
      }
      // Handle the case when the machine is not yet started.
      if (isLinked) {
        return persistedIsConnectorLocked ? ConnectorState.Docked : ConnectorState.Plugged;
      }
      return persistedIsConnectorLocked ? ConnectorState.Locked : ConnectorState.Deployed;
    }
  }

  /// <summary>Physical joint module that control the cable.</summary>
  /// <remarks>
  /// Note, that the module will <i>not</i> notice any changes done to the joint. Always call
  /// <see cref="UpdateContextMenu"/> on this module after the update to the joint settings.
  /// </remarks>
  /// <value>The module instance. It's never <c>null</c>.</value>
  /// <seealso cref="SetCableLength"/>
  protected ILinkCableJoint cableJoint {
    get {
      var res = linkJoint as ILinkCableJoint;
      if (linkJoint != null && res == null) {
        throw new InvalidOperationException(
            "Joint is not cable: " + DebugEx.ObjectToString(linkJoint));
      }
      return res;
    }
  }

  /// <summary>State machine that defines and controls the connector state.</summary>
  /// <remarks>
  /// It's not safe to change the connector state on a part with no physics! If the state needs to
  /// be changed on the part load, consider overriding <see cref="OnPartUnpack"/>.
  /// </remarks>
  /// <value>The state machine instance. It's never <c>null</c>.</value>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ProcessingUtils.SimpleStateMachine_1']/*"/>
  protected SimpleStateMachine<ConnectorState> connectorStateMachine { get; private set; }

  /// <summary>The physical object of the connector.</summary>
  /// <remarks>
  /// Only exists in state state <see cref="ConnectorState.Deployed"/>. In this mode the renderer
  /// pipe target model is attached to this object and aligned at the pipe/part anchors. This is
  /// <i>not</i> the actual model of the connector!
  /// </remarks>
  /// <value>The object or <c>null</c> if the connector is not physical.</value>
  /// <seealso cref="connectorState"/>
  protected Transform connectorObj { get; private set; }
  #endregion

  #region Local fields & properties
  /// <summary>Connector grab event to inject into the linked target.</summary>
  /// <see cref="UpdateContextMenu"/>
  BaseEvent GrabConnectorEventInject;
  #endregion

  #region IHasDebugAdjustables implementation
  PosAndRot dbgOldConnectorPosAndRot;
  ConnectorState dbgOldConnectorState;
  float dbgOldCableLength;

  /// <inheritdoc/>
  public override void OnBeforeDebugAdjustablesUpdate() {
    dbgOldConnectorState = connectorState;
    if (connectorState == ConnectorState.Deployed) {
      dbgOldCableLength = currentCableLength;
      SaveConnectorModelPosAndRot();
      dbgOldConnectorPosAndRot = persistedConnectorPosAndRot;
      SetConnectorState(ConnectorState.Locked);
    }
    base.OnBeforeDebugAdjustablesUpdate();
  }

  /// <inheritdoc/>
  public override void OnDebugAdjustablesUpdated() {
    base.OnDebugAdjustablesUpdated();
    AsyncCall.CallOnEndOfFrame(
        this,
        () => {
          if (dbgOldConnectorState == ConnectorState.Deployed) {
            HostedDebugLog.Warning(
                this, "Restoring connector: state={0}, at={1}, length={2}",
                dbgOldConnectorState, dbgOldConnectorPosAndRot, dbgOldCableLength);
            persistedConnectorPosAndRot = dbgOldConnectorPosAndRot;
            SetConnectorState(dbgOldConnectorState);
            SetCableLength(dbgOldCableLength);
          }
        },
        skipFrames: 1);  // To match the base class delay.
  }
  #endregion

  #region KASLinkSourceBase overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();

    // The GUI name of this event is copied from GrabConnectorEvent in UpdateContextMenu.
    GrabConnectorEventInject = new BaseEvent(
        Events,
        "autoEventAttach" + part.Modules.IndexOf(this),
        ClaimLinkedConnector,
        new KSPEvent());
    GrabConnectorEventInject.guiActive = true;
    GrabConnectorEventInject.guiActiveUncommand = true;
    GrabConnectorEventInject.guiActiveUnfocused = true;

    RegisterGameEventListener(GameEvents.onVesselChange, OnVesselChange);
  }

  /// <inheritdoc/>
  protected override void CheckSettingsConsistency() {
    if (!allowCoupling) {
      // Connector docking mode is required!
      // TODO(ihsoft): Allow non-docking mode.
      allowCoupling = true;
      HostedDebugLog.Warning(
          this, "Inconsistent setting fixed: allowCoupling => {0}, due to it's required by physical"
          + " source", allowCoupling);
    }
    base.CheckSettingsConsistency();
    if (connectorMass > part.mass) {
      connectorMass = 0.1f * part.mass;  // A fail safe value. 
      HostedDebugLog.Warning(
          this, "Inconsistent setting fixed: connectorMass => {0}, due to partMass={1}",
          connectorMass, part.mass);
    }
    if (linkJoint != null && cableJoint == null) {
      HostedDebugLog.Error(
          this, "Cannot fix inconsistent setting: jointName={0} is not cable joint", jointName);
    }
  }

  /// <inheritdoc/>
  public override void OnSave(ConfigNode node) {
    if (connectorObj != null) {
      SaveConnectorModelPosAndRot();  // Update to the actual position.
    } 
    base.OnSave(node);
  }

  /// <inheritdoc/>
  public override void OnPartUnpack() {
    base.OnPartUnpack();
    // The physics has started. It's safe to restore the connector (it can be physical).
    SetConnectorState(connectorState);
  }

  /// <inheritdoc/>
  protected override void RestoreOtherPeer() {
    base.RestoreOtherPeer();
    if (otherPeer == null) {
      persistedIsConnectorLocked = true;
      if (linkJoint != null) {
        linkJoint.DropJoint();  // Cleanup the joints state.
      }
    }
  }

  /// <inheritdoc/>
  public override void OnPartDie() {
    base.OnPartDie();
    // Make sure the connector is locked into the winch to not leave it behind.
    if (connectorObj != null) {
      // Don't relay on the connector state machine, it will try to destory immediately.
      KASInternalPhysicalConnector.Demote(connectorObj.gameObject, true);
    }
    SetConnectorState(ConnectorState.Locked);
  }

  /// <inheritdoc/>
  protected override void SetupStateMachine() {
    base.SetupStateMachine();
    linkStateMachine.onAfterTransition += (start, end) => UpdateContextMenu();
    linkStateMachine.AddStateHandlers(
        LinkState.Linked,
        enterHandler: oldState => {
          var module = linkTarget as PartModule;
          PartModuleUtils.InjectEvent(this, DetachConnectorEvent, module);
          PartModuleUtils.AddEvent(module, GrabConnectorEventInject);
        },
        leaveHandler: newState => {
          var module = linkTarget as PartModule;
          PartModuleUtils.WithdrawEvent(this, DetachConnectorEvent, module);
          PartModuleUtils.DropEvent(module, GrabConnectorEventInject);
        });

    // The default state is "Locked". All the enter state handlers rely on it, and all the exit
    // state handlers reset the state back to the default.
    connectorStateMachine = new SimpleStateMachine<ConnectorState>(strict: true);
    connectorStateMachine.onAfterTransition += (start, end) => {
      if (end != null) { // Do nothing on state machine shutdown.
        persistedIsConnectorLocked = isConnectorLocked;
        if (end == ConnectorState.Locked) {
          KASAPI.AttachNodesUtils.AddNode(part, coupleNode);
        } else if (coupleNode.attachedPart == null) {
          KASAPI.AttachNodesUtils.DropNode(part, coupleNode);
        }
        UpdateContextMenu();
      }
      HostedDebugLog.Info(this, "Connector state changed: {0} => {1}", start, end);
    };
    connectorStateMachine.SetTransitionConstraint(
        ConnectorState.Docked,
        new[] {
            ConnectorState.Plugged,
            ConnectorState.Locked,  // External detach.
        });
    connectorStateMachine.SetTransitionConstraint(
        ConnectorState.Locked,
        new[] {
            ConnectorState.Deployed,
            ConnectorState.Plugged,
            ConnectorState.Docked,  // External attach.
        });
    connectorStateMachine.SetTransitionConstraint(
        ConnectorState.Deployed,
        new[] {
            ConnectorState.Locked,
            ConnectorState.Plugged,
        });
    connectorStateMachine.SetTransitionConstraint(
        ConnectorState.Plugged,
        new[] {
            ConnectorState.Deployed,
            ConnectorState.Docked,
        });
    connectorStateMachine.AddStateHandlers(
        ConnectorState.Locked,
        enterHandler: oldState => {
          SaveConnectorModelPosAndRot();
          if (oldState.HasValue) {  // Skip when restoring state.
            UISoundPlayer.instance.Play(sndPathLockConnector);
          }
        },
        leaveHandler: newState =>
            SaveConnectorModelPosAndRot(saveNonPhysical: newState == ConnectorState.Deployed),
        callOnShutdown: false);
    connectorStateMachine.AddStateHandlers(
        ConnectorState.Docked,
        enterHandler: oldState => {
          SaveConnectorModelPosAndRot();

          // Align the docking part to the nodes if it's a separate vessel.
          if (oldState.HasValue && linkTarget.part.vessel != vessel) {
            AlignTransforms.SnapAlignNodes(linkTarget.coupleNode, coupleNode);
            linkJoint.SetCoupleOnLinkMode(true);
            UISoundPlayer.instance.Play(sndPathDockConnector);
          }
        },
        leaveHandler: newState => {
          SaveConnectorModelPosAndRot(saveNonPhysical: newState == ConnectorState.Deployed);
          linkJoint.SetCoupleOnLinkMode(false);
        },
        callOnShutdown: false);
    connectorStateMachine.AddStateHandlers(
        ConnectorState.Plugged,
        enterHandler: oldState => SaveConnectorModelPosAndRot(),
        leaveHandler: newState =>
            SaveConnectorModelPosAndRot(saveNonPhysical: newState == ConnectorState.Deployed),
        callOnShutdown: false);
    connectorStateMachine.AddStateHandlers(
        ConnectorState.Deployed,
        enterHandler: oldState => StartPhysicsOnConnector(),
        leaveHandler: newState => StopPhysicsOnConnector(),
        callOnShutdown: false);
  }

  /// <inheritdoc/>
  protected override void ShutdownStateMachine() {
    base.ShutdownStateMachine();
    connectorStateMachine.currentState = null;
  }

  /// <inheritdoc/>
  protected override void LogicalLink(ILinkTarget target) {
    StopPhysicsOnConnector();
    base.LogicalLink(target);
    if (target.part == parsedAttachNode.attachedPart && part == target.coupleNode.attachedPart) {
      // The target part is externally attached.
      linkJoint.SetCoupleOnLinkMode(true);
      SetConnectorState(ConnectorState.Docked);
    } else {
      SetConnectorState(ConnectorState.Plugged);
      if (linkActor == LinkActorType.Player) {
        UISoundPlayer.instance.Play(target.part.vessel.isEVA
            ? sndPathGrabConnector
            : sndPathPlugConnector);
      }
    }
  }

  /// <inheritdoc/>
  protected override void LogicalUnlink(LinkActorType actorType) {
    SaveConnectorModelPosAndRot(saveNonPhysical: true);
    if (actorType == LinkActorType.Physics) {
      UISoundPlayer.instance.Play(sndPathBroke);
      ShowStatusMessage(CableLinkBrokenMsg, isError: true);
    }
    base.LogicalUnlink(actorType);
    SetConnectorState(isConnectorLocked ? ConnectorState.Locked : ConnectorState.Deployed);
    if (actorType == LinkActorType.Player && connectorState == ConnectorState.Deployed) {
      UISoundPlayer.instance.Play(sndPathUnplugConnector);
    }
  }

  /// <inheritdoc/>
  protected override void PhysicalUnlink() {
    SetCableLength(cableJoint.realCableLength);
    base.PhysicalUnlink();
  }

  /// <inheritdoc/>
  protected override void CheckCoupleNode() {
    base.CheckCoupleNode();
    if (linkState == LinkState.NodeIsBlocked
        && coupleNode != null && coupleNode.attachedPart != null) {
      HostedDebugLog.Warning(this, "Decouple incompatible part from the node: {0}",
                             coupleNode.FindOpposingNode().attachedPart);
      UISoundPlayer.instance.Play(KASAPI.CommonConfig.sndPathBipWrong);
      ShowStatusMessage(
          CannotLinkToPreattached.Format(coupleNode.attachedPart), isError: true);
      KASAPI.LinkUtils.DecoupleParts(part, coupleNode.attachedPart);
    }
  }

  /// <inheritdoc/>
  public override void UpdateContextMenu() {
    base.UpdateContextMenu();

    connectorStateMenuInfo = ConnectorStatesMsgLookup.Lookup(connectorState);
    PartModuleUtils.SetupEvent(this, ToggleVesselsDockModeEvent, e => {
      e.active &= !isConnectorLocked;
    });
    PartModuleUtils.SetupEvent(this, GrabConnectorEvent, e => {
      e.active = connectorState == ConnectorState.Locked;
      if (GrabConnectorEventInject != null) {
        GrabConnectorEventInject.guiName = e.guiName;
      }
    });
    PartModuleUtils.SetupEvent(this, ReturnConnectorEvent, e => {
      e.active = IsActiveEvaHoldingConnector();
    });
    PartModuleUtils.SetupEvent(this, DetachConnectorEvent, e => {
      e.active = isLinked;
    });
    PartModuleUtils.SetupEvent(this, InstantLockConnectorEvent, e => {
      e.active = connectorState == ConnectorState.Deployed;
    });
    if (GrabConnectorEventInject != null) {
      GrabConnectorEventInject.active = linkTarget != null
          && FlightGlobals.ActiveVessel != linkTarget.part.vessel;
    }
  }
  #endregion

  #region Inheritable utility methods
  /// <summary>Changes the connector state</summary>
  /// <remarks>
  /// It's a convinience method. The caller can change the state of the connector state machine
  /// instead.
  /// </remarks>
  /// <param name="newState">The new state.</param>
  /// <seealso cref="connectorStateMachine"/>
  /// <seealso cref="connectorState"/>
  protected void SetConnectorState(ConnectorState newState) {
    connectorStateMachine.currentState = newState;
  }

  /// <summary>
  /// Tells if the currently active vessel is an EVA kerbal who carries the connector.
  /// </summary>
  /// <returns><c>true</c> if the connector on the kerbal.</returns>
  protected bool IsActiveEvaHoldingConnector() {
    return FlightGlobals.fetch != null  // To prevent NRE on the game shutdown. 
        && FlightGlobals.ActiveVessel != null  // It's null in the non-flight scenes.
        && FlightGlobals.ActiveVessel.isEVA
        && isLinked
        && linkTarget != null && linkTarget.part != null
        && linkTarget.part.vessel == FlightGlobals.ActiveVessel;
  }

  /// <summary>Returns connector's model.</summary>
  /// <returns>The model object. It's never <c>null</c>.</returns>
  /// <exception cref="ArgumentException">If model cannot be retrieved.</exception>
  protected Transform GetConnectorModel() {
    return linkRenderer.GetMeshByName(KASRendererPipe.TargetNodeMesh);
  }

  /// <summary>Returns connector's anchor, at wich it attaches to the pipe.</summary>
  /// <returns>The anchor object. It's never <c>null</c>.</returns>
  /// <exception cref="ArgumentException">If model cannot be retrieved.</exception>
  protected Transform GetConnectorModelPipeAnchor() {
    return FindModelOrThrow(GetConnectorModel(), KASRendererPipe.PipeJointTransformName);
  }

  /// <summary>Returns connector's anchor, at which it attaches to the target part.</summary>
  /// <returns>The anchor object. It's never <c>null</c>.</returns>
  /// <exception cref="ArgumentException">If model cannot be retrieved.</exception>
  protected Transform GetConnectorModelPartAnchor() {
    return FindModelOrThrow(GetConnectorModel(), KASRendererPipe.PartJointTransformName);
  }

  /// <summary>Finds model by path or logs&amp;throws.</summary>
  /// <remarks>Just a convinience method to avoid unclear NREs.</remarks>
  /// <returns>The model. It's never <c>null</c>.</returns>
  /// <exception cref="ArgumentException">If model cannot be retrieved.</exception>
  protected Transform FindModelOrThrow(Transform root, string path) {
    var res = Hierarchy.FindTransformByPath(root, path);
    if (res == null) {
      HostedDebugLog.Error(this, "Cannot find model: path={0}, parent={1}", path, root);
      throw new ArgumentException("Model not found: " + path);
    }
    return res;
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
  /// This argument is basically the same as the length, passed to
  /// <see cref="ILinkCableJoint.SetCableLength"/>. However, if the value is not infinite, it's
  /// brought to the range [0; maxLinkLength].
  /// </param>
  /// <seealso cref="connectorState"/>
  /// <seealso cref="ILinkCableJoint"/>
  protected virtual void SetCableLength(float length) {
    if (cableJoint != null) {
      if (!float.IsInfinity(length)) {
        length = Mathf.Min(Mathf.Max(length, 0f), cableJoint.cfgMaxCableLength);
      }
      cableJoint.SetCableLength(length);
    }
    UpdateContextMenu();
  }
  #endregion

  #region Local utility methods
  /// <summary>Helper method to execute context menu updates on vessel switch.</summary>
  /// <param name="v">The new active vessel.</param>
  void OnVesselChange(Vessel v) {
    UpdateContextMenu();
    MonoUtilities.RefreshContextWindows(part);
  }

  /// <summary>Moves a linked connected from another target to the active EVA.</summary>
  void ClaimLinkedConnector() {
    if (FlightGlobals.ActiveVessel.isEVA && isLinked) {
      var kerbalTarget = FlightGlobals.ActiveVessel.rootPart.Modules.OfType<ILinkTarget>()
          .FirstOrDefault(t => t.cfgLinkType == cfgLinkType && t.linkState == LinkState.Available);
      if (kerbalTarget != null
          && CheckCanLinkTo(kerbalTarget, reportToGUI: true, checkStates: false)) {
        BreakCurrentLink(LinkActorType.API);
        if (LinkToTarget(LinkActorType.Player, kerbalTarget)) {
          SetCableLength(float.PositiveInfinity);
        }
      }
      if (!isLinked || !linkTarget.part.vessel.isEVA) {
        UISoundPlayer.instance.Play(KASAPI.CommonConfig.sndPathBipWrong);
      }
    }
  }

  /// <summary>Saves the connector relative position and rotation.</summary>
  /// <remarks>If there is no physical connector started, then erases any saved state.</remarks>
  /// <param name="saveNonPhysical">
  /// Tells to update state to the connector model position if the physical connector is not
  /// started. However, the state will be saved only if there was no previous state.
  /// </param>
  void SaveConnectorModelPosAndRot(bool saveNonPhysical = false) {
    if (!saveNonPhysical && connectorObj == null) {
      persistedConnectorPosAndRot = null;
      return;
    }
    if (saveNonPhysical && connectorObj == null && persistedConnectorPosAndRot != null) {
      // For non physical connector only update connector if not yet updated. To allow restoring
      // a deployed connector at an arbitrary location.
      return;
    }
    var connector = connectorObj ?? GetConnectorModel();
    persistedConnectorPosAndRot = gameObject.transform.InverseTransformPosAndRot(
        new PosAndRot(connector.position, connector.rotation.eulerAngles));
  }

  /// <summary>Converts a physicsless connector model into a physical object.</summary>
  void StartPhysicsOnConnector() {
    HostedDebugLog.Info(this, "Make the cable connector physical");
    var connectorPosAndRot =
        gameObject.transform.TransformPosAndRot(persistedConnectorPosAndRot);
    var connectorModel = GetConnectorModel();
    var pipeAttach = GetConnectorModelPipeAnchor();
    var partAttach = GetConnectorModelPartAnchor();
    
    // Make a physical object and attach renderer to it. This will make connector following physics.
    // Adjust pipe and part transforms the same way as in the connector.
    connectorObj = new GameObject(
        "physicalConnectorObj" + part.launchID + "-" + linkRendererName).transform;
    connectorObj.SetPositionAndRotation(connectorModel.position, connectorModel.rotation);
    var physPartAttach = new GameObject(partAttach.name + "-reverseAnchor").transform;
    physPartAttach.SetPositionAndRotation(
        partAttach.position, Quaternion.LookRotation(-partAttach.forward, -partAttach.up));
    physPartAttach.parent = connectorObj;
    var physPipeAttachObj = new GameObject(pipeAttach.name + "-Anchor").transform;
    physPipeAttachObj.parent = connectorObj;
    physPipeAttachObj.SetPositionAndRotation(pipeAttach.position, pipeAttach.rotation);
    connectorObj.SetPositionAndRotation(connectorPosAndRot.pos, connectorPosAndRot.rot);

    var connector = KASInternalPhysicalConnector.Promote(
        this, connectorObj.gameObject, connectorInteractDistance);
    connector.connectorRb.mass = connectorMass;
    part.mass -= connectorMass;
    part.rb.mass -= connectorMass;

    linkRenderer.StartRenderer(nodeTransform, physPartAttach);
    Colliders.UpdateColliders(connectorModel.gameObject, isEnabled: true);
    cableJoint.StartPhysicalHead(this, physPipeAttachObj);
    SaveConnectorModelPosAndRot();
  }

  /// <summary>Converts a physical connector back into the physicsless model.</summary>
  /// <remarks>It's a cleanup method that must always succeed.</remarks>
  void StopPhysicsOnConnector() {
    if (connectorObj == null || !linkRenderer.isStarted) {
      return;  // Nothing to do.
    }
    HostedDebugLog.Info(this, "Make the cable connector non-physical");
    linkRenderer.StopRenderer();
    cableJoint.StopPhysicalHead();
    KASInternalPhysicalConnector.Demote(connectorObj.gameObject, false);
    Destroy(connectorObj.gameObject);
    connectorObj = null;
    part.mass += connectorMass;
    part.rb.mass += connectorMass;
    SaveConnectorModelPosAndRot();
  }
  #endregion
}

}  // namespace
