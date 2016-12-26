// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
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
/// TODO(ihsoft): Handle KIS actions.
/// TODO(ihsoft): Handle part destroyed action.
/// TODO(ihsoft): Handle part staged action.
public class KASModuleLinkSourceBase :
    // KSP parents.
    PartModule, IModuleInfo, IActivateOnDecouple,
    // KAS parents.
    ILinkSource, ILinkStateEventListener,
    // Syntax sugar parents.
    IPartModule, IJointEventsListener,
    IsPackable, IsDestroyable, IKSPDevModuleInfo, IKSPActivateOnDecouple {

  #region Localizable GUI strings
  /// <summary>Message to display when target link type doesn't match source type.</summary>
  protected readonly static Message IncompatibleTargetLinkTypeMsg =
      "Incompatible target link type";
  /// <summary>
  /// Message to display when link mode requires different vessels but target belongs to the same
  /// vessel as the source.
  /// </summary>
  protected readonly static Message CannotLinkToTheSameVesselMsg =
      "Cannot link to the same vessel";
  /// <summary>
  /// Message to display when link mode requires parts to belong to the same vessel but source and
  /// target are onwed by different crafts.
  /// </summary>
  protected readonly static Message CannotLinkDifferentVesselsMsg =
      "Cannot link different vessels";
  /// <summary>Message to display when source refuses to start the link.</summary>
  protected readonly static Message SourceIsNotAvailableForLinkMsg =
      "Source is not available for link";
  /// <summary>Message to display when target refuses to accept the link.</summary>
  protected readonly static Message TargetDoesntAcceptLinksMsg =
      "Target doesn't accept links";
  /// <summary>Message to display when linked source and target cannot be matched on laod.</summary>
  protected readonly static Message<string> CannotRestoreLinkMsg =
      "Cannot restore link for: {0}";
  /// <summary>Info string in the editor for link type setting.</summary>
  protected readonly static Message<string> LinksWithSocketTypeInfo =
      "Links with socket type: {0}";
  /// <summary>Title of the module to present in the editor details window.</summary>
  protected readonly static Message ModuleTitleInfo =
      "KAS Joint Source";
  /// <summary>Info string in the editor to identify if part can link with another vessel.</summary>
  protected readonly static MessageEnumValue<LinkMode> LinkModeInfo =
      new MessageEnumValue<LinkMode>() {
        {LinkMode.DockVessels, "<color=#00ffff>Links to <b>another</b> vessel only</color>"},
        {LinkMode.TiePartsOnDifferentVessels, "<color=#00ffff>Links to <b>another</b> vessel only</color>"},
        {LinkMode.TiePartsOnSameVessel, "<color=#00ffff>Links to the <b>same</b> vessel only</color>"},
      };
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
      return linkStateMachine.isStarted ? linkStateMachine.currentState : persistedLinkState;
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
  public AttachNode attachNode { get; private set; }
  /// <inheritdoc/>
  public GUILinkMode guiLinkMode { get; private set; }
  #endregion

  #region Persistent fields
  /// <summary>Persistent config field. Source link state in the last save action.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field that is saved/restored with the vessel. It's
  /// handled by the KSP core and must <i>not</i> be altered directly. Moreover, in spite of it's
  /// declared <c>public</c> it must not be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField(isPersistant = true)]
  public LinkState persistedLinkState = LinkState.Available;

  /// <summary>Persistent config field. Target part flight ID.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field that is saved/restored with the vessel. It's
  /// handled by the KSP core and must <i>not</i> be altered directly. Moreover, in spite of it's
  /// declared <c>public</c> it must not be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField(isPersistant = true)]
  public uint persistedLinkTargetPartId;
  #endregion

  #region Part's config fields
  /// <summary>Config setting. See <see cref="cfgLinkType"/>.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public string linkType = "";

  /// <summary>Config setting. See <see cref="cfgLinkMode"/>.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public LinkMode linkMode = LinkMode.DockVessels;

  /// <summary>Config setting. See <see cref="cfgLinkRendererName"/>.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public string linkRendererName = "";

  /// <summary>Config setting. See <see cref="cfgAttachNodeName"/>.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public string attachNodeName = "";

  /// <summary>Config setting. Defines attach node position in the local units.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public Vector3 attachNodePosition = Vector3.zero;

  /// <summary>Config setting. Defines attach node orientation in the local units.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public Vector3 attachNodeOrientation = Vector3.up;
  #endregion

  #region Inheritable properties
  /// <summary>Joint module that manages source &lt;=&gt; target physical connection.</summary>
  /// <remarks>
  /// This module must always exist on the part. If there is no such module then on start a simple
  /// <see cref="KASModuleStockJoint"/> will be added with all the default settings. Proper part
  /// design must always specify a joint module (exactly one).
  /// </remarks>
  protected ILinkJoint linkJoint { get; private set; }
  /// <summary>Renderer of the link meshes. It cannot be <c>null</c>.</summary>
  /// <remarks>
  /// This module must always exist on the part. If there is no such module then on start a NO-OP
  /// renderer will be added. This renderer doesn't draw anything. Proper part design must always
  /// specify a renderer module that draws linked state.
  /// </remarks>
  /// <seealso cref="cfgLinkRendererName"/>
  protected ILinkRenderer linkRenderer { get; private set; }

  /// <summary>Timeout to show various onload errors. Seconds.</summary>
  protected const float BadLinkStatusTimeout = 10f;

  /// <summary>Tells if this source is currectly linked with a target.</summary>
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
  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);

    linkJoint = part.FindModuleImplementing<ILinkJoint>();
    linkRenderer = part.FindModulesImplementing<ILinkRenderer>()
        .FirstOrDefault(x => x.cfgRendererName == linkRendererName);
    if (linkJoint == null) {
      Debug.LogErrorFormat(
          "KAS part {0} misses joint module. It won't work properly", part.name);
    }
    if (linkRenderer == null) {
      Debug.LogErrorFormat(
          "KAS part {0} misses renderer module. It won't work properly", part.name);
    }

    // Try to restore link to the target and update module's state.
    if (persistedLinkState == LinkState.Linked) {
      if (linkMode == LinkMode.DockVessels || linkMode == LinkMode.TiePartsOnSameVessel) {
        RestoreTarget();  // Same vessel links can be restored right away.
      } else {
        // It's unknown in which order the vessels will load, so postpone the restoring process.
        AsyncCall.CallOnEndOfFrame(this, x => RestoreTarget());
      }

      if (linkMode == LinkMode.TiePartsOnDifferentVessels) {
        // It's unknown in which order the vessels will load, so postpone the restoring process.
        AsyncCall.CallOnEndOfFrame(this, x => RestoreTarget());
      } else {
        RestoreTarget();
      }
    } else {
      linkStateMachine.Start(persistedLinkState);
      linkState = linkState;  // Trigger state updates.
    }
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);

    // Create attach node transform. It will become a part of the model.
    if (HighLogic.LoadedScene == GameScenes.LOADING) {
      nodeTransform = new GameObject(attachNodeName + "-node").transform;
      nodeTransform.parent = Hierarchy.GetPartModelTransform(part);
      nodeTransform.localPosition = attachNodePosition;
      nodeTransform.localScale = Vector3.one;
      nodeTransform.localRotation = Quaternion.LookRotation(attachNodeOrientation);
    } else {
      nodeTransform = part.FindModelTransform(attachNodeName + "-node");
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
    if (linkState != LinkState.Linked) {
      return;
    }
    if (linkTarget == null) {
      LogicalUnlink(LinkActorType.None);
      ScreenMessaging.ShowErrorScreenMessage(CannotRestoreLinkMsg.Format(part.name));
      if (linkMode == LinkMode.DockVessels) {
        Debug.LogWarningFormat("Fix docking state for a bad link on part {0}...",
                               DbgFormatter.PartId(part));
        AsyncCall.CallOnEndOfFrame(this, x => UndockFromBadTarget());
      } else {
        Debug.LogWarningFormat(
            "Mark source part {0} as unlinked since link state cannot be restored.",
            DbgFormatter.PartId(part));
      }
    } else if (linkTarget.linkSource == null) {
      ScreenMessaging.ShowErrorScreenMessage(CannotRestoreLinkMsg.Format(part.name));
      Debug.LogWarningFormat(
          "Detach part {0} from target {1} since target failed to restore its state.",
          DbgFormatter.PartId(part), DbgFormatter.PartId(linkTarget.part));
      AsyncCall.CallOnEndOfFrame(this, x => BreakCurrentLink(LinkActorType.None));
    }
  }

  /// <inheritdoc/>
  public virtual void OnPartPack() {
  }
  #endregion

  #region IsDestroyable implementation
  /// <inheritdoc/>
  public virtual void OnDestroy() {
    linkStateMachine.Stop();
  }
  #endregion

  #region IModuleInfo implementation
  /// <inheritdoc/>
  public override string GetInfo() {
    var sb = new StringBuilder(base.GetInfo());
    sb.AppendLine(LinksWithSocketTypeInfo.Format(linkType));
    sb.AppendLine();
    sb.AppendLine(LinkModeInfo.Format(linkMode));
    return sb.ToString();
  }

  /// <inheritdoc/>
  public string GetModuleTitle() {
    return ModuleTitleInfo;
  }

  /// <inheritdoc/>
  public Callback<Rect> GetDrawModulePanelCallback() {
    return null;
  }

  /// <inheritdoc/>
  public string GetPrimaryField() {
    return null;
  }
  #endregion

  #region ILinkSource implementation
  /// <inheritdoc/>
  public virtual bool StartLinking(GUILinkMode mode) {
    if (!linkStateMachine.CheckCanSwitchTo(LinkState.Linking)) {
      Debug.LogWarningFormat("Cannot start linking mode in state: {0}", linkState);
      return false;
    }
    if (mode == GUILinkMode.Eva && !FlightGlobals.ActiveVessel.isEVA) {
      Debug.LogWarning("Cannot start EVA linking mode since active vessel is not EVA");
      return false;
    }
    linkState = LinkState.Linking;
    StartLinkGUIMode(mode);
    return true;
  }
  
  /// <inheritdoc/>
  public virtual void CancelLinking() {
    if (!linkStateMachine.CheckCanSwitchTo(LinkState.Available)) {
      Debug.LogWarningFormat("Cannot stop linking mode in state: {0}", linkState);
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
    var actorType = guiLinkMode == GUILinkMode.Eva || guiLinkMode == GUILinkMode.Interactive
        ? LinkActorType.Player
        : LinkActorType.API;
    LogicalLink(target, actorType);
    // When GUI linking mode is stopped all the targets stop accepting link requests. I.e. the mode
    // must not be stopped before the link is created.
    StopLinkGUIMode();
    return true;
  }

  /// <inheritdoc/>
  public virtual void BreakCurrentLink(LinkActorType actorType, bool moveFocusOnTarget = false) {
    if (linkState != LinkState.Linked) {
      Debug.LogWarningFormat(
          "Cannot break link: part {0} is not linked to anything", DbgFormatter.PartId(part));
      return;
    }
    // Logical unlink must be done first before doing actual decouple.
    var oldTarget = linkTarget;
    var targetRootPart = linkTarget.part;
    LogicalUnlink(actorType);
    PhysicalUnink(oldTarget);
    // If either source or target part after the separation belong to the active vessel then adjust
    // the focus. Otherwise, actor was external (e.g. EVA).
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
        Debug.LogWarningFormat("Cannot link part {0} (type={1}) and part {2} (type={3}): {4}",
                               DbgFormatter.PartId(part), cfgLinkType,
                               DbgFormatter.PartId(target.part), target.cfgLinkType,
                               errorMsg);
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

  #region IJointEventsListener implementation
  /// <inheritdoc/>
  public virtual void OnJointBreak(float breakForce) {
    if (linkState == LinkState.Linked) {
      LogicalUnlink(LinkActorType.Physics);
    }
  }
  #endregion

  #region IActivateOnDecouple implementation
  /// <inheritdoc/>
  public virtual void DecoupleAction(string nodeName, bool weDecouple) {
    if (nodeName == attachNodeName) {
      if (linkState == LinkState.Linked) {
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
  /// <summary>Triggers when state has been assigned with a value.</summary>
  /// <remarks>
  /// This method triggers even when new state doesn't differ from the old one. When it's important
  /// to catch the transition check for <paramref name="oldState"/>.
  /// </remarks>
  /// <param name="oldState">State prior to the change.</param>
  protected virtual void OnStateChange(LinkState oldState) {
    // Start renderer in a linked state with a valid target, and stop it in all other states.
    if (linkState == LinkState.Linked && !linkRenderer.isStarted && linkTarget != null) {
      linkRenderer.StartRenderer(nodeTransform, linkTarget.nodeTransform);
    }
    if (linkState != LinkState.Linked && linkRenderer.isStarted) {
      linkRenderer.StopRenderer();
    }
    // Create attach node for linking state t oallow coupling. Drop the node once linking mode is
    // over and link hasn't been established.
    if (linkState == LinkState.Linking && attachNode == null) {
      attachNode = KASAPI.AttachNodesUtils.CreateAttachNode(part, attachNodeName, nodeTransform);
    }
    if (oldState == LinkState.Linking && linkState != LinkState.Linked
        && attachNode != null) {
      KASAPI.AttachNodesUtils.DropAttachNode(part, attachNodeName);
      attachNode = null;
    }
  }

  /// <summary>Initiates GUI mode, and starts displaying linking process.</summary>
  /// <param name="mode">Mode to start with.</param>
  protected virtual void StartLinkGUIMode(GUILinkMode mode) {
    guiLinkMode = mode;
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
      Debug.LogFormat("Physically linking {0} to {1}",
                      DbgFormatter.PartId(part), DbgFormatter.PartId(target.part));
      KASAPI.LinkUtils.CoupleParts(attachNode, target.attachNode);
    }
  }

  /// <summary>Breaks link with the target in the physical world.</summary>
  /// <param name="target">Target to break physical link with.</param>
  protected virtual void PhysicalUnink(ILinkTarget target) {
    // FIXME: restore vessels names/types
    if (linkMode == LinkMode.DockVessels) {
      Debug.LogFormat("Physically unlinking {0} to {1}",
                      DbgFormatter.PartId(part), DbgFormatter.PartId(target.part));
      KASAPI.LinkUtils.DecoupleParts(part, target.part);
    }
  }

  /// <summary>Logically links source and target.</summary>
  /// <remarks>
  /// No actual joint or connection is created in the game. Though, this method is always called
  /// before any physics changes.
  /// </remarks>
  /// <param name="target">Target to link with.</param>
  /// <param name="actorType">Initator of the link.</param>
  /// <seealso cref="PhysicalLink"/>
  protected virtual void LogicalLink(ILinkTarget target, LinkActorType actorType) {
    var linkInfo = new KASEvents.LinkEvent(this, target, actorType);
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
      Debug.LogErrorFormat(
          "Source {0} cannot restore link to target part id={1} on attach node {2}",
          DbgFormatter.PartId(part), persistedLinkTargetPartId, attachNodeName);
    }
    linkStateMachine.Start(persistedLinkState);
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
      Debug.LogWarningFormat("Cannot decouple part {0} because target candidate is not found.",
                             DbgFormatter.PartId(part));
      return;
    }
    Part partToDecouple;
    if (part.parent == attachNode.attachedPart) {
      partToDecouple = part;
    } else if (attachNode.attachedPart.parent == part) {
      partToDecouple = attachNode.attachedPart;
    } else {
      Debug.LogErrorFormat(
          "Unexpected setup of attach node on part {0}", DbgFormatter.PartId(part));
      return;
    }
    Debug.LogWarningFormat(
        "Decouple part {0} from {1} since the link state cannot be restored.",
        DbgFormatter.PartId(partToDecouple), DbgFormatter.PartId(partToDecouple.parent));
    partToDecouple.decouple();
  }
}

}  // namespace
