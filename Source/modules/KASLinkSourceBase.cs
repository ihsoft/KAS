﻿// Kerbal Attachment System
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv2;
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

// ReSharper disable once CheckNamespace
namespace KAS {

/// <summary>Base link source module. Does all the job on making two parts linked.</summary>
/// <remarks>
/// This module deals with main logic of linking two parts together. The other party of the
/// link must be aware of the linking porcess. The targets must implement <see cref="ILinkTarget"/>.
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
// TODO(ihsoft): Handle part staged action.
// ReSharper disable once InconsistentNaming
public class KASLinkSourceBase : AbstractLinkPeer,
    // KSP interfaces.
    IModuleInfo,
    // KAS interfaces.
    ILinkSource,
    // KSPDev syntax sugar interfaces.
    IKSPDevModuleInfo, IHasContextMenu {

  #region Localizable GUI strings
  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message IncompatibleTargetLinkTypeMsg = new Message(
      "#kasLOC_02000",
      defaultTemplate: "Incompatible target link type",
      description: "Message to display when the target link type doesn't match the source type.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message SourceIsNotAvailableForLinkMsg = new Message(
      "#kasLOC_02001",
      defaultTemplate: "Source is not available for a link",
      description: "Message to display when a source is refusing to start the link.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message TargetDoesntAcceptLinksMsg = new Message(
      "#kasLOC_02002",
      defaultTemplate: "Target doesn't accept links",
      description: "Message to display when the target is refusing to accept the link.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> CannotRestoreLinkMsg = new Message<string>(
      "#kasLOC_02003",
      defaultTemplate: "Cannot restore link for: <<1>>",
      description: "Message to display when a linked source and target cannot be matched on load."
      + "\nArgument <<1>> is a name of the SOURCE part.",
      example: "Cannot restore link for: KAS.TJ1");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> LinksWithSocketTypeInfo = new Message<string>(
      "#kasLOC_02004",
      defaultTemplate: "Links with socket type: <<1>>",
      description: "Info string in the editor for the link type setting."
      + "\nArgument <<1>> is the type string from the part's config.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ModuleTitleInfo = new Message(
      "#kasLOC_02005",
      defaultTemplate: "KAS Joint Source",
      description: "Title of the module to present in the editor details window.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message DockedModeMenuTxt = new Message(
      "#kasLOC_02006",
      defaultTemplate: "Link mode: DOCKED",
      description: "The name of the part's context menu event that triggers a separation of the"
      + " linked parts into two different vessels if they are coupled thru this link. At the same"
      + " time, the name of the event gives a currently selected state.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message UndockedModeMenuTxt = new Message(
      "#kasLOC_02007",
      defaultTemplate: "Link mode: UNDOCKED",
      description: "The name of the part's context menu event that triggers a merging of the"
      + " linked parts if they were not coupled before. At  the same time, the name of the event"
      + " gives a currently selected state.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message TargetCannotCoupleMsg = new Message(
      "#kasLOC_02008",
      defaultTemplate: "Target cannot couple",
      description: "Message to display when the target is refusing to couple (dock) with the link"
      + " source.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message EvaActionBrokeLinkMsg = new Message(
      "#kasLOC_02009",
      defaultTemplate: "Unlinking due to the EVA construction action",
      description: "Message to display when a linked part becomes a target to EVA construction move or detach"
      + " operation.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CannotLinkInEvaConstructionModeMsg = new Message(
      "#kasLOC_02010",
      defaultTemplate: "Interactive links are not allowed in the EVA construction mode",
      description: "Message to display when an interactive link mode is being enabled while the stock EVA construction"
      + " mode is active. In this mode the KAS interactive links are completely disabled to not interfere with the"
      + " stock game functionality.");
  #endregion

  #region Constants and inheratable fields
  /// <summary>Path to the sound that indicates the link was broken forcibly (for any reason).</summary>
  /// TODO(IgorZ): Move to the common config.
  protected const string SoundLinkForceBroken = "KAS/Sounds/broke";
  #endregion

  #region ILinkSource properties implementation
  /// <inheritdoc/>
  public ILinkTarget linkTarget => otherPeer as ILinkTarget;

  /// <inheritdoc/>
  /// <seealso cref="jointName"/>
  public ILinkJoint linkJoint { get; private set; }

  /// <inheritdoc/>
  /// <seealso cref="linkRendererName"/>
  public ILinkRenderer linkRenderer { get; private set; }
  #endregion

  #region Part's config fields
  /// <summary>Specifies how/if the parts should be coupled on link.</summary>
  /// <remarks>
  /// Never change the existing values since they may be used in the part configs.
  /// </remarks>
  public enum CoupleMode {
    /// <summary>The docking state of the link is determined from the persistent state.</summary>
    /// <remarks>
    /// A context menu item will be presented to allow changing docking mode. Both the source and
    /// target parts must be enabled for coupling in order to be able to dock. IF any of the peers
    /// doesn't allow it, there will be no context menu option.
    /// </remarks>
    // ReSharper disable once InconsistentNaming
    SetViaGUI,

    /// <summary>The link is always established in docked mode.</summary>
    /// <remarks>
    /// It implies that <see cref="AbstractLinkPeer.allowCoupling"/> must be <c>true</c>. If it's
    /// not, then a warning will be thrown and the settings fixed.
    /// </remarks>
    /// <seealso cref="CheckSettingsConsistency"/>
    AlwaysCoupled,

    /// <summary>The link is always established in undocked mode.</summary>
    NeverCouple,
  }

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
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Renderer name")]
  public string linkRendererName = "";

  /// <summary>Name of the joint to use with this source.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Joint name")]
  public string jointName = "";

  /// <summary>Audio sample to play when the parts are docked by the player.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Sound - part dock")]
  public string sndPathDock = "";

  /// <summary>Audio sample to play when the parts are undocked by the player.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Sound - part undock")]
  public string sndPathUndock = "";

  /// <summary>Specifies if the parts should couple on link creation.</summary>
  /// <remarks>
  /// In case of <see cref="CoupleMode.AlwaysCoupled"/> mode is selected, the both parts must
  /// support coupling. If they don't, then the coupling will be made without using the attach
  /// nodes. An error will be logged, and the further behavior of the assembly is undetermined.
  /// </remarks>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Coupling mode")]
  public CoupleMode coupleMode = CoupleMode.NeverCouple;
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
  /// <include file="../SpecialDocTags.xml" path="Tags/KspEvent/*"/>
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
  /// <summary>Reacts on a part de-coupling and adjusts the docking mode.</summary>
  /// <remarks>
  /// This is a cleanup method that verifies that all links in the DOCKED mode remained coupled
  /// after decoupling of the part. If it's not the case, the DOCKED mode is reset to ATTACHED. In
  /// the normal case the joint module takes care of restoring the affected couplings, and this
  /// method becomes NO-OP.  
  /// </remarks>
  /// <param name="originator">The part that has decoupled.</param>
  void OnPartDeCoupleCompleteEvent(Part originator) {
    if (!isLinked || !linkJoint.coupleOnLinkMode || linkTarget.part.vessel == vessel) {
      return;  // Not interested.
    }
    // Wait for one frame to allow joint logic to restore the coupling, and then check.
    AsyncCall.CallOnEndOfFrame(
        this,
        () => {
          if (isLinked && linkJoint.coupleOnLinkMode && linkTarget.part.vessel != vessel) {
            HostedDebugLog.Fine(
                this,
                "Coupling has not been restored, resetting the docking mode: {0} <=> {1}",
                part, linkTarget.part);
            linkJoint.SetCoupleOnLinkMode(false);
            UpdateContextMenu();
          }
        },
        skipFrames: 1);
  }

  /// <inheritdoc/>
  public override void OnStart(StartState state) {
    base.OnStart(state);
    InitStartState();
    RegisterGameEventListener(GameEvents.onPartDeCoupleComplete, OnPartDeCoupleCompleteEvent);
  }
  /// <inheritdoc/>
  public override void OnStartFinished(StartState state) {
    base.OnStartFinished(state);
    if (isLinked && !linkJoint.isLinked) {
      if (linkJoint.CreateJoint(this, linkTarget)) {
        HostedDebugLog.Info(this, "Restored joint with {0}", linkTarget);
      } else {
        HostedDebugLog.Info(this, "Cannot restore joint with {0}", linkTarget);
      }
    }
  }

  /// <inheritdoc/>
  public override void OnInitialize() {
    base.OnInitialize();
    if (isLinked && linkTarget.part.vessel != vessel) {
      // When the target is at the different vessel, there is no automatic collision ignore set.
      AsyncCall.CallOnFixedUpdate(this, () => {
        // Copied from KerbalEVA.OnVesselGoOffRails() method.
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
        this, "Source state changed: node={0}, state {1} => {2}", attachNodeName, start, end);
    linkStateMachine.SetTransitionConstraint(
        LinkState.Available,
        new[] {
            LinkState.Linking, LinkState.RejectingLinks,
            LinkState.NodeIsBlocked, LinkState.Locked
        });
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

    // Handle a case when this source doesn't want to couple with the child.
    if (coupleNode == null) {
      if (parsedAttachNode.attachedPart != null) {
        HostedDebugLog.Error(this, "Cannot maintain coupling with: {0}", parsedAttachNode.attachedPart);
        if (linkState == LinkState.Available) {
          AsyncCall.CallOnEndOfFrame(this, () => {
            if (parsedAttachNode.attachedPart) {
              HostedDebugLog.Info(this, "Decoupling incompatible part: {0}", parsedAttachNode.attachedPart);
              parsedAttachNode.attachedPart.decouple();
              UpdateContextMenu();
            }
          });
        } else if (linkState == LinkState.Linked && linkTarget != null) {
          HostedDebugLog.Warning(this, "Breaking the link to: {0}", linkTarget);
          AsyncCall.CallOnEndOfFrame(this, () => {
            BreakCurrentLink(LinkActorType.API);
            UpdateContextMenu();
          });
        } else {
          AsyncCall.CallOnEndOfFrame(this, () => {
            if (parsedAttachNode.attachedPart) {
              HostedDebugLog.Error(this, "Cannot pickup coupling in unexpected link state: {0}", linkState);
              parsedAttachNode.attachedPart.decouple();
              UpdateContextMenu();
            }
          });
        }
      }
      return;
    }

    // Handle the case when a part is attached externally. 
    if (linkState == LinkState.Available && coupleNode?.attachedPart != null) {
      // Try all the possible targets on the other part to make the link.
      var targets = coupleNode.attachedPart.Modules
          .OfType<ILinkTarget>()
          .Where(t => t.coupleNode?.attachedPart == part && CheckCanLinkTo(t, reportToLog: false));
      foreach (var target in targets) {
        if (LinkToTarget(LinkActorType.API, target)) {
          HostedDebugLog.Info(this, "Restored the link with the externally attached part: {0}", target);
          break;
        }
        HostedDebugLog.Warning(this, "The link attempt has been rejected: {0}", target);
      }
      AsyncCall.CallOnEndOfFrame(this, () => {
        if (linkState == LinkState.Available && coupleNode?.attachedPart != null) {
          HostedDebugLog.Warning(this, "Cannot link to the pre-attached part: from={0}, to={1}",
                                 KASAPI.AttachNodesUtils.NodeId(coupleNode),
                                 KASAPI.AttachNodesUtils.NodeId(coupleNode.FindOpposingNode()));
          SetLinkState(LinkState.NodeIsBlocked);
          UpdateContextMenu();
        }
      });
    }
    
    UpdateContextMenu();
  }

  /// <inheritdoc/>
  protected override void CheckSettingsConsistency() {
    base.CheckSettingsConsistency();
    if (!allowCoupling && coupleMode == CoupleMode.AlwaysCoupled) {
      allowCoupling = true;
      HostedDebugLog.Warning(
          this, "Inconsistent setting fixed: allowCoupling => true, due to coupleMode={0}",
          coupleMode);
    }
    if (!allowCoupling && linkJoint != null && linkJoint.coupleOnLinkMode) {
      // This check is needed for debug only.
      linkJoint.SetCoupleOnLinkMode(false);
      HostedDebugLog.Warning(
          this, "Inconsistent setting fixed: coupleOnLinkMode => false, due to allowCoupling={0}",
          allowCoupling);
    }
  }

  /// <inheritdoc/>
  protected override void InitModuleSettings() {
    base.InitModuleSettings();
    UpdateContextMenu();
  }
  #endregion

  #region IHasDebugAdjustables implementation
  /// <inheritdoc/>
  public override void OnBeforeDebugAdjustablesUpdate() {
    if (linkState != LinkState.Available) {
      throw new InvalidOperationException("Cannot adjust value in link state: " + linkState);
    }
    base.OnBeforeDebugAdjustablesUpdate();
  }

  /// <inheritdoc/>
  public override void OnDebugAdjustablesUpdated() {
    base.OnDebugAdjustablesUpdated();
    HostedDebugLog.Warning(this, "Reloading settings...");
    InitModuleSettings();
    InitStartState();
    UpdateContextMenu();
  }
  #endregion

  #region IsPartDeathListener implementation
  /// <inheritdoc/>
  public override void OnPartDie() {
    if (isLinked) {
      HostedDebugLog.Info(this, "Part has died. Drop the link to: {0}", linkTarget);
      BreakCurrentLink(LinkActorType.Physics);
    }
  }
  #endregion

  #region IModuleInfo implementation
  /// <inheritdoc cref="IKSPDevModuleInfo.GetInfo" />
  public override string GetInfo() {
    var sb = new StringBuilder(base.GetInfo());
    sb.AppendLine(LinksWithSocketTypeInfo.Format(
        !string.IsNullOrEmpty(linkTypeDisplayName)
        ? linkTypeDisplayName
        : linkType));
    return sb.ToString();
  }

  /// <inheritdoc cref="IKSPDevModuleInfo.GetModuleTitle" />
  public virtual string GetModuleTitle() {
    return ModuleTitleInfo;
  }

  /// <inheritdoc cref="IKSPDevModuleInfo.GetDrawModulePanelCallback" />
  public virtual Callback<Rect> GetDrawModulePanelCallback() {
    return null;
  }

  /// <inheritdoc cref="IKSPDevModuleInfo.GetPrimaryField" />
  public virtual string GetPrimaryField() {
    return null;
  }
  #endregion

  #region ILinkSource implementation
  /// <inheritdoc/>
  public virtual bool StartLinking(GUILinkMode mode, LinkActorType actor) {
    if (mode == GUILinkMode.Interactive && EVAConstructionModeController.Instance.IsOpen) {
      ShowStatusMessage(CannotLinkInEvaConstructionModeMsg, isError: true);
      HostedDebugLog.Warning(this, "Cannot make interactive links in the EVA construction mode");
      UISoundPlayer.instance.Play(KASAPI.CommonConfig.sndPathBipWrong);
      return false;
    }
    if (!linkStateMachine.CheckCanSwitchTo(LinkState.Linking)) {
      if (actor == LinkActorType.Player) {
        ShowStatusMessage(SourceIsNotAvailableForLinkMsg, isError: true);
      }
      HostedDebugLog.Warning(this, "Cannot start linking mode in state: {0}", linkState);
      return false;
    }
    guiLinkMode = mode;
    linkActor = actor;
    SetLinkState(LinkState.Linking);
    KASAPI.KasEvents.OnStartLinking.Fire(this);
    return true;
  }

  /// <inheritdoc/>
  public virtual void CancelLinking() {
    if (!linkStateMachine.CheckCanSwitchTo(LinkState.Available)) {
      HostedDebugLog.Fine(this, "Ignore linking mode cancel in state: {0}", linkState);
      return;
    }
    SetLinkState(LinkState.Available);
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
    if (!CheckCanLinkTo(target, reportToGui: linkActor == LinkActorType.Player)) {
      return false;
    }
    if (coupleMode == CoupleMode.AlwaysCoupled
        || coupleNode != null && coupleNode.attachedPart != null) {
      linkJoint.SetCoupleOnLinkMode(true);
    } else if (coupleMode == CoupleMode.NeverCouple) {
      linkJoint.SetCoupleOnLinkMode(false);
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
                                     bool reportToGui = false, bool reportToLog = true) {
    var errors = new List<string>()
        .Concat(CheckBasicLinkConditions(target, checkStates))
        .Concat(linkRenderer.CheckColliderHits(nodeTransform, target.nodeTransform))
        .Concat(linkJoint.CheckConstraints(this, target))
        .ToArray();
    if (errors.Length > 0) {
      if (reportToGui || reportToLog) {
        HostedDebugLog.Warning(
            this, "Cannot link a part of type={0} with: part={1}, type={2}, errors={3}",
            cfgLinkType, target.part, target.cfgLinkType, DbgFormatter.C2S(errors));
      }
      if (reportToGui) {
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
        e.guiName = linkJoint.coupleOnLinkMode ? DockedModeMenuTxt : UndockedModeMenuTxt;
        if (coupleMode == CoupleMode.SetViaGUI) {
          e.active = coupleNode != null && (linkTarget == null || linkTarget.coupleNode != null);
        } else if (isLinked) {
          // Just in case show GUI if the link is established, and its couple mode contradicts the
          // joint setting. GUI will allow fixing it manually.
          e.active = coupleMode == CoupleMode.NeverCouple && linkJoint.coupleOnLinkMode
              || coupleMode == CoupleMode.AlwaysCoupled && !linkJoint.coupleOnLinkMode;
        } else {
          e.active = false;
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
    SetOtherPeer(target);
    linkTarget.linkSource = this;
    SetLinkState(LinkState.Linked);
    linkRenderer.StartRenderer(nodeTransform, linkTarget.nodeTransform);
    part.Modules.OfType<ILinkStateEventListener>().ToList()
        .ForEach(x => x.OnKASLinkedState(linkInfo, isLinked: true));
    KASAPI.KasEvents.OnStopLinking.Fire(this);
    KASAPI.KasEvents.OnLinkCreated.Fire(linkInfo);
  }

  /// <summary>
  /// Logically un-links the source and the current target, and stops the renderer.
  /// </summary>
  /// <remarks>It's always called <i>after</i> the physical link updates.</remarks>
  /// <param name="actorType">The actor which has initiated the un-linking.</param>
  protected virtual void LogicalUnlink(LinkActorType actorType) {
    HostedDebugLog.Info(this, "Un-linking from target: {0}, actor={1}", linkTarget, actorType);
    linkActor = actorType;
    var linkInfo = new KasLinkEventImpl(this, linkTarget, actorType);
    linkRenderer.StopRenderer();
    SetLinkState(LinkState.Available);
    if (linkTarget != null) {
      linkTarget.linkSource = null;
      SetOtherPeer(null);
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
  // ReSharper disable once VirtualMemberNeverOverridden.Global
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
    if (coupleMode == CoupleMode.AlwaysCoupled && target.coupleNode == null) {
      errors.Add(TargetCannotCoupleMsg);
    }
    return errors.ToArray();
  }

  /// <inheritdoc/>
  protected override void OnPeerManipulatedInEva(ILinkPeer target) {
    base.OnPeerManipulatedInEva(target);
    if (isLinked) {
      HostedDebugLog.Info(this, "Unlinking from {0} due EVA construction action: target={1}", otherPeer, target);
      ShowStatusMessage(EvaActionBrokeLinkMsg, isError: true);
      UISoundPlayer.instance.Play(SoundLinkForceBroken);
      BreakCurrentLink(LinkActorType.API);
    }
  }
  #endregion

  #region KASEvents listeners
  /// <summary>Sets rejecting state when some other source has started connection mode.</summary>
  /// <remarks>It's only listened in state <see cref="LinkState.Available"/>.
  /// <para>Event handler for <see cref="IKasEvents.OnStopLinking"/>.</para>
  /// </remarks>
  /// <param name="source">Source module that started connecting mode.</param>
  void OnStartLinkingKASEvent(ILinkSource source) {
    SetLinkState(LinkState.RejectingLinks);
  }

  /// <summary>Restores available state when connection mode is over.</summary>
  /// <remarks>It's only listened in state <see cref="LinkState.RejectingLinks"/>.  
  /// <para>Event handler for <see cref="IKasEvents.OnStopLinking"/>.</para>
  /// </remarks>
  /// <param name="source">Source module that started the mode.</param>
  void OnStopLinkingKASEvent(ILinkSource source) {
    if (!isLocked) {
      SetLinkState(LinkState.Available);
    }
  }
  #endregion 

  #region Local untility methods
  /// <summary>Reacts on the vessel destruction and break the link if needed.</summary>
  /// <remarks>This event can get called from the physics callbacks.</remarks>
  /// <param name="targetVessel">The vessel that is being destroyed.</param>
  void OnVesselWillDestroyGameEvent(Vessel targetVessel) {
    if (!isLinked || vessel == linkTarget.part.vessel
        || (targetVessel != vessel && targetVessel != linkTarget.part.vessel)) {
      return;  // Nothing to do.
    }
    HostedDebugLog.Info(
        this, "Drop the link due to the peer vessel destruction: {0}", targetVessel);
    BreakCurrentLink(LinkActorType.Physics);
  }

  /// <summary>Loads the state that should be processed after all the modules are created.</summary>
  /// <remarks>
  /// This method can be called by the debug tool, so add some extra checks to not critically fail
  /// if the settings are not correct.
  /// </remarks>
  void InitStartState() {
    var oldLinkJoint = linkJoint;
    linkJoint = part.Modules.OfType<ILinkJoint>()
        .FirstOrDefault(x => x.cfgJointName == jointName);
    if (linkJoint == null) {
      HostedDebugLog.Error(this, "Cannot find joint module: {0}", jointName);
    }
    linkJoint = linkJoint ?? oldLinkJoint;

    var oldLinkRenderer = linkRenderer;
    linkRenderer = part.Modules.OfType<ILinkRenderer>()
        .FirstOrDefault(x => x.cfgRendererName == linkRendererName);
    if (linkRenderer == null) {
      HostedDebugLog.Error(this, "Cannot find renderer module: {0}", linkRendererName);
    }
    linkRenderer = linkRenderer ?? oldLinkRenderer;
  }
  #endregion
}

}  // namespace
