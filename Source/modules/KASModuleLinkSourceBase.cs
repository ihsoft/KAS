// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using KSPDev.GUIUtils;
using KSPDev.KSPInterfaces;
using KSPDev.ModelUtils;
using KSPDev.LogUtils;
using KSPDev.Types;
using KSPDev.ProcessingUtils;
using KASAPIv1;

namespace KAS {

/// <summary>Base link source module. Does all the job on making two parts linked.</summary>
/// <remarks>This module deals with main logic of linking two parts together. The other party of the
/// link must be aware of the linking porcess. The targets must implement <see cref="ILinkTarget"/>.
/// <para>External callers must access methods and properties declared in base classes or interfaces
/// only. Members and methods that are not part of these declarations are not intended for the
/// public use <b>regardless</b> to their visibility level.</para>
/// <para>Decendand classes may use any members and methods but good practice is restricting the
/// usage to the interfaces and virtuals only.</para>
/// </remarks>
/// <seealso href="https://kerbalspaceprogram.com/api/interface_i_module_info.html">KSP: IModuleInfo
/// </seealso>
/// <seealso href="https://kerbalspaceprogram.com/api/class_part_module.html">KSP: PartModule
/// </seealso>
/// <seealso href="https://kerbalspaceprogram.com/api/interface_i_activate_on_decouple.html">
/// KSP: IActivateOnDecouple</seealso>
/// <seealso cref="ILinkSource"/>
/// <seealso cref="ILinkStateEventListener"/>
// TODO(ihsoft): Handle KIS actions.
// TODO(ihsoft): Handle part destroyed action.
// TODO(ihsoft): Handle part staged action.
public class KASModuleLinkSourceBase : PartModule,
    // KSP interfaces.
    IModuleInfo, IActivateOnDecouple,
    // KAS interfaces.
    ILinkSource, ILinkStateEventListener,
    // KSPDev syntax sugar interfaces.
    IPartModule, IsPackable, IsDestroyable, IsPartDeathListener,
    IKSPDevModuleInfo, IKSPActivateOnDecouple {

  #region Localizable GUI strings
  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected readonly static Message IncompatibleTargetLinkTypeMsg = new Message(
      "#kasLOC_02000",
      defaultTemplate: "Incompatible target link type",
      description: "Message to display when the target link type doesn't match the source type.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected readonly static Message CannotLinkToTheSameVesselMsg = new Message(
      "#kasLOC_02001",
      defaultTemplate:  "Cannot link to the same vessel",
      description: "Message to display when the link mode requires the target to belong to a"
      + " different vessel but it belongs to the same vessel as the source.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected readonly static Message CannotLinkDifferentVesselsMsg = new Message(
      "#kasLOC_02002",
      defaultTemplate: "Cannot link different vessels",
      description: "Message to display when the link mode requires the target to belong to the same"
      + " vessel but it belongs to a different vessel.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected readonly static Message SourceIsNotAvailableForLinkMsg = new Message(
      "#kasLOC_02003",
      defaultTemplate: "Source is not available for a link",
      description: "Message to display when a source is refusing to start the link.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected readonly static Message TargetDoesntAcceptLinksMsg = new Message(
      "#kasLOC_02004",
      defaultTemplate: "Target doesn't accept links",
      description: "Message to display when the target is refusing to accept the link.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  protected readonly static Message<string> CannotRestoreLinkMsg = new Message<string>(
      "#kasLOC_02005",
      defaultTemplate: "Cannot restore link for: <<1>>",
      description: "Message to display when a linked source and target cannot be matched on load."
      + "\nArgument <<1>> is a name of the SOURCE part.",
      example: "Cannot restore link for: KAS.TJ1");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  protected readonly static Message<string> LinksWithSocketTypeInfo = new Message<string>(
      "#kasLOC_02006",
      defaultTemplate: "Links with socket type: <<1>>",
      description: "Info string in the editor for the link type setting."
      + "\nArgument <<1>> is the type string from the part's config.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected readonly static Message ModuleTitleInfo = new Message(
      "#kasLOC_02007",
      defaultTemplate: "KAS Joint Source",
      description: "Title of the module to present in the editor details window.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected readonly static Message LinkModeDockVesselsInfo = new Message(
      "#kasLOC_02008",
      defaultTemplate: "Can dock the vessels",
      description: "Info string in the editor that tells if the part can joint two vessels into one"
      + " (dock one vessel to another).");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected readonly static Message LinkModeTiePartsOnDifferentVesselsInfo = new Message(
      "#kasLOC_02009",
      defaultTemplate: "Links to another vessel",
      description: "Info string in the editor that tells if the part acn establish a link to"
      + " another vessel without docking.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected readonly static Message LinkModeTiePartsOnSameVesselInfo = new Message(
      "#kasLOC_02010",
      defaultTemplate: "Links to the same vessel",
      description: "Info string in the editor that tells if the part can establish a link to"
      + " another part of the same vessel,");
  #endregion

  #region ILinkSource config properties implementation
  /// <inheritdoc/>
  public string cfgLinkType { get { return linkType; } }

  /// <inheritdoc/>
  public LinkMode cfgLinkMode { get { return linkMode; } }

  /// <inheritdoc/>
  public string cfgAttachNodeName { get { return attachNodeName; } }

  /// <inheritdoc/>
  public string cfgLinkRendererName { get { return linkRendererName; } }
  #endregion

  #region ILinkSource properties implementation
  /// <inheritdoc/>
  public ILinkTarget linkTarget {
    get { return _linkTarget; }
    private set {
      if (_linkTarget != value) {
        if (value != null && value.part.vessel != vessel) {
          // Set ignores on the new target part.
          Colliders.SetCollisionIgnores(part, value.part, true);
        }
        if (_linkTarget != null && _linkTarget.part.vessel != vessel) {
          // Reset ignores on the old target part.
          Colliders.SetCollisionIgnores(part, _linkTarget.part, false);
        }
      }
      _linkTarget = value;
      persistedLinkTargetPartId = value != null ? value.part.flightID : 0;
    }
  }
  ILinkTarget _linkTarget;

  /// <inheritdoc/>
  public uint linkTargetPartId { get { return persistedLinkTargetPartId; } }

  /// <inheritdoc/>
  public LinkState linkState {
    get {
      return linkStateMachine.currentState ?? persistedLinkState;
    }
    protected set {
      var oldState = linkStateMachine.currentState;
      linkStateMachine.currentState = value;
      persistedLinkState = value;
      OnStateChange(oldState);
    }
  }

  /// <inheritdoc/>
  public virtual bool isLocked {
    get { return linkState == LinkState.Locked; }
    set {
      if (value != isLocked) {  // Don't trigger state change events when value hasn't changed.
        linkState = value ? LinkState.Locked : LinkState.Available;
      }
    }
  }

  /// <inheritdoc/>
  public Transform nodeTransform { get; private set; }

  /// <inheritdoc/>
  public PosAndRot physicalAnchor {
    get { return PosAndRot.FromString(physicalAnchorAtSourcePosAndRot); }
  }

  /// <inheritdoc/>
  public AttachNode attachNode { get; private set; }

  /// <inheritdoc/>
  public GUILinkMode guiLinkMode { get; private set; }

  /// <inheritdoc/>
  public LinkActorType linkActor { get; private set; }

  /// <inheritdoc/>
  public PosAndRot targetPhysicalAnchor {
    get { return PosAndRot.FromString(physicalAnchorAtTargetPosAndRot); }
  }
  #endregion

  #region Persistent fields
  /// <summary>Source link state in the last save action.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  public LinkState persistedLinkState = LinkState.Available;

  /// <summary>Target part flight ID.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  public uint persistedLinkTargetPartId;
  #endregion

  #region Part's config fields
  /// <summary>See <see cref="cfgLinkType"/>.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string linkType = "";

  /// <summary>See <see cref="cfgLinkMode"/>.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public LinkMode linkMode = LinkMode.DockVessels;

  /// <summary>See <see cref="cfgLinkRendererName"/>.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string linkRendererName = "";

  /// <summary>See <see cref="cfgAttachNodeName"/>.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string attachNodeName = "";

  /// <summary>Name of object in the model that defines the attach node.</summary>
  /// <remarks>
  /// The value is a <see cref="Hierarchy.FindPartModelByPath(Part,string,Transform)"/> search
  /// path. The path is looked starting from the part's model root.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='M:KSPDev.Hierarchy.FindTransformByPath']/*"/>
  [KSPField]
  public string attachNodeTransformName = "";

  /// <summary>Defines the attach node position in the local space.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Vector3 attachNodePosition = Vector3.zero;

  /// <summary>Defines the attach node orientation in the local space.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Vector3 attachNodeOrientation = Vector3.up;

  /// <summary>See <see cref="physicalAnchor"/>.</summary>
  /// <remarks>The value is a serialized <see cref="PosAndRot"/> instance.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string physicalAnchorAtSourcePosAndRot = new PosAndRot().SerializeToString();

  /// <summary>See <see cref="targetPhysicalAnchor"/>.</summary>
  /// <remarks>The value is a serialized <see cref="PosAndRot"/> instance.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string physicalAnchorAtTargetPosAndRot = new PosAndRot().SerializeToString();
  #endregion

  #region Inheritable properties
  /// <summary>Joint module that manages a physical link.</summary>
  /// <value>The physical joint module on the part.</value>
  /// <remarks>A part must have exactly one joint module.</remarks>
  protected ILinkJoint linkJoint { get; private set; }

  /// <summary>Renderer of the link meshes.</summary>
  /// <value>A renderer module with name <see cref="cfgLinkRendererName"/>.</value>
  protected ILinkRenderer linkRenderer { get; private set; }

  /// <summary>Tells if this source is currectly linked with a target.</summary>
  /// <value>The current state of the link.</value>
  protected bool isLinked {
    get { return linkState == LinkState.Linked; }
  }
  #endregion

  /// <summary>State machine that controls event reaction in different states.</summary>
  /// <remarks>
  /// Primary usage of the machine is managing subscriptions to the different game events. It's
  /// highly discouraged to use it for firing events or taking actions. Initial state can be setup
  /// under different circumstances, and the associated events and actions may get triggered at the
  /// inappropriate moment.
  /// </remarks>
  SimpleStateMachine<LinkState> linkStateMachine;

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    linkStateMachine = new SimpleStateMachine<LinkState>(true /* strict */);
    linkStateMachine.SetTransitionConstraint(
        LinkState.Available,
        new[] {LinkState.Linking, LinkState.RejectingLinks});
    linkStateMachine.SetTransitionConstraint(
        LinkState.Linking,
        new[] {LinkState.Available, LinkState.Linked});
    linkStateMachine.SetTransitionConstraint(
        LinkState.Linked,
        new[] {LinkState.Available});
    linkStateMachine.SetTransitionConstraint(
        LinkState.Locked,
        new[] {LinkState.Available});
    linkStateMachine.SetTransitionConstraint(
        LinkState.RejectingLinks,
        new[] {LinkState.Available, LinkState.Locked});

    linkStateMachine.AddStateHandlers(
        LinkState.Available,
        enterHandler: x => KASEvents.OnStartLinking.Add(OnStartLinkingKASEvent),
        leaveHandler: x => KASEvents.OnStartLinking.Remove(OnStartLinkingKASEvent));
    linkStateMachine.AddStateHandlers(
        LinkState.RejectingLinks,
        enterHandler: x => KASEvents.OnStopLinking.Add(OnStopLinkingKASEvent),
        leaveHandler: x => KASEvents.OnStopLinking.Remove(OnStopLinkingKASEvent));
    linkStateMachine.AddStateHandlers(
        LinkState.Linking,
        enterHandler: x => KASEvents.OnLinkAccepted.Add(OnLinkActionAcceptedKASEvent),
        leaveHandler: x => KASEvents.OnLinkAccepted.Remove(OnLinkActionAcceptedKASEvent));
  }

