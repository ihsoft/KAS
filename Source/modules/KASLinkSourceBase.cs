// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.DebugUtils;
using KSPDev.GUIUtils;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using KSPDev.ProcessingUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KAS {

/// <summary>Base link source module. Does all the job on making two parts linked.</summary>
/// <remarks>
/// This module deals with main logic of linking two parts together. The other party of the
/// link must be aware of the linking porcess. The targets must implement <see cref="ILinkTarget"/>.
/// <para>
/// External callers must access methods and properties declared in base classes or interfaces
/// only. Members and methods that are not part of these declarations are not intended for the
/// public use <b>regardless</b> to their visibility level.
/// </para>
/// <para>
/// Decendand classes may use any members and methods but good practice is restricting the usage to
/// the interfaces and virtuals only.
/// </para>
/// <para>
/// The descendants of this module can use the custom persistent fields of groups:
/// </para>
/// <list type="bullet">
/// <item><c>StdPersistentGroups.PartConfigLoadGroup</c></item>
/// <item><c>StdPersistentGroups.PartPersistant</c></item>
/// </list>
/// </remarks>
/// <seealso href="https://kerbalspaceprogram.com/api/interface_i_module_info.html">KSP: IModuleInfo
/// </seealso>
/// <seealso href="https://kerbalspaceprogram.com/api/class_part_module.html">KSP: PartModule
/// </seealso>
/// <seealso href="https://kerbalspaceprogram.com/api/interface_i_activate_on_decouple.html">
/// KSP: IActivateOnDecouple</seealso>
/// <seealso cref="ILinkSource"/>
/// <seealso cref="ILinkStateEventListener"/>
/// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ConfigUtils.StdPersistentGroups']/*"/>
// Next localization ID: #kasLOC_02008.
// TODO(ihsoft): Handle KIS actions.
// TODO(ihsoft): Handle part destroyed action.
// TODO(ihsoft): Handle part staged action.
// FIXME: implement cable stretching
public class KASLinkSourceBase : AbstractLinkPeer,
    // KSP interfaces.
    IModuleInfo,
    // KAS interfaces.
    ILinkSource, IHasDebugAdjustables,
    // KSPDev syntax sugar interfaces.
    IPartModule, IsPartDeathListener, IKSPDevModuleInfo, IHasContextMenu {

  #region Localizable GUI strings
  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  readonly static Message IncompatibleTargetLinkTypeMsg = new Message(
      "#kasLOC_02000",
      defaultTemplate: "Incompatible target link type",
      description: "Message to display when the target link type doesn't match the source type.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  readonly static Message SourceIsNotAvailableForLinkMsg = new Message(
      "#kasLOC_02001",
      defaultTemplate: "Source is not available for a link",
      description: "Message to display when a source is refusing to start the link.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  readonly static Message TargetDoesntAcceptLinksMsg = new Message(
      "#kasLOC_02002",
      defaultTemplate: "Target doesn't accept links",
      description: "Message to display when the target is refusing to accept the link.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  readonly static Message<string> CannotRestoreLinkMsg = new Message<string>(
      "#kasLOC_02003",
      defaultTemplate: "Cannot restore link for: <<1>>",
      description: "Message to display when a linked source and target cannot be matched on load."
      + "\nArgument <<1>> is a name of the SOURCE part.",
      example: "Cannot restore link for: KAS.TJ1");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  readonly static Message<string> LinksWithSocketTypeInfo = new Message<string>(
      "#kasLOC_02004",
      defaultTemplate: "Links with socket type: <<1>>",
      description: "Info string in the editor for the link type setting."
      + "\nArgument <<1>> is the type string from the part's config.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  readonly static Message ModuleTitleInfo = new Message(
      "#kasLOC_02005",
      defaultTemplate: "KAS Joint Source",
      description: "Title of the module to present in the editor details window.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message DockedModeMenuTxt = new Message(
      "#kasLOC_02006",
      defaultTemplate: "Link mode: DOCKED",
      description: "The name of the part's context menu event that triggers a separtation of the"
      + " linked parts into two different vessels if they are coupled thru this link. At the same"
      + " time, the name of the event gives a currently selected state.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message UndockedModeMenuTxt = new Message(
      "#kasLOC_02007",
      defaultTemplate: "Link mode: UNDOCKED",
      description: "The name of the part's context menu event that triggers a merging of the"
      + " linked parts if they were not coupled before. At  the same time, the name of the event"
      + " gives a currently selected state.");
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
  /// <summary>Tells if coupling mode can be changed via the part's context menu.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Show coupling GUI")]
  public bool showCouplingUi;
  
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
  [Debug.KASDebugAdjustable("Renderer name")]
  public string linkRendererName = "";

  /// <summary>Name of the joint to use with this source.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Joint name")]
  public string jointName = "";

  /// <summary>Audio sample to play when the parts are docked by the player.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Sound - part dock")]
  public string sndPathDock = "";

  /// <summary>Audio sample to play when the parts are undocked by the player.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Sound - part undock")]
  public string sndPathUndock = "";
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

  #region Context menu events/actions
  // Keep the events that may change their visibility states at the bottom. When an item goes out
  // of the menu, its height is reduced, but the lower left corner of the dialog is retained. 
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true, guiActiveUncommand = true, guiActiveUnfocused = true)]
  [LocalizableItem(tag = null)]
  public virtual void ToggleVesselsDockModeEvent() {
    if (!linkJoint.SetCoupleOnLinkMode(!linkJoint.coupleOnLinkMode)) {
      UISoundPlayer.instance.Play(KASAPI.CommonConfig.sndPathBipWrong);
    } else {
      if (isLinked) {
        UISoundPlayer.instance.Play(linkJoint.coupleOnLinkMode ? sndPathDock : sndPathUndock);
      }
      UpdateContextMenu();
    }
  }
  #endregion

  #region AbstractLinkPeer overrides
  /// <inheritdoc/>
  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);
    InitStartState();
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
        enterHandler: x => KASAPI.KasEvents.OnStartLinking.Add(OnStartLinkingKASEvent),
        leaveHandler: x => KASAPI.KasEvents.OnStartLinking.Remove(OnStartLinkingKASEvent));
    linkStateMachine.AddStateHandlers(
        LinkState.RejectingLinks,
        enterHandler: x => KASAPI.KasEvents.OnStopLinking.Add(OnStopLinkingKASEvent),
        leaveHandler: x => KASAPI.KasEvents.OnStopLinking.Remove(OnStopLinkingKASEvent));
    linkStateMachine.AddStateHandlers(
        LinkState.Linked,
        enterHandler: x => {
          GameEvents.onVesselWillDestroy.Add(OnVesselWillDestroyGameEvent);
          var module = linkTarget as PartModule;
          PartModuleUtils.InjectEvent(this, ToggleVesselsDockModeEvent, module);
        },
        leaveHandler: x => {
          GameEvents.onVesselWillDestroy.Remove(OnVesselWillDestroyGameEvent);
          var module = linkTarget as PartModule;
          PartModuleUtils.WithdrawEvent(this, ToggleVesselsDockModeEvent, module);
        });
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
  protected override void CheckCoupleNode() {
    base.CheckCoupleNode();
    if (linkState == LinkState.Available && parsedAttachNode.attachedPart != null) {
      var target = parsedAttachNode.attachedPart.Modules
          .OfType<ILinkTarget>()
          .FirstOrDefault(t => t.coupleNode != null && t.coupleNode.attachedPart == part
                               && CheckCanLinkTo(t, reportToLog: false));
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

    UpdateContextMenu();  // To update the dock/undock menu.
  }
  #endregion

  #region IHasDebugAdjustables implementation
  ILinkTarget dbgOldTarget;
  float dbgOldCableLength;

  /// <inheritdoc/>
  public virtual void OnBeforeDebugAdjustablesUpdate() {
    if (linkState != LinkState.Linked && linkState != LinkState.Available) {
      throw new InvalidOperationException("Cannot adjust value in link state: " + linkState);
    }
    dbgOldTarget = linkTarget;
    dbgOldCableLength = -1;
    if (isLinked) {
      var cableJoint = linkJoint as ILinkCableJoint;
      if (cableJoint != null) {
        dbgOldCableLength = cableJoint.deployedCableLength;
      }
      BreakCurrentLink(LinkActorType.Player);
    }
  }

  /// <inheritdoc/>
  public virtual void OnDebugAdjustablesUpdated() {
    AsyncCall.CallOnEndOfFrame(
        this,
        () => {
          HostedDebugLog.Warning(this, "Reloading settings...");
          LoadModuleSettings();
          InitStartState();
          if (dbgOldTarget != null) {
            HostedDebugLog.Warning(this, "Relinking to target: {0}", dbgOldTarget);
            LinkToTarget(LinkActorType.Player, dbgOldTarget);
            var cableJoint = linkJoint as ILinkCableJoint;
            if (cableJoint != null) {
              HostedDebugLog.Warning(this, "Restoring cable length: {0}", dbgOldCableLength);
              cableJoint.SetCableLength(dbgOldCableLength);
            }
          }
        },
        skipFrames: 1);  // The link's logic is asynchronous.
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
    sb.AppendLine(LinksWithSocketTypeInfo.Format(
        !string.IsNullOrEmpty(linkTypeDisplayName)
        ? linkTypeDisplayName
        : linkType));
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
    KASAPI.KasEvents.OnStartLinking.Fire(this);
    return true;
  }

  /// <inheritdoc/>
  public virtual void CancelLinking() {
    if (!linkStateMachine.CheckCanSwitchTo(LinkState.Available)) {
      HostedDebugLog.Fine(this, "Ignore linking mode cancel in state: {0}", linkState);
      return;
    }
    linkState = LinkState.Available;
    KASAPI.KasEvents.OnStopLinking.Fire(this);
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
    PhysicalLink();
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
  public virtual void BreakCurrentLink(LinkActorType actorType) {
    if (!isLinked) {
      HostedDebugLog.Error(this, "Cannot break link in state: {0}", linkState);
      return;
    }
    PhysicalUnlink();
    LogicalUnlink(actorType);
  }

  /// <inheritdoc/>
  public virtual bool CheckCanLinkTo(ILinkTarget target,
                                     bool checkStates = true,
                                     bool reportToGUI = false, bool reportToLog = true) {
    var errors = new List<string>()
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

  #region IHasContextMenu implementation
  /// <inheritdoc/>
  public virtual void UpdateContextMenu() {
    PartModuleUtils.SetupEvent(this, ToggleVesselsDockModeEvent, e => {
      if (linkJoint != null) {
        if (linkJoint.coupleOnLinkMode) {
          e.active = true;
          e.guiName = DockedModeMenuTxt;
        } else {
          e.active = showCouplingUi && allowCoupling
              && (linkTarget == null || linkTarget.coupleNode != null);
          e.guiName = UndockedModeMenuTxt;
        }
      } else {
        e.active = false;
      }
    });
  }
  #endregion

  #region Inheritable methods
  /// <summary>Logically links the source and the target, and starts the renderer.</summary>
  /// <remarks>It's always called <i>before</i> the physical link updates.</remarks>
  /// <param name="target">The target to link with.</param>
  protected virtual void LogicalLink(ILinkTarget target) {
    HostedDebugLog.Info(this, "Linking to target: {0}, actor={1}", target, linkActor);
    var linkInfo = new KasLinkEventImpl(this, target, linkActor);
    otherPeer = target;
    linkTarget.linkSource = this;
    linkState = LinkState.Linked;
    linkRenderer.StartRenderer(nodeTransform, linkTarget.nodeTransform);
    part.Modules.OfType<ILinkStateEventListener>().ToList()
        .ForEach(x => x.OnKASLinkedState(linkInfo, isLinked: true));
    KASAPI.KasEvents.OnStopLinking.Fire(this);
    KASAPI.KasEvents.OnLinkCreated.Fire(linkInfo);
  }

  /// <summary>
  /// Logically unlinks the source and the current target, and stops the renderer.
  /// </summary>
  /// <remarks>It's always called <i>after</i> the physical link updates.</remarks>
  /// <param name="actorType">The actor which has intiated the unlinking.</param>
  protected virtual void LogicalUnlink(LinkActorType actorType) {
    HostedDebugLog.Info(this, "Unlinking from target: {0}, actor={1}", linkTarget, actorType);
    linkActor = actorType;
    var linkInfo = new KasLinkEventImpl(this, linkTarget, actorType);
    linkRenderer.StopRenderer();
    linkState = LinkState.Available;
    if (linkTarget != null) {
      linkTarget.linkSource = null;
      otherPeer = null;
    }
    linkActor = LinkActorType.None;
    KASAPI.KasEvents.OnLinkBroken.Fire(linkInfo);
    part.Modules.OfType<ILinkStateEventListener>().ToList()
        .ForEach(x => x.OnKASLinkedState(linkInfo, isLinked: false));
  }

  /// <summary>Creates a physical link between the parts.</summary>
  /// <remarks>It's called after the logical link is established.</remarks>
  protected virtual void PhysicalLink() {
    linkJoint.CreateJoint(this, linkTarget);
  }

  /// <summary>Destroys the physical link between the parts.</summary>
  /// <remarks>It's called before the logical link is dropped.</remarks>
  protected virtual void PhysicalUnlink() {
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
    return errors.ToArray();
  }
  #endregion

  #region KASEvents listeners
  /// <summary>Sets rejecting state when some other source has started connection mode.</summary>
  /// <remarks>It's only listened in state <see cref="LinkState.Available"/>.
  /// <para>Event handler for <see cref="IKasEvents.OnStopLinking"/>.</para>
  /// </remarks>
  /// <param name="source">Source module that started connecting mode.</param>
  void OnStartLinkingKASEvent(ILinkSource source) {
    linkState = LinkState.RejectingLinks;
  }

  /// <summary>Restores available state when connection mode is over.</summary>
  /// <remarks>It's only listened in state <see cref="LinkState.RejectingLinks"/>.  
  /// <para>Event handler for <see cref="IKasEvents.OnStopLinking"/>.</para>
  /// </remarks>
  /// <param name="source">Source module that started the mode.</param>
  void OnStopLinkingKASEvent(ILinkSource source) {
    if (!isLocked) {
      linkState = LinkState.Available;
    }
  }
  #endregion 

  #region Local untility methods
  /// <summary>Reacts on the vessel destruction and break the link if needed.</summary>
  /// <remarks>This event can get called from the physics callbacks.</remarks>
  /// <param name="targetVessel">The vessel that is being destroyed.</param>
  void OnVesselWillDestroyGameEvent(Vessel targetVessel) {
    if (isLinked && vessel != linkTarget.part.vessel
        && (targetVessel == vessel || targetVessel == linkTarget.part.vessel)) {
      HostedDebugLog.Info(
          this, "Drop the link due to the peer vessel destruction: {0}", targetVessel);
      BreakCurrentLink(LinkActorType.Physics);
    }
  }

  /// <summary>Loads the state that should be processed after all the modules are created.</summary>
  /// <remarks>
  /// This method can be called by the debug tool, so add some extra checks to not critically fail
  /// if the settings are not correct.
  /// </remarks>
  void InitStartState() {
    linkJoint = part.Modules.OfType<ILinkJoint>()
        .FirstOrDefault(x => x.cfgJointName == jointName)
        ?? linkJoint;
    if (linkJoint == null) {
      HostedDebugLog.Error(this, "Cannot find joint module: {0}", jointName);
    }
    linkRenderer = part.Modules.OfType<ILinkRenderer>()
        .FirstOrDefault(x => x.cfgRendererName == linkRendererName)
        ?? linkRenderer;
    if (linkRenderer == null) {
      HostedDebugLog.Error(this, "Cannot find renderer module: {0}", linkRendererName);
    }
  }
  #endregion
}

}  // namespace
