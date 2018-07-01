// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
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
using KSPDev.Types;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Highlighting;

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
/// The module is a <see cref="ILinkSource">link source</see>. And the target must be a compatible
/// <see cref="ILinkTarget">link target</see>.
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

  /// <summary>Offset from the link node for the physical connector to park.</summary>
  /// <remarks>
  /// When the connector is "locked" to the owner part, it will be placed here, aligned at the
  /// connector's cable anchor.
  /// </remarks>
  /// <seealso cref="ILinkPeer.nodeTransform"/>
  /// <seealso cref="connectorCableAttachAt"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="Unity3D_HelpIndex.xml" path="//item[@name='T:UnityEngine.Vector3']/*"/>
  [KSPField]
  public Vector3 connectorParkPositionOffset = Vector3.zero;

  /// <summary>Maximum distance at which an EVA kerbal can pickup a dropped connector.</summary>
  /// <seealso cref="KASLinkTargetKerbal"/>
  [KSPField]
  public float connectorInteractDistance = 0.3f;

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
  protected PosAndRot persistedConnectorPosAndRot;
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
        UISoundPlayer.instance.Play(CommonConfig.sndPathBipWrong);
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

  /// <inheritdoc/>
  protected float currentCableLength { get { return cableJoint.deployedCableLength; } }

  /// <inheritdoc/>
  protected float cfgMaxCableLength { get { return cableJoint.cfgMaxCableLength; } }

  /// <inheritdoc/>
  protected bool isConnectorLocked {
    get {
      return connectorState == ConnectorState.Locked
          || connectorState == ConnectorState.Docked;
    }
  }

  /// <summary>Anchor transform at the connector to attach the cable.</summary>
  protected Transform connectorCableAnchor { get; private set; }

  /// <summary>Anchor transform at the connector to attach with the part.</summary>
  protected Transform connectorPartAnchor { get; private set; }

  /// <summary>Anchor transform at the owning part to attach the cable.</summary>
  protected Transform partCableAnchor { get; private set; }

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

  /// <summary>Winch connector model transformation object.</summary>
  /// <remarks>
  /// Depending on the current state this model can be a child to the part's model or a standalone
  /// object.
  /// </remarks>
  /// <value>The root transformation of the connector object.</value>
  /// <seealso cref="ConnectorState"/>
  protected Transform connectorModelObj { get; private set; }

  /// <summary>Physical joint module that control the cable.</summary>
  /// <remarks>
  /// Note, that the module will <i>not</i> notice any changes done to the joint. Always call
  /// <see cref="UpdateContextMenu"/> on this module after the update to the joint settings.
  /// </remarks>
  /// <value>The module instance.</value>
  /// <seealso cref="SetCableLength"/>
  protected ILinkCableJoint cableJoint { get { return linkJoint as ILinkCableJoint; } }

  /// <summary>State machine that defines and controls the connector state.</summary>
  /// <remarks>
  /// It's not safe to change the connector state on a part with no physics! If the state needs to
  /// be changed on the part load, consider overriding <see cref="OnPartUnpack"/>.
  /// </remarks>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ProcessingUtils.SimpleStateMachine_1']/*"/>
  protected SimpleStateMachine<ConnectorState> connectorStateMachine { get; private set; }
  #endregion

  #region Local fields & properties
  /// <summary>Connector grab event to inject into the linked target.</summary>
  /// <see cref="UpdateContextMenu"/>
  BaseEvent GrabConnectorEventInject;
  #endregion

  #region KASLikSourceBase overrides
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

    GameEvents.onVesselChange.Add(OnVesselChange);
  }

  /// <inheritdoc/>
  public override void OnDestroy() {
    base.OnDestroy();
    GameEvents.onVesselChange.Remove(OnVesselChange);
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    // The locked connector with a part attached is get docked. So we require docking mode here.
    // TODO(ihsoft): Allow non-docking mode.
    if (!allowCoupling) {
      HostedDebugLog.Error(this, "The coupling must be allowed for this part to work. Overriding"
                           + " allowCoupling settings to true.");
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
      // In case of the connector is not locked to either the winch or the target part, adjust its
      // model position and rotation. The rest of the state will be restored in the state machine. 
      if (persistedConnectorPosAndRot != null) {
        var world = gameObject.transform.TransformPosAndRot(persistedConnectorPosAndRot);
        connectorModelObj.position = world.pos;
        connectorModelObj.rotation = world.rot;
      }
    }
  }

  /// <inheritdoc/>
  public override void OnSave(ConfigNode node) {
    // Persist the connector data only if its position is not fixed to the winch model.
    // It must be the peristsent state since the state machine can be in a different state at this
    // moment (e.g. during the vessel backup).
    if (!persistedIsConnectorLocked) {
      persistedConnectorPosAndRot = gameObject.transform.InverseTransformPosAndRot(
          new PosAndRot(connectorModelObj.position, connectorModelObj.rotation.eulerAngles));
    }
    base.OnSave(node);
  }

  /// <inheritdoc/>
  public override void OnPartUnpack() {
    base.OnPartUnpack();
    // The physics has started. It's safe to restore the connector (it can be physical).
    if (!connectorStateMachine.currentState.HasValue) {
      SetConnectorState(connectorState);  // The connectorState property handles the defaults.
    }
  }

  /// <inheritdoc/>
  public override void OnPartDie() {
    base.OnPartDie();
    // Make sure the connector is locked into the winch to not leave it behind.
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
      persistedIsConnectorLocked = isConnectorLocked;
      if (end == ConnectorState.Locked) {
        KASAPI.AttachNodesUtils.AddNode(part, coupleNode);
      } else if (coupleNode.attachedPart == null) {
        KASAPI.AttachNodesUtils.DropNode(part, coupleNode);
      }
      UpdateContextMenu();
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
          connectorModelObj.parent = nodeTransform;
          PartModel.UpdateHighlighters(part);
          connectorModelObj.GetComponentsInChildren<Renderer>().ToList()
              .ForEach(r => r.SetPropertyBlock(part.mpb));
          AlignTransforms.SnapAlign(connectorModelObj, connectorCableAnchor, partCableAnchor);
          SetCableLength(0);
          if (oldState.HasValue) {  // Skip when restoring state.
            UISoundPlayer.instance.Play(sndPathLockConnector);
          }
        },
        callOnShutdown: false);
    connectorStateMachine.AddStateHandlers(
        ConnectorState.Docked,
        enterHandler: oldState => {
          connectorModelObj.parent = nodeTransform;
          AlignTransforms.SnapAlign(connectorModelObj, connectorCableAnchor, partCableAnchor);
          SetCableLength(0);

          // Align the docking part to the nodes if it's a separate vessel.
          if (oldState.HasValue && linkTarget.part.vessel != vessel) {
            AlignTransforms.SnapAlignNodes(linkTarget.coupleNode, coupleNode);
            linkJoint.SetCoupleOnLinkMode(true);
            UISoundPlayer.instance.Play(sndPathDockConnector);
          }
        },
        leaveHandler: newState => linkJoint.SetCoupleOnLinkMode(false),
        callOnShutdown: false);
    connectorStateMachine.AddStateHandlers(
        ConnectorState.Deployed,
        enterHandler: oldState => {
          TurnConnectorPhysics(true);
          connectorModelObj.parent = connectorModelObj;
          PartModel.UpdateHighlighters(part);
          linkRenderer.StartRenderer(partCableAnchor, connectorCableAnchor);
        },
        leaveHandler: newState => {
          TurnConnectorPhysics(false);
          linkRenderer.StopRenderer();
        },
        callOnShutdown: false);
    connectorStateMachine.AddStateHandlers(
        ConnectorState.Plugged,
        enterHandler: oldState => {
          // Destroy the previous highlighter if any, since it would interfere with the new owner.
          DestroyImmediate(connectorModelObj.GetComponent<Highlighter>());
          connectorModelObj.parent = linkTarget.nodeTransform;
          PartModel.UpdateHighlighters(part);
          PartModel.UpdateHighlighters(linkTarget.part);
          connectorModelObj.GetComponentsInChildren<Renderer>().ToList()
              .ForEach(r => r.SetPropertyBlock(linkTarget.part.mpb));
          AlignTransforms.SnapAlign(
              connectorModelObj, connectorPartAnchor, linkTarget.nodeTransform);
          linkRenderer.StartRenderer(partCableAnchor, connectorCableAnchor);
        },
        leaveHandler: newState => {
          var oldParent = connectorModelObj.GetComponentInParent<Part>();
          var oldHigh = oldParent.HighlightActive;
          if (oldHigh) {
            // Disable the part highlight to restore the connector's renderer materials.
            oldParent.SetHighlight(false, false);
          }
          connectorModelObj.parent = nodeTransform;  // Back to the model.
          PartModel.UpdateHighlighters(part);
          PartModel.UpdateHighlighters(oldParent);
          if (oldHigh) {
            oldParent.SetHighlight(true, false);
          }
          linkRenderer.StopRenderer();
        },
        callOnShutdown: false);
  }

  /// <inheritdoc/>
  protected override void ShutdownStateMachine() {
    base.ShutdownStateMachine();
    connectorStateMachine.currentState = null;
  }

  /// <inheritdoc/>
  protected override void LogicalLink(ILinkTarget target) {
    base.LogicalLink(target);
    if (target.part == parsedAttachNode.attachedPart && part == target.coupleNode.attachedPart) {
      // The target part is externally attached.
      SetConnectorState(ConnectorState.Docked);
    } else {
      SetConnectorState(ConnectorState.Plugged);
      if (linkActor == LinkActorType.Player) {
        UISoundPlayer.instance.Play(target.part.vessel.isEVA
            ? sndPathGrabConnector
            : sndPathPlugConnector);
      }
    }
    linkRenderer.StartRenderer(partCableAnchor, connectorCableAnchor);
  }

  /// <inheritdoc/>
  protected override void LogicalUnlink(LinkActorType actorType) {
    base.LogicalUnlink(actorType);
    SetConnectorState(isConnectorLocked ? ConnectorState.Locked : ConnectorState.Deployed);
    if (actorType == LinkActorType.Physics) {
      UISoundPlayer.instance.Play(sndPathBroke);
      ShowStatusMessage(CableLinkBrokenMsg, isError: true);
    } else if (actorType == LinkActorType.Player) {
      if (connectorState == ConnectorState.Deployed) {
        UISoundPlayer.instance.Play(sndPathUnplugConnector);
      }
    }
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
      // Only do it for the visual improvements. The base class will attach the renderer to
      // the node, instead of the cable attach position. The state machine will get it fixed, but
      // it'll only happen when the physics is started.
      linkRenderer.StartRenderer(partCableAnchor, connectorCableAnchor);
    }
  }

  /// <inheritdoc/>
  protected override void CheckCoupleNode() {
    base.CheckCoupleNode();
    if (linkState == LinkState.NodeIsBlocked && coupleNode.attachedPart != null) {
      HostedDebugLog.Warning(this, "Decouple incompatible part from the node: {0}",
                             coupleNode.FindOpposingNode().attachedPart);
      UISoundPlayer.instance.Play(CommonConfig.sndPathBipWrong);
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
      GrabConnectorEventInject.guiName = e.guiName;
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
    GrabConnectorEventInject.active = linkTarget != null
        && FlightGlobals.ActiveVessel != linkTarget.part.vessel;
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
  protected virtual void SetCableLength(float length) {
    if (cableJoint != null) {
      cableJoint.SetCableLength(length);
    }
    UpdateContextMenu();
  }
  #endregion

  #region Local utility methods
  /// <summary>
  /// Makes the winch connector an idependent physcal onbject or returns it into a part's model as
  /// a physicsless object.
  /// </summary>
  /// <remarks>
  /// Note, that physics objects on the connector don't die in this method call. They will be
  /// cleaned up at the frame end. The caller must consider it when dealing with the connector.
  /// </remarks>
  /// <param name="state">The physical state of the connector: <c>true</c> means "physical".</param>
  void TurnConnectorPhysics(bool state) {
    if (state && cableJoint.headRb == null) {
      HostedDebugLog.Info(this, "Make the cable connector physical");
      var connector = KASInternalPhysicalConnector.Promote(
          this, connectorModelObj.gameObject, connectorInteractDistance);
      cableJoint.StartPhysicalHead(this, connectorCableAnchor);
      connector.connectorRb.mass = connectorMass;
      part.mass -= connectorMass;
      part.rb.mass -= connectorMass;
    } else if (!state && cableJoint.headRb != null) {
      HostedDebugLog.Info(this, "Make the cable connector non-physical");
      cableJoint.StopPhysicalHead();
      KASInternalPhysicalConnector.Demote(connectorModelObj.gameObject);
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
    var ConnectorModelName = "ConnectorModel" + part.Modules.IndexOf(this);
    var ConnectorParkAnchorName = "ConnectorParkAnchor" + part.Modules.IndexOf(this);
    const string CableAnchorName = "CableAnchor";
    const string PartAnchorName = "PartAnchor";
    
    if (!PartLoader.Instance.IsReady()) {
      // Make the missing models and set the proper hierarchy.
      connectorModelObj = Hierarchy.FindPartModelByPath(part, connectorModel);
      connectorCableAnchor = connectorCableAttachAt != ""
          ? Hierarchy.FindPartModelByPath(part, connectorCableAttachAt) : null;
      connectorPartAnchor = connectorPartAttachAt != ""
          ? Hierarchy.FindPartModelByPath(part, connectorPartAttachAt) : null;

      if (connectorModelObj == null) {
        HostedDebugLog.Error(this, "Cannot find a connector model: {0}", connectorModel);
        // Fallback to not have the whole code to crash.
        connectorModelObj = new GameObject().transform;
      }
      connectorModelObj.name = ConnectorModelName;
      connectorModelObj.parent = nodeTransform;
      

      if (connectorCableAnchor == null) {
        if (connectorCableAttachAt != "") {
          HostedDebugLog.Error(
              this, "Cannot find cable anchor transform: {0}", connectorCableAttachAt);
        }
        connectorCableAnchor = new GameObject().transform;
        var posAndRot = PosAndRot.FromString(connectorCableAttachAtPosAndRot);
        Hierarchy.MoveToParent(connectorCableAnchor, connectorModelObj,
                               newPosition: posAndRot.pos, newRotation: posAndRot.rot);
      }
      connectorCableAnchor.name = CableAnchorName;
      connectorCableAnchor.parent = connectorModelObj;

      if (connectorPartAnchor == null) {
        if (connectorPartAttachAt != "") {
          HostedDebugLog.Error(
              this, "Cannot find part anchor transform: {0}", connectorPartAttachAt);
        }
        connectorPartAnchor = new GameObject().transform;
        var posAndRot = PosAndRot.FromString(connectorPartAttachAtPosAndRot);
        Hierarchy.MoveToParent(connectorPartAnchor, connectorModelObj,
                               newPosition: posAndRot.pos, newRotation: posAndRot.rot);
      }
      connectorPartAnchor.name = PartAnchorName;
      connectorPartAnchor.parent = connectorModelObj;

      partCableAnchor = new GameObject(ConnectorParkAnchorName).transform;
      Hierarchy.MoveToParent(
          partCableAnchor, nodeTransform, newPosition: connectorParkPositionOffset);
    } else {
      connectorModelObj = nodeTransform.Find(ConnectorModelName);
      connectorCableAnchor = connectorModelObj.Find(CableAnchorName);
      connectorPartAnchor = connectorModelObj.Find(PartAnchorName);
      partCableAnchor = nodeTransform.Find(ConnectorParkAnchorName);
    }
    AlignTransforms.SnapAlign(connectorModelObj, connectorCableAnchor, partCableAnchor);
  }

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
        UISoundPlayer.instance.Play(CommonConfig.sndPathBipWrong);
      }
    }
  }
  #endregion
}

}  // namespace
