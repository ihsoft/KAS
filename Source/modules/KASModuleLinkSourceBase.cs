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
    IModuleInfo,
    // KAS interfaces.
    ILinkSource, ILinkStateEventListener,
    // KSPDev syntax sugar interfaces.
    IPartModule, IsPackable, IsDestroyable, IsPartDeathListener, IKSPDevModuleInfo {

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
          // Set ignores on the new target part. It takes some time for the vessel to settle down.
          // To be on a safe side, disable the physical effects of the colliders. In the game's core
          // it's hardcoded to wait for 3 fixed frames before kicking in the physics. So we wait 6!
          var colliders = gameObject.GetComponentsInChildren<Collider>()
              .Where(c => !c.isTrigger)
              .ToList();
          colliders.ForEach(x => x.isTrigger = true);
          AsyncCall.WaitForPhysics(
              this, 6, () => false,  // Use all the frames for the waiting.
              failure: () => {
                colliders.ForEach(c => c.isTrigger = false);
                Colliders.SetCollisionIgnores(part, value.part, true);
              });
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
    private set {
      var oldState = linkStateMachine.currentState;
      linkStateMachine.currentState = value;
      persistedLinkState = value;
      OnStateChange(oldState);
    }
  }

  /// <inheritdoc/>
  public bool isLocked {
    get { return linkState == LinkState.Locked; }
    set {
      // Don't trigger state change events when the value hasn't changed.
      if (value != (linkState == LinkState.Locked)) {
        linkState = value ? LinkState.Locked : LinkState.Available;
      }
    }
  }

  /// <inheritdoc/>
  public Transform nodeTransform { get; private set; }

  /// <inheritdoc/>
  public Transform physicalAnchorTransform { get; private set; }

  /// <inheritdoc/>
  public GUILinkMode guiLinkMode { get; private set; }

  /// <inheritdoc/>
  public LinkActorType linkActor { get; private set; }

  /// <inheritdoc/>
  public Vector3 targetPhysicalAnchor {
    get { return physicalAnchorAtTarget; }
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
  public LinkMode linkMode = LinkMode.TieAnyParts;

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

  /// <summary>See <see cref="physicalAnchorTransform"/>.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Vector3 physicalAnchorAtSource;

  /// <summary>See <see cref="targetPhysicalAnchor"/>.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Vector3 physicalAnchorAtTarget;

  /// <summary>Name of the joint to use with this source.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string jointName = "";
  #endregion

  #region Inheritable properties
  /// <summary>Joint module that manages a physical link.</summary>
  /// <value>The physical joint module on the part.</value>
  protected ILinkJointBase linkJoint { get; private set; }

  /// <summary>Renderer of the link meshes.</summary>
  /// <value>A renderer module with name <see cref="cfgLinkRendererName"/>.</value>
  protected ILinkRenderer linkRenderer { get; private set; }

  /// <summary>Tells if this source is currectly linked with a target.</summary>
  /// <value>The current state of the link.</value>
  protected bool isLinked {
    get { return linkState == LinkState.Linked; }
  }

  /// <summary>
  /// State machine that controls the source state tranistions and defines the reaction on these
  /// changes.
  /// </summary>
  /// <remarks>
  /// When the state is restored form a config file, it can be set to any arbitrary value. To
  /// properly handle it, the state transition handlers behavior must be consistent:  
  /// <list type="bullet">
  /// <item>
  /// Define the "default" state which is set on the module in the <c>OnAwake</c> method.
  /// </item>
  /// <item>
  /// In the <c>enterState</c> handlers assume the current state is the "default", and do <i>all</i>
  /// the adjustment to set the new state. Do <i>not</i> assume the transition has happen from
  /// another known state.
  /// </item>
  /// <item>
  /// In the <c>leaveState</c> handlers reset all the settings to bring the module back to the
  /// "default" state. Don't leave anything behind!
  /// </item>
  /// </list>
  /// </remarks>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@anme='T:KSPDev.ProcessingUtils.SimpleStateMachine_1']/*"/>
  protected SimpleStateMachine<LinkState> linkStateMachine { get; private set; }
  #endregion

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    linkStateMachine = new SimpleStateMachine<LinkState>(strict: true);
    linkStateMachine.onAfterTransition +=
        (start, end) => HostedDebugLog.Fine(this, "Link state changed: {0} => {1}", start, end);
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
    linkStateMachine.AddStateHandlers(
        LinkState.Linked,
        enterHandler: x => GameEvents.onVesselWillDestroy.Add(OnVesselWillDestroyGameEvent),
        leaveHandler: x => GameEvents.onVesselWillDestroy.Remove(OnVesselWillDestroyGameEvent));
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

    linkJoint = part.FindModulesImplementing<ILinkJointBase>()
        .FirstOrDefault(x => x.cfgJointName == jointName);
    if (linkJoint == null) {
      HostedDebugLog.Error(this, "KAS part misses a joint module. It won't work properly");
    }
    linkRenderer = part.FindModulesImplementing<ILinkRenderer>()
        .FirstOrDefault(x => x.cfgRendererName == linkRendererName);
    if (linkRenderer == null) {
      HostedDebugLog.Error(this, "KAS part misses a renderer module. It won't work properly");
    }
  }

  /// <inheritdoc/>
  public override void OnStartFinished(PartModule.StartState state) {
    if (persistedLinkState == LinkState.Linked) {
      RestoreTarget();
    }
    linkStateMachine.currentState = persistedLinkState;
    linkState = linkState;  // Trigger state updates.
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
      HostedDebugLog.Fine(this, "Create attach node transform {0}: pos={1}, euler={2}",
                          nodeTransform,
                          DbgFormatter.Vector(nodeTransform.localPosition),
                          DbgFormatter.Vector(nodeTransform.localRotation.eulerAngles));
    } else {
      HostedDebugLog.Fine(this, "Use attach node transform {0}: pos={1}, euler={2}",
                          nodeTransform,
                          DbgFormatter.Vector(nodeTransform.localPosition),
                          DbgFormatter.Vector(nodeTransform.localRotation.eulerAngles));
    }

    // Create physical anchor node transform. It will become a part of the model.
    const string anchorName = "physicalAnchor";
    physicalAnchorTransform = nodeTransform.FindChild(anchorName);
    if (physicalAnchorTransform == null) {
      physicalAnchorTransform = new GameObject(anchorName).transform;
      Hierarchy.MoveToParent(
          physicalAnchorTransform, nodeTransform, newPosition: physicalAnchorAtSource);
    }
  }
  #endregion

  #region IsPackable implementation
  /// <inheritdoc/>
  public virtual void OnPartUnpack() {
    if (isLinked) {
      linkJoint.CreateJoint(this, linkTarget);
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
      HostedDebugLog.Info(this, "Part has died. Drop the link to: {0}", linkTarget);
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
    if (linkState != LinkState.Available) {
      HostedDebugLog.Warning(this, "Cannot start linking mode is state: {0}", linkState);
      return false;
    }
    linkState = LinkState.Linking;
    StartLinkGUIMode(mode, actor);
    return true;
  }

  /// <inheritdoc/>
  public virtual void CancelLinking() {
    if (linkState != LinkState.Linking) {
      HostedDebugLog.Fine(this, "Ignore linking mode cancel in state: {0}", linkState);
      return;
    }
    StopLinkGUIMode();
    linkState = LinkState.Available;
  }

  /// <inheritdoc/>
  public virtual bool LinkToTarget(ILinkTarget target) {
    if (linkState != LinkState.Linking) {
      HostedDebugLog.Error(this, "Cannot link in state: {0}", linkState);
      return false;
    }
    if (!CheckCanLinkTo(target)) {
      return false;
    }
    LogicalLink(target);
    linkJoint.CreateJoint(this, target);
    // When GUI linking mode is stopped, all the targets stop accepting the link requests.
    // I.e. the mode must not be stopped before the link is created.
    StopLinkGUIMode();
    return true;
  }

  /// <inheritdoc/>
  public virtual void BreakCurrentLink(LinkActorType actorType, bool moveFocusOnTarget = false) {
    if (!isLinked) {
      HostedDebugLog.Error(this, "Cannot break link in state: {0}", linkState);
      return;
    }
    var targetRootPart = linkTarget.part;
    linkJoint.DropJoint();
    LogicalUnlink(actorType);
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
    var errors = new[] {
        CheckBasicLinkConditions(target),
        linkRenderer.CheckColliderHits(nodeTransform, target.nodeTransform),
    };
    errors = errors
        .Where(x => x != null)
        .Concat(linkJoint.CheckConstraints(this, target.nodeTransform))
        .ToArray();
    if (errors.Length > 0) {
      if (reportToGUI || reportToLog) {
        HostedDebugLog.Warning(
            this, "Cannot link a part of type={0} with: part={1}, type={2}, errors={3}",
            cfgLinkType, target.part, target.cfgLinkType, DbgFormatter.C2S(errors));
      }
      if (reportToGUI) {
        ScreenMessaging.ShowScreenMessage(
            ScreenMessageStyle.UPPER_CENTER,
            ScreenMessaging.DefaultMessageTimeout,
            ScreenMessaging.ErrorColor,
            DbgFormatter.C2S(errors, separator: "\n"));
      }
    }
    return errors.Length == 0;
  }
  #endregion

  #region ILinkStateEventListener implementation
  /// <inheritdoc/>
  public virtual void OnKASLinkCreatedEvent(KASEvents.LinkEvent info) {
    // Lock this source if another source on the part has made the link.
    if (!isLocked && !ReferenceEquals(info.source, this)) {
      isLocked = true;
    }
  }

  /// <inheritdoc/>
  public virtual void OnKASLinkBrokenEvent(KASEvents.LinkEvent info) {
    // Unlock this source if link with the another source one the part has broke.
    if (isLocked && !ReferenceEquals(info.source, this)) {
      isLocked = false;
    }
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

  /// <summary>Logically links the source and the target, and starts the renderer.</summary>
  /// <remarks>It's always called <i>before</i> the physical link updates.</remarks>
  /// <param name="target">The target to link with.</param>
  protected virtual void LogicalLink(ILinkTarget target) {
    HostedDebugLog.Info(this, "Linking to target: {0}, actor={1}", target, linkActor);
    var linkInfo = new KASEvents.LinkEvent(this, target, linkActor);
    linkTarget = target;
    linkTarget.linkSource = this;
    linkState = LinkState.Linked;
    linkRenderer.StartRenderer(physicalAnchorTransform, linkTarget.physicalAnchorTransform);
    KASEvents.OnLinkCreated.Fire(linkInfo);
    part.FindModulesImplementing<ILinkStateEventListener>()
        .ForEach(x => x.OnKASLinkCreatedEvent(linkInfo));
  }

  /// <summary>
  /// Logically unlinks the source and the current target, and stops the renderer.
  /// </summary>
  /// <remarks>It's always called <i>after</i> the physical link updates.</remarks>
  /// <param name="actorType">The actor which has intiated the unlinking.</param>
  protected virtual void LogicalUnlink(LinkActorType actorType) {
    HostedDebugLog.Info(this, "Unlinking from target: {0}, actor={1}", linkTarget, actorType);
    var linkInfo = new KASEvents.LinkEvent(this, linkTarget, actorType);
    if (linkTarget != null) {
      linkTarget.linkSource = null;
      linkTarget = null;
    }
    linkState = LinkState.Available;
    linkRenderer.StopRenderer();
    KASEvents.OnLinkBroken.Fire(linkInfo);
    part.FindModulesImplementing<ILinkStateEventListener>()
        .ForEach(x => x.OnKASLinkBrokenEvent(linkInfo));
  }

  /// <summary>Finds linked target for the source, and updates the related states.</summary>
  /// <remarks>
  /// This method is only called if the part is linked. It's called ater all the modules in the
  /// scene have properly set up. However, the other links at this point may not have the link state
  /// set yet.
  /// </remarks>
  protected virtual void RestoreTarget() {
    linkTarget = KASAPI.LinkUtils.FindLinkTargetFromSource(this);
    if (linkTarget != null) {
      HostedDebugLog.Fine(this, "Restored link to: {0}", linkTarget);
      linkRenderer.StartRenderer(physicalAnchorTransform, linkTarget.physicalAnchorTransform);
    } else {
      ScreenMessaging.ShowErrorScreenMessage(CannotRestoreLinkMsg.Format(part.name));
      HostedDebugLog.Error(
          this, "Source cannot restore link to target part id={0} on the attach node {1}",
          persistedLinkTargetPartId, attachNodeName);
      persistedLinkState = LinkState.Available;
    }
  }
  #endregion

  #region New utility methods
  /// <summary>
  /// Performs a check to ensure that the link between the source and the target, if it's made, will
  /// be consistent.
  /// </summary>
  /// <remarks>This method must pass for both started and not started linking mode.</remarks>
  /// <param name="target">Target of the pipe to check link with.</param>
  /// <returns>An error message if link cannot be established or <c>null</c> otherwise.</returns>
  protected string CheckBasicLinkConditions(ILinkTarget target) {
    if (linkState != LinkState.Available && linkState != LinkState.Linking || isLocked) {
      return SourceIsNotAvailableForLinkMsg;
    }
    if (linkState == LinkState.Available && target.linkState != LinkState.Available
        || linkState == LinkState.Linking && target.linkState != LinkState.AcceptingLinks
        || target.isLocked) {
      return TargetDoesntAcceptLinksMsg;
    }
    if (cfgLinkType != target.cfgLinkType) {
      return IncompatibleTargetLinkTypeMsg;
    }
    if (linkMode == LinkMode.TiePartsOnDifferentVessels
        && vessel == target.part.vessel) {
      return CannotLinkToTheSameVesselMsg;
    }
    if (linkMode == LinkMode.TiePartsOnSameVessel && vessel != target.part.vessel) {
      return CannotLinkDifferentVesselsMsg;
    }
    return null;
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

  #region Local untility methods
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
          linkTarget.part.vessel, part.GetComponentsInChildren<Collider>());
    }
  }

  /// <summary>Reacts on the vessel destruction and break the link if needed.</summary>
  /// <remarks>
  /// This event is pretty rare. It happens when the vessel needs to be destroyed in a non-physical
  /// way. E.g. when an EVA kerbal boards the pod, he just "disappears" from the scene. By contrast,
  /// the docked vessel is removed only logically, all its parts remain alive in the physical world.
  /// </remarks>
  /// <param name="targetVessel">The vessel that is being destroyed.</param>
  void OnVesselWillDestroyGameEvent(Vessel targetVessel) {
    if (isLinked && vessel != linkTarget.part.vessel
        && (targetVessel == vessel || targetVessel == linkTarget.part.vessel)) {
      HostedDebugLog.Info(
          this, "Drop the link due to the peer vessel destruction: {0}", targetVessel);
      BreakCurrentLink(LinkActorType.Physics);
    }
  }
  #endregion
}

}  // namespace