  /// <inheritdoc/>
  public override void OnInitialize() {
    base.OnInitialize();
    if (isLinked && linkTarget.part.vessel != vessel) {
      // When the target is at the different vessel, there is no automatic collision ignore set.
      StartCoroutine(WaitAndSetCollisionIgnores());
    }
  }
  
  /// <inheritdoc/>
  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);

    linkJoint = part.FindModuleImplementing<ILinkJoint>();
    linkRenderer = part.FindModulesImplementing<ILinkRenderer>()
        .FirstOrDefault(x => x.cfgRendererName == linkRendererName);
    if (linkJoint == null) {
      HostedDebugLog.Error(this, "KAS part misses a joint module. It won't work properly");
    }
    if (linkRenderer == null) {
      HostedDebugLog.Error(this, "KAS part misses a renderer module. It won't work properly");
    }

    // Try to restore link to the target and update module's state.
    if (persistedLinkState == LinkState.Linked) {
      if (linkMode == LinkMode.DockVessels) {
        RestoreTarget();
      } else {
        // Target vessel may not be loaded yet. Wait for it.
        AsyncCall.CallOnEndOfFrame(this, RestoreTarget);
      }
    } else {
      linkStateMachine.currentState = persistedLinkState;
      linkState = linkState;  // Trigger state updates.
    }
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);

    // Create attach node transform. It will become a part of the model.
    var nodeName = attachNodeTransformName != ""
        ? attachNodeTransformName
        : attachNodeName + "-node";
    nodeTransform = Hierarchy.FindPartModelByPath(part, nodeName);
    if (nodeTransform == null) {
      nodeTransform = new GameObject(nodeName).transform;
      Hierarchy.MoveToParent(nodeTransform, Hierarchy.GetPartModelTransform(part),
                             newPosition: attachNodePosition,
                             newRotation: Quaternion.LookRotation(attachNodeOrientation));
      HostedDebugLog.Info(this, "Create attach node transform {0}: pos={1}, euler={2}",
                          nodeTransform,
                          DbgFormatter.Vector(nodeTransform.localPosition),
                          DbgFormatter.Vector(nodeTransform.localRotation.eulerAngles));
    } else {
      HostedDebugLog.Info(this, "Use attach node transform {0}: pos={1}, euler={2}",
                          nodeTransform,
                          DbgFormatter.Vector(nodeTransform.localPosition),
                          DbgFormatter.Vector(nodeTransform.localRotation.eulerAngles));
    }

    // If source is docked to the target then we need actual attach node. Create it.
    if (persistedLinkState == LinkState.Linked && linkMode == LinkMode.DockVessels) {
      attachNode = KASAPI.AttachNodesUtils.CreateAttachNode(part, attachNodeName, nodeTransform);
    }
  }
  #endregion

  #region IsPackable implementation
  /// <inheritdoc/>
  public virtual void OnPartUnpack() {
    if (!isLinked) {
      return;
    }
    if (linkTarget == null) {
      LogicalUnlink(LinkActorType.None);
      ScreenMessaging.ShowErrorScreenMessage(CannotRestoreLinkMsg.Format(part.name));
      if (linkMode == LinkMode.DockVessels) {
        HostedDebugLog.Warning(this, "Fix the docking state for a bad link...");
        AsyncCall.CallOnEndOfFrame(this, UndockFromBadTarget);
      } else {
        HostedDebugLog.Warning(
            this, "Mark the source as unlinked since the link state cannot be restored.");
      }
    } else if (linkTarget.linkSource == null) {
      ScreenMessaging.ShowErrorScreenMessage(CannotRestoreLinkMsg.Format(part.name));
      HostedDebugLog.Warning(
          this, "Detach from the target {0} since it's failed to restore the state.",
          linkTarget.part);
      AsyncCall.CallOnEndOfFrame(this, () => BreakCurrentLink(LinkActorType.None));
    }
  }

  /// <inheritdoc/>
  public virtual void OnPartPack() {
  }
  #endregion

  #region IsDestroyable implementation
  /// <inheritdoc/>
  public virtual void OnDestroy() {
    linkStateMachine.currentState = null;  // Stop.
  }
  #endregion

  #region IsPartDeathListener implementation
  /// <inheritdoc/>
  public virtual void OnPartDie() {
    if (isLinked) {
      BreakCurrentLink(LinkActorType.Physics);
    }
  }
  #endregion

  #region IModuleInfo implementation
  /// <inheritdoc/>
  public override string GetInfo() {
    var sb = new StringBuilder(base.GetInfo());
    sb.AppendLine(LinksWithSocketTypeInfo.Format(linkType));
    sb.AppendLine();

    if (linkMode == LinkMode.DockVessels) {
      sb.AppendLine(ScreenMessaging.SetColorToRichText(
          LinkModeDockVesselsInfo, Color.cyan));
    }
    if (linkMode == LinkMode.TiePartsOnDifferentVessels || linkMode == LinkMode.TieAnyParts) {
      sb.AppendLine(ScreenMessaging.SetColorToRichText(
          LinkModeTiePartsOnDifferentVesselsInfo, Color.cyan));
    }
    if (linkMode == LinkMode.TiePartsOnSameVessel || linkMode == LinkMode.TieAnyParts) {
      sb.AppendLine(ScreenMessaging.SetColorToRichText(
          LinkModeTiePartsOnSameVesselInfo, Color.cyan));
    }
    return sb.ToString();
  }

  /// <inheritdoc/>
  public virtual string GetModuleTitle() {
    return ModuleTitleInfo;
  }

  /// <inheritdoc/>
  public virtual Callback<Rect> GetDrawModulePanelCallback() {
    return null;
  }

  /// <inheritdoc/>
  public virtual string GetPrimaryField() {
    return null;
  }
  #endregion

  #region ILinkSource implementation
  /// <inheritdoc/>
  public virtual bool StartLinking(GUILinkMode mode, LinkActorType actor) {
    if (!linkStateMachine.CheckCanSwitchTo(LinkState.Linking)) {
      HostedDebugLog.Warning(this, "Cannot start linking mode in state: {0}", linkState);
      return false;
    }
    if (mode == GUILinkMode.Eva && !FlightGlobals.ActiveVessel.isEVA) {
      HostedDebugLog.Warning(this, "Cannot start EVA linking mode since active vessel is not EVA");
      return false;
    }
    linkState = LinkState.Linking;
    StartLinkGUIMode(mode, actor);
    return true;
  }

  /// <inheritdoc/>
  public virtual void CancelLinking(LinkActorType actor) {
    if (!linkStateMachine.CheckCanSwitchTo(LinkState.Available)) {
      HostedDebugLog.Warning(this, "Cannot stop linking mode in state: {0}", linkState);
      return;
    }
    StopLinkGUIMode();
    linkState = LinkState.Available;
  }

  /// <inheritdoc/>
  public virtual bool LinkToTarget(ILinkTarget target) {
    if (!CheckCanLinkTo(target)) {
      return false;
    }
    PhysicalLink(target);
    LogicalLink(target);
    // When GUI linking mode is stopped all the targets stop accepting link requests. I.e. the mode
    // must not be stopped before the link is created.
    StopLinkGUIMode();
    return true;
  }

  /// <inheritdoc/>
  public virtual void BreakCurrentLink(LinkActorType actorType, bool moveFocusOnTarget = false) {
    if (!isLinked) {
      HostedDebugLog.Warning(this, "Cannot break a link: the part is not linked to anything");
      return;
    }
    // Logical unlink must be done first before doing actual decouple.
    var oldTarget = linkTarget;
    var targetRootPart = linkTarget.part;
    LogicalUnlink(actorType);
    PhysicalUnink(oldTarget);
    // If either source or target part after the separation belong to the active vessel then adjust
    // the focus. Otherwise, the actor was external (e.g. EVA).
    if (moveFocusOnTarget && FlightGlobals.ActiveVessel == vessel) {
      FlightGlobals.ForceSetActiveVessel(targetRootPart.vessel);
    } else if (!moveFocusOnTarget && FlightGlobals.ActiveVessel == targetRootPart.vessel) {
      FlightGlobals.ForceSetActiveVessel(vessel);
    }
  }

  /// <inheritdoc/>
  public virtual bool CheckCanLinkTo(
      ILinkTarget target, bool reportToGUI = false, bool reportToLog = true) {
    string errorMsg =
        CheckBasicLinkConditions(target)
        ?? CheckJointLimits(target.nodeTransform)
        ?? linkRenderer.CheckColliderHits(nodeTransform, target.nodeTransform);
    if (errorMsg != null) {
      if (reportToGUI || reportToLog) {
        HostedDebugLog.Warning(
            this, "Cannot link a part of type={0} with the part {1}/type={2}: {3}",
            cfgLinkType, target.part, target.cfgLinkType, errorMsg);
      }
      if (reportToGUI) {
        ScreenMessaging.ShowScreenMessage(
            ScreenMessageStyle.UPPER_CENTER,
            ScreenMessaging.DefaultMessageTimeout,
            ScreenMessaging.ErrorColor,
            errorMsg);
      }
    }
    return errorMsg == null;
  }
  #endregion

  #region ILinkStateEventListener implementation
  /// <inheritdoc/>
  public virtual void OnKASLinkCreatedEvent(KASEvents.LinkEvent info) {
    // Lock this source if another source on the part made the link.
    if (!isLocked && !ReferenceEquals(info.source, this)) {
      isLocked = true;
    }
  }

  /// <inheritdoc/>
  public virtual void OnKASLinkBrokenEvent(KASEvents.LinkEvent info) {
    // Unlock this source if link with another source one the part has broke.
    if (isLocked && !ReferenceEquals(info.source, this)) {
      isLocked = false;
    }
  }
  #endregion

  #region IActivateOnDecouple implementation
  /// <inheritdoc/>
  public virtual void DecoupleAction(string nodeName, bool weDecouple) {
    if (nodeName == attachNodeName) {
      if (isLinked) {
        // In case of event was external to KAS.
        LogicalUnlink(LinkActorType.None);
      }
      // Cleanup the node since once decoupled it's no more needed.
      KASAPI.AttachNodesUtils.DropAttachNode(part, attachNodeName);
      attachNode = null;
    }
    //FIXME: restore source vessel info
  }
  #endregion

  #region Inheritable methods
  /// <summary>Triggers when a state has been assigned with a value.</summary>
  /// <remarks>
  /// This method triggers even when the new state doesn't differ from the old one. When it's
  /// important to catch the transition, check for the <paramref name="oldState"/>.
  /// </remarks>
  /// <param name="oldState">State prior to the change.</param>
  protected virtual void OnStateChange(LinkState? oldState) {
    // Start a renderer in a linked state with a valid target, and stop it in all the other states.
    if (isLinked && !linkRenderer.isStarted && linkTarget != null) {
      linkRenderer.StartRenderer(nodeTransform, linkTarget.nodeTransform);
    }
    if (!isLinked && linkRenderer.isStarted) {
      linkRenderer.StopRenderer();
    }
    // Create attach node for linking state t oallow coupling. Drop the node once linking mode is
    // over and link hasn't been established.
    if (linkState == LinkState.Linking && attachNode == null) {
      attachNode = KASAPI.AttachNodesUtils.CreateAttachNode(part, attachNodeName, nodeTransform);
    }
    if (oldState == LinkState.Linking && !isLinked && attachNode != null) {
      KASAPI.AttachNodesUtils.DropAttachNode(part, attachNodeName);
      attachNode = null;
    }
  }

  /// <summary>Initiates GUI mode, and starts displaying linking process.</summary>
  /// <param name="mode">The mode to start with.</param>
  /// <param name="actor">The actor, who has initiated the linking mode.</param>
  protected virtual void StartLinkGUIMode(GUILinkMode mode, LinkActorType actor) {
    guiLinkMode = mode;
    linkActor = actor;
    KASEvents.OnStartLinking.Fire(this);
  }

  /// <summary>Stops any pending GUI mode that displays linking process.</summary>
  /// <remarks>Does nothing if no GUI mode started.
  /// <para>
  /// If link is created then this method is called <i>after</i> <see cref="LogicalLink"/> callback
  /// gets fired.
  /// </para>
  /// </remarks>
  protected virtual void StopLinkGUIMode() {
    if (guiLinkMode != GUILinkMode.None) {
      KASEvents.OnStopLinking.Fire(this);
      guiLinkMode = GUILinkMode.None;
    }
  }

  /// <summary>Links source and target in the physical world.</summary>
  /// <remarks>
  /// Usually linked parts are physically related in the game's world but it's not required. E.g.
  /// implementation may choose to handle the relation procedurally.
  /// </remarks>
  /// <param name="target">Target to physically link with.</param>
  protected virtual void PhysicalLink(ILinkTarget target) {
    // FIXME: store source vessel info. needs to be restored on decouple.
    if (linkMode == LinkMode.DockVessels) {
      HostedDebugLog.Info(this, "Physically linking to {0}", target.part);
      KASAPI.LinkUtils.CoupleParts(attachNode, target.attachNode);
    }
  }

  /// <summary>Breaks link with the target in the physical world.</summary>
  /// <param name="target">Target to break physical link with.</param>
  protected virtual void PhysicalUnink(ILinkTarget target) {
    // FIXME: restore vessels names/types
    if (linkMode == LinkMode.DockVessels) {
      HostedDebugLog.Info(this, "Physically unlinking from {0}", target.part);
      KASAPI.LinkUtils.DecoupleParts(part, target.part);
    }
  }

  /// <summary>Logically links source and target.</summary>
  /// <remarks>
  /// No actual joint or connection is created in the game. Though, this method is always called
  /// before any physics changes.
  /// </remarks>
  /// <param name="target">Target to link with.</param>
  /// <seealso cref="PhysicalLink"/>
  protected virtual void LogicalLink(ILinkTarget target) {
    var linkInfo = new KASEvents.LinkEvent(this, target, linkActor);
    linkTarget = target;
    linkTarget.linkSource = this;
    linkState = LinkState.Linked;
    KASEvents.OnLinkCreated.Fire(linkInfo);
    part.FindModulesImplementing<ILinkStateEventListener>()
        .ForEach(x => x.OnKASLinkCreatedEvent(linkInfo));
  }

  /// <summary>Logically unlinks source and the current target.</summary>
  /// <remarks>Physics state is undetermined at this moment.</remarks>
  /// <param name="actorType">Actor who intiated the unlinking.</param>
  protected virtual void LogicalUnlink(LinkActorType actorType) {
    var linkInfo = new KASEvents.LinkEvent(this, linkTarget, actorType);
    if (linkTarget != null) {
      linkTarget.linkSource = null;
      linkTarget = null;
    }
    linkState = LinkState.Available;
    KASEvents.OnLinkBroken.Fire(linkInfo);
    part.FindModulesImplementing<ILinkStateEventListener>()
        .ForEach(x => x.OnKASLinkBrokenEvent(linkInfo));
  }

  /// <summary>Finds linked target for the source, and updates the state.</summary>
  /// <remarks>
  /// Depending on link mode this method may be called synchronously when part is started or
  /// asynchronously at the end of frame.
  /// </remarks>
  /// <seealso cref="linkMode"/>
  protected virtual void RestoreTarget() {
    linkTarget = KASAPI.LinkUtils.FindLinkTargetFromSource(this);
    if (linkTarget == null) {
      HostedDebugLog.Error(
          this, "Source cannot restore link to target part id={0} on the attach node {1}",
          persistedLinkTargetPartId, attachNodeName);
    }
    linkStateMachine.currentState = persistedLinkState;
    linkState = linkState;  // Trigger state updates.
  }
  #endregion

  #region New utility methods
  /// <summary>Checks if basic source and target states allows linking.</summary>
  /// <param name="target">Target of the tube to check link with.</param>
  /// <returns>An error message if link cannot be established or <c>null</c> otherwise.</returns>
  protected string CheckBasicLinkConditions(ILinkTarget target) {
    if (cfgLinkType != target.cfgLinkType) {
      return IncompatibleTargetLinkTypeMsg;
    }
    if ((linkMode == LinkMode.DockVessels || linkMode == LinkMode.TiePartsOnDifferentVessels)
        && part.vessel == target.part.vessel) {
      return CannotLinkToTheSameVesselMsg;
    }
    if (linkMode == LinkMode.TiePartsOnSameVessel && part.vessel != target.part.vessel) {
      return CannotLinkDifferentVesselsMsg;
    }
    if (!linkStateMachine.CheckCanSwitchTo(LinkState.Linked)) {
      return SourceIsNotAvailableForLinkMsg;
    }
    if (target.linkState != LinkState.AcceptingLinks) {
      return TargetDoesntAcceptLinksMsg;
    }
    return null;
  }

  /// <summary>Checks if joint module would allow linking with the specified transform.</summary>
  /// <param name="targetTransform">Target transform of the link being checked.</param>
  /// <returns>An error message if link cannot be established or <c>null</c> otherwise.</returns>
  protected string CheckJointLimits(Transform targetTransform) {
    return
        linkJoint.CheckLengthLimit(this, targetTransform)
        ?? linkJoint.CheckAngleLimitAtSource(this, targetTransform)
        ?? linkJoint.CheckAngleLimitAtTarget(this, targetTransform);
  }
  #endregion

  #region KASEvents listeners
  /// <summary>Sets rejecting state when some other source has started connection mode.</summary>
  /// <remarks>It's only listened in state <see cref="LinkState.Available"/>.
  /// <para>Event handler for <see cref="KASEvents.OnStopLinking"/>.</para>
  /// </remarks>
  /// <param name="source">Source module that started connecting mode.</param>
  void OnStartLinkingKASEvent(ILinkSource source) {
    linkState = LinkState.RejectingLinks;
  }

  /// <summary>Restores available state when connection mode is over.</summary>
  /// <remarks>It's only listened in state <see cref="LinkState.RejectingLinks"/>.  
  /// <para>Event handler for <see cref="KASEvents.OnStopLinking"/>.</para>
  /// </remarks>
  /// <param name="source">Source module that started the mode.</param>
  void OnStopLinkingKASEvent(ILinkSource source) {
    linkState = LinkState.Available;
  }

  /// <summary>Establishes a link if target has accepted connection from this source.</summary>
  /// <remarks>
  /// Any problems that prevent from a successful creation will be logged to the user. The accepting
  /// party must ensure the link can be done.
  /// </remarks>
  /// <param name="target">Target that has accepted connetion.</param>
  void OnLinkActionAcceptedKASEvent(ILinkTarget target) {
    if (CheckCanLinkTo(target, reportToGUI: true)) {
      LinkToTarget(target);
    }
  }
  #endregion 

  /// <summary>
  /// Undocks source part from its probable target. Used to fix state when target cannot be resolved
  /// from the config or it failed to properly re-link with source.
  /// </summary>
  void UndockFromBadTarget() {
    if (attachNode == null || attachNode.attachedPart == null) {
      HostedDebugLog.Warning(this, "Cannot decouple because the target candidate is not found.");
      return;
    }
    Part partToDecouple;
    if (part.parent == attachNode.attachedPart) {
      partToDecouple = part;
    } else if (attachNode.attachedPart.parent == part) {
      partToDecouple = attachNode.attachedPart;
    } else {
      HostedDebugLog.Error(this, "Unexpected setup of еру attach node");
      return;
    }
    HostedDebugLog.Warning(this, "Decouple {0} from {1} since the link state cannot be restored.",
                           partToDecouple, partToDecouple.parent);
    partToDecouple.decouple();
  }

  /// <summary>
  /// Waits till the physics easement logic warmed up and disables the collision between the source
  /// and target parts.
  /// </summary>
  /// <remarks>Needed when the source and target parts belong to different vessels.</remarks>
  /// <seealso cref="linkTarget"/>
  /// <seealso href="http://ihsoft.github.io/KSPDev/Utils/html/M_KSPDev_ModelUtils_Colliders_SetCollisionIgnores_1.htm">
  /// KSPDev Utils: Colliders.SetCollisionIgnores
  /// </seealso>
  IEnumerator WaitAndSetCollisionIgnores() {
    // Copied from KervalEVA.OnVesselGoOffRails() method.
    // There must be at least 3 fixed frames.
    yield return new WaitForFixedUpdate();
    yield return new WaitForFixedUpdate();
    yield return new WaitForFixedUpdate();
    if (isLinked) {  // Link may get broken during the physics easyment.
      CollisionManager.IgnoreCollidersOnVessel(
          linkTarget.part.vessel, part.GetComponentInChildren<Collider>());
    }
  }
}

}  // namespace
