// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using KSPDev.ProcessingUtils;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

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
// Next localization ID: #kasLOC_02011.
// TODO(ihsoft): Handle KIS actions.
// TODO(ihsoft): Handle part destroyed action.
// TODO(ihsoft): Handle part staged action.
public class KASModuleLinkSourceBase : AbstractLinkPeer,
    // KSP interfaces.
    IModuleInfo,
    // KAS interfaces.
    ILinkSource,
    // KSPDev syntax sugar interfaces.
    IPartModule, IsPartDeathListener, IKSPDevModuleInfo {

  #region Localizable GUI strings
  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  readonly static Message IncompatibleTargetLinkTypeMsg = new Message(
      "#kasLOC_02000",
      defaultTemplate: "Incompatible target link type",
      description: "Message to display when the target link type doesn't match the source type.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  readonly static Message CannotLinkToTheSameVesselMsg = new Message(
      "#kasLOC_02001",
      defaultTemplate:  "Cannot link to the same vessel",
      description: "Message to display when the link mode requires the target to belong to a"
      + " different vessel but it belongs to the same vessel as the source.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  readonly static Message CannotLinkDifferentVesselsMsg = new Message(
      "#kasLOC_02002",
      defaultTemplate: "Cannot link different vessels",
      description: "Message to display when the link mode requires the target to belong to the same"
      + " vessel but it belongs to a different vessel.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  readonly static Message SourceIsNotAvailableForLinkMsg = new Message(
      "#kasLOC_02003",
      defaultTemplate: "Source is not available for a link",
      description: "Message to display when a source is refusing to start the link.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  readonly static Message TargetDoesntAcceptLinksMsg = new Message(
      "#kasLOC_02004",
      defaultTemplate: "Target doesn't accept links",
      description: "Message to display when the target is refusing to accept the link.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  readonly static Message<string> CannotRestoreLinkMsg = new Message<string>(
      "#kasLOC_02005",
      defaultTemplate: "Cannot restore link for: <<1>>",
      description: "Message to display when a linked source and target cannot be matched on load."
      + "\nArgument <<1>> is a name of the SOURCE part.",
      example: "Cannot restore link for: KAS.TJ1");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  readonly static Message<string> LinksWithSocketTypeInfo = new Message<string>(
      "#kasLOC_02006",
      defaultTemplate: "Links with socket type: <<1>>",
      description: "Info string in the editor for the link type setting."
      + "\nArgument <<1>> is the type string from the part's config.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  readonly static Message ModuleTitleInfo = new Message(
      "#kasLOC_02007",
      defaultTemplate: "KAS Joint Source",
      description: "Title of the module to present in the editor details window.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  readonly static Message LinkModeTiePartsOnDifferentVesselsInfo = new Message(
      "#kasLOC_02009",
      defaultTemplate: "Links to another vessel",
      description: "Info string in the editor that tells if the part acn establish a link to"
      + " another vessel without docking.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  readonly static Message LinkModeTiePartsOnSameVesselInfo = new Message(
      "#kasLOC_02010",
      defaultTemplate: "Links to the same vessel",
      description: "Info string in the editor that tells if the part can establish a link to"
      + " another part of the same vessel,");
  #endregion

  #region ILinkSource config properties implementation
  /// <inheritdoc/>
  public LinkMode cfgLinkMode { get { return linkMode; } }
  #endregion

  #region ILinkSource properties implementation
  /// <inheritdoc/>
  public ILinkTarget linkTarget {
    get { return otherPeer as ILinkTarget; }
  }

  /// <inheritdoc/>
  /// <seealso cref="jointName"/>
  public ILinkJoint linkJoint { get; private set; }

  /// <inheritdoc/>
  /// <seealso cref="linkRendererName"/>
  public ILinkRenderer linkRenderer { get; private set; }
  #endregion

  #region Part's config fields
  /// <summary>See <see cref="cfgLinkMode"/>.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public LinkMode linkMode = LinkMode.TieAnyParts;

  /// <summary>Name of the renderer that draws the link.</summary>
  /// <value>Arbitrary string. Can be empty.</value>
  /// <remarks>
  /// The source will find a renderer module using this name as a key. It will be used to draw the
  /// link when connected to the target. The behavior is undefined if there is no renderer found on
  /// the part.
  /// </remarks>
  /// <seealso cref="ILinkRenderer.cfgRendererName"/>
  /// <seealso cref="linkRenderer"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ILinkSourceExample_linkRenderer"/></example>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string linkRendererName = "";

  /// <summary>Name of the joint to use with this source.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string jointName = "";
  #endregion

  #region Inheritable fields & properties
  /// <summary>Mode in which a link between the source and target is being created.</summary>
  /// <remarks>
  /// The mode is set right before going into state <seealso cref="LinkState.Linking"/> , and it's
  /// not cleared until the next linking action. It's not persisted, so it's only valid to check
  /// this value during the linking session.
  /// </remarks>
  /// <value>The last used GUI mode.</value>
  /// <seealso cref="StartLinking"/>
  /// <seealso cref="ILinkPeer.linkState"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectParts"/></example>
  protected GUILinkMode guiLinkMode { get; private set; }

  /// <summary>Actor, who has initiated the link.</summary>
  /// <remarks>
  /// The actor is set right before going into state <seealso cref="LinkState.Linking"/> , and it's
  /// not cleared until the next linking action. It's not persisted, so it's only valid to check
  /// this value during the linking session.
  /// </remarks>
  /// <value>The last used actor.</value>
  /// <seealso cref="StartLinking"/>
  /// <seealso cref="ILinkPeer.linkState"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectParts"/></example>
  protected LinkActorType linkActor { get; private set; }
  #endregion

  #region AbstractLinkPeer overrides
  /// <inheritdoc/>
  protected override void SetupStateMachine() {
    base.SetupStateMachine();
    linkStateMachine.onAfterTransition += (start, end) => HostedDebugLog.Fine(
        this, "Source state changed at {0}: {1} => {2}", attachNodeName, start, end);
    linkStateMachine.SetTransitionConstraint(
        LinkState.Available,
        new[] {LinkState.Linking, LinkState.RejectingLinks, LinkState.NodeIsBlocked});
    linkStateMachine.SetTransitionConstraint(
        LinkState.NodeIsBlocked,
        new[] {LinkState.Available});
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
        LinkState.Linked,
        enterHandler: x => GameEvents.onVesselWillDestroy.Add(OnVesselWillDestroyGameEvent),
        leaveHandler: x => GameEvents.onVesselWillDestroy.Remove(OnVesselWillDestroyGameEvent));
    linkStateMachine.AddStateHandlers(
        LinkState.Linking,
        enterHandler: x => KASEvents.OnStartLinking.Fire(this),
        leaveHandler: x => KASEvents.OnStopLinking.Fire(this));
  }

  /// <inheritdoc/>
  protected override void RestoreOtherPeer() {
    base.RestoreOtherPeer();
    if (linkTarget != null) {
      linkRenderer.StartRenderer(nodeTransform, linkTarget.nodeTransform);
    } else {
      ShowStatusMessage(CannotRestoreLinkMsg.Format(part.name), isError: true);
    }
  }

  /// <inheritdoc/>
  public override void OnInitialize() {
    base.OnInitialize();
    if (isLinked && linkTarget.part.vessel != vessel) {
      // When the target is at the different vessel, there is no automatic collision ignore set.
      AsyncCall.CallOnFixedUpdate(this, () => {
        // Copied from KervalEVA.OnVesselGoOffRails() method.
        // There must be a delay for at least 3 fixed frames.
        if (isLinked) {  // Link may get broken during the physics easyment.
          CollisionManager.IgnoreCollidersOnVessel(
              linkTarget.part.vessel, part.GetComponentsInChildren<Collider>());
        }
      }, skipFrames: 3);
    }
  }

  /// <inheritdoc/>
  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);

    linkJoint = part.Modules.OfType<ILinkJoint>()
        .FirstOrDefault(x => x.cfgJointName == jointName);
    if (linkJoint == null) {
      HostedDebugLog.Error(this, "KAS part misses a joint module. It won't work properly");
    }
    linkRenderer = part.Modules.OfType<ILinkRenderer>()
        .FirstOrDefault(x => x.cfgRendererName == linkRendererName);
    if (linkRenderer == null) {
      HostedDebugLog.Error(this, "KAS part misses a renderer module. It won't work properly");
    }
  }

  /// <inheritdoc/>
  protected override void CheckAttachNode() {
    base.CheckAttachNode();
    if (linkState == LinkState.Available && parsedAttachNode.attachedPart != null) {
      var target = parsedAttachNode.attachedPart.Modules
          .OfType<ILinkTarget>()
          .FirstOrDefault(t => t.coupleNode != null && t.coupleNode.attachedPart == part
                               && CheckCanLinkTo(t));
      if (target != null) {
        HostedDebugLog.Fine(this, "Linking with the preattached part: {0}", target);
        LinkToTarget(LinkActorType.API, target);
      }
      if (!isLinked) {
        HostedDebugLog.Warning(this, "Cannot link to the preattached part via {0}",
                               KASAPI.AttachNodesUtils.NodeId(parsedAttachNode.FindOpposingNode()));
        isNodeBlocked = true;
      }
    } else if (linkState == LinkState.NodeIsBlocked && parsedAttachNode.attachedPart == null) {
      isNodeBlocked = false;
    }
    
    // Restore the link state if not yet done.
    if (isLinked && !linkJoint.isLinked) {
      linkJoint.CreateJoint(this, linkTarget);
    }
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
    if (!linkStateMachine.CheckCanSwitchTo(LinkState.Linking)) {
      if (actor == LinkActorType.Player) {
        ShowStatusMessage(SourceIsNotAvailableForLinkMsg, isError: true);
      }
      HostedDebugLog.Warning(this, "Cannot start linking mode in state: {0}", linkState);
      return false;
    }
    guiLinkMode = mode;
    linkActor = actor;
    linkState = LinkState.Linking;
    return true;
  }

  /// <inheritdoc/>
  public virtual void CancelLinking() {
    if (!linkStateMachine.CheckCanSwitchTo(LinkState.Available)) {
      HostedDebugLog.Fine(this, "Ignore linking mode cancel in state: {0}", linkState);
      return;
    }
    linkState = LinkState.Available;
  }

  /// <inheritdoc/>
  public virtual bool LinkToTarget(ILinkTarget target) {
    if (!linkStateMachine.CheckCanSwitchTo(LinkState.Linked)) {
      if (linkActor == LinkActorType.Player) {
        ShowStatusMessage(SourceIsNotAvailableForLinkMsg, isError: true);
      }
      HostedDebugLog.Error(this, "Cannot link in state: {0}", linkState);
      return false;
    }
    if (!CheckCanLinkTo(target, reportToGUI: linkActor == LinkActorType.Player)) {
      return false;
    }
    LogicalLink(target);
    PhysicaLink();
    return true;
  }

  /// <inheritdoc/>
  public virtual bool LinkToTarget(LinkActorType actor, ILinkTarget target) {
    if (StartLinking(GUILinkMode.API, actor)) {
      if (LinkToTarget(target)) {
        return true;
      }
      CancelLinking();
    }
    return false;
  }

  /// <inheritdoc/>
  public virtual void BreakCurrentLink(LinkActorType actorType, bool moveFocusOnTarget = false) {
    if (!isLinked) {
      HostedDebugLog.Error(this, "Cannot break link in state: {0}", linkState);
      return;
    }
    var targetRootPart = linkTarget.part;
    PhysicaUnlink();
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
  public virtual bool CheckCanLinkTo(ILinkTarget target,
                                     bool checkStates = true,
                                     bool reportToGUI = false, bool reportToLog = true) {
    var errors = new string[]{ };
    errors = errors
        .Concat(CheckBasicLinkConditions(target, checkStates))
        .Concat(linkRenderer.CheckColliderHits(nodeTransform, target.nodeTransform))
        .Concat(linkJoint.CheckConstraints(this, target))
        .ToArray();
    if (errors.Length > 0) {
      if (reportToGUI || reportToLog) {
        HostedDebugLog.Warning(
            this, "Cannot link a part of type={0} with: part={1}, type={2}, errors={3}",
            cfgLinkType, target.part, target.cfgLinkType, DbgFormatter.C2S(errors));
      }
      if (reportToGUI) {
        ShowStatusMessage(DbgFormatter.C2S(errors, separator: "\n"), isError: true);
      }
    }
    return errors.Length == 0;
  }
  #endregion

  #region Inheritable methods
  /// <summary>Logically links the source and the target, and starts the renderer.</summary>
  /// <remarks>It's always called <i>before</i> the physical link updates.</remarks>
  /// <param name="target">The target to link with.</param>
  protected virtual void LogicalLink(ILinkTarget target) {
    HostedDebugLog.Info(this, "Linking to target: {0}, actor={1}", target, linkActor);
    var linkInfo = new KASEvents.LinkEvent(this, target, linkActor);
    otherPeer = target;
    linkTarget.linkSource = this;
    linkState = LinkState.Linked;
    linkRenderer.StartRenderer(nodeTransform, linkTarget.nodeTransform);
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
    linkActor = actorType;
    var linkInfo = new KASEvents.LinkEvent(this, linkTarget, actorType);
    linkRenderer.StopRenderer();
    linkState = LinkState.Available;
    if (linkTarget != null) {
      linkTarget.linkSource = null;
      otherPeer = null;
    }
    linkActor = LinkActorType.None;
    KASEvents.OnLinkBroken.Fire(linkInfo);
    part.FindModulesImplementing<ILinkStateEventListener>()
        .ForEach(x => x.OnKASLinkBrokenEvent(linkInfo));
  }

  /// <summary>Creates a physical link between the parts.</summary>
  /// <remarks>It's called after the logical link is established.</remarks>
  protected virtual void PhysicaLink() {
    linkJoint.CreateJoint(this, linkTarget);
  }

  /// <summary>Destroys the physical link between the parts.</summary>
  /// <remarks>It's called before the logical link is dropped.</remarks>
  protected virtual void PhysicaUnlink() {
    linkJoint.DropJoint();
  }

  /// <summary>
  /// Performs a check to ensure that the link between the source and the target, if it's made, will
  /// be consistent.
  /// </summary>
  /// <remarks>
  /// This method must pass for both started and not started linking mode even when the state
  /// checking is requested.
  /// </remarks>
  /// <param name="target">The target of the pipe to check link with.</param>
  /// <param name="checkStates">Tells if the source and target states need to be validated.</param>
  /// <returns>
  /// An empty array if the link can be created, or a list of user friendly errors otherwise.
  /// </returns>
  protected virtual string[] CheckBasicLinkConditions(ILinkTarget target, bool checkStates) {
    var errors = new List<string>();
    if (checkStates) {
      if (linkState != LinkState.Available && linkState != LinkState.Linking || isLocked) {
        errors.Add(SourceIsNotAvailableForLinkMsg);
      }
      if (linkState == LinkState.Available && target.linkState != LinkState.Available
          || linkState == LinkState.Linking && target.linkState != LinkState.AcceptingLinks
          || target.isLocked) {
        errors.Add(TargetDoesntAcceptLinksMsg);
      }
    }
    if (cfgLinkType != target.cfgLinkType) {
      errors.Add(IncompatibleTargetLinkTypeMsg);
    }
    if (linkMode == LinkMode.TiePartsOnDifferentVessels
        && vessel == target.part.vessel) {
      errors.Add(CannotLinkToTheSameVesselMsg);
    }
    if (linkMode == LinkMode.TiePartsOnSameVessel && vessel != target.part.vessel) {
      errors.Add(CannotLinkDifferentVesselsMsg);
    }
    return errors.ToArray();
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
  #endregion 

  #region Local untility methods
  /// <summary>Reacts on the vessel destruction and break the link if needed.</summary>
  /// <remarks>This event can get called from the physics callbacks.</remarks>
  /// <param name="targetVessel">The vessel that is being destroyed.</param>
  void OnVesselWillDestroyGameEvent(Vessel targetVessel) {
    AsyncCall.CallOnEndOfFrame(this, () => {
      if (isLinked && vessel != linkTarget.part.vessel
          && (targetVessel == vessel || targetVessel == linkTarget.part.vessel)) {
        HostedDebugLog.Info(
            this, "Drop the link due to the peer vessel destruction: {0}", targetVessel);
        BreakCurrentLink(LinkActorType.Physics);
      }
    });
  }
  #endregion
}

}  // namespace
